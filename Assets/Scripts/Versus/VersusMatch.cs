using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    public class VersusResult
    {
        public GameMode mode;
        public string mapName;
        public int winner;            // winning side: 0 blue, 1 red, -1 draw
        public int side;              // the side you played on
        public int blueScore, redScore, scoreLimit;
        public float time;
        public string reason;
        public int playerKills, playerDeaths, playerCaptures;
        public bool online, hosted;   // an online match, and whether you were the host
        public readonly List<ScoreRow> rows = new List<ScoreRow>();

        public struct ScoreRow
        {
            public string name;
            public int side, kills, deaths, captures, actorId;
            public bool isPlayer;
        }
    }

    // Everyone in a match other than you: bots, and in online matches the other players.
    public interface IVersusMember : ICombatTarget
    {
        int NetId { get; }
        int Side { get; }
        string Callsign { get; }
        bool Seen { get; }
        float Health { get; }
        float MaxHealth { get; }
        float RespawnAt { get; }
        float LastShotTime { get; }
        int Kills { get; set; }
        int Deaths { get; set; }
        int Captures { get; set; }
        bool IsHuman { get; }
        void SetSeen(bool seen);
        void SetVisible(bool visible);
    }

    public enum MatchEventKind { Takedown, FlagTaken, FlagDropped, FlagReturned, FlagCaptured, ZoneTaken, PlayerLeft }

    // Something worth a line in the feed (and maybe a banner). The host decides these and sends them
    // to everyone, and each player words them from their own side ("You", "your flag").
    public struct MatchEvent
    {
        public MatchEventKind kind;
        public int side;      // victim's side, the flag's side, or the zone's new owner
        public int a, b;      // actors: killer and victim, or whoever moved the flag (-1 none)
        public int weapon;    // weapon index for takedowns (-1 none)
        public string name;   // a player who left
    }

    // Runs a game-mode match: Blue Team against Red Team on one of the mission
    // maps, with respawns. Offline, you and your squad (as bots) are Blue and
    // the Red Team is bots. Online, the host runs the match (bots, scoring,
    // flags, zone) and the other players' copies mirror it (NetSession). The
    // modes are framed as Tactical Response Unit training exercises with
    // marking rounds, so "tagged out" just means out until the respawn.
    //   Team Deathmatch: first team to the tag-out limit.
    //   Capture the Flag: take the other team's flag back to your own (yours must be home).
    //   Zone Control: hold the zone alone to own it; the owner scores every second.
    public class VersusMatch : MonoBehaviour
    {
        public static VersusMatch Instance { get; private set; }
        public static bool Active { get { return Instance != null && Instance.running; } }

        // ---- Options (shown on the Game Modes screen) ----

        public static readonly string[] ModeNames = { "Mission", "Team Deathmatch", "Capture the Flag", "Zone Control" };
        public static readonly string[] ModeGoals =
        {
            "",
            "Tag out the other team. First to the limit wins.",
            "Bring the other team's flag to your base. Your own flag must be home to score.",
            "Stand in the zone with no opponents inside to take it. Your team scores every second it's yours.",
        };
        static readonly int[][] ScoreLimits = { new[] { 0 }, new[] { 15, 25, 40 }, new[] { 1, 3, 5 }, new[] { 60, 100, 200 } };
        static readonly int[][] TimeLimits = { new[] { 0 }, new[] { 5, 10, 15 }, new[] { 8, 12, 20 }, new[] { 5, 8, 12 } };
        public static readonly string[] SkillNames = { "Easy", "Normal", "Hard" };
        // Maps that work for team play (single floor, room to move).
        public static readonly string[] MapIds = { "warehouse", "office", "store", "motel", "bank", "clinic", "nightclub", "factory", "training" };
        static readonly string[] RedNames = { "Viper", "Rook", "Jackal", "Mako", "Vandal", "Hex", "Talon", "Sable" };
        public const float RespawnDelay = 5f;
        public const int MaxTeamSize = 6;

        public static int ScoreLimitFor(GameMode mode, int index)
        {
            var list = ScoreLimits[(int)mode];
            return list[Mathf.Clamp(index, 0, list.Length - 1)];
        }

        public static int MinutesFor(GameMode mode, int index)
        {
            var list = TimeLimits[(int)mode];
            return list[Mathf.Clamp(index, 0, list.Length - 1)];
        }

        public static string ScoreUnit(GameMode mode)
        {
            return mode == GameMode.TeamDeathmatch ? "tag-outs" : mode == GameMode.CaptureTheFlag ? "captures" : "points";
        }

        public static string SideName(int side) { return side == 0 ? "Blue" : "Red"; }

        // The mission record the normal briefing-free flow deploys with (officer selection, loadout, deploy).
        public static MissionData CreateMission(VersusOptions options)
        {
            var mode = (GameMode)Mathf.Clamp(options.mode, 1, 3);
            var m = ScriptableObject.CreateInstance<MissionData>();
            string map = System.Array.IndexOf(MapIds, options.mapId) >= 0 ? options.mapId : MapIds[0];
            m.name = m.id = "versus_" + mode + "_" + map;
            m.displayName = ModeNames[(int)mode];
            m.location = MissionBriefing.MapName(map);
            m.description = ModeGoals[(int)mode];
            m.mapId = map;
            m.mode = mode;
            m.maxSquad = 3;
            m.timeOfDay = (TimeOfDay)Mathf.Clamp(options.timeOfDay, 0, 2);
            m.alarmArmedChance = m.camerasActiveChance = m.randomLockChance = m.powerOutageChance = 0f;
            m.optionalCount = 0;
            m.parTime = MinutesFor(mode, options.timeIndex) * 60f;
            m.thumbnailColor = mode == GameMode.TeamDeathmatch ? new Color(0.45f, 0.18f, 0.18f) : mode == GameMode.CaptureTheFlag ? new Color(0.2f, 0.3f, 0.5f) : new Color(0.25f, 0.4f, 0.25f);
            return m;
        }

        // Weapons travel over the network as their index in the game's weapon list.
        public static int WeaponIndex(WeaponData weapon)
        {
            return weapon != null ? GameData.AllWeapons.IndexOf(weapon) : -1;
        }

        public static WeaponData WeaponAt(int index)
        {
            var all = GameData.AllWeapons;
            return index >= 0 && index < all.Count ? all[index] : null;
        }

        // ---- Match state ----

        public class Flag
        {
            public int side;
            public Vector3 home, position;
            public ICombatTarget carrier;
            public bool AtHome { get { return carrier == null && (position - home).sqrMagnitude < 0.01f; } }
            public float droppedAt;
            public Transform visual;
        }

        public struct FeedLine
        {
            public string text;
            public int side;
            public float time;
        }

        public GameMode Mode { get; private set; }
        public int ScoreLimit { get; private set; }
        public float TimeLimit { get; private set; }
        public float Elapsed { get; private set; }
        public float TimeLeft { get { return Mathf.Max(0f, TimeLimit - Elapsed); } }
        public readonly float[] Score = new float[2];
        public readonly List<ArenaBot> Bots = new List<ArenaBot>();
        public readonly List<NetActor> Remotes = new List<NetActor>();      // other players (online)
        public readonly List<IVersusMember> Others = new List<IVersusMember>(); // bots and other players
        public readonly Flag[] Flags = new Flag[2];
        public Bounds Zone { get; private set; }
        public float ZoneControl { get; private set; }   // -1 red owns ... 0 neutral ... +1 blue owns
        public int ZoneOwner { get; private set; }       // -1 nobody, 0 blue, 1 red
        public int[] ZoneCount { get; private set; }
        public readonly List<FeedLine> Feed = new List<FeedLine>();
        public float PlayerRespawnAt { get; private set; }
        public string PlayerTaggedBy { get; private set; }
        public int PlayerKills, PlayerDeaths, PlayerCaptures;
        public string MapName { get; private set; }
        public Vector3[] Bases { get; private set; }
        public int MySide { get; private set; }          // the side you play on (always blue offline or hosting)
        public int MyId { get; private set; }            // your actor id (0 offline or hosting)
        public bool Mirror { get; private set; }         // online, not hosting: the host runs the match
        public string LocalName { get; private set; }
        public float[] BaseYaw { get { return baseYaw; } }

        readonly List<ICombatTarget>[] members = { new List<ICombatTarget>(), new List<ICombatTarget>() };
        readonly List<Vector3>[] spawns = { new List<Vector3>(), new List<Vector3>() };
        readonly List<Vector3> roamPoints = new List<Vector3>();
        readonly float[] baseYaw = new float[2];
        readonly int[] spawnCursor = new int[2];
        bool running;
        int skill, nextId;
        float nextVisibility, nextDoors;
        LevelLayout level;
        PlayerController player;
        Renderer[] zoneEdges;
        int zoneColorState = -2;
        readonly List<GameObject> spawned = new List<GameObject>();

        void Awake()
        {
            Instance = this;
            ZoneCount = new int[2];
            Bases = new Vector3[2];
        }

        public List<ICombatTarget> Opponents(int side) { return members[1 - side]; }
        public List<ICombatTarget> Team(int side) { return members[side]; }
        public PlayerController Player { get { return player; } }

        // ---- Setup ----

        // Offline or hosting: this copy of the game runs the match.
        public void Begin(MissionData mission, VersusOptions options, LevelLayout layout, Transform actors, PlayerController leader)
        {
            Clear();
            Setup(mission, options, layout, actors, leader);
            skill = Mathf.Clamp(options.botSkill, 0, 2);
            MySide = 0;
            MyId = 0;
            nextId = 1;

            foreach (var door in level.doors) if (door != null) door.OpenForMatch();
            PickBases();
            foreach (var room in level.rooms)
                if (room.Indoor || room.Bounds.size.x * room.Bounds.size.z > 30f) roamPoints.Add(OnNavMesh(room.Bounds.center));
            if (Mode == GameMode.ZoneControl) PickZone();
            if (Mode == GameMode.CaptureTheFlag)
            {
                Vector3 toBuilding = (Bases[1] - Bases[0]);
                toBuilding.y = 0f;
                Flags[0] = MakeFlag(0, OnNavMesh(Bases[0] + toBuilding.normalized * 3f));
                Flags[1] = MakeFlag(1, Bases[1]);
            }

            // Online, the other players take places on their teams and bots fill the rest.
            var humans = NetSession.IsHost ? NetSession.Instance.RemotePlayers : new List<NetPlayer>();
            int blueHumans = 1, redHumans = 0;
            foreach (var human in humans) if (human.side == 0) blueHumans++; else redHumans++;
            int teamSize = Mathf.Clamp(options.teamSize, 1, MaxTeamSize);
            members[0].Add(player);
            player.Health.ProtectedUntil = Time.time + 2f;
            SpawnBlueTeam(actors, Mathf.Max(teamSize, blueHumans) - blueHumans);
            SpawnRedTeam(actors, Mathf.Max(teamSize, redHumans) - redHumans);
            foreach (var human in humans)
            {
                var actor = NetActor.CreateProxy(actors, nextId++, human, NextSpawn(human.side), baseYaw[human.side]);
                human.actorId = actor.NetId;
                AddRemote(actor);
            }
            AssignRoles(0);
            AssignRoles(1);
            running = true;
            UIManager.Banner(ModeNames[(int)Mode].ToUpperInvariant(), ModeGoals[(int)Mode], BannerKind.Info);
            if (NetSession.IsHost) NetSession.Instance.OnMatchBuilt(this);
        }

        // Online, not hosting: shows the host's match. Bases, zone, flags and everyone else come from the host.
        public void BeginMirror(MissionData mission, VersusOptions options, LevelLayout layout, Transform actors, PlayerController leader, MatchSetup setup)
        {
            Clear();
            Setup(mission, options, layout, actors, leader);
            Mirror = true;
            MySide = setup.side;
            MyId = setup.actorId;
            foreach (var door in level.doors) if (door != null) door.OpenForMatch();
            for (int side = 0; side < 2; side++)
            {
                Bases[side] = setup.bases[side];
                baseYaw[side] = setup.baseYaw[side];
                BasePad(side, Bases[side]);
            }
            if (Mode == GameMode.ZoneControl)
            {
                Zone = setup.zone;
                BuildZoneVisual();
            }
            if (Mode == GameMode.CaptureTheFlag)
                for (int side = 0; side < 2; side++) Flags[side] = MakeFlag(side, setup.flagHomes[side]);
            members[MySide].Add(player);
            foreach (var entry in setup.roster)
                if (entry.id != MyId) AddRemote(NetActor.CreatePuppet(actors, entry));
            player.TeleportTo(setup.spawn, setup.spawnYaw);
            player.Health.ProtectedUntil = Time.time + 2f;
            running = true;
            UIManager.Banner(ModeNames[(int)Mode].ToUpperInvariant() + "  -  " + SideName(MySide).ToUpperInvariant() + " TEAM", ModeGoals[(int)Mode], BannerKind.Info);
        }

        void Setup(MissionData mission, VersusOptions options, LevelLayout layout, Transform actors, PlayerController leader)
        {
            level = layout;
            player = leader;
            Mode = mission.mode;
            ScoreLimit = ScoreLimitFor(Mode, options.scoreIndex);
            TimeLimit = MinutesFor(Mode, options.timeIndex) * 60f;
            MapName = mission.location;
            Elapsed = 0f;
            Score[0] = Score[1] = 0f;
            PlayerKills = PlayerDeaths = PlayerCaptures = 0;
            PlayerRespawnAt = -1f;
            ZoneControl = 0f;
            ZoneOwner = -1;
            LocalName = NetSession.Online ? NetSession.Instance.LocalName : leader != null ? leader.Officer.callsign : "You";
        }

        // During the van arrival nobody is shown; afterwards your team is, and the other team follows line of sight.
        public void ShowTeams(bool visible)
        {
            foreach (var other in Others) other.SetVisible(visible && (other.Side == MySide || !SaveManager.Settings.lineOfSight));
        }

        public void Clear()
        {
            running = false;
            foreach (var go in spawned) if (go != null) Destroy(go);
            spawned.Clear();
            Bots.Clear();
            Remotes.Clear();
            Others.Clear();
            members[0].Clear();
            members[1].Clear();
            spawns[0].Clear();
            spawns[1].Clear();
            roamPoints.Clear();
            Feed.Clear();
            Flags[0] = Flags[1] = null;
            zoneEdges = null;
            zoneColorState = -2;
            level = null;
            player = null;
            Mirror = false;
            MySide = 0;
            MyId = 0;
        }

        // Blue base where the team arrives; red base in the indoor room furthest away by walking distance.
        void PickBases()
        {
            Bases[0] = OnNavMesh(level.playerSpawn);
            baseYaw[0] = level.playerYaw;
            spawns[0].Add(Bases[0]);
            foreach (var point in level.squadSpawns) spawns[0].Add(OnNavMesh(point));
            for (int i = 0; i < 4; i++)
                spawns[0].Add(OnNavMesh(Bases[0] + Quaternion.Euler(0f, level.playerYaw + 45f + i * 90f, 0f) * Vector3.forward * 2.5f));

            RoomController best = null;
            float bestDistance = -1f;
            var path = new NavMeshPath();
            foreach (var room in level.rooms)
            {
                if (!room.Indoor) continue;
                var size = room.Bounds.size;
                if (size.x < 3.5f || size.z < 3.5f) continue;
                Vector3 center = OnNavMesh(room.Bounds.center);
                if (!NavMesh.CalculatePath(Bases[0], center, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                float length = PathLength(path);
                if (length > bestDistance)
                {
                    bestDistance = length;
                    best = room;
                }
            }
            if (best != null)
            {
                Bases[1] = OnNavMesh(best.Bounds.center);
                var b = best.Bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = new Vector3(Random.Range(b.min.x + 1f, b.max.x - 1f), 0f, Random.Range(b.min.z + 1f, b.max.z - 1f));
                    spawns[1].Add(OnNavMesh(p));
                }
            }
            else
            {
                // No usable room: the far corner of the walkable area.
                Bases[1] = OnNavMesh(level.navBounds.center + (level.navBounds.center - Bases[0]) * 0.6f);
                for (int i = 0; i < 6; i++) spawns[1].Add(OnNavMesh(Bases[1] + Random.insideUnitSphere * 3f));
            }
            Vector3 face = Bases[0] - Bases[1];
            baseYaw[1] = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;
            BasePad(0, Bases[0]);
            BasePad(1, Bases[1]);
        }

        // The zone: the room whose walking distance to both bases is most even.
        void PickZone()
        {
            RoomController best = null;
            float bestScore = float.MaxValue;
            var path = new NavMeshPath();
            foreach (var room in level.rooms)
            {
                var size = room.Bounds.size;
                if (size.x < 4f || size.z < 4f || room.Contains(Bases[1]) || room.Contains(Bases[0])) continue;
                Vector3 center = OnNavMesh(room.Bounds.center);
                if (!NavMesh.CalculatePath(Bases[0], center, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                float blue = PathLength(path);
                if (!NavMesh.CalculatePath(Bases[1], center, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                float red = PathLength(path);
                float score = Mathf.Abs(blue - red) + (room.Indoor ? 0f : 6f) + Mathf.Abs(size.x * size.z - 60f) * 0.05f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = room;
                }
            }
            Bounds zone;
            if (best != null)
            {
                zone = best.Bounds;
                zone.Expand(new Vector3(-1f, 0f, -1f));
                // Big rooms get a zone of at most 9 x 9 m in the middle.
                zone.size = new Vector3(Mathf.Min(zone.size.x, 9f), 2f, Mathf.Min(zone.size.z, 9f));
            }
            else zone = new Bounds((Bases[0] + Bases[1]) * 0.5f, new Vector3(7f, 2f, 7f));
            zone.center = new Vector3(zone.center.x, 1f, zone.center.z);
            Zone = zone;
            BuildZoneVisual();
        }

        void SpawnBlueTeam(Transform actors, int count)
        {
            var officers = new List<OfficerData>(OfficerSelectionManager.Squad);
            foreach (var officer in GameData.AllOfficers)
                if (!officers.Contains(officer) && officer != OfficerSelectionManager.Leader && Progression.IsAvailable(officer)) officers.Add(officer);
            for (int i = 0; i < count; i++)
            {
                Vector3 point = NextSpawn(0);
                if (i < officers.Count)
                {
                    var officer = officers[i];
                    var loadout = GameData.LoadoutFor(officer);
                    var weapon = GameData.Weapon(loadout.primaryId);
                    if (weapon == null || weapon.lessLethal || loadout.useShield) weapon = GameData.Weapon("rifle_compact");
                    var look = PlayerController.OfficerAppearance(officer, loadout, false);
                    look.shield = false;
                    AddBot(ArenaBot.Spawn(actors, 0, officer.callsign, look, weapon, loadout, point, baseYaw[0], skill), officer.id);
                }
                else AddBot(ArenaBot.Spawn(actors, 0, "Blue " + (i + 2), BlueLook(CharacterFactory.RandomSkin()), RandomWeapon(), null, point, baseYaw[0], skill), null);
            }
        }

        void SpawnRedTeam(Transform actors, int count)
        {
            for (int i = 0; i < count; i++)
                AddBot(ArenaBot.Spawn(actors, 1, RedNames[i % RedNames.Length], RedLook(CharacterFactory.RandomSkin()), RandomWeapon(), null, NextSpawn(1), baseYaw[1], skill), null);
        }

        void AddBot(ArenaBot bot, string officerId)
        {
            bot.NetId = nextId++;
            bot.OfficerId = officerId;
            bot.NextThink = Time.time + Random.value * 0.3f;
            Bots.Add(bot);
            Others.Add(bot);
            members[bot.Side].Add(bot);
            spawned.Add(bot.gameObject);
        }

        void AddRemote(NetActor actor)
        {
            Remotes.Add(actor);
            Others.Add(actor);
            members[actor.Side].Add(actor);
            spawned.Add(actor.gameObject);
        }

        // A player left: their stand-in goes (dropping a flag they carried).
        public void RemoveRemote(NetActor actor, bool announce)
        {
            if (actor == null || !Remotes.Contains(actor)) return;
            foreach (var flag in Flags)
                if (flag != null && flag.carrier == (ICombatTarget)actor)
                {
                    if (Mirror) flag.carrier = null;
                    else Drop(flag);
                }
            if (announce && !Mirror) Post(new MatchEvent { kind = MatchEventKind.PlayerLeft, a = actor.NetId, side = actor.Side, name = actor.Callsign });
            Remotes.Remove(actor);
            Others.Remove(actor);
            members[actor.Side].Remove(actor);
            spawned.Remove(actor.gameObject);
            Destroy(actor.gameObject);
        }

        // Capture the Flag: about half attack, the rest defend. Other modes: everyone roams or holds the zone.
        void AssignRoles(int side)
        {
            int index = 0;
            foreach (var bot in Bots)
            {
                if (bot.Side != side) continue;
                bot.Role = Mode == GameMode.CaptureTheFlag ? (index % 2 == 0 ? BotRole.Attack : BotRole.Defend) : BotRole.Roam;
                index++;
            }
        }

        static WeaponData RandomWeapon()
        {
            var options = new List<WeaponData>();
            foreach (var weapon in GameData.AllWeapons)
                if (!weapon.isSidearm && !weapon.lessLethal) options.Add(weapon);
            return options.Count > 0 ? options[Random.Range(0, options.Count)] : GameData.Weapon("rifle_compact");
        }

        public static Appearance BlueLook(Color skin)
        {
            return new Appearance
            {
                shirt = new Color(0.12f, 0.16f, 0.26f), pants = new Color(0.1f, 0.13f, 0.21f), skin = skin,
                headwear = new Color(0.08f, 0.09f, 0.12f), head = HeadStyle.Helmet, vestOn = true, vest = new Color(0.16f, 0.17f, 0.2f),
                ring = new Color(0.2f, 0.45f, 1f), armed = true, outfit = Outfit.Tactical, idMarker = true, idColor = new Color(0.36f, 0.62f, 0.95f),
                holster = true,
            };
        }

        public static Appearance RedLook(Color skin)
        {
            return new Appearance
            {
                shirt = new Color(0.42f, 0.12f, 0.12f), pants = new Color(0.16f, 0.12f, 0.12f), skin = skin,
                headwear = new Color(0.14f, 0.1f, 0.1f), head = HeadStyle.Helmet, vestOn = true, vest = new Color(0.22f, 0.18f, 0.18f),
                ring = new Color(1f, 0.25f, 0.2f), armed = true, outfit = Outfit.Tactical, idMarker = true, idColor = new Color(0.95f, 0.3f, 0.25f),
                width = 1.04f, holster = true,
            };
        }

        // An officer playing for the Red Team wears its colours.
        public static Appearance TeamColours(Appearance look, int side)
        {
            if (side != 1) return look;
            look.shirt = new Color(0.42f, 0.12f, 0.12f);
            look.pants = new Color(0.16f, 0.12f, 0.12f);
            look.ring = new Color(1f, 0.25f, 0.2f);
            look.idColor = new Color(0.95f, 0.3f, 0.25f);
            look.shield = false;
            return look;
        }

        Vector3 NextSpawn(int side)
        {
            var list = spawns[side];
            if (list.Count == 0) return Bases[side];
            // Prefer a spawn point no opponent is standing near.
            for (int attempt = 0; attempt < list.Count; attempt++)
            {
                var point = list[spawnCursor[side]++ % list.Count];
                bool safe = true;
                foreach (var enemy in members[1 - side])
                    if (enemy != null && enemy.IsAlive && (enemy.Position - point).sqrMagnitude < 36f) { safe = false; break; }
                if (safe) return point;
            }
            return list[spawnCursor[side]++ % list.Count];
        }

        static Vector3 OnNavMesh(Vector3 point)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(new Vector3(point.x, 0f, point.z), out hit, 3f, NavMesh.AllAreas)) return hit.position;
            return new Vector3(point.x, 0f, point.z);
        }

        static float PathLength(NavMeshPath path)
        {
            float length = 0f;
            var corners = path.corners;
            for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            return length;
        }

        // ---- Visuals: base pads, flags and the zone ----

        static Color SideColor(int side) { return side == 0 ? new Color(0.25f, 0.55f, 1f) : new Color(1f, 0.28f, 0.22f); }

        void BasePad(int side, Vector3 position)
        {
            var pad = Shapes.Make(PrimitiveType.Cylinder, side == 0 ? "Blue Base" : "Red Base", level.root, position + Vector3.up * 0.03f, new Vector3(2.6f, 0.01f, 2.6f), SideColor(side), false, 1.2f);
            spawned.Add(pad);
        }

        Flag MakeFlag(int side, Vector3 home)
        {
            var root = new GameObject(side == 0 ? "Blue Flag" : "Red Flag").transform;
            root.SetParent(level.root, false);
            root.position = home;
            Shapes.Box("Pole", root, new Vector3(0f, 1.1f, 0f), new Vector3(0.06f, 2.2f, 0.06f), new Color(0.75f, 0.75f, 0.78f), false);
            Shapes.Box("Cloth", root, new Vector3(0.36f, 1.95f, 0f), new Vector3(0.7f, 0.42f, 0.04f), SideColor(side), false, 1.3f);
            Shapes.Box("Stripe", root, new Vector3(0.36f, 1.95f, 0.025f), new Vector3(0.7f, 0.08f, 0.01f), Color.white, false, 1.2f);
            Shapes.Box("Top", root, new Vector3(0f, 2.25f, 0f), new Vector3(0.12f, 0.12f, 0.12f), SideColor(side), false, 2f);
            spawned.Add(root.gameObject);
            return new Flag { side = side, home = home, position = home, visual = root };
        }

        void BuildZoneVisual()
        {
            var root = new GameObject("Zone").transform;
            root.SetParent(level.root, false);
            var b = Zone;
            float y = 0.05f;
            zoneEdges = new Renderer[5];
            zoneEdges[0] = Shapes.Box("Edge", root, new Vector3(b.center.x, y, b.min.z), new Vector3(b.size.x, 0.02f, 0.14f), Color.white, false, 1.4f).GetComponent<Renderer>();
            zoneEdges[1] = Shapes.Box("Edge", root, new Vector3(b.center.x, y, b.max.z), new Vector3(b.size.x, 0.02f, 0.14f), Color.white, false, 1.4f).GetComponent<Renderer>();
            zoneEdges[2] = Shapes.Box("Edge", root, new Vector3(b.min.x, y, b.center.z), new Vector3(0.14f, 0.02f, b.size.z), Color.white, false, 1.4f).GetComponent<Renderer>();
            zoneEdges[3] = Shapes.Box("Edge", root, new Vector3(b.max.x, y, b.center.z), new Vector3(0.14f, 0.02f, b.size.z), Color.white, false, 1.4f).GetComponent<Renderer>();
            zoneEdges[4] = Shapes.Box("Beacon", root, new Vector3(b.center.x, 1.4f, b.center.z), new Vector3(0.18f, 2.8f, 0.18f), Color.white, false, 1.6f).GetComponent<Renderer>();
            spawned.Add(root.gameObject);
            UpdateZoneColor();
        }

        void UpdateZoneColor()
        {
            if (zoneEdges == null || ZoneOwner == zoneColorState) return;
            zoneColorState = ZoneOwner;
            var material = Shapes.Mat(ZoneOwner < 0 ? new Color(0.9f, 0.9f, 0.85f) : SideColor(ZoneOwner), 1.5f);
            foreach (var edge in zoneEdges) if (edge != null) edge.sharedMaterial = material;
        }

        // ---- Per frame ----

        void Update()
        {
            var game = GameManager.Instance;
            if (!running || game == null || !game.WorldRunning || level == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float now = Time.time;
            if (Mirror)
            {
                // The host keeps the score and the clock; snapshots correct this.
                Elapsed += dt;
                for (int side = 0; side < 2; side++) if (Flags[side] != null) PlaceFlagVisual(Flags[side]);
                UpdateZoneColor();
                if (now >= nextVisibility)
                {
                    nextVisibility = now + 0.2f;
                    UpdateVisibility();
                }
                return;
            }

            Elapsed += dt;
            float interval = Mathf.Max(0.15f, QualityManager.Current.aiThinkInterval);
            for (int i = 0; i < Bots.Count; i++)
            {
                var bot = Bots[i];
                bot.FrameUpdate(dt);
                if (!bot.IsAlive)
                {
                    if (now >= bot.RespawnAt) bot.Respawn(NextSpawn(bot.Side), baseYaw[bot.Side]);
                    continue;
                }
                if (now < bot.NextThink) continue;
                bot.NextThink = now + interval;
                bot.Think(this);
            }
            // Other players who were tagged out come back when their time is up.
            foreach (var remote in Remotes)
                if (remote.Ready && remote.Down && remote.RespawnAt > 0f && now >= remote.RespawnAt) RespawnRemote(remote);

            if (player != null && !player.IsAlive && PlayerRespawnAt > 0f && now >= PlayerRespawnAt) RespawnPlayer();
            if (Mode == GameMode.CaptureTheFlag) UpdateFlags(now);
            if (Mode == GameMode.ZoneControl) UpdateZone(dt);
            if (now >= nextVisibility)
            {
                nextVisibility = now + 0.2f;
                UpdateVisibility();
            }
            if (now >= nextDoors)
            {
                nextDoors = now + 0.4f;
                ReopenDoors();
            }
            CheckEnd();
        }

        void RespawnPlayer()
        {
            PlayerRespawnAt = -1f;
            Vector3 point = NextSpawn(MySide);
            RespawnLocal(point, baseYaw[MySide]);
        }

        // Puts you back in at a spawn point (online, the host chooses it).
        public void RespawnLocal(Vector3 point, float yaw)
        {
            if (player == null) return;
            PlayerRespawnAt = -1f;
            player.TeleportTo(point, yaw);
            player.ResetStance();
            player.Health.RestoreFull();
            player.Health.ProtectedUntil = Time.time + 1.5f;
            player.Weapons.Resupply();
            GameManager.Instance.CameraRig.Snap();
            AudioManager.Play2D(Sound.Equip, 0.6f);
        }

        void RespawnRemote(NetActor remote)
        {
            Vector3 point = NextSpawn(remote.Side);
            remote.Revive(point, baseYaw[remote.Side]);
            NetSession.Instance.SendRespawn(remote, point, baseYaw[remote.Side]);
        }

        // Fog of war for the other team: drawn only while someone on your team can see them
        // (or just after they fire near you). Respects the Line of Sight setting.
        void UpdateVisibility()
        {
            bool lineOfSight = SaveManager.Settings.lineOfSight;
            foreach (var other in Others)
            {
                if (other.Side == MySide || !lineOfSight || !other.IsAlive) { other.SetSeen(true); continue; }
                bool seen = Time.time - other.LastShotTime < 1f && player != null && (other.Position - player.Position).sqrMagnitude < 30f * 30f;
                if (!seen)
                {
                    Vector3 chest = other.ChestPosition;
                    float visibility = AIVisibility.VisibilityOf(other);
                    foreach (var friend in members[MySide])
                    {
                        if (friend == null || !friend.IsAlive) continue;
                        // From the friend's chest, so a teammate peeking past a door frame spots what they see.
                        if (AIVisibility.CanSee(friend.ChestPosition + Vector3.up * 0.3f, Vector3.forward, 360f, 26f, chest, visibility)) { seen = true; break; }
                    }
                }
                other.SetSeen(seen);
            }
        }

        // Bots don't push doors; anything closed again opens when someone walks up to it.
        void ReopenDoors()
        {
            foreach (var door in level.doors)
            {
                if (door == null || door.State != DoorState.Closed) continue;
                Vector3 p = door.transform.position;
                foreach (var bot in Bots)
                    if (bot.IsAlive && AIManager.FlatDistance(bot.Position, p) < 1.4f) { door.Open(bot.Position); break; }
            }
        }

        // ---- Flags ----

        public bool IsCarrying(IVersusMember member)
        {
            return Mode == GameMode.CaptureTheFlag && Flags[1 - member.Side] != null && Flags[1 - member.Side].carrier == (ICombatTarget)member;
        }

        public bool PlayerCarrying { get { return Mode == GameMode.CaptureTheFlag && Flags[1 - MySide] != null && player != null && Flags[1 - MySide].carrier == (ICombatTarget)player; } }

        public Vector3 FlagHome(int side)
        {
            return Flags[side] != null ? Flags[side].home : Bases[side];
        }

        void UpdateFlags(float now)
        {
            for (int side = 0; side < 2; side++)
            {
                var flag = Flags[side];
                if (flag == null) continue;
                if (flag.carrier != null)
                {
                    if (!flag.carrier.IsAlive) { Drop(flag); continue; }
                    flag.position = flag.carrier.Position;
                    // A carrier who reaches their own base while their flag is home scores.
                    var own = Flags[1 - side];
                    if (own != null && own.AtHome && AIManager.FlatDistance(flag.position, own.home) < 2.2f) Capture(flag);
                }
                else
                {
                    if (!flag.AtHome && now - flag.droppedAt > 20f) ReturnFlag(flag, null);
                    else Touch(flag);
                }
                PlaceFlagVisual(flag);
            }
        }

        void Touch(Flag flag)
        {
            foreach (var target in members[flag.side])
            {
                // Your own team returns a dropped flag by touching it.
                if (flag.AtHome || target == null || !target.IsAlive) continue;
                if (AIManager.FlatDistance(target.Position, flag.position) < 1.3f) { ReturnFlag(flag, target); return; }
            }
            foreach (var target in members[1 - flag.side])
            {
                if (target == null || !target.IsAlive) continue;
                if (AIManager.FlatDistance(target.Position, flag.position) < 1.3f)
                {
                    flag.carrier = target;
                    Post(new MatchEvent { kind = MatchEventKind.FlagTaken, side = flag.side, a = IdOf(target), b = -1, weapon = -1 });
                    return;
                }
            }
        }

        void Drop(Flag flag)
        {
            int by = IdOf(flag.carrier);
            flag.position = OnNavMesh(flag.carrier.Position);
            flag.carrier = null;
            flag.droppedAt = Time.time;
            Post(new MatchEvent { kind = MatchEventKind.FlagDropped, side = flag.side, a = by, b = -1, weapon = -1 });
        }

        void ReturnFlag(Flag flag, ICombatTarget by)
        {
            flag.carrier = null;
            flag.position = flag.home;
            Post(new MatchEvent { kind = MatchEventKind.FlagReturned, side = flag.side, a = by != null ? IdOf(by) : -1, b = -1, weapon = -1 });
        }

        void Capture(Flag flag)
        {
            var carrier = flag.carrier;
            int side = 1 - flag.side;
            Score[side] += 1f;
            var member = carrier as IVersusMember;
            if (member != null) member.Captures++;
            if (carrier == (ICombatTarget)player) PlayerCaptures++;
            flag.carrier = null;
            flag.position = flag.home;
            Post(new MatchEvent { kind = MatchEventKind.FlagCaptured, side = flag.side, a = IdOf(carrier), b = -1, weapon = -1 });
        }

        void PlaceFlagVisual(Flag flag)
        {
            if (flag.visual == null) return;
            if (flag.carrier != null)
            {
                // Carried on the back, slightly smaller.
                var t = flag.carrier.Transform;
                flag.visual.position = t.position - t.forward * 0.3f + Vector3.up * 0.2f;
                flag.visual.rotation = t.rotation;
                flag.visual.localScale = Vector3.one * 0.8f;
            }
            else
            {
                flag.visual.position = flag.position;
                flag.visual.rotation = Quaternion.Euler(0f, Time.time * 40f, 0f);
                flag.visual.localScale = Vector3.one;
            }
        }

        // ---- Zone ----

        public bool InZone(Vector3 point)
        {
            var b = Zone;
            return point.x >= b.min.x && point.x <= b.max.x && point.z >= b.min.z && point.z <= b.max.z;
        }

        public bool HoldsZone(ArenaBot bot)
        {
            return Mode == GameMode.ZoneControl && InZone(bot.Position);
        }

        public Vector3 ClampToZone(Vector3 point)
        {
            var b = Zone;
            return new Vector3(Mathf.Clamp(point.x, b.min.x + 0.5f, b.max.x - 0.5f), point.y, Mathf.Clamp(point.z, b.min.z + 0.5f, b.max.z - 0.5f));
        }

        void UpdateZone(float dt)
        {
            ZoneCount[0] = ZoneCount[1] = 0;
            for (int side = 0; side < 2; side++)
                foreach (var target in members[side])
                    if (target != null && target.IsAlive && InZone(target.Position)) ZoneCount[side]++;
            // Alone in the zone pushes control your way (faster with more people); contested stalls it.
            if (ZoneCount[0] > 0 && ZoneCount[1] == 0) ZoneControl = Mathf.MoveTowards(ZoneControl, 1f, dt * 0.2f * Mathf.Min(3, ZoneCount[0]));
            else if (ZoneCount[1] > 0 && ZoneCount[0] == 0) ZoneControl = Mathf.MoveTowards(ZoneControl, -1f, dt * 0.2f * Mathf.Min(3, ZoneCount[1]));
            int owner = ZoneOwner;
            if (ZoneControl >= 1f) owner = 0;
            else if (ZoneControl <= -1f) owner = 1;
            else if (owner == 0 && ZoneControl <= 0f || owner == 1 && ZoneControl >= 0f) owner = -1;
            if (owner != ZoneOwner)
            {
                ZoneOwner = owner;
                if (owner >= 0) Post(new MatchEvent { kind = MatchEventKind.ZoneTaken, side = owner, a = -1, b = -1, weapon = -1 });
                UpdateZoneColor();
            }
            if (ZoneOwner >= 0) Score[ZoneOwner] += dt;
        }

        // ---- Bot objectives ----

        public bool ObjectiveFor(ArenaBot bot, out Vector3 point, out bool run)
        {
            point = bot.Position;
            run = true;
            switch (Mode)
            {
                case GameMode.CaptureTheFlag: return FlagObjective(bot, out point, out run);
                case GameMode.ZoneControl:
                    if (!InZone(bot.Position))
                    {
                        point = ZonePoint();
                        return true;
                    }
                    if (Time.time > bot.RoamUntil)
                    {
                        bot.RoamPoint = ZonePoint();
                        bot.RoamUntil = Time.time + Random.Range(3f, 6f);
                    }
                    point = bot.RoamPoint;
                    run = false;
                    return true;
                default:
                    if (Time.time > bot.RoamUntil || AIManager.FlatDistance(bot.Position, bot.RoamPoint) < 1.5f)
                    {
                        bot.RoamPoint = RoamPoint(bot.Side);
                        bot.RoamUntil = Time.time + Random.Range(12f, 22f);
                    }
                    point = bot.RoamPoint;
                    return true;
            }
        }

        bool FlagObjective(ArenaBot bot, out Vector3 point, out bool run)
        {
            run = true;
            var own = Flags[bot.Side];
            var enemy = Flags[1 - bot.Side];
            point = bot.Position;
            if (own == null || enemy == null) return false;
            // Our flag is lying somewhere: whoever is closest of the defenders (or anyone nearby) goes to return it.
            if (own.carrier == null && !own.AtHome && (bot.Role == BotRole.Defend || AIManager.FlatDistance(bot.Position, own.position) < 15f))
            {
                point = own.position;
                return true;
            }
            // Our flag is being carried away: defenders chase the carrier.
            if (own.carrier != null && (bot.Role == BotRole.Defend || AIManager.FlatDistance(bot.Position, own.carrier.Position) < 15f))
            {
                point = own.carrier.Position;
                return true;
            }
            if (bot.Role == BotRole.Attack)
            {
                if (enemy.carrier != null)
                {
                    // Escort the teammate carrying their flag.
                    point = enemy.carrier.Position + (bot.Position - enemy.carrier.Position).normalized * 2f;
                    return true;
                }
                point = enemy.position;
                return true;
            }
            // Defend: wander near our flag.
            if (Time.time > bot.RoamUntil)
            {
                Vector2 offset = Random.insideUnitCircle * 6f;
                bot.RoamPoint = OnNavMesh(own.home + new Vector3(offset.x, 0f, offset.y));
                bot.RoamUntil = Time.time + Random.Range(4f, 8f);
            }
            point = bot.RoamPoint;
            run = false;
            return true;
        }

        Vector3 ZonePoint()
        {
            var b = Zone;
            return OnNavMesh(new Vector3(Random.Range(b.min.x + 0.6f, b.max.x - 0.6f), 0f, Random.Range(b.min.z + 0.6f, b.max.z - 0.6f)));
        }

        // Roaming: sometimes toward where opponents are, otherwise a random room.
        Vector3 RoamPoint(int side)
        {
            if (Random.value < 0.45f)
            {
                var foes = members[1 - side];
                for (int attempt = 0; attempt < 4 && foes.Count > 0; attempt++)
                {
                    var foe = foes[Random.Range(0, foes.Count)];
                    if (foe != null && foe.IsAlive) return OnNavMesh(foe.Position + Random.insideUnitSphere * 4f);
                }
            }
            if (roamPoints.Count > 0) return roamPoints[Random.Range(0, roamPoints.Count)];
            return Bases[1 - side];
        }

        // A bot that sees an opponent tells teammates close by.
        public void ShareSighting(ArenaBot spotter, Vector3 position)
        {
            foreach (var bot in Bots)
                if (bot != spotter && bot.Side == spotter.Side && bot.IsAlive && (bot.Position - spotter.Position).sqrMagnitude < 18f * 18f) bot.HearOf(position);
        }

        // ---- Takedowns ----

        public void OnBotDown(ArenaBot bot, DamageInfo info)
        {
            if (!running) return;
            bot.RespawnAt = Time.time + RespawnDelay;
            Credit(info, bot.Side, bot.NetId);
        }

        // You were tagged out (offline, hosting or as a client).
        public void OnPlayerDown()
        {
            if (!running || player == null) return;
            PlayerDeaths++;
            PlayerRespawnAt = Time.time + RespawnDelay;
            var hit = player.Health.LastHit;
            var shooter = hit.shooter as IVersusMember;
            PlayerTaggedBy = shooter != null ? shooter.Callsign + (hit.weapon != null ? "  (" + hit.weapon.displayName + ")" : "") : "the other team";
            UIManager.Notify("Tagged out! Back in " + Mathf.RoundToInt(RespawnDelay) + " seconds", true);
            // Online the host keeps score: tell it who did it.
            if (Mirror) NetSession.Instance.SendDown(shooter != null ? shooter.NetId : -1, WeaponIndex(hit.weapon));
            else Credit(hit, MySide, MyId);
        }

        // Host: another player reports being tagged out.
        public void OnRemoteDown(NetActor remote, int attackerId, int weaponIndex)
        {
            if (!running || Mirror || remote == null || remote.Down) return;
            remote.SetDown(true);
            remote.Deaths++;
            remote.RespawnAt = Time.time + RespawnDelay;
            var info = new DamageInfo { weapon = WeaponAt(weaponIndex), shooter = Find(attackerId) as MonoBehaviour, byPlayer = attackerId == MyId };
            Credit(info, remote.Side, remote.NetId);
        }

        void Credit(DamageInfo info, int victimSide, int victimId)
        {
            var member = info.shooter as IVersusMember;
            if (member != null) member.Kills++;
            else if (info.byPlayer) PlayerKills++;
            if (Mode == GameMode.TeamDeathmatch) Score[1 - victimSide] += 1f;
            int killer = member != null ? member.NetId : info.byPlayer ? MyId : -1;
            Post(new MatchEvent { kind = MatchEventKind.Takedown, a = killer, b = victimId, side = victimSide, weapon = WeaponIndex(info.weapon) });
        }

        // ---- Events: the feed and banners ----

        // Host or offline: something happened. Everyone (including you) hears about it.
        void Post(MatchEvent e)
        {
            Apply(e);
            if (NetSession.IsHost) NetSession.Instance.SendEvent(e);
        }

        public void Apply(MatchEvent e)
        {
            string flagName = SideName(e.side).ToLowerInvariant() + " flag";
            switch (e.kind)
            {
                case MatchEventKind.Takedown:
                    var weapon = WeaponAt(e.weapon);
                    Announce(Name(e.a) + "  >  " + Name(e.b) + (weapon != null ? "   [" + weapon.displayName + "]" : ""), 1 - e.side);
                    // As a client, your takedowns are confirmed by the host.
                    if (Mirror && e.a == MyId && player != null)
                    {
                        PlayerKills++;
                        player.Weapons.ConfirmTakedown();
                    }
                    break;
                case MatchEventKind.FlagTaken:
                    Announce(Name(e.a) + " took the " + flagName, 1 - e.side);
                    AudioManager.Play2D(Sound.ObjectiveTone, 0.55f, e.side == MySide ? 0.8f : 1.1f, SoundCategory.Interface);
                    if (e.a == MyId) UIManager.Banner("YOU HAVE THE FLAG", "Bring it back to your base", BannerKind.Good);
                    else if (e.side == MySide) UIManager.Banner("YOUR FLAG WAS TAKEN", "Tag out the carrier to drop it", BannerKind.Bad);
                    break;
                case MatchEventKind.FlagDropped:
                    Announce(Name(e.a) + " dropped the " + flagName, e.side);
                    break;
                case MatchEventKind.FlagReturned:
                    Announce(e.a >= 0 ? Name(e.a) + " returned the " + flagName : "The " + flagName + " returned to base", e.side);
                    AudioManager.Play2D(Sound.RadioOrder, 0.45f, 1f, SoundCategory.Interface);
                    break;
                case MatchEventKind.FlagCaptured:
                    int scorer = 1 - e.side;
                    Announce(Name(e.a) + " CAPTURED the " + flagName, scorer);
                    UIManager.Banner(scorer == MySide ? "FLAG CAPTURED" : SideName(scorer).ToUpperInvariant() + " TEAM SCORED",
                        scorer == MySide ? "One more for your team" : "Keep your flag at home", scorer == MySide ? BannerKind.Good : BannerKind.Bad);
                    AudioManager.Play2D(scorer == MySide ? Sound.Complete : Sound.Warning, 0.6f, 1f, SoundCategory.Interface);
                    break;
                case MatchEventKind.ZoneTaken:
                    Announce(SideName(e.side) + " team took the zone", e.side);
                    UIManager.Banner(e.side == MySide ? "ZONE TAKEN" : "ZONE LOST", e.side == MySide ? "Hold it to keep scoring" : "Get in there and take it back", e.side == MySide ? BannerKind.Good : BannerKind.Bad);
                    AudioManager.Play2D(e.side == MySide ? Sound.ObjectiveTone : Sound.Warning, 0.55f, 1f, SoundCategory.Interface);
                    break;
                case MatchEventKind.PlayerLeft:
                    Announce((string.IsNullOrEmpty(e.name) ? "A player" : e.name) + " left the match", e.side);
                    break;
            }
        }

        void Announce(string text, int side)
        {
            Feed.Add(new FeedLine { text = text, side = side, time = Time.unscaledTime });
            if (Feed.Count > 6) Feed.RemoveAt(0);
        }

        // ---- Who is who ----

        public IVersusMember Find(int id)
        {
            if (id < 0) return null;
            foreach (var other in Others) if (other.NetId == id) return other;
            return null;
        }

        // You, or anyone else, by actor id.
        public ICombatTarget TargetById(int id)
        {
            if (id < 0) return null;
            if (id == MyId) return player;
            return Find(id);
        }

        public int IdOf(ICombatTarget target)
        {
            if (target == null) return -1;
            if (target == (ICombatTarget)player) return MyId;
            var member = target as IVersusMember;
            return member != null ? member.NetId : -1;
        }

        string Name(int id)
        {
            if (id < 0) return "Someone";
            if (id == MyId) return "You";
            var member = Find(id);
            return member != null ? member.Callsign : "Someone";
        }

        public string NameOf(ICombatTarget target)
        {
            if (target == null) return "Someone";
            if (target == (ICombatTarget)player) return "You";
            var member = target as IVersusMember;
            return member != null ? member.Callsign : "Someone";
        }

        // ---- Online: the host's view of the match, applied on the other players' copies ----

        public void ApplyState(float elapsed, float blue, float red, float zoneControl, int zoneOwner, int inZoneBlue, int inZoneRed)
        {
            Elapsed = elapsed;
            Score[0] = blue;
            Score[1] = red;
            ZoneControl = zoneControl;
            ZoneOwner = zoneOwner;
            ZoneCount[0] = inZoneBlue;
            ZoneCount[1] = inZoneRed;
        }

        public void ApplyFlag(int side, Vector3 position, int carrierId, float droppedAgo)
        {
            var flag = Flags[side];
            if (flag == null) return;
            flag.carrier = TargetById(carrierId);
            flag.position = flag.carrier != null ? flag.carrier.Position : position;
            if ((position - flag.home).sqrMagnitude < 0.0025f) flag.position = flag.home;
            flag.droppedAt = Time.time - droppedAgo;
        }

        // ---- End of match ----

        void CheckEnd()
        {
            int winner = -2;
            string reason = null;
            if (Score[0] >= ScoreLimit) { winner = 0; reason = "Score limit reached"; }
            else if (Score[1] >= ScoreLimit) { winner = 1; reason = "Score limit reached"; }
            else if (Elapsed >= TimeLimit)
            {
                int blue = Mathf.FloorToInt(Score[0]), red = Mathf.FloorToInt(Score[1]);
                winner = blue > red ? 0 : red > blue ? 1 : -1;
                reason = "Time is up";
            }
            if (winner == -2) return;
            running = false;
            var result = new VersusResult
            {
                mode = Mode, mapName = MapName, winner = winner, side = MySide, reason = reason, time = Elapsed,
                blueScore = Mathf.FloorToInt(Score[0]), redScore = Mathf.FloorToInt(Score[1]), scoreLimit = ScoreLimit,
                playerKills = PlayerKills, playerDeaths = PlayerDeaths, playerCaptures = PlayerCaptures,
                online = NetSession.Online, hosted = NetSession.IsHost,
            };
            result.rows.Add(new VersusResult.ScoreRow { name = LocalName, side = MySide, kills = PlayerKills, deaths = PlayerDeaths, captures = PlayerCaptures, actorId = MyId, isPlayer = true });
            foreach (var other in Others)
                result.rows.Add(new VersusResult.ScoreRow { name = other.Callsign, side = other.Side, kills = other.Kills, deaths = other.Deaths, captures = other.Captures, actorId = other.NetId });
            SortRows(result);
            if (NetSession.IsHost) NetSession.Instance.SendEnd(result);
            GameManager.Instance.EndMatch(result);
        }

        public static void SortRows(VersusResult result)
        {
            result.rows.Sort((a, b) => a.side != b.side ? a.side.CompareTo(b.side) : (b.kills * 2 + b.captures * 5 - b.deaths).CompareTo(a.kills * 2 + a.captures * 5 - a.deaths));
        }

        // Online, not hosting: the host ended the match.
        public void Finish(VersusResult result)
        {
            if (!running) return;
            running = false;
            GameManager.Instance.EndMatch(result);
        }
    }
}
