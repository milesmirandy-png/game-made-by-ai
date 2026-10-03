// Tests for Assets/Scripts/Net/NetTransport.cs outside Unity (see Tests/README.md).
using System;
using System.Collections.Generic;
using System.Net;
using Swat;

// An in-memory network: packets between sockets are delayed, dropped, duplicated and reordered.
class FakeNet
{
    public double loss, dup, jitter, latency;
    public double now;
    public readonly Random rng = new Random(1234);
    readonly Dictionary<IPEndPoint, FakeSocket> sockets = new Dictionary<IPEndPoint, FakeSocket>();
    public long sent, dropped;
    public FakeSocket Open(int port) { var ep = new IPEndPoint(IPAddress.Loopback, port); var s = new FakeSocket(this, ep); sockets[ep] = s; return s; }
    public void Deliver(IPEndPoint from, IPEndPoint to, byte[] data, int length)
    {
        sent++;
        FakeSocket target;
        if (!sockets.TryGetValue(to, out target) || target.closed) return;
        if (rng.NextDouble() < loss) { dropped++; return; }
        int copies = rng.NextDouble() < dup ? 2 : 1;
        for (int i = 0; i < copies; i++)
        {
            var copy = new byte[length]; Buffer.BlockCopy(data, 0, copy, 0, length);
            target.inbox.Add(new Tuple<double, IPEndPoint, byte[]>(now + latency + rng.NextDouble() * jitter, from, copy));
        }
    }
}
class FakeSocket : INetSocket
{
    readonly FakeNet net; public readonly IPEndPoint ep; public bool closed;
    public readonly List<Tuple<double, IPEndPoint, byte[]>> inbox = new List<Tuple<double, IPEndPoint, byte[]>>();
    public FakeSocket(FakeNet net, IPEndPoint ep) { this.net = net; this.ep = ep; }
    public bool Send(byte[] data, int length, IPEndPoint to) { if (!closed) net.Deliver(ep, to, data, length); return true; }
    public int Receive(byte[] buffer, out IPEndPoint from)
    {
        from = null;
        int best = -1;
        for (int i = 0; i < inbox.Count; i++) if (inbox[i].Item1 <= net.now && (best < 0 || inbox[i].Item1 < inbox[best].Item1)) best = i;
        if (best < 0) return -1;
        var p = inbox[best]; inbox.RemoveAt(best);
        from = p.Item2; Buffer.BlockCopy(p.Item3, 0, buffer, 0, p.Item3.Length); return p.Item3.Length;
    }
    public void Close() { closed = true; }
}

