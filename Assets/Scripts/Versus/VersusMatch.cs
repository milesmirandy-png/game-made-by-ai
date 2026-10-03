using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    public class VersusResult
    {
        public GameMode mode;
        public string mapName;
        public int winner;            // 0 your team, 1 red team, -1 draw
        public int blueScore, redScore, scoreLimit;
        public float time;
        public string reason;
        public int playerKills, playerDeaths, playerCaptures;
        public readonly List<ScoreRow> rows = new List<ScoreRow>();

        public struct ScoreRow
        {
            public string name;
            public int side, kills, deaths, captures;
            public bool isPlayer;
        }
    }

    // Runs a game-mode match: Blue Team (you and your squad, as bots) against
    // Red Team (bots) on one of the mission maps, with respawns. The modes are
    // framed as Tactical Response Unit training exercises with marking rounds,
    // so "tagged out" just means out until the respawn.
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
            "Bring the red flag to your base. Your own flag must be home to score.",
            "Stand in the zone with no opponents inside to take it. Your team scores every second it's yours.",
        };
        static readonly int[][] ScoreLimits = { new[] { 0 }, new[] { 15, 25, 40 }, new[] { 1, 3, 5 }, new[] { 60, 100, 200 } };
        static readonly int[][] TimeLimits = { new[] { 0 }, new[] { 5, 10, 15 }, new[] { 8, 12, 20 }, new[] { 5, 8, 12 } };
        public static readonly string[] SkillNames = { "Easy", "Normal", "Hard" };
        // Maps that work for team play (single floor, room to move).
        public static readonly string[] MapIds = { "warehouse", "office", "store", "motel", "bank", "clinic", "nightclub", "factory", "training" };
        static readonly string[] RedNames = { "Viper", "Rook", "Jackal", "Mako", "Vandal", "Hex", "Talon", "Sable" };
        public const float RespawnDelay = 5f;

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

        readonly List<ICombatTarget>[] members = { new List<ICombatTarget>(), new List<ICombatTarget>() };
        readonly List<Vector3>[] spawns = { new List<Vector3>(), new List<Vector3>() };
        readonly List<Vector3> roamPoints = new List<Vector3>();
        readonly float[] baseYaw = new float[2];
        readonly int[] spawnCursor = new int[2];
        bool running;
        int skill;
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

        // ---- Setup ----

        public void Begin(MissionData mission, VersusOptions options, LevelLayout layout, Transform actors, PlayerController leader)
        {
            Clear();
            level = layout;
            player = leader;
            Mode = mission.mode;
            skill = Mathf.Clamp(options.botSkill, 0, 2);
            ScoreLimit = ScoreLimitFor(Mode, options.scoreIndex);
            TimeLimit = MinutesFor(Mode, options.timeIndex) * 60f;
            MapName = mission.location;
            Elapsed = 0f;
            Score[0] = Score[1] = 0f;
            PlayerKills = PlayerDeaths = PlayerCaptures = 0;
            PlayerRespawnAt = -1f;
            ZoneControl = 0f;
            ZoneOwner = -1;

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

            int teamSize = Mathf.Clamp(options.teamSize, 2, 6);
            members[0].Add(player);
            player.Health.ProtectedUntil = Time.time + 2f;
            SpawnBlueTeam(actors, teamSize - 1);
            SpawnRedTeam(actors, teamSize);
            AssignRoles(0);
            AssignRoles(1);
            running = true;
            UIManager.Banner(ModeNames[(int)Mode].ToUpperInvariant(), ModeGoals[(int)Mode], BannerKind.Info);
        }

        // During the van arrival nobody is shown; afterwards your team is, and red follows line of sight.
        public void ShowTeams(bool visible)
        {
            foreach (var bot in Bots) bot.Parts.SetVisible(visible && (bot.Side == 0 || !SaveManager.Settings.lineOfSight));
        }

        public void Clear()
        {
            running = false;
            foreach (var go in spawned) if (go != null) Destroy(go);
            spawned.Clear();
            Bots.Clear();
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
                    AddBot(ArenaBot.Spawn(actors, 0, officer.callsign, look, weapon, loadout, point, baseYaw[0], skill));
                }
                else AddBot(ArenaBot.Spawn(actors, 0, "Blue " + (i + 2), BlueLook(), RandomWeapon(), null, point, baseYaw[0], skill));
            }
        }

        void SpawnRedTeam(Transform actors, int count)
        {
            for (int i = 0; i < count; i++)
                AddBot(ArenaBot.Spawn(actors, 1, RedNames[i % RedNames.Length], RedLook(), RandomWeapon(), null, NextSpawn(1), baseYaw[1], skill));
        }

        void AddBot(ArenaBot bot)
        {
            bot.NextThink = Time.time + Random.value * 0.3f;
            Bots.Add(bot);
            members[bot.Side].Add(bot);
            spawned.Add(bot.gameObject);
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

        static Appearance BlueLook()
        {
            return new Appearance
            {
                shirt = new Color(0.12f, 0.16f, 0.26f), pants = new Color(0.1f, 0.13f, 0.21f), skin = CharacterFactory.RandomSkin(),
                headwear = new Color(0.08f, 0.09f, 0.12f), head = HeadStyle.Helmet, vestOn = true, vest = new Color(0.16f, 0.17f, 0.2f),
                ring = new Color(0.2f, 0.45f, 1f), armed = true, outfit = Outfit.Tactical, idMarker = true, idColor = new Color(0.36f, 0.62f, 0.95f),
                holster = true,
            };
        }

        static Appearance RedLook()
        {
            return new Appearance
            {
                shirt = new Color(0.42f, 0.12f, 0.12f), pants = new Color(0.16f, 0.12f, 0.12f), skin = CharacterFactory.RandomSkin(),
                headwear = new Color(0.14f, 0.1f, 0.1f), head = HeadStyle.Helmet, vestOn = true, vest = new Color(0.22f, 0.18f, 0.18f),
                ring = new Color(1f, 0.25f, 0.2f), armed = true, outfit = Outfit.Tactical, idMarker = true, idColor = new Color(0.95f, 0.3f, 0.25f),
                width = 1.04f, holster = true,
            };
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
            if (!running || game == null || !game.IsPlaying || level == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Elapsed += dt;
            float now = Time.time;
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
            Vector3 point = NextSpawn(0);
            player.TeleportTo(point, baseYaw[0]);
            player.ResetStance();
            player.Health.RestoreFull();
            player.Health.ProtectedUntil = Time.time + 1.5f;
            player.Weapons.Resupply();
            GameManager.Instance.CameraRig.Snap();
            AudioManager.Play2D(Sound.Equip, 0.6f);
        }

        // Fog of war for the red team: drawn only while someone on your team can see them
        // (or just after they fire near you). Respects the Line of Sight setting.
        void UpdateVisibility()
        {
            bool lineOfSight = SaveManager.Settings.lineOfSight;
            foreach (var bot in Bots)
            {
                if (bot.Side == 0) { bot.SetSeen(true); continue; }
                if (!lineOfSight || !bot.IsAlive) { bot.SetSeen(true); continue; }
                bool seen = Time.time - bot.LastShotTime < 1f && player != null && (bot.Position - player.Position).sqrMagnitude < 30f * 30f;
                if (!seen)
                {
                    Vector3 chest = bot.ChestPosition;
                    float visibility = AIVisibility.VisibilityOf(bot.Position, false, false);
                    foreach (var friend in members[0])
                    {
                        if (friend == null || !friend.IsAlive) continue;
                        if (AIVisibility.CanSee(friend.Position + Vector3.up * 1.5f, Vector3.forward, 360f, 26f, chest, visibility)) { seen = true; break; }
                    }
                }
                bot.SetSeen(seen);
            }
        }

        // Bots don't push doors; anything closed again (by you) opens when someone walks up to it.
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

        public bool IsCarrying(ArenaBot bot)
        {
            return Mode == GameMode.CaptureTheFlag && Flags[1 - bot.Side] != null && Flags[1 - bot.Side].carrier == (ICombatTarget)bot;
        }

        public bool PlayerCarrying { get { return Mode == GameMode.CaptureTheFlag && Flags[1] != null && player != null && Flags[1].carrier == (ICombatTarget)player; } }

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
                    Announce(NameOf(target) + " took the " + (flag.side == 0 ? "blue" : "red") + " flag", 1 - flag.side);
                    AudioManager.Play2D(Sound.ObjectiveTone, 0.55f, flag.side == 0 ? 0.8f : 1.1f, SoundCategory.Interface);
                    if (target == (ICombatTarget)player) UIManager.Banner("YOU HAVE THE FLAG", "Bring it back to your base", BannerKind.Good);
                    else if (flag.side == 0) UIManager.Banner("YOUR FLAG WAS TAKEN", "Tag out the carrier to drop it", BannerKind.Bad);
                    return;
                }
            }
        }

        void Drop(Flag flag)
        {
            Announce(NameOf(flag.carrier) + " dropped the " + (flag.side == 0 ? "blue" : "red") + " flag", flag.side);
            flag.position = OnNavMesh(flag.carrier.Position);
            flag.carrier = null;
            flag.droppedAt = Time.time;
        }

        void ReturnFlag(Flag flag, ICombatTarget by)
        {
            flag.carrier = null;
            flag.position = flag.home;
            Announce(by != null ? NameOf(by) + " returned the " + (flag.side == 0 ? "blue" : "red") + " flag" : "The " + (flag.side == 0 ? "blue" : "red") + " flag returned to base", flag.side);
            AudioManager.Play2D(Sound.RadioOrder, 0.45f, 1f, SoundCategory.Interface);
        }

        void Capture(Flag flag)
        {
            var carrier = flag.carrier;
            int side = 1 - flag.side;
            Score[side] += 1f;
            var bot = carrier as ArenaBot;
            if (bot != null) bot.Captures++;
            if (carrier == (ICombatTarget)player) PlayerCaptures++;
            flag.carrier = null;
            flag.position = flag.home;
            Announce(NameOf(carrier) + " CAPTURED the " + (flag.side == 0 ? "blue" : "red") + " flag", side);
            UIManager.Banner(side == 0 ? "FLAG CAPTURED" : "RED TEAM SCORED", Mathf.RoundToInt(Score[0]) + " - " + Mathf.RoundToInt(Score[1]), side == 0 ? BannerKind.Good : BannerKind.Bad);
            AudioManager.Play2D(side == 0 ? Sound.Complete : Sound.Warning, 0.6f, 1f, SoundCategory.Interface);
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
                if (owner >= 0)
                {
                    Announce((owner == 0 ? "Blue" : "Red") + " team took the zone", owner);
                    UIManager.Banner(owner == 0 ? "ZONE TAKEN" : "ZONE LOST", owner == 0 ? "Hold it to keep scoring" : "Get in there and take it back", owner == 0 ? BannerKind.Good : BannerKind.Bad);
                    AudioManager.Play2D(owner == 0 ? Sound.ObjectiveTone : Sound.Warning, 0.55f, 1f, SoundCategory.Interface);
                }
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
            Credit(info, bot.Side, bot.Callsign);
        }

        public void OnPlayerDown()
        {
            if (!running || player == null) return;
            PlayerDeaths++;
            PlayerRespawnAt = Time.time + RespawnDelay;
            var hit = player.Health.LastHit;
            var shooter = hit.shooter as ArenaBot;
            PlayerTaggedBy = shooter != null ? shooter.Callsign + (hit.weapon != null ? "  (" + hit.weapon.displayName + ")" : "") : "the other team";
            Credit(hit, 0, "You");
            UIManager.Notify("Tagged out! Back in " + Mathf.RoundToInt(RespawnDelay) + " seconds", true);
        }

        void Credit(DamageInfo info, int victimSide, string victimName)
        {
            string by = "Someone";
            var shooter = info.shooter as ArenaBot;
            if (shooter != null)
            {
                shooter.Kills++;
                by = shooter.Callsign;
            }
            else if (info.byPlayer)
            {
                PlayerKills++;
                by = "You";
            }
            if (Mode == GameMode.TeamDeathmatch) Score[1 - victimSide] += 1f;
            Announce(by + "  >  " + victimName + (info.weapon != null ? "   [" + info.weapon.displayName + "]" : ""), 1 - victimSide);
        }

        void Announce(string text, int side)
        {
            Feed.Add(new FeedLine { text = text, side = side, time = Time.unscaledTime });
            if (Feed.Count > 6) Feed.RemoveAt(0);
        }

        public string NameOf(ICombatTarget target)
        {
            if (target == null) return "Someone";
            if (target == (ICombatTarget)player) return "You";
            var bot = target as ArenaBot;
            return bot != null ? bot.Callsign : "Someone";
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
                mode = Mode, mapName = MapName, winner = winner, reason = reason, time = Elapsed,
                blueScore = Mathf.FloorToInt(Score[0]), redScore = Mathf.FloorToInt(Score[1]), scoreLimit = ScoreLimit,
                playerKills = PlayerKills, playerDeaths = PlayerDeaths, playerCaptures = PlayerCaptures,
            };
            result.rows.Add(new VersusResult.ScoreRow { name = player != null ? player.Officer.callsign + " (you)" : "You", side = 0, kills = PlayerKills, deaths = PlayerDeaths, captures = PlayerCaptures, isPlayer = true });
            foreach (var bot in Bots) result.rows.Add(new VersusResult.ScoreRow { name = bot.Callsign, side = bot.Side, kills = bot.Kills, deaths = bot.Deaths, captures = bot.Captures });
            result.rows.Sort((a, b) => a.side != b.side ? a.side.CompareTo(b.side) : (b.kills * 2 + b.captures * 5 - b.deaths).CompareTo(a.kills * 2 + a.captures * 5 - a.deaths));
            GameManager.Instance.EndMatch(result);
        }
    }
}
