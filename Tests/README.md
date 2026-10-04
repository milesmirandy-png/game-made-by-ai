# Tests outside Unity

These programs check the plain-C# parts of the game without the Unity editor.
They are outside `Assets/`, so Unity never compiles them.

**Online transport** (`Assets/Scripts/Net/NetTransport.cs`), with
[Mono](https://www.mono-project.com/) (`mcs` and `mono`), from the project folder:

```
mcs -out:nettest.exe Tests/NetTransportTest.cs Assets/Scripts/Net/NetTransport.cs
mono nettest.exe

mcs -out:sockettest.exe Tests/NetSocketTest.cs Assets/Scripts/Net/NetTransport.cs
mono sockettest.exe
```

- `NetTransportTest` runs hosts and players over a simulated network that
  loses, duplicates, delays and reorders packets, and checks that reliable
  messages arrive once and in order, plus timeouts, refusals, sequence
  wrap-around, garbage packets and large messages.
- `NetSocketTest` uses real UDP sockets on this computer (ports 27777 and
  27778 must be free): LAN discovery (broadcast, each network adapter's
  broadcast address, this computer), finding this computer's address,
  connecting, messages both ways, leaving, and that two hosts can't share a port.

Each prints PASS/FAIL per check and exits with the number of failures.