static class NetTransportTest
{
    static int failures;
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + what); if (!ok) failures++; }

    static byte[] Msg(int sender, int index, int size)
    {
        var w = new NetWriter(); w.Int(sender); w.Int(index); for (int i = 8; i < size; i++) w.Byte((index + i) & 255); return w.ToArray();
    }

    static void Run(string name, double loss, double dup, double jitter, double latency, int clients, int messages, int bigEvery)
    {
        Console.WriteLine("== " + name + ": loss " + loss + ", duplicates " + dup + ", jitter " + jitter + "s, latency " + latency + "s, " + clients + " clients, " + messages + " reliable messages each way");
        var net = new FakeNet { loss = loss, dup = dup, jitter = jitter, latency = latency };
        var host = NetPeer.Host(net.Open(27777));
        host.Approve = (ep, h) => new NetReader(h).String() == "let me in" ? null : "wrong password";
        host.Welcome = c => { var w = new NetWriter(); w.Byte(c.Id); return w.ToArray(); };
        var peers = new List<NetPeer>();
        var hello = new NetWriter(); hello.String("let me in");
        for (int i = 0; i < clients; i++) peers.Add(NetPeer.Client(net.Open(40000 + i), new IPEndPoint(IPAddress.Loopback, 27777), hello.ToArray(), 0));
        var hostEvents = new List<NetEvent>();
        var clientEvents = new List<NetEvent>[clients];
        for (int i = 0; i < clients; i++) clientEvents[i] = new List<NetEvent>();
        var hostConnByClient = new Dictionary<int, NetConnection>();
        var nextFromHost = new int[clients]; var nextAtHost = new Dictionary<int, int>();
        var gotFromHost = new int[clients];
        bool ordered = true; int unreliableGot = 0, unreliableSent = 0; int connected = 0; int[] clientId = new int[clients];
        for (int i = 0; i < clients; i++) clientId[i] = -1;
        double dt = 1.0 / 60.0;
        for (int frame = 0; frame < 60 * 120; frame++)
        {
            net.now = frame * dt;
            hostEvents.Clear(); host.Update(net.now, hostEvents);
            foreach (var e in hostEvents)
            {
                if (e.kind == NetEventKind.Connected) { var r = new NetReader(e.data); r.String(); }
                if (e.kind != NetEventKind.Message) continue;
                var rd = new NetReader(e.data); int sender = rd.Int(); int index = rd.Int();
                if (!e.reliable) { unreliableGot++; continue; }
                int expected; nextAtHost.TryGetValue(sender, out expected);
                if (index != expected) ordered = false;
                nextAtHost[sender] = index + 1;
                hostConnByClient[sender] = e.connection;
            }
            for (int i = 0; i < clients; i++)
            {
                clientEvents[i].Clear(); peers[i].Update(net.now, clientEvents[i]);
                foreach (var e in clientEvents[i])
                {
                    if (e.kind == NetEventKind.Connected) { clientId[i] = new NetReader(e.data).Byte(); connected++; }
                    if (e.kind != NetEventKind.Message) continue;
                    var rd = new NetReader(e.data); rd.Int(); int index = rd.Int();
                    if (!e.reliable) { unreliableGot++; continue; }
                    if (index != gotFromHost[i]) ordered = false;
                    gotFromHost[i] = index + 1;
                }
                // Send some reliable and unreliable traffic both ways once connected.
                if (peers[i].Connections.Count == 1 && frame % 2 == 0)
                {
                    var c = peers[i].Connections[0];
                    int k = 0; while (k < 3 && nextFromHost.Length > i && cSent(i) < messages) { peers[i].Send(c, Msg(i, cSent(i), (cSent(i) % bigEvery == 0) ? 900 : 24), true); clientSent[i]++; k++; }
                    peers[i].Send(c, Msg(i, 0, 40), false); unreliableSent++;
                }
            }
            foreach (var pair in hostConnByClient)
            {
                int i = pair.Key;
                if (frame % 2 == 0 && nextFromHost[i] < messages)
                    for (int k = 0; k < 3 && nextFromHost[i] < messages; k++) { host.Send(pair.Value, Msg(99, nextFromHost[i], (nextFromHost[i] % bigEvery == 0) ? 1000 : 30), true); nextFromHost[i]++; }
                if (frame % 4 == 0) { host.Send(pair.Value, Msg(99, 0, 300), false); unreliableSent++; }
            }
        }
        int allAtHost = 0; foreach (var v in nextAtHost.Values) allAtHost += v;
        int allAtClients = 0; foreach (var v in gotFromHost) allAtClients += v;
        Check(connected == clients, "all " + clients + " clients connected (" + connected + ")");
        Check(ordered, "reliable messages arrive in order with no gaps or repeats");
        Check(allAtHost == clients * messages, "host received every reliable message (" + allAtHost + "/" + clients * messages + ")");
        Check(allAtClients == clients * messages, "clients received every reliable message (" + allAtClients + "/" + clients * messages + ")");
        Console.WriteLine("      unreliable delivered " + unreliableGot + " of " + unreliableSent + "; packets sent " + net.sent + ", dropped " + net.dropped);
        double rtt = 0; foreach (var c in host.Connections) rtt += c.RoundTrip; if (host.Connections.Count > 0) rtt /= host.Connections.Count;
        Console.WriteLine("      host's average round-trip estimate " + (rtt * 1000).ToString("0") + " ms");
        clientSent = new int[8];
    }
    static int[] clientSent = new int[8];
    static int cSent(int i) { return clientSent[i]; }

    static void Timeouts()
    {
        Console.WriteLine("== timeouts, refusals and disconnects");
        var net = new FakeNet();
        var host = NetPeer.Host(net.Open(27777));
        host.Approve = (ep, hello) => new NetReader(hello).String() == "ok" ? null : "Lobby is full";
        var good = new NetWriter(); good.String("ok");
        var bad = new NetWriter(); bad.String("no");
        var a = NetPeer.Client(net.Open(40001), new IPEndPoint(IPAddress.Loopback, 27777), good.ToArray(), 0);
        var b = NetPeer.Client(net.Open(40002), new IPEndPoint(IPAddress.Loopback, 27777), bad.ToArray(), 0);
        var c = NetPeer.Client(net.Open(40003), new IPEndPoint(IPAddress.Loopback, 29999), good.ToArray(), 0);
        var ev = new List<NetEvent>(); string bReason = null, cReason = null; bool aConnected = false, hostSawTimeout = false, aSawLeave = false;
        for (int f = 0; f < 60 * 30; f++)
        {
            net.now = f / 60.0;
            ev.Clear(); host.Update(net.now, ev);
            foreach (var e in ev) if (e.kind == NetEventKind.Disconnected && e.reason == "Connection timed out") hostSawTimeout = true;
            ev.Clear(); a.Update(net.now, ev); foreach (var e in ev) { if (e.kind == NetEventKind.Connected) aConnected = true; if (e.kind == NetEventKind.Disconnected) aSawLeave = true; }
            ev.Clear(); b.Update(net.now, ev); foreach (var e in ev) if (e.kind == NetEventKind.ConnectFailed) bReason = e.reason;
            ev.Clear(); c.Update(net.now, ev); foreach (var e in ev) if (e.kind == NetEventKind.ConnectFailed) cReason = e.reason;
            if (f == 60 * 5) net.loss = 1.0; // the line goes dead
        }
        Check(aConnected, "approved client connects");
        Check(bReason == "Lobby is full", "refused client is told why (" + bReason + ")");
        Check(cReason != null && cReason.StartsWith("No answer"), "connecting to nobody gives up after the timeout");
        Check(hostSawTimeout, "host notices a client that went silent");
        Check(aSawLeave, "client notices the host went silent");

        // A clean leave.
        net = new FakeNet();
        host = NetPeer.Host(net.Open(27777));
        a = NetPeer.Client(net.Open(40001), new IPEndPoint(IPAddress.Loopback, 27777), good.ToArray(), 0);
        string leaveReason = null;
        for (int f = 0; f < 120; f++)
        {
            net.now = f / 60.0;
            ev.Clear(); host.Update(net.now, ev); foreach (var e in ev) if (e.kind == NetEventKind.Disconnected) leaveReason = e.reason;
            ev.Clear(); a.Update(net.now, ev);
            if (f == 60) a.Close("Left the lobby");
        }
        Check(leaveReason == "Left the lobby", "host hears a clean leave at once (" + leaveReason + ")");
    }

    static void Wraparound()
    {
        Console.WriteLine("== sequence numbers wrap past 65535");
        var net = new FakeNet { loss = 0.2, jitter = 0.05, latency = 0.02 };
        var host = NetPeer.Host(net.Open(27777));
        var hello = new NetWriter(); hello.String("x");
        var a = NetPeer.Client(net.Open(40001), new IPEndPoint(IPAddress.Loopback, 27777), hello.ToArray(), 0);
        var ev = new List<NetEvent>(); int got = 0; bool ordered = true; int sent = 0; const int total = 70000;
        for (int f = 0; f < 60 * 400 && got < total; f++)
        {
            net.now = f / 60.0;
            ev.Clear(); host.Update(net.now, ev);
            foreach (var e in ev) if (e.kind == NetEventKind.Message) { int idx = new NetReader(e.data).Int(); if (idx != got) ordered = false; got = idx + 1; }
            ev.Clear(); a.Update(net.now, ev);
            if (a.Connections.Count == 1) for (int k = 0; k < 40 && sent < total; k++) { var w = new NetWriter(); w.Int(sent++); a.Send(a.Connections[0], w.ToArray(), true); }
        }
        Check(got == total && ordered, "70000 reliable messages through 20% loss, in order (" + got + ")");
    }

    static void Malformed()
    {
        Console.WriteLine("== malformed packets");
        var net = new FakeNet();
        var hostSocket = net.Open(27777);
        var host = NetPeer.Host(hostSocket);
        var hello = new NetWriter(); hello.String("x");
        var a = NetPeer.Client(net.Open(40001), new IPEndPoint(IPAddress.Loopback, 27777), hello.ToArray(), 0);
        var junk = net.Open(40009);
        var ev = new List<NetEvent>(); var rng = new Random(7); int messages = 0; bool crashed = false;
        try
        {
            for (int f = 0; f < 600; f++)
            {
                net.now = f / 60.0;
                // Random garbage, and truncated real headers, at the host.
                var g = new byte[rng.Next(0, 64)]; rng.NextBytes(g);
                if (g.Length >= 4 && f % 2 == 0) { g[0] = 0x53; g[1] = 0x57; g[2] = NetPeer.Protocol; g[3] = (byte)rng.Next(1, 8); }
                junk.Send(g, g.Length, new IPEndPoint(IPAddress.Loopback, 27777));
                ev.Clear(); host.Update(net.now, ev); foreach (var e in ev) if (e.kind == NetEventKind.Message) messages++;
                ev.Clear(); a.Update(net.now, ev);
                if (a.Connections.Count == 1 && f % 10 == 0) { var w = new NetWriter(); w.Int(f); a.Send(a.Connections[0], w.ToArray(), true); }
            }
        }
        catch (Exception ex) { crashed = true; Console.WriteLine(ex); }
        Check(!crashed, "garbage packets never throw");
        Check(messages > 50, "real traffic still gets through (" + messages + " messages)");
        var r = new NetReader(new byte[] { 1, 2 });
        r.Int();
        Check(r.Failed, "reading past the end of a message is detected");
    }

    static void BigMessages()
    {
        Console.WriteLine("== large reliable messages (match setup) among small ones, with loss");
        var net = new FakeNet { loss = 0.2, jitter = 0.05, latency = 0.03 };
        var host = NetPeer.Host(net.Open(27777));
        var hello = new NetWriter(); hello.String("x");
        var a = NetPeer.Client(net.Open(40001), new IPEndPoint(IPAddress.Loopback, 27777), hello.ToArray(), 0);
        var ev = new List<NetEvent>(); var sizes = new List<int>(); bool sent = false;
        for (int f = 0; f < 60 * 20; f++)
        {
            net.now = f / 60.0;
            ev.Clear(); host.Update(net.now, ev);
            if (!sent && host.Connections.Count == 1)
            {
                sent = true;
                foreach (int size in new[] { 30, 3000, 40, 7900, 12 }) { var w = new NetWriter(); for (int i = 0; i < size; i++) w.Byte(i & 255); host.Send(host.Connections[0], w.ToArray(), true); }
            }
            ev.Clear(); a.Update(net.now, ev);
            foreach (var e in ev) if (e.kind == NetEventKind.Message) sizes.Add(e.data.Length);
        }
        Check(string.Join(",", sizes) == "30,3000,40,7900,12", "big and small messages arrive whole and in order (" + string.Join(",", sizes) + ")");
    }

    static int Main()
    {
        Run("clean network", 0, 0, 0, 0.01, 3, 2000, 50);
        Run("bad network", 0.25, 0.05, 0.12, 0.04, 3, 2000, 40);
        Run("terrible network", 0.5, 0.1, 0.3, 0.08, 2, 600, 30);
        Timeouts();
        Wraparound();
        Malformed();
        BigMessages();
        Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
        return failures;
    }
}
