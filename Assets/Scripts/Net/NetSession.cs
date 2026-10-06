using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Swat
{
    public enum NetRole { None, Host, Client }
    public enum NetPhase { Idle, Connecting, Lobby, Loading, Match, Results }

    // A player in an online lobby or match.
    public class NetPlayer
    {
        public int peerId;            // 0 is the host
        public string name;
        public int side;              // 0 blue, 1 red
        public string officerId, weaponId;
        public Color skin;            // alpha 0 = from the officer
        public NetConnection connection;
        public int actorId = -1;      // in the current match
        public int ping;              // milliseconds, as the host measures it
    }

    // A game found on the local network.
    public class LanGame
    {
        public IPEndPoint address;
        public string host, map;
        public int mode, players, max;
        public bool open;
    }

    // One person or bot as the host describes them at the start of a match.
    public struct RosterEntry
    {
        public int id, side, weapon;
        public string name, officerId;
        public Color skin;
        public bool human;
        public Vector3 position;
        public float yaw;
    }

    // What a joining player needs to show the host's match: their team and place, bases, zone, flags and everyone in it.
    public class MatchSetup
    {
        public int side, actorId;
        public Vector3 spawn;
        public float spawnYaw;
        public readonly Vector3[] bases = new Vector3[2];
        public readonly float[] baseYaw = new float[2];
        public Bounds zone;
        public readonly Vector3[] flagHomes = new Vector3[2];
        public readonly List<RosterEntry> roster = new List<RosterEntry>();
    }

    // Online play, peer to peer: one player hosts (their copy of the game runs
    // the match: bots, scoring, flags and the zone), the others join by address
    // or from the local network list. In the lobby everyone sees the host's
    // match settings and picks a team. During a match:
    //   the host sends everyone a snapshot 15 times a second (where everybody
    //   is, scores, clock, flags, zone) and events (takedowns, flag moves);
    //   each player moves their own officer and sends where they are 20 times
    //   a second, and reports their own shots and the hits they land; the host
    //   passes hits on to whoever was hit, and that player reports being
    //   tagged out. Nobody's copy pauses during an online match.
    public class NetSession : MonoBehaviour
    {
        public const int GamePort = 27777;
        public const int MaxPlayers = 8;
        const float SnapshotInterval = 1f / 15f, StateInterval = 1f / 20f;
        const int NoId = 255;

        enum Msg : byte { Lobby = 1, ChooseSide, Profile, Start, Setup, Loaded, Snapshot, State, Shot, Hit, Down, Respawn, Event, End, ToLobby, Remove, Ping }

        public static NetSession Instance { get; private set; }
        public static bool IsHost { get { return Instance != null && Instance.Role == NetRole.Host; } }
        public static bool IsClient { get { return Instance != null && Instance.Role == NetRole.Client && Instance.Phase != NetPhase.Connecting; } }
        public static bool Online { get { return IsHost || IsClient; } }
        // Shared scratch list for the end points of one trigger pull.
        public static readonly List<Vector3> ShotEnds = new List<Vector3>();

        public NetRole Role { get; private set; }
        public NetPhase Phase { get; private set; }
        public string Status { get; private set; }
        public bool StatusBad { get; private set; }
        public string LocalName { get; private set; }
        public string HostName { get; private set; }
        public string Addresses { get; private set; }
        public bool LanVisible { get { return responder != null; } }   // hosting: answering LAN searches
        public int Port { get; private set; }
        public int MyPeerId { get; private set; }
        public int LocalSide { get; private set; }
        public VersusOptions Options { get; private set; }
        public MatchSetup PendingSetup { get; private set; }
        public readonly List<NetPlayer> Players = new List<NetPlayer>();
        // The local network is searched automatically while the Game Modes screen is open.
        public bool Searching { get { return searcher != null; } }
        public float SearchingFor { get { return searcher != null ? (float)(Now - searchStarted) : 0f; } }
        public readonly List<LanGame> LanGames = new List<LanGame>();

        public List<NetPlayer> RemotePlayers
        {
            get
            {
                var list = new List<NetPlayer>();
                foreach (var player in Players) if (player.peerId != 0) list.Add(player);
                return list;
            }
        }

        NetPeer peer;
        NetDiscovery responder, searcher;
        readonly List<NetEvent> events = new List<NetEvent>();
        readonly NetWriter writer = new NetWriter();
        readonly Vector3[] shotBuffer = new Vector3[16];
        float nextSnapshot, nextState, nextLobby, nextSearchAttempt;
        double searchStarted, nextQuery, nextTargets;
        List<IPEndPoint> searchTargets;
        int hostToken;
        int snapshotSeq, lastSnapshotSeq = -1;
        int seed;
        int lastAttackerId = -1, lastAttackerWeapon = -1;

        static double Now { get { return Time.realtimeSinceStartupAsDouble; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            ShotEnds.Clear();
            backgroundSetting = null;
        }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            Leave(null);
            if (Instance == this) Instance = null;
        }

        void OnApplicationQuit()
        {
            Leave(null);
        }

        // ---- Starting, joining, leaving ----

        public bool Host(string name)
        {
            Leave(null);
            LocalName = CleanName(name);
            UdpNetSocket socket = null;
            for (int port = GamePort; port < GamePort + 4 && socket == null; port++)
            {
                try { socket = new UdpNetSocket(port, false); }
                catch (SocketException) { }
            }
            if (socket == null)
            {
                SetStatus("Couldn't open a network port (" + GamePort + "-" + (GamePort + 3) + " are in use).", true);
                return false;
            }
            Port = socket.Port;
            peer = NetPeer.Host(socket);
            peer.Approve = Approve;
            peer.Welcome = connection =>
            {
                writer.Reset();
                writer.Byte(connection.Id);
                return writer.ToArray();
            };
            hostToken = Random.Range(1, int.MaxValue);
            try { responder = NetDiscovery.Responder(new UdpNetSocket(NetDiscovery.DiscoveryPort, true), Port, Describe); }
            catch (SocketException) { responder = null; } // another game on this computer answers the local network
            Role = NetRole.Host;
            Phase = NetPhase.Lobby;
            KeepRunningInBackground(true);
            HostName = LocalName;
            MyPeerId = 0;
            LocalSide = 0;
            Options = SaveManager.Progress.versus;
            Players.Clear();
            Players.Add(new NetPlayer { peerId = 0, name = LocalName, side = 0, officerId = LeaderId(), weaponId = LeaderWeapon(), skin = new Color(0f, 0f, 0f, 0f) });
            Addresses = LocalAddresses();
            SetStatus("Hosting on port " + Port + ". Others join with " + Addresses + (Port != GamePort ? ":" + Port : "") + ".", false);
            return true;
        }

        public bool Join(string address, string name)
        {
            Leave(null);
            LocalName = CleanName(name);
            IPEndPoint endPoint;
            string error;
            if (!Resolve(address, out endPoint, out error))
            {
                SetStatus(error, true);
                return false;
            }
            UdpNetSocket socket;
            try { socket = new UdpNetSocket(0, false); }
            catch (SocketException e)
            {
                SetStatus("Couldn't open a network socket: " + e.Message, true);
                return false;
            }
            writer.Reset();
            writer.Int(ContentHash());
            writer.String(LocalName);
            writer.String(LeaderId());
            writer.String(LeaderWeapon());
            peer = NetPeer.Client(socket, endPoint, writer.ToArray(), Now);
            Role = NetRole.Client;
            Phase = NetPhase.Connecting;
            KeepRunningInBackground(true);
            Players.Clear();
            SetStatus("Connecting to " + endPoint + "...", false);
            return true;
        }

        // Leaves the lobby or match (as host: closes it for everyone).
        public void Leave(string status)
        {
            if (peer != null) peer.Close(Role == NetRole.Host ? "The host closed the game" : "Left the game");
            peer = null;
            if (responder != null) responder.Close();
            responder = null;
            Role = NetRole.None;
            Phase = NetPhase.Idle;
            Players.Clear();
            PendingSetup = null;
            KeepRunningInBackground(false);
            if (status != null) SetStatus(status, false);
        }

        public void StopSearching()
        {
            if (searcher != null) searcher.Close();
            searcher = null;
            LanGames.Clear();
        }

        // Starts (or restarts) asking the local network who is hosting; repeats every two seconds.
        public void SearchLan()
        {
            if (searcher == null)
            {
                try { searcher = NetDiscovery.Searcher(new UdpNetSocket(0, true)); }
                catch (SocketException e)
                {
                    SetStatus("Couldn't search the local network: " + e.Message, true);
                    return;
                }
                searchStarted = Now;
            }
            nextTargets = 0.0;
            Query();
        }

        void Query()
        {
            nextQuery = Now + 2.0;
            // Network adapters can change (Wi-Fi reconnects); look them up again now and then.
            if (searchTargets == null || Now >= nextTargets)
            {
                searchTargets = NetDiscovery.BroadcastTargets(NetDiscovery.DiscoveryPort);
                nextTargets = Now + 10.0;
            }
            searcher.Query(searchTargets);
        }

        // The games found, one entry per host (a host on this computer answers on several addresses;
        // its network address is shown rather than 127.0.0.1).
        void RefreshLanGames()
        {
            LanGames.Clear();
            var tokens = new List<int>();
            foreach (var found in searcher.Found)
            {
                var r = new NetReader(found.info);
                int token = r.Int();
                var game = new LanGame { address = found.host, host = r.String(), mode = r.Byte(), map = r.String(), players = r.Byte(), max = r.Byte(), open = r.Bool() };
                if (r.Failed) continue;
                int existing = tokens.IndexOf(token);
                if (existing >= 0)
                {
                    if (IPAddress.IsLoopback(LanGames[existing].address.Address) && !IPAddress.IsLoopback(game.address.Address)) LanGames[existing] = game;
                    continue;
                }
                tokens.Add(token);
                LanGames.Add(game);
            }
        }

        // A game whose window isn't focused normally stops. Online it has to keep going (the host's
        // game runs everyone's match, and two copies on one PC must both run), so while you are in an
        // online game it keeps running in the background; offline it goes back to the project setting.
        static bool? backgroundSetting;

        static void KeepRunningInBackground(bool keep)
        {
            if (keep)
            {
                if (backgroundSetting == null) backgroundSetting = Application.runInBackground;
                Application.runInBackground = true;
            }
            else if (backgroundSetting != null)
            {
                Application.runInBackground = backgroundSetting.Value;
                backgroundSetting = null;
            }
        }

        void SetStatus(string text, bool bad)
        {
            Status = text;
            StatusBad = bad;
        }

        static string CleanName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length > 16) name = name.Substring(0, 16);
            if (name.Length == 0) name = OfficerSelectionManager.Leader != null ? OfficerSelectionManager.Leader.callsign : "Officer";
            return name;
        }

        static string LeaderId()
        {
            return OfficerSelectionManager.Leader != null ? OfficerSelectionManager.Leader.id : "";
        }

        static string LeaderWeapon()
        {
            var leader = OfficerSelectionManager.Leader;
            if (leader == null) return "rifle_compact";
            var loadout = GameData.LoadoutFor(leader);
            var weapon = GameData.Weapon(loadout.primaryId);
            return weapon != null && !loadout.useShield ? weapon.id : loadout.sidearmId;
        }

        // Raised whenever a message gains or changes a field (2: lean and slide in movement updates;
        // 3: Gun Game and Elimination, round state in snapshots, pings).
        const int MessageVersion = 3;

        // Both copies of the game must have the same weapons, officers, maps and messages.
        static int ContentHash()
        {
            unchecked
            {
                uint hash = 2166136261u;
                var text = new System.Text.StringBuilder("swat-net-" + NetPeer.Protocol + "." + MessageVersion);
                foreach (var weapon in GameData.AllWeapons) text.Append('|').Append(weapon.id);
                foreach (var officer in GameData.AllOfficers) text.Append('|').Append(officer.id);
                foreach (var map in VersusMatch.MapIds) text.Append('|').Append(map);
                foreach (char c in text.ToString())
                {
                    hash ^= c;
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }

        static bool Resolve(string address, out IPEndPoint endPoint, out string error)
        {
            endPoint = null;
            error = null;
            address = (address ?? "").Trim();
            if (address.Length == 0)
            {
                error = "Type the host's address first (for example 192.168.1.20).";
                return false;
            }
            int port = GamePort;
            string host = address;
            int colon = address.LastIndexOf(':');
            if (colon > 0 && address.IndexOf(':') == colon)
            {
                if (!int.TryParse(address.Substring(colon + 1), out port) || port <= 0 || port > 65535)
                {
                    error = "The port after ':' must be a number.";
                    return false;
                }
                host = address.Substring(0, colon);
            }
            IPAddress ip;
            if (!IPAddress.TryParse(host, out ip))
            {
                try
                {
                    foreach (var candidate in Dns.GetHostAddresses(host))
                        if (candidate.AddressFamily == AddressFamily.InterNetwork) { ip = candidate; break; }
                }
                catch (System.Exception) { ip = null; }
                if (ip == null)
                {
                    error = "Couldn't find '" + host + "'. Use the host's IP address.";
                    return false;
                }
            }
            endPoint = new IPEndPoint(ip, port);
            return true;
        }

        static string LocalAddresses()
        {
            var list = NetDiscovery.LocalAddresses();
            if (list.Count > 3) list.RemoveRange(3, list.Count - 3);
            return list.Count > 0 ? string.Join(" or ", list.ToArray()) : "this computer's IP address";
        }

        // ---- Host: lobby ----

        string Approve(IPEndPoint from, byte[] hello)
        {
            var reader = new NetReader(hello);
            int hash = reader.Int();
            if (reader.Failed) return "That isn't SWAT: Tactical Response.";
            if (hash != ContentHash()) return "Different game version: both players need the same build of the game.";
            if (Phase == NetPhase.Results) return "The host is looking at match results. Try again in a moment.";
            if (Phase != NetPhase.Lobby) return "A match is in progress. Try again when the host is back in the lobby.";
            if (Players.Count >= MaxPlayers) return "The lobby is full (" + MaxPlayers + " players).";
            return null;
        }

        byte[] Describe()
        {
            var w = new NetWriter();
            w.Int(hostToken);
            w.String(HostName);
            w.Byte(Options != null ? Options.mode : 1);
            w.String(Options != null ? Options.mapId : "");
            w.Byte(Players.Count);
            w.Byte(MaxPlayers);
            w.Bool(Phase == NetPhase.Lobby);
            return w.ToArray();
        }

        void PlayerJoined(NetConnection connection, byte[] hello)
        {
            var reader = new NetReader(hello);
            reader.Int();
            string name = CleanName(reader.String());
            string officerId = reader.String();
            string weaponId = reader.String();
            int blue = 0, red = 0;
            foreach (var p in Players) if (p.side == 0) blue++; else red++;
            var player = new NetPlayer { peerId = connection.Id, name = UniqueName(name), side = red < blue ? 1 : 0, officerId = officerId, weaponId = weaponId, connection = connection, skin = new Color(0f, 0f, 0f, 0f) };
            connection.Tag = player;
            Players.Add(player);
            AudioManager.Play2D(Sound.RadioOrder, 0.5f, 1.1f, SoundCategory.Interface);
            UIManager.Notify(player.name + " joined");
            SendLobby();
        }

        string UniqueName(string name)
        {
            string candidate = name;
            for (int n = 2; Players.Exists(p => p.name == candidate); n++) candidate = name + " " + n;
            return candidate;
        }

        // Host: the lobby as everyone should see it.
        public void SendLobby()
        {
            if (!IsHost) return;
            nextLobby = Time.unscaledTime + 2f;
            foreach (var player in Players)
                if (player.connection != null) player.ping = Mathf.RoundToInt((float)player.connection.RoundTrip * 1000f);
            writer.Reset();
            writer.Byte((byte)Msg.Lobby);
            writer.String(HostName);
            WriteOptions(writer, Options);
            writer.Byte((int)Phase);
            writer.Byte(Players.Count);
            foreach (var player in Players)
            {
                writer.Byte(player.peerId);
                writer.String(player.name);
                writer.Byte(player.side);
                writer.UShort(Mathf.Clamp(player.ping, 0, 9999));
            }
            peer.SendToAll(writer.ToArray(), true);
        }

        // Host: move a player to the other team (from the lobby list).
        public void SetSide(NetPlayer player, int side)
        {
            if (!IsHost || Phase != NetPhase.Lobby || player == null || player.peerId == 0) return;
            player.side = Mathf.Clamp(side, 0, 1);
            SendLobby();
        }

        public void HostStartMatch()
        {
            if (!IsHost || (Phase != NetPhase.Lobby && Phase != NetPhase.Results && Phase != NetPhase.Match)) return;
            Options = SaveManager.Progress.versus;
            seed = Random.Range(1, int.MaxValue);
            Phase = NetPhase.Loading;
            foreach (var player in Players) player.actorId = -1;
            Players[0].officerId = LeaderId();
            Players[0].weaponId = LeaderWeapon();
            writer.Reset();
            writer.Byte((byte)Msg.Start);
            WriteOptions(writer, Options);
            writer.Int(seed);
            peer.SendToAll(writer.ToArray(), true);
            SendLobby();
            GameManager.Instance.StartOnlineMatch(Options, seed, 0);
        }

        // Host: everyone back to the lobby.
        public void HostToLobby()
        {
            if (!IsHost) return;
            Phase = NetPhase.Lobby;
            writer.Reset();
            writer.Byte((byte)Msg.ToLobby);
            peer.SendToAll(writer.ToArray(), true);
            SendLobby();
            GameManager.Instance.OpenVersusSetup();
        }

        // ---- Host: the match ----

        // The host's match is built: tell each player their place and who else is in it.
        public void OnMatchBuilt(VersusMatch match)
        {
            Phase = NetPhase.Match;
            nextSnapshot = 0f;
            var roster = new List<RosterEntry>();
            var me = match.Player;
            roster.Add(new RosterEntry
            {
                id = match.MyId, side = match.MySide, name = LocalName, officerId = LeaderId(), human = true, skin = new Color(0f, 0f, 0f, 0f),
                weapon = VersusMatch.WeaponIndex(me != null ? me.Weapons.Current.Data : null), position = me != null ? me.Position : Vector3.zero, yaw = me != null ? me.transform.eulerAngles.y : 0f,
            });
            foreach (var other in match.Others)
            {
                var entry = new RosterEntry { id = other.NetId, side = other.Side, name = other.Callsign, human = other.IsHuman, position = other.Position, yaw = other.Transform.eulerAngles.y, skin = new Color(0f, 0f, 0f, 0f) };
                var bot = other as ArenaBot;
                var remote = other as NetActor;
                if (bot != null)
                {
                    entry.officerId = bot.OfficerId;
                    entry.weapon = VersusMatch.WeaponIndex(bot.Gun.Data);
                    entry.skin = bot.Look.skin;
                }
                else if (remote != null)
                {
                    entry.officerId = remote.Owner != null ? remote.Owner.officerId : null;
                    entry.weapon = VersusMatch.WeaponIndex(remote.Weapon);
                }
                roster.Add(entry);
            }
            foreach (var remote in match.Remotes)
            {
                var owner = remote.Owner;
                if (owner == null || owner.connection == null) continue;
                writer.Reset();
                writer.Byte((byte)Msg.Setup);
                writer.Byte(owner.side);
                writer.Byte(remote.NetId);
                Vec(writer, remote.Position);
                writer.Float(remote.transform.eulerAngles.y);
                for (int side = 0; side < 2; side++)
                {
                    Vec(writer, match.Bases[side]);
                    writer.Float(match.BaseYaw[side]);
                }
                Vec(writer, match.Zone.center);
                Vec(writer, match.Zone.size);
                for (int side = 0; side < 2; side++) Vec(writer, match.FlagHome(side));
                writer.Byte(roster.Count);
                foreach (var entry in roster)
                {
                    writer.Byte(entry.id);
                    writer.Byte(entry.side);
                    writer.String(entry.name);
                    writer.String(entry.officerId ?? "");
                    writer.Byte(entry.weapon < 0 ? NoId : entry.weapon);
                    writer.Byte(Mathf.RoundToInt(entry.skin.r * 255f));
                    writer.Byte(Mathf.RoundToInt(entry.skin.g * 255f));
                    writer.Byte(Mathf.RoundToInt(entry.skin.b * 255f));
                    writer.Byte(entry.skin.a > 0f ? 1 : 0);
                    writer.Bool(entry.human);
                    Vec(writer, entry.position);
                    writer.Float(entry.yaw);
                }
                peer.Send(owner.connection, writer.ToArray(), true);
            }
        }

        void SendSnapshot(VersusMatch match)
        {
            writer.Reset();
            writer.Byte((byte)Msg.Snapshot);
            writer.UShort(snapshotSeq++ & 0xFFFF);
            writer.Float(Time.time);
            writer.Float(match.Elapsed);
            writer.Float(match.Score[0]);
            writer.Float(match.Score[1]);
            writer.Short(Mathf.RoundToInt(match.ZoneControl * 1000f));
            writer.Byte(match.ZoneOwner + 1);
            writer.Byte(match.ZoneCount[0]);
            writer.Byte(match.ZoneCount[1]);
            writer.Byte(Mathf.Clamp(match.Round, 0, 255));
            writer.Bool(match.RoundOver);
            writer.Float(match.RoundStart);
            for (int side = 0; side < 2; side++)
            {
                var flag = match.Flags[side];
                writer.Bool(flag != null);
                if (flag == null) continue;
                Pos(writer, flag.position);
                int carrier = match.IdOf(flag.carrier);
                writer.Byte(carrier < 0 ? NoId : carrier);
                writer.Byte(Mathf.Clamp(Mathf.RoundToInt((Time.time - flag.droppedAt) * 4f), 0, 255));
            }
            var me = match.Player;
            int count = match.Others.Count + (me != null ? 1 : 0);
            writer.Byte(count);
            if (me != null)
                ActorState(writer, match.MyId, me.Position, me.transform.eulerAngles.y, me.IsAlive, me.IsMoving, me.IsSprinting, me.IsCrouched, me.FlashlightOn, me.Weapons.Current.Data,
                    me.Health.Fraction * 100f, match.PlayerKills, match.PlayerDeaths, match.PlayerCaptures, me.IsSliding, me.Peeking, me.Lean);
            foreach (var other in match.Others)
            {
                var bot = other as ArenaBot;
                var remote = other as NetActor;
                WeaponData weapon = bot != null ? bot.Gun.Data : remote != null ? remote.Weapon : null;
                bool running = bot != null ? bot.IsMoving : remote != null && remote.IsRunning;
                // A player still loading counts as not alive here, but shouldn't look tagged out to the others.
                bool alive = remote != null ? !remote.Down : other.IsAlive;
                ActorState(writer, other.NetId, other.Position, other.Transform.eulerAngles.y, alive, other.IsMoving, running, other.IsCrouched, other.FlashlightOn, weapon,
                    other.Health, other.Kills, other.Deaths, other.Captures, remote != null && remote.IsSliding, remote != null && remote.Peeking, remote != null ? remote.Lean : 0f);
            }
            var data = writer.ToArray();
            foreach (var player in Players)
                if (player.connection != null && player.actorId >= 0) peer.Send(player.connection, data, false);
        }

        static void ActorState(NetWriter w, int id, Vector3 position, float yaw, bool alive, bool moving, bool running, bool crouched, bool light, WeaponData weapon, float health, int kills, int deaths, int captures,
            bool sliding, bool peeking, float lean)
        {
            w.Byte(id);
            w.Byte((alive ? 1 : 0) | (moving ? 2 : 0) | (running ? 4 : 0) | (crouched ? 8 : 0) | (light ? 16 : 0) | (sliding ? 32 : 0) | (peeking ? 64 : 0));
            Pos(w, position);
            w.Byte(Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 360f * 256f) & 255);
            int index = VersusMatch.WeaponIndex(weapon);
            w.Byte(index < 0 ? NoId : index);
            w.Byte(Mathf.Clamp(Mathf.RoundToInt(health), 0, 255));
            w.Byte(Mathf.Clamp(kills, 0, 255));
            w.Byte(Mathf.Clamp(deaths, 0, 255));
            w.Byte(Mathf.Clamp(captures, 0, 255));
            w.Byte(LeanByte(lean));
        }

        // Lean (-1 left .. +1 right) in one byte.
        static int LeanByte(float lean)
        {
            return Mathf.Clamp(Mathf.RoundToInt(lean * 100f) + 128, 28, 228);
        }

        static float LeanOf(int value)
        {
            return Mathf.Clamp((value - 128) / 100f, -1f, 1f);
        }

        public void SendEvent(MatchEvent e)
        {
            if (!IsHost || peer == null) return;
            writer.Reset();
            writer.Byte((byte)Msg.Event);
            writer.Byte((int)e.kind);
            writer.Byte(e.side);
            writer.Byte(e.a < 0 ? NoId : e.a);
            writer.Byte(e.b < 0 ? NoId : e.b);
            writer.Byte(e.weapon < 0 ? NoId : e.weapon);
            writer.String(e.name ?? "");
            peer.SendToAll(writer.ToArray(), true);
        }

        public void SendEnd(VersusResult result)
        {
            if (!IsHost || peer == null) return;
            Phase = NetPhase.Results;
            writer.Reset();
            writer.Byte((byte)Msg.End);
            writer.Byte(result.winner + 1);
            writer.String(result.reason);
            writer.Float(result.time);
            writer.UShort(result.blueScore);
            writer.UShort(result.redScore);
            writer.UShort(result.scoreLimit);
            writer.Byte(result.rows.Count);
            foreach (var row in result.rows)
            {
                writer.Byte(row.actorId < 0 ? NoId : row.actorId);
                writer.String(row.name);
                writer.Byte(row.side);
                writer.UShort(row.kills);
                writer.UShort(row.deaths);
                writer.UShort(row.captures);
            }
            peer.SendToAll(writer.ToArray(), true);
            SendLobby();
        }

        public void SendRespawn(NetActor remote, Vector3 position, float yaw)
        {
            if (!IsHost || remote.Owner == null || remote.Owner.connection == null) return;
            writer.Reset();
            writer.Byte((byte)Msg.Respawn);
            Vec(writer, position);
            writer.Float(yaw);
            peer.Send(remote.Owner.connection, writer.ToArray(), true);
        }

        // Host: someone hit another player's stand-in; that player takes the damage on their own copy.
        public void SendHitToOwner(NetActor remote, int attackerId, DamageInfo info)
        {
            if (!IsHost || remote.Owner == null || remote.Owner.connection == null) return;
            WriteHit(attackerId, info);
            peer.Send(remote.Owner.connection, writer.ToArray(), true);
        }

        // ---- Client ----

        // Client: you hit someone; the host applies it.
        public void SendHitToHost(NetActor target, DamageInfo info)
        {
            if (!IsClient || peer.Connections.Count == 0) return;
            WriteHit(target.NetId, info);
            peer.Send(peer.Connections[0], writer.ToArray(), true);
        }

        void WriteHit(int id, DamageInfo info)
        {
            writer.Reset();
            writer.Byte((byte)Msg.Hit);
            writer.Byte(id < 0 ? NoId : id);
            writer.Float(info.amount);
            writer.Bool(info.lessLethal);
            writer.Float(info.stun);
            int weapon = VersusMatch.WeaponIndex(info.weapon);
            writer.Byte(weapon < 0 ? NoId : weapon);
            writer.Short(Mathf.RoundToInt(Mathf.Clamp(info.direction.x, -1f, 1f) * 1000f));
            writer.Short(Mathf.RoundToInt(Mathf.Clamp(info.direction.z, -1f, 1f) * 1000f));
        }

        // Client: you were tagged out; the host credits whoever did it.
        // A ping: a player sends theirs to the host; the host passes it to that player's teammates.
        public void SendPing(int fromId, int side, bool enemy, Vector3 point)
        {
            if (peer == null) return;
            writer.Reset();
            writer.Byte((byte)Msg.Ping);
            writer.Byte(fromId < 0 ? NoId : fromId);
            writer.Bool(enemy);
            Pos(writer, point);
            var data = writer.ToArray();
            if (IsClient)
            {
                if (peer.Connections.Count > 0) peer.Send(peer.Connections[0], data, true);
                return;
            }
            if (!IsHost) return;
            foreach (var player in Players)
                if (player.connection != null && player.side == side && player.actorId != fromId) peer.Send(player.connection, data, true);
        }

        public void SendDown(int attackerId, int weaponIndex)
        {
            if (!IsClient || peer.Connections.Count == 0) return;
            if (attackerId < 0) attackerId = lastAttackerId;
            if (weaponIndex < 0) weaponIndex = lastAttackerWeapon;
            writer.Reset();
            writer.Byte((byte)Msg.Down);
            writer.Byte(attackerId < 0 ? NoId : attackerId);
            writer.Byte(weaponIndex < 0 ? NoId : weaponIndex);
            peer.Send(peer.Connections[0], writer.ToArray(), true);
        }

        // Your shot (or, on the host, a bot's): everyone else sees the flash and tracers.
        public void SendShot(int actorId, WeaponData weapon, Vector3 muzzle, List<Vector3> ends)
        {
            if (!Online || peer == null || Phase != NetPhase.Match) return;
            WriteShot(actorId, weapon, muzzle, ends, ends.Count);
            var data = writer.ToArray();
            if (IsHost)
            {
                foreach (var player in Players)
                    if (player.connection != null && player.actorId >= 0) peer.Send(player.connection, data, false);
            }
            else if (peer.Connections.Count > 0) peer.Send(peer.Connections[0], data, false);
        }

        void WriteShot(int actorId, WeaponData weapon, Vector3 muzzle, IList<Vector3> ends, int count)
        {
            writer.Reset();
            writer.Byte((byte)Msg.Shot);
            writer.Byte(actorId);
            int index = VersusMatch.WeaponIndex(weapon);
            writer.Byte(index < 0 ? NoId : index);
            Pos(writer, muzzle);
            count = Mathf.Min(count, shotBuffer.Length);
            writer.Byte(count);
            for (int i = 0; i < count; i++) Pos(writer, ends[i]);
        }

        void SendState(PlayerController me)
        {
            if (peer.Connections.Count == 0) return;
            writer.Reset();
            writer.Byte((byte)Msg.State);
            writer.Float(Time.time);
            Vec(writer, me.Position);
            writer.UShort(Mathf.RoundToInt(Mathf.Repeat(me.transform.eulerAngles.y, 360f) / 360f * 65535f));
            writer.Byte((me.IsAlive ? 1 : 0) | (me.IsMoving ? 2 : 0) | (me.IsSprinting ? 4 : 0) | (me.IsCrouched ? 8 : 0) | (me.FlashlightOn ? 16 : 0)
                | (me.IsSliding ? 32 : 0) | (me.Peeking ? 64 : 0));
            int weapon = VersusMatch.WeaponIndex(me.Weapons.Current.Data);
            writer.Byte(weapon < 0 ? NoId : weapon);
            writer.Byte(Mathf.Clamp(Mathf.RoundToInt(me.Health.Fraction * 100f), 0, 100));
            writer.Byte(LeanByte(me.Lean));
            peer.Send(peer.Connections[0], writer.ToArray(), false);
        }

        public void ChooseSide(int side)
        {
            if (!IsClient || peer.Connections.Count == 0) return;
            writer.Reset();
            writer.Byte((byte)Msg.ChooseSide);
            writer.Byte(Mathf.Clamp(side, 0, 1));
            peer.Send(peer.Connections[0], writer.ToArray(), true);
        }

        // Client: your current officer and weapon (after visiting the squad or loadout screens).
        public void SendProfile()
        {
            if (!IsClient || peer.Connections.Count == 0) return;
            sentOfficer = LeaderId();
            sentWeapon = LeaderWeapon();
            writer.Reset();
            writer.Byte((byte)Msg.Profile);
            writer.String(sentOfficer);
            writer.String(sentWeapon);
            peer.Send(peer.Connections[0], writer.ToArray(), true);
        }

        // Client, in the lobby: tells the host if you picked another officer or weapon.
        public void SyncProfile()
        {
            if (IsClient && (LeaderId() != sentOfficer || LeaderWeapon() != sentWeapon)) SendProfile();
        }

        string sentOfficer, sentWeapon;

        // Client: the map is built and you are in the host's match.
        public void OnMirrorStarted()
        {
            PendingSetup = null;
            Phase = NetPhase.Match;
            lastSnapshotSeq = -1;
            if (peer.Connections.Count == 0) return;
            writer.Reset();
            writer.Byte((byte)Msg.Loaded);
            peer.Send(peer.Connections[0], writer.ToArray(), true);
        }

        // ---- Every frame ----

        void Update()
        {
            // LAN: while the Game Modes screen is open and you aren't in a game, keep the list of local games fresh.
            var screen = GameManager.Instance;
            bool lookForGames = Role == NetRole.None && screen != null && screen.State == GameState.VersusSetup;
            if (lookForGames && searcher == null && Time.unscaledTime >= nextSearchAttempt)
            {
                nextSearchAttempt = Time.unscaledTime + 5f; // if the socket can't open, don't retry every frame
                SearchLan();
            }
            if (searcher != null)
            {
                if (!lookForGames) StopSearching();
                else
                {
                    searcher.Update(Now);
                    if (Now >= nextQuery) Query();
                    // Hosts that stop answering drop off the list.
                    searcher.Found.RemoveAll(g => Now - g.seenAt > 6.0);
                    RefreshLanGames();
                }
            }
            if (peer == null) return;
            if (responder != null) responder.Update(Now);

            events.Clear();
            peer.Update(Now, events);
            foreach (var e in events)
            {
                if (peer == null) break; // left while handling an earlier event
                switch (e.kind)
                {
                    case NetEventKind.Connected:
                        if (Role == NetRole.Host) PlayerJoined(e.connection, e.data);
                        else
                        {
                            MyPeerId = new NetReader(e.data).Byte();
                            Phase = NetPhase.Lobby;
                            sentOfficer = LeaderId(); // the hello carried these
                            sentWeapon = LeaderWeapon();
                            SetStatus("Connected. Waiting for the host to start.", false);
                            AudioManager.Play2D(Sound.RadioOrder, 0.5f, 1.1f, SoundCategory.Interface);
                        }
                        break;
                    case NetEventKind.ConnectFailed:
                        Leave(null);
                        SetStatus(e.reason, true);
                        break;
                    case NetEventKind.Disconnected:
                        if (Role == NetRole.Host) PlayerLeft(e.connection, e.reason);
                        else HostLost(e.reason);
                        break;
                    case NetEventKind.Message:
                        if (Role == NetRole.Host) HostReceive(e.connection, e.data);
                        else ClientReceive(e.data);
                        break;
                }
            }
            if (peer == null) return;

            var match = VersusMatch.Instance;
            bool inMatch = match != null && VersusMatch.Active && Phase == NetPhase.Match;
            if (Role == NetRole.Host)
            {
                if (inMatch && Time.unscaledTime >= nextSnapshot)
                {
                    nextSnapshot = Time.unscaledTime + SnapshotInterval;
                    SendSnapshot(match);
                }
                if (Phase == NetPhase.Lobby && Time.unscaledTime >= nextLobby) SendLobby(); // keeps pings fresh
            }
            else if (inMatch && match.Mirror && match.Player != null && Time.unscaledTime >= nextState)
            {
                nextState = Time.unscaledTime + StateInterval;
                SendState(match.Player);
            }
        }

        // Messages queued during the frame (shots, hits) go out now rather than next frame.
        void LateUpdate()
        {
            if (peer != null) peer.Flush(Now);
        }

        void PlayerLeft(NetConnection connection, string reason)
        {
            var player = connection.Tag as NetPlayer;
            if (player == null) return;
            Players.Remove(player);
            UIManager.Notify(player.name + " left" + (reason == "Connection timed out" ? " (connection lost)" : ""), true);
            var match = VersusMatch.Instance;
            if (match != null && VersusMatch.Active && player.actorId >= 0)
            {
                foreach (var remote in match.Remotes)
                    if (remote.NetId == player.actorId)
                    {
                        match.RemoveRemote(remote, true);
                        break;
                    }
                writer.Reset();
                writer.Byte((byte)Msg.Remove);
                writer.Byte(player.actorId);
                peer.SendToAll(writer.ToArray(), true);
            }
            SendLobby();
        }

        void HostLost(string reason)
        {
            bool playing = Phase == NetPhase.Loading || Phase == NetPhase.Match || Phase == NetPhase.Results;
            Leave(null);
            SetStatus("Disconnected: " + (string.IsNullOrEmpty(reason) ? "the host left" : reason) + ".", true);
            if (playing && GameManager.Instance != null)
            {
                GameManager.Instance.OpenVersusSetup();
                UIManager.Notify("Lost the connection to the host", true);
            }
        }

        // ---- Host: messages from players ----

        void HostReceive(NetConnection connection, byte[] data)
        {
            var player = connection.Tag as NetPlayer;
            if (player == null || data.Length == 0) return;
            var r = new NetReader(data);
            var kind = (Msg)r.Byte();
            var match = VersusMatch.Instance;
            NetActor actor = FindRemote(match, player.actorId);
            switch (kind)
            {
                case Msg.ChooseSide:
                {
                    int side = r.Byte();
                    if (!r.Failed && Phase == NetPhase.Lobby && side != player.side)
                    {
                        player.side = Mathf.Clamp(side, 0, 1);
                        SendLobby();
                    }
                    break;
                }
                case Msg.Profile:
                {
                    string officerId = r.String();
                    string weaponId = r.String();
                    if (r.Failed) break;
                    player.officerId = officerId;
                    player.weaponId = weaponId;
                    break;
                }
                case Msg.Loaded:
                    if (actor != null) actor.Ready = true;
                    break;
                case Msg.State:
                {
                    float time = r.Float();
                    Vector3 position = Vec(r);
                    float yaw = r.UShort() / 65535f * 360f;
                    int flags = r.Byte();
                    int weapon = r.Byte();
                    int health = r.Byte();
                    float lean = LeanOf(r.Byte());
                    if (r.Failed || actor == null || !Sane(position)) break;
                    actor.Ready = true;
                    actor.Push(time, position, yaw, (flags & 2) != 0, (flags & 4) != 0, (flags & 8) != 0, (flags & 16) != 0);
                    actor.SetMoves((flags & 32) != 0, (flags & 64) != 0, lean);
                    if (weapon != NoId) actor.SetWeapon(VersusMatch.WeaponAt(weapon));
                    if (!actor.Down) actor.Health = health;
                    break;
                }
                case Msg.Shot:
                {
                    r.Byte(); // sender's own actor id; the host knows who sent it
                    int weapon = r.Byte();
                    Pos(r);
                    int count = Mathf.Min(r.Byte(), shotBuffer.Length);
                    for (int i = 0; i < count; i++) shotBuffer[i] = Pos(r);
                    if (r.Failed || actor == null) break;
                    var data2 = VersusMatch.WeaponAt(weapon);
                    actor.ShowShot(data2, shotBuffer, count);
                    // Pass it on to everyone else.
                    WriteShot(actor.NetId, data2, actor.ChestPosition, shotBuffer, count);
                    var forward = writer.ToArray();
                    foreach (var other in Players)
                        if (other.connection != null && other != player && other.actorId >= 0) peer.Send(other.connection, forward, false);
                    break;
                }
                case Msg.Hit:
                {
                    int targetId = r.Byte();
                    var info = ReadHit(r);
                    if (r.Failed || actor == null || match == null || !VersusMatch.Active) break;
                    info.shooter = actor;
                    info.attacker = actor.Team;
                    var target = match.TargetById(targetId);
                    if (target == null || !target.IsAlive) break;
                    int targetSide = target == (ICombatTarget)match.Player ? match.MySide : ((IVersusMember)target).Side;
                    if (targetSide == actor.Side) break; // no friendly fire
                    info.point = target.ChestPosition;
                    // The host's own officer only ignores police fire, so other players' hits come in as the other team's.
                    if (target == (ICombatTarget)match.Player) info.attacker = Team.Suspect;
                    target.Damageable.TakeDamage(info);
                    break;
                }
                case Msg.Down:
                {
                    int attacker = r.Byte();
                    int weapon = r.Byte();
                    if (r.Failed || actor == null || match == null) break;
                    match.OnRemoteDown(actor, attacker == NoId ? -1 : attacker, weapon == NoId ? -1 : weapon);
                    break;
                }
                case Msg.Ping:
                {
                    r.Byte(); // the sender's own idea of its id; the connection says who it is
                    bool enemy = r.Bool();
                    Vector3 point = Pos(r);
                    if (r.Failed || actor == null || match == null || !VersusMatch.Active || !Sane(point)) break;
                    match.AddPing(point, enemy, actor.NetId, actor.Side, actor.Side == match.MySide);
                    SendPing(actor.NetId, actor.Side, enemy, point);
                    break;
                }
            }
        }

        static NetActor FindRemote(VersusMatch match, int actorId)
        {
            if (match == null || actorId < 0) return null;
            foreach (var remote in match.Remotes) if (remote.NetId == actorId) return remote;
            return null;
        }

        static DamageInfo ReadHit(NetReader r)
        {
            var info = new DamageInfo();
            info.amount = Mathf.Clamp(r.Float(), 0f, 500f);
            info.lessLethal = r.Bool();
            info.stun = Mathf.Clamp(r.Float(), 0f, 10f);
            info.weapon = VersusMatch.WeaponAt(r.Byte());
            info.direction = new Vector3(r.Short() / 1000f, 0f, r.Short() / 1000f);
            return info;
        }

        static bool Sane(Vector3 p)
        {
            return Mathf.Abs(p.x) < 2000f && Mathf.Abs(p.y) < 500f && Mathf.Abs(p.z) < 2000f;
        }

        // ---- Client: messages from the host ----

        void ClientReceive(byte[] data)
        {
            if (data.Length == 0) return;
            var r = new NetReader(data);
            var kind = (Msg)r.Byte();
            var match = VersusMatch.Instance;
            bool mirroring = match != null && VersusMatch.Active && match.Mirror;
            switch (kind)
            {
                case Msg.Lobby:
                {
                    string host = r.String();
                    var options = ReadOptions(r);
                    var phase = (NetPhase)r.Byte();
                    int count = r.Byte();
                    var players = new List<NetPlayer>();
                    for (int i = 0; i < count; i++)
                        players.Add(new NetPlayer { peerId = r.Byte(), name = r.String(), side = r.Byte(), ping = r.UShort() });
                    if (r.Failed) break;
                    HostName = host;
                    if (Phase == NetPhase.Lobby || Options == null) Options = options;
                    Players.Clear();
                    Players.AddRange(players);
                    foreach (var p in Players) if (p.peerId == MyPeerId) LocalSide = p.side;
                    if (Phase == NetPhase.Lobby && phase != NetPhase.Lobby) SetStatus("The host is in a match; you'll join the next one.", false);
                    break;
                }
                case Msg.Start:
                {
                    var options = ReadOptions(r);
                    int startSeed = r.Int();
                    if (r.Failed) break;
                    Options = options;
                    seed = startSeed;
                    PendingSetup = null;
                    Phase = NetPhase.Loading;
                    SendProfile();
                    GameManager.Instance.StartOnlineMatch(options, startSeed, LocalSide);
                    break;
                }
                case Msg.Setup:
                {
                    var setup = new MatchSetup();
                    setup.side = r.Byte();
                    setup.actorId = r.Byte();
                    setup.spawn = Vec(r);
                    setup.spawnYaw = r.Float();
                    for (int side = 0; side < 2; side++)
                    {
                        setup.bases[side] = Vec(r);
                        setup.baseYaw[side] = r.Float();
                    }
                    Vector3 center = Vec(r), size = Vec(r);
                    setup.zone = new Bounds(center, size);
                    for (int side = 0; side < 2; side++) setup.flagHomes[side] = Vec(r);
                    int count = r.Byte();
                    for (int i = 0; i < count; i++)
                    {
                        var entry = new RosterEntry();
                        entry.id = r.Byte();
                        entry.side = r.Byte();
                        entry.name = r.String();
                        entry.officerId = r.String();
                        int weapon = r.Byte();
                        entry.weapon = weapon == NoId ? -1 : weapon;
                        float cr = r.Byte() / 255f, cg = r.Byte() / 255f, cb = r.Byte() / 255f;
                        entry.skin = new Color(cr, cg, cb, r.Byte() > 0 ? 1f : 0f);
                        entry.human = r.Bool();
                        entry.position = Vec(r);
                        entry.yaw = r.Float();
                        setup.roster.Add(entry);
                    }
                    if (r.Failed) break;
                    LocalSide = setup.side;
                    PendingSetup = setup;
                    GameManager.Instance.OnOnlineSetup();
                    break;
                }
                case Msg.Snapshot:
                    if (mirroring) ApplySnapshot(match, r);
                    break;
                case Msg.Shot:
                {
                    int id = r.Byte();
                    var weapon = VersusMatch.WeaponAt(r.Byte());
                    Pos(r);
                    int count = Mathf.Min(r.Byte(), shotBuffer.Length);
                    for (int i = 0; i < count; i++) shotBuffer[i] = Pos(r);
                    if (r.Failed || !mirroring || id == match.MyId) break;
                    var actor = FindRemote(match, id);
                    if (actor != null) actor.ShowShot(weapon, shotBuffer, count);
                    break;
                }
                case Msg.Hit:
                {
                    int attackerId = r.Byte();
                    var info = ReadHit(r);
                    var me = match != null ? match.Player : null;
                    if (r.Failed || !mirroring || me == null || !me.IsAlive) break;
                    var attacker = match.Find(attackerId == NoId ? -1 : attackerId);
                    info.shooter = attacker as MonoBehaviour;
                    info.attacker = Team.Suspect; // your officer ignores police fire; the host already ruled out your own team
                    info.point = me.ChestPosition;
                    lastAttackerId = attackerId == NoId ? -1 : attackerId;
                    lastAttackerWeapon = VersusMatch.WeaponIndex(info.weapon);
                    me.Health.TakeDamage(info);
                    break;
                }
                case Msg.Respawn:
                {
                    Vector3 position = Vec(r);
                    float yaw = r.Float();
                    if (!r.Failed && mirroring) match.RespawnLocal(position, yaw);
                    break;
                }
                case Msg.Event:
                {
                    var e = new MatchEvent { kind = (MatchEventKind)r.Byte(), side = r.Byte() };
                    int a = r.Byte(), b = r.Byte(), weapon = r.Byte();
                    e.a = a == NoId ? -1 : a;
                    e.b = b == NoId ? -1 : b;
                    e.weapon = weapon == NoId ? -1 : weapon;
                    e.name = r.String();
                    if (!r.Failed && mirroring) match.Apply(e);
                    break;
                }
                case Msg.End:
                {
                    var result = new VersusResult { winner = r.Byte() - 1, reason = r.String(), time = r.Float(), online = true };
                    result.blueScore = r.UShort();
                    result.redScore = r.UShort();
                    result.scoreLimit = r.UShort();
                    int count = r.Byte();
                    for (int i = 0; i < count; i++)
                    {
                        var row = new VersusResult.ScoreRow { actorId = r.Byte(), name = r.String(), side = r.Byte(), kills = r.UShort(), deaths = r.UShort(), captures = r.UShort() };
                        if (row.actorId == NoId) row.actorId = -1;
                        result.rows.Add(row);
                    }
                    if (r.Failed || !mirroring) break;
                    result.mode = match.Mode;
                    result.mapName = match.MapName;
                    result.side = match.MySide;
                    for (int i = 0; i < result.rows.Count; i++)
                    {
                        var row = result.rows[i];
                        if (row.actorId != match.MyId) continue;
                        row.isPlayer = true;
                        result.rows[i] = row;
                        result.playerKills = row.kills;
                        result.playerDeaths = row.deaths;
                        result.playerCaptures = row.captures;
                    }
                    Phase = NetPhase.Results;
                    match.Finish(result);
                    break;
                }
                case Msg.ToLobby:
                    Phase = NetPhase.Lobby;
                    PendingSetup = null;
                    SetStatus("Back in the lobby. Waiting for the host to start.", false);
                    GameManager.Instance.OpenVersusSetup();
                    break;
                case Msg.Remove:
                {
                    int id = r.Byte();
                    if (!r.Failed && mirroring) match.RemoveRemote(FindRemote(match, id), false);
                    break;
                }
                case Msg.Ping:
                {
                    int from = r.Byte();
                    bool enemy = r.Bool();
                    Vector3 point = Pos(r);
                    if (!r.Failed && mirroring && Sane(point)) match.AddPing(point, enemy, from == NoId ? -1 : from, match.MySide, true);
                    break;
                }
            }
        }

        void ApplySnapshot(VersusMatch match, NetReader r)
        {
            int seq = r.UShort();
            // Drop snapshots older than the newest one applied (they can arrive out of order).
            if (lastSnapshotSeq >= 0 && (short)(ushort)(seq - lastSnapshotSeq) <= 0) return;
            float hostTime = r.Float();
            float elapsed = r.Float();
            float blue = r.Float(), red = r.Float();
            float zone = r.Short() / 1000f;
            int owner = r.Byte() - 1;
            int inBlue = r.Byte(), inRed = r.Byte();
            int round = r.Byte();
            bool roundOver = r.Bool();
            float roundStart = r.Float();
            var flagPosition = new Vector3[2];
            var flagCarrier = new int[2];
            var flagDropped = new float[2];
            var hasFlag = new bool[2];
            for (int side = 0; side < 2; side++)
            {
                hasFlag[side] = r.Bool();
                if (!hasFlag[side]) continue;
                flagPosition[side] = Pos(r);
                int carrier = r.Byte();
                flagCarrier[side] = carrier == NoId ? -1 : carrier;
                flagDropped[side] = r.Byte() / 4f;
            }
            int count = r.Byte();
            if (r.Failed) return;
            lastSnapshotSeq = seq;
            match.ApplyState(elapsed, blue, red, zone, owner, inBlue, inRed);
            match.ApplyRound(round, roundOver, roundStart);
            for (int i = 0; i < count; i++)
            {
                int id = r.Byte();
                int flags = r.Byte();
                Vector3 position = Pos(r);
                float yaw = r.Byte() / 256f * 360f;
                int weapon = r.Byte();
                int health = r.Byte(), kills = r.Byte(), deaths = r.Byte(), captures = r.Byte();
                float lean = LeanOf(r.Byte());
                if (r.Failed) return;
                if (id == match.MyId) continue; // you move yourself
                var actor = FindRemote(match, id);
                if (actor == null) continue;
                actor.Push(hostTime, position, yaw, (flags & 2) != 0, (flags & 4) != 0, (flags & 8) != 0, (flags & 16) != 0);
                actor.SetMoves((flags & 32) != 0, (flags & 64) != 0, lean);
                bool alive = (flags & 1) != 0;
                if (alive == actor.Down) actor.SetDown(!alive);
                if (weapon != NoId) actor.SetWeapon(VersusMatch.WeaponAt(weapon));
                if (alive) actor.Health = health;
                actor.Kills = kills;
                actor.Deaths = deaths;
                actor.Captures = captures;
            }
            for (int side = 0; side < 2; side++)
                if (hasFlag[side]) match.ApplyFlag(side, flagPosition[side], flagCarrier[side], flagDropped[side]);
        }

        // ---- Encoding helpers ----

        static void WriteOptions(NetWriter w, VersusOptions o)
        {
            w.Byte(o.mode);
            w.String(o.mapId);
            w.Byte(o.teamSize);
            w.Byte(o.scoreIndex);
            w.Byte(o.timeIndex);
            w.Byte(o.botSkill);
            w.Byte(o.timeOfDay);
        }

        static VersusOptions ReadOptions(NetReader r)
        {
            return new VersusOptions
            {
                mode = Mathf.Clamp(r.Byte(), 1, VersusMatch.LastMode), mapId = r.String(), teamSize = Mathf.Clamp(r.Byte(), 1, VersusMatch.MaxTeamSize),
                scoreIndex = Mathf.Clamp(r.Byte(), 0, 2), timeIndex = Mathf.Clamp(r.Byte(), 0, 2), botSkill = Mathf.Clamp(r.Byte(), 0, 2), timeOfDay = Mathf.Clamp(r.Byte(), 0, 2),
            };
        }

        static void Vec(NetWriter w, Vector3 v)
        {
            w.Float(v.x);
            w.Float(v.y);
            w.Float(v.z);
        }

        static Vector3 Vec(NetReader r)
        {
            return new Vector3(r.Float(), r.Float(), r.Float());
        }

        // Positions in snapshots and shots: centimetres in 16 bits (maps are well inside +-327 m).
        static void Pos(NetWriter w, Vector3 v)
        {
            w.Short(Mathf.Clamp(Mathf.RoundToInt(v.x * 100f), -32767, 32767));
            w.Short(Mathf.Clamp(Mathf.RoundToInt(v.y * 100f), -32767, 32767));
            w.Short(Mathf.Clamp(Mathf.RoundToInt(v.z * 100f), -32767, 32767));
        }

        static Vector3 Pos(NetReader r)
        {
            return new Vector3(r.Short() / 100f, r.Short() / 100f, r.Short() / 100f);
        }
    }
}
