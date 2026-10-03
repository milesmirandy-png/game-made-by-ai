using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace Swat
{
    // Peer-to-peer networking for the game modes, in plain C# (no Unity types,
    // so it can be tested outside the editor). One player hosts; the others
    // send UDP packets straight to the host's address. Every packet carries
    // any number of messages:
    //   reliable   - delivered once and in order (resent until acknowledged),
    //   unreliable - sent once; newer ones replace lost ones (positions).
    // Connections keep themselves alive with tiny packets and time out after
    // a few seconds of silence. NetDiscovery finds hosts on the local network.

    // ---- Message encoding ----

    public sealed class NetWriter
    {
        [StructLayout(LayoutKind.Explicit)]
        struct FloatBits
        {
            [FieldOffset(0)] public float f;
            [FieldOffset(0)] public uint u;
        }

        byte[] buffer = new byte[256];
        int length;

        public int Length { get { return length; } }

        public NetWriter Reset()
        {
            length = 0;
            return this;
        }

        void Ensure(int extra)
        {
            if (length + extra > buffer.Length) Array.Resize(ref buffer, Math.Max(buffer.Length * 2, length + extra));
        }

        public void Byte(int value)
        {
            Ensure(1);
            buffer[length++] = (byte)value;
        }

        public void Bool(bool value) { Byte(value ? 1 : 0); }

        public void UShort(int value)
        {
            Ensure(2);
            buffer[length++] = (byte)value;
            buffer[length++] = (byte)(value >> 8);
        }

        public void Short(int value) { UShort((ushort)(short)value); }

        public void UInt(uint value)
        {
            Ensure(4);
            buffer[length++] = (byte)value;
            buffer[length++] = (byte)(value >> 8);
            buffer[length++] = (byte)(value >> 16);
            buffer[length++] = (byte)(value >> 24);
        }

        public void Int(int value) { UInt((uint)value); }

        public void Float(float value)
        {
            var bits = new FloatBits { f = value };
            UInt(bits.u);
        }

        // Up to 255 bytes of UTF-8.
        public void String(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? "");
            int count = Math.Min(bytes.Length, 255);
            Byte(count);
            Bytes(bytes, 0, count);
        }

        public void Bytes(byte[] data, int offset, int count)
        {
            Ensure(count);
            System.Buffer.BlockCopy(data, offset, buffer, length, count);
            length += count;
        }

        public byte[] ToArray()
        {
            var copy = new byte[length];
            System.Buffer.BlockCopy(buffer, 0, copy, 0, length);
            return copy;
        }

        internal byte[] Raw { get { return buffer; } }
    }

    // Reads what NetWriter wrote. Reading past the end yields zeros and sets Failed,
    // so a short or corrupt packet can be detected and dropped instead of throwing.
    public sealed class NetReader
    {
        [StructLayout(LayoutKind.Explicit)]
        struct FloatBits
        {
            [FieldOffset(0)] public float f;
            [FieldOffset(0)] public uint u;
        }

        readonly byte[] data;
        int position;
        readonly int end;

        public bool Failed { get; private set; }
        public int Remaining { get { return end - position; } }

        public NetReader(byte[] data) : this(data, 0, data != null ? data.Length : 0) { }

        public NetReader(byte[] data, int offset, int count)
        {
            this.data = data;
            position = offset;
            end = offset + count;
        }

        bool Has(int count)
        {
            if (position + count <= end) return true;
            Failed = true;
            position = end;
            return false;
        }

        public int Byte()
        {
            if (!Has(1)) return 0;
            return data[position++];
        }

        public bool Bool() { return Byte() != 0; }

        public int UShort()
        {
            if (!Has(2)) return 0;
            int value = data[position] | (data[position + 1] << 8);
            position += 2;
            return value;
        }

        public int Short() { return (short)UShort(); }

        public uint UInt()
        {
            if (!Has(4)) return 0u;
            uint value = (uint)(data[position] | (data[position + 1] << 8) | (data[position + 2] << 16) | (data[position + 3] << 24));
            position += 4;
            return value;
        }

        public int Int() { return (int)UInt(); }

        public float Float()
        {
            var bits = new FloatBits { u = UInt() };
            float value = bits.f;
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        public string String()
        {
            int count = Byte();
            if (!Has(count)) return "";
            string value = Encoding.UTF8.GetString(data, position, count);
            position += count;
            return value;
        }

        public byte[] Bytes(int count)
        {
            if (count < 0 || !Has(count)) return new byte[0];
            var copy = new byte[count];
            System.Buffer.BlockCopy(data, position, copy, 0, count);
            position += count;
            return copy;
        }
    }

    // ---- Sockets ----

    public interface INetSocket
    {
        bool Send(byte[] data, int length, IPEndPoint to);
        // Bytes received into the buffer, or -1 when nothing is waiting.
        int Receive(byte[] buffer, out IPEndPoint from);
        void Close();
    }

    // A non-blocking UDP socket.
    public sealed class UdpNetSocket : INetSocket
    {
        readonly Socket socket;

        public int Port { get; private set; }

        public UdpNetSocket(int port, bool broadcast)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                socket.Blocking = false;
                if (broadcast) socket.EnableBroadcast = true;
                // One game per port: a second host on this computer moves to the next port instead of sharing.
                try
                {
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
                    socket.ExclusiveAddressUse = true;
                }
                catch (Exception) { }
                try
                {
                    // Windows reports "port unreachable" as an error on the next receive; turn that off.
                    const int SioUdpConnReset = -1744830452;
                    socket.IOControl(SioUdpConnReset, new byte[] { 0 }, null);
                }
                catch (Exception) { }
                socket.Bind(new IPEndPoint(IPAddress.Any, port));
                Port = ((IPEndPoint)socket.LocalEndPoint).Port;
            }
            catch
            {
                socket.Close();
                throw;
            }
        }

        public bool Send(byte[] data, int length, IPEndPoint to)
        {
            try
            {
                socket.SendTo(data, 0, length, SocketFlags.None, to);
                return true;
            }
            catch (SocketException) { return false; }
            catch (ObjectDisposedException) { return false; }
        }

        public int Receive(byte[] buffer, out IPEndPoint from)
        {
            from = null;
            try
            {
                if (socket.Available <= 0) return -1;
                EndPoint any = new IPEndPoint(IPAddress.Any, 0);
                int count = socket.ReceiveFrom(buffer, ref any);
                from = (IPEndPoint)any;
                return count;
            }
            catch (SocketException) { return 0; }
            catch (ObjectDisposedException) { return -1; }
        }

        public void Close()
        {
            try { socket.Close(); }
            catch (Exception) { }
        }
    }

    // ---- Connections ----

    public enum NetEventKind { Connected, Disconnected, Message, ConnectFailed }

    public struct NetEvent
    {
        public NetEventKind kind;
        public NetConnection connection;
        public byte[] data;
        public bool reliable;
        public string reason;
    }

    public sealed class NetConnection
    {
        internal struct Pending
        {
            public ushort seq;
            public byte[] data;
            public double firstSent, lastSent;
            public int sends;
        }

        public int Id { get; internal set; }
        public IPEndPoint EndPoint { get; internal set; }
        public bool Open { get; internal set; }
        public double RoundTrip { get; internal set; }   // seconds, smoothed
        public object Tag;

        internal uint token;
        internal ushort nextSendSeq, nextExpected;
        internal readonly List<Pending> unacked = new List<Pending>();
        internal readonly Dictionary<ushort, byte[]> early = new Dictionary<ushort, byte[]>();
        internal readonly List<byte[]> unreliable = new List<byte[]>();
        internal double lastReceive, lastSend;
        internal bool ackOwed;
        internal double ackOwedSince;
        // Round-trip timing: the peer's last packet stamp (milliseconds) and when it arrived.
        internal int peerStamp = -1;
        internal double peerStampAt;

        public int PendingReliable { get { return unacked.Count; } }
    }

    public sealed class NetPeer
    {
        public const byte Protocol = 1;
        // Packets are filled up to MaxPacket bytes (safe for any network path). A single bigger
        // message (the match setup with a full roster) travels alone in a larger packet.
        public const int MaxPacket = 1200;
        public const int MaxMessage = 8000;
        public const double TimeoutSeconds = 10.0;
        public const double ConnectTimeoutSeconds = 10.0;
        const double KeepAliveSeconds = 0.4, AckDelay = 0.03, ConnectRetry = 0.5;
        const byte Magic0 = 0x53, Magic1 = 0x57; // "SW"
        internal const byte TypeConnect = 1, TypeAccept = 2, TypeReject = 3, TypeData = 4, TypeDisconnect = 5, TypeQuery = 6, TypeReply = 7;

        readonly INetSocket socket;
        readonly List<NetConnection> connections = new List<NetConnection>();
        readonly byte[] receiveBuffer = new byte[MaxMessage + 64];
        readonly NetWriter packet = new NetWriter();
        readonly Random random = new Random();
        int nextId = 1;

        public bool IsHost { get; private set; }
        public IList<NetConnection> Connections { get { return connections; } }

        // Host: decides whether to let a player in (null = yes, otherwise the reason shown to them),
        // and what to tell them in the reply.
        public Func<IPEndPoint, byte[], string> Approve;
        public Func<NetConnection, byte[]> Welcome;

        // Client: the connection attempt in progress.
        IPEndPoint connectTo;
        byte[] hello;
        uint connectToken;
        double connectStarted, nextConnectSend;
        bool connecting;

        NetPeer(INetSocket socket, bool host)
        {
            this.socket = socket;
            IsHost = host;
        }

        public static NetPeer Host(INetSocket socket)
        {
            return new NetPeer(socket, true);
        }

        public static NetPeer Client(INetSocket socket, IPEndPoint host, byte[] hello, double now)
        {
            var peer = new NetPeer(socket, false);
            peer.connectTo = host;
            peer.hello = hello ?? new byte[0];
            peer.connectToken = (uint)peer.random.Next(1, int.MaxValue);
            peer.connectStarted = now;
            peer.nextConnectSend = now;
            peer.connecting = true;
            return peer;
        }

        public bool Connecting { get { return connecting; } }

        public void Send(NetConnection connection, byte[] data, bool reliable)
        {
            if (connection == null || !connection.Open || data == null) return;
            if (data.Length > MaxMessage) throw new ArgumentException("Message too large: " + data.Length + " bytes");
            if (reliable)
                connection.unacked.Add(new NetConnection.Pending { seq = connection.nextSendSeq++, data = data, firstSent = -1.0, lastSent = -1.0 });
            else
                connection.unreliable.Add(data);
        }

        public void SendToAll(byte[] data, bool reliable, NetConnection except = null)
        {
            foreach (var connection in connections)
                if (connection != except) Send(connection, data, reliable);
        }

        // Tells the other side and forgets the connection (no event is raised for it).
        public void Disconnect(NetConnection connection, string reason)
        {
            if (connection == null || !connection.Open) return;
            SendControl(TypeDisconnect, connection.EndPoint, w => w.String(reason ?? ""));
            connection.Open = false;
            connections.Remove(connection);
        }

        public void Close(string reason)
        {
            foreach (var connection in connections.ToArray()) Disconnect(connection, reason);
            connecting = false;
            socket.Close();
        }

        // Receives everything waiting, handles timeouts and retries, and sends what is due.
        public void Update(double now, List<NetEvent> events)
        {
            for (int guard = 0; guard < 512; guard++)
            {
                IPEndPoint from;
                int count = socket.Receive(receiveBuffer, out from);
                if (count < 0) break;
                if (count < 4 || from == null) continue;
                Handle(receiveBuffer, count, from, now, events);
            }

            if (connecting)
            {
                if (now - connectStarted > ConnectTimeoutSeconds)
                {
                    connecting = false;
                    events.Add(new NetEvent { kind = NetEventKind.ConnectFailed, reason = "No answer from " + connectTo + ". Check the address, and that the host's firewall allows the game." });
                }
                else if (now >= nextConnectSend)
                {
                    nextConnectSend = now + ConnectRetry;
                    SendControl(TypeConnect, connectTo, w =>
                    {
                        w.UInt(connectToken);
                        w.UShort(hello.Length);
                        w.Bytes(hello, 0, hello.Length);
                    });
                }
            }

            for (int i = connections.Count - 1; i >= 0; i--)
            {
                var connection = connections[i];
                if (now - connection.lastReceive > TimeoutSeconds)
                {
                    connection.Open = false;
                    connections.RemoveAt(i);
                    events.Add(new NetEvent { kind = NetEventKind.Disconnected, connection = connection, reason = "Connection timed out" });
                    continue;
                }
                Flush(connection, now);
            }
        }

        // Sends whatever is due without reading anything (for messages queued late in a frame).
        public void Flush(double now)
        {
            foreach (var connection in connections) Flush(connection, now);
        }

        void Handle(byte[] data, int count, IPEndPoint from, double now, List<NetEvent> events)
        {
            if (data[0] != Magic0 || data[1] != Magic1 || data[2] != Protocol) return;
            var reader = new NetReader(data, 4, count - 4);
            var connection = Find(from);
            switch (data[3])
            {
                case TypeConnect:
                    if (IsHost) HandleConnect(reader, from, connection, now, events);
                    break;

                case TypeAccept:
                {
                    if (IsHost || !connecting || !from.Equals(connectTo)) return;
                    uint token = reader.UInt();
                    int id = reader.Byte();
                    var welcome = reader.Bytes(reader.UShort());
                    if (reader.Failed || token != connectToken) return;
                    connecting = false;
                    var host = new NetConnection { Id = id, EndPoint = from, Open = true, token = token, lastReceive = now, lastSend = now, RoundTrip = Math.Min(1.0, now - connectStarted) };
                    connections.Add(host);
                    events.Add(new NetEvent { kind = NetEventKind.Connected, connection = host, data = welcome });
                    break;
                }

                case TypeReject:
                {
                    if (IsHost || !connecting || !from.Equals(connectTo)) return;
                    uint token = reader.UInt();
                    string reason = reader.String();
                    if (reader.Failed || token != connectToken) return;
                    connecting = false;
                    events.Add(new NetEvent { kind = NetEventKind.ConnectFailed, reason = reason });
                    break;
                }

                case TypeDisconnect:
                    if (connection == null) return;
                    connection.Open = false;
                    connections.Remove(connection);
                    events.Add(new NetEvent { kind = NetEventKind.Disconnected, connection = connection, reason = reader.String() });
                    break;

                case TypeData:
                    if (connection != null) HandleData(connection, reader, now, events);
                    break;
            }
        }

        void HandleConnect(NetReader reader, IPEndPoint from, NetConnection existing, double now, List<NetEvent> events)
        {
            uint token = reader.UInt();
            var helloData = reader.Bytes(reader.UShort());
            if (reader.Failed) return;
            if (existing != null)
            {
                // The accept was lost: say it again. A new token means the player restarted: start over.
                if (existing.token == token)
                {
                    SendAccept(existing);
                    return;
                }
                existing.Open = false;
                connections.Remove(existing);
                events.Add(new NetEvent { kind = NetEventKind.Disconnected, connection = existing, reason = "Reconnected" });
            }
            string refusal = Approve != null ? Approve(from, helloData) : null;
            if (refusal != null)
            {
                SendControl(TypeReject, from, w =>
                {
                    w.UInt(token);
                    w.String(refusal);
                });
                return;
            }
            while (connections.Exists(c => c.Id == nextId)) nextId = nextId % 250 + 1;
            var connection = new NetConnection { Id = nextId, EndPoint = from, Open = true, token = token, lastReceive = now, lastSend = now, RoundTrip = 0.1 };
            nextId = nextId % 250 + 1;
            connections.Add(connection);
            SendAccept(connection);
            events.Add(new NetEvent { kind = NetEventKind.Connected, connection = connection, data = helloData });
        }

        void SendAccept(NetConnection connection)
        {
            var welcome = Welcome != null ? Welcome(connection) ?? new byte[0] : new byte[0];
            SendControl(TypeAccept, connection.EndPoint, w =>
            {
                w.UInt(connection.token);
                w.Byte(connection.Id);
                w.UShort(welcome.Length);
                w.Bytes(welcome, 0, welcome.Length);
            });
        }

        void HandleData(NetConnection connection, NetReader reader, double now, List<NetEvent> events)
        {
            ushort ack = (ushort)reader.UShort();
            int stamp = reader.UShort();
            int echo = reader.UShort();
            int held = reader.UShort();
            int reliableCount = reader.Byte();
            var reliable = new List<KeyValuePair<ushort, byte[]>>(reliableCount);
            for (int i = 0; i < reliableCount; i++)
            {
                ushort seq = (ushort)reader.UShort();
                var payload = reader.Bytes(reader.UShort());
                reliable.Add(new KeyValuePair<ushort, byte[]>(seq, payload));
            }
            int unreliableCount = reader.Byte();
            var unreliable = new List<byte[]>(unreliableCount);
            for (int i = 0; i < unreliableCount; i++) unreliable.Add(reader.Bytes(reader.UShort()));
            if (reader.Failed) return; // corrupt or truncated: ignore the whole packet

            connection.lastReceive = now;
            // A late duplicate carries an older stamp; keep the newest.
            if (connection.peerStamp < 0 || (short)(ushort)(stamp - connection.peerStamp) > 0)
            {
                connection.peerStamp = stamp;
                connection.peerStampAt = now;
            }
            // The peer echoes our newest stamp and how long it held it: the rest is the round trip.
            if (echo != NoStamp)
            {
                int trip = ((Millis(now) - echo) & 0xFFFF) - held;
                if (trip >= 0 && trip < 5000) connection.RoundTrip = connection.RoundTrip * 0.875 + trip * 0.001 * 0.125;
            }
            // Everything before the acknowledgement arrived.
            for (int i = connection.unacked.Count - 1; i >= 0; i--)
                if (Diff(connection.unacked[i].seq, ack) < 0) connection.unacked.RemoveAt(i);

            foreach (var message in reliable)
            {
                connection.ackOwed = true;
                if (connection.ackOwedSince <= 0.0) connection.ackOwedSince = now;
                int ahead = Diff(message.Key, connection.nextExpected);
                if (ahead < 0 || ahead > 1024) continue; // already delivered (a resend), or nonsense
                if (ahead > 0)
                {
                    connection.early[message.Key] = message.Value;
                    continue;
                }
                events.Add(new NetEvent { kind = NetEventKind.Message, connection = connection, data = message.Value, reliable = true });
                connection.nextExpected++;
                byte[] next;
                while (connection.early.TryGetValue(connection.nextExpected, out next))
                {
                    connection.early.Remove(connection.nextExpected);
                    events.Add(new NetEvent { kind = NetEventKind.Message, connection = connection, data = next, reliable = true });
                    connection.nextExpected++;
                }
            }
            foreach (var message in unreliable)
                events.Add(new NetEvent { kind = NetEventKind.Message, connection = connection, data = message, reliable = false });
        }

        // Sends due reliable messages (new, or unacknowledged for a while), queued unreliable
        // ones, owed acknowledgements and keep-alives, in as few packets as fit.
        void Flush(NetConnection connection, double now)
        {
            double resend = Math.Max(0.1, connection.RoundTrip * 1.5 + 0.03);
            var due = new List<int>();
            for (int i = 0; i < connection.unacked.Count; i++)
            {
                var pending = connection.unacked[i];
                if (pending.lastSent < 0.0 || now - pending.lastSent >= resend) due.Add(i);
            }
            bool ackDue = connection.ackOwed && now - connection.ackOwedSince >= AckDelay;
            bool keepAlive = now - connection.lastSend >= KeepAliveSeconds;
            if (due.Count == 0 && connection.unreliable.Count == 0 && !ackDue && !keepAlive) return;

            int reliableIndex = 0, unreliableIndex = 0;
            do
            {
                StartData(connection, now);
                int countAt = packet.Length;
                packet.Byte(0);
                int sentReliable = 0;
                while (reliableIndex < due.Count && sentReliable < 255)
                {
                    int index = due[reliableIndex];
                    var pending = connection.unacked[index];
                    if (packet.Length + 4 + pending.data.Length + 1 > MaxPacket && sentReliable > 0) break;
                    packet.UShort(pending.seq);
                    packet.UShort(pending.data.Length);
                    packet.Bytes(pending.data, 0, pending.data.Length);
                    if (pending.firstSent < 0.0) pending.firstSent = now;
                    pending.lastSent = now;
                    pending.sends++;
                    connection.unacked[index] = pending;
                    sentReliable++;
                    reliableIndex++;
                }
                packet.Raw[countAt] = (byte)sentReliable;

                int unreliableAt = packet.Length;
                packet.Byte(0);
                int sentUnreliable = 0;
                while (unreliableIndex < connection.unreliable.Count && sentUnreliable < 255)
                {
                    var data = connection.unreliable[unreliableIndex];
                    if (packet.Length + 2 + data.Length > MaxPacket && (sentUnreliable > 0 || sentReliable > 0)) break;
                    packet.UShort(data.Length);
                    packet.Bytes(data, 0, data.Length);
                    sentUnreliable++;
                    unreliableIndex++;
                }
                packet.Raw[unreliableAt] = (byte)sentUnreliable;
                socket.Send(packet.Raw, packet.Length, connection.EndPoint);
            } while (reliableIndex < due.Count || unreliableIndex < connection.unreliable.Count);

            connection.unreliable.Clear();
            connection.ackOwed = false;
            connection.ackOwedSince = 0.0;
            connection.lastSend = now;
        }

        void StartData(NetConnection connection, double now)
        {
            packet.Reset();
            packet.Byte(Magic0);
            packet.Byte(Magic1);
            packet.Byte(Protocol);
            packet.Byte(TypeData);
            packet.UShort(connection.nextExpected);
            packet.UShort(Millis(now));
            if (connection.peerStamp < 0)
            {
                packet.UShort(NoStamp);
                packet.UShort(0);
            }
            else
            {
                packet.UShort(connection.peerStamp);
                packet.UShort(Math.Min(60000, (int)((now - connection.peerStampAt) * 1000.0)));
            }
        }

        // A 16-bit millisecond clock for round-trip stamps (65535 means "none yet").
        const int NoStamp = 0xFFFF;

        static int Millis(double now)
        {
            int ms = (int)((long)(now * 1000.0) & 0xFFFF);
            return ms == NoStamp ? 0 : ms;
        }

        void SendControl(byte type, IPEndPoint to, Action<NetWriter> body)
        {
            packet.Reset();
            packet.Byte(Magic0);
            packet.Byte(Magic1);
            packet.Byte(Protocol);
            packet.Byte(type);
            body(packet);
            socket.Send(packet.Raw, packet.Length, to);
        }

        NetConnection Find(IPEndPoint endPoint)
        {
            foreach (var connection in connections)
                if (connection.EndPoint.Equals(endPoint)) return connection;
            return null;
        }

        // Sequence numbers wrap at 65536: the signed distance from b to a.
        internal static int Diff(ushort a, ushort b)
        {
            return (short)(ushort)(a - b);
        }

        internal static bool IsPacket(byte[] data, int count, byte type)
        {
            return count >= 4 && data[0] == Magic0 && data[1] == Magic1 && data[2] == Protocol && data[3] == type;
        }

        internal static void WriteHeader(NetWriter w, byte type)
        {
            w.Byte(Magic0);
            w.Byte(Magic1);
            w.Byte(Protocol);
            w.Byte(type);
        }
    }

    // ---- Finding games on the local network ----

    public struct NetGameInfo
    {
        public IPEndPoint host;   // address and game port to connect to
        public byte[] info;       // whatever the host describes itself with
        public double seenAt;
    }

    // Hosts answer a broadcast query on DiscoveryPort; players send the query and collect the replies.
    public sealed class NetDiscovery
    {
        public const int DiscoveryPort = 27778;

        readonly INetSocket socket;
        readonly bool responder;
        readonly byte[] buffer = new byte[2048];
        readonly NetWriter writer = new NetWriter();
        public readonly List<NetGameInfo> Found = new List<NetGameInfo>();

        // Host side: answers queries with the game port and the info the callback returns.
        public int GamePort;
        public Func<byte[]> Describe;

        NetDiscovery(INetSocket socket, bool responder)
        {
            this.socket = socket;
            this.responder = responder;
        }

        public static NetDiscovery Responder(INetSocket socket, int gamePort, Func<byte[]> describe)
        {
            return new NetDiscovery(socket, true) { GamePort = gamePort, Describe = describe };
        }

        public static NetDiscovery Searcher(INetSocket socket)
        {
            return new NetDiscovery(socket, false);
        }

        // Searcher: ask everyone on the local network (and optionally specific addresses).
        public void Query(IEnumerable<IPEndPoint> targets)
        {
            writer.Reset();
            NetPeer.WriteHeader(writer, NetPeer.TypeQuery);
            foreach (var target in targets) socket.Send(writer.Raw, writer.Length, target);
        }

        public void Update(double now)
        {
            for (int guard = 0; guard < 256; guard++)
            {
                IPEndPoint from;
                int count = socket.Receive(buffer, out from);
                if (count < 0) break;
                if (from == null) continue;
                if (responder && NetPeer.IsPacket(buffer, count, NetPeer.TypeQuery))
                {
                    var info = Describe != null ? Describe() ?? new byte[0] : new byte[0];
                    writer.Reset();
                    NetPeer.WriteHeader(writer, NetPeer.TypeReply);
                    writer.UShort(GamePort);
                    writer.UShort(info.Length);
                    writer.Bytes(info, 0, info.Length);
                    socket.Send(writer.Raw, writer.Length, from);
                }
                else if (!responder && NetPeer.IsPacket(buffer, count, NetPeer.TypeReply))
                {
                    var reader = new NetReader(buffer, 4, count - 4);
                    int port = reader.UShort();
                    var info = reader.Bytes(reader.UShort());
                    if (reader.Failed || port <= 0) continue;
                    var host = new IPEndPoint(from.Address, port);
                    Found.RemoveAll(g => g.host.Equals(host));
                    Found.Add(new NetGameInfo { host = host, info = info, seenAt = now });
                }
            }
        }

        public void Close()
        {
            socket.Close();
        }
    }
}
