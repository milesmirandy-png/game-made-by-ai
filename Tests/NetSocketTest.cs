// Tests NetTransport over real UDP sockets on this computer (see Tests/README.md).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using Swat;

static class NetSocketTest
{
    static int Main()
    {
        int failures = 0;
        Action<bool, string> check = (ok, what) => { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + what); if (!ok) failures++; };
        var clock = Stopwatch.StartNew();
        Func<double> now = () => clock.Elapsed.TotalSeconds;

        var hostSocket = new UdpNetSocket(27777, false);
        var host = NetPeer.Host(hostSocket);
        host.Welcome = c => new byte[] { (byte)c.Id };
        var discovery = NetDiscovery.Responder(new UdpNetSocket(NetDiscovery.DiscoveryPort, true), hostSocket.Port, () => { var w = new NetWriter(); w.String("Test host"); return w.ToArray(); });

        var searcher = NetDiscovery.Searcher(new UdpNetSocket(0, true));
        var targets = NetDiscovery.BroadcastTargets(NetDiscovery.DiscoveryPort);
        Console.WriteLine("      searching: " + string.Join(", ", targets));
        searcher.Query(targets);
        var hello = new NetWriter(); hello.String("player one");
        var clientSocket = new UdpNetSocket(0, false);
        var client = NetPeer.Client(clientSocket, new IPEndPoint(IPAddress.Loopback, 27777), hello.ToArray(), now());
        var ev = new List<NetEvent>();
        string helloSeen = null; int welcomeId = -1; int toHost = 0, toClient = 0; bool disconnectSeen = false;
        for (int i = 0; i < 400; i++)
        {
            double t = now();
            discovery.Update(t);
            searcher.Update(t);
            ev.Clear(); host.Update(t, ev);
            foreach (var e in ev)
            {
                if (e.kind == NetEventKind.Connected) helloSeen = new NetReader(e.data).String();
                if (e.kind == NetEventKind.Message) toHost++;
                if (e.kind == NetEventKind.Disconnected) disconnectSeen = true;
            }
            ev.Clear(); client.Update(t, ev);
            foreach (var e in ev)
            {
                if (e.kind == NetEventKind.Connected) welcomeId = e.data[0];
                if (e.kind == NetEventKind.Message) toClient++;
            }
            if (client.Connections.Count == 1 && i < 300) { var w = new NetWriter(); w.Int(i); client.Send(client.Connections[0], w.ToArray(), i % 2 == 0); }
            if (host.Connections.Count == 1 && i < 300) { var w = new NetWriter(); w.Int(i); host.Send(host.Connections[0], w.ToArray(), true); }
            if (i == 350) client.Close("bye");
            Thread.Sleep(5);
        }
        foreach (var g in searcher.Found) Console.WriteLine("      found " + g.host);
        check(searcher.Found.Count >= 1 && searcher.Found.TrueForAll(g => new NetReader(g.info).String() == "Test host" && g.host.Port == 27777), "LAN discovery (broadcast, each adapter's broadcast, this PC) finds the host and its game port");
        check(NetDiscovery.LocalAddresses().Count >= 1, "this computer's network address is found (" + string.Join(", ", NetDiscovery.LocalAddresses()) + ")");
        check(helloSeen == "player one", "host receives the player's hello over a real UDP socket");
        check(welcomeId >= 1, "player receives the host's welcome (id " + welcomeId + ")");
        check(toHost > 250, "player's messages reach the host (" + toHost + ")");
        check(toClient > 250, "host's messages reach the player (" + toClient + ")");
        check(disconnectSeen, "host sees the player leave");
        bool threw = false;
        try { new UdpNetSocket(27777, false); } catch (System.Net.Sockets.SocketException) { threw = true; }
        check(threw, "a second host on the same port is refused (the game then tries the next port)");
        host.Close("done"); discovery.Close(); searcher.Close();
        Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
        return failures;
    }
}
