using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Turns a level from the level creator into a real map (through the same
    // LevelBuilder the built-in maps use) and a MissionData with objectives
    // generated from what was placed.
    public static class CustomLevelBuilder
    {
        public static readonly string[] RoomTypeNames =
        {
            "Office", "Lobby", "Hallway", "Storage", "Security Room", "Restroom", "Break Room", "Warehouse",
            "Apartment", "Medical", "Club", "Vault", "Shop Floor", "Conference Room", "Utility Room",
        };

        static readonly RoomKind[] RoomKinds =
        {
            RoomKind.Office, RoomKind.Lobby, RoomKind.Hallway, RoomKind.Storage, RoomKind.Security, RoomKind.Restroom, RoomKind.Breakroom, RoomKind.Warehouse,
            RoomKind.Residential, RoomKind.Medical, RoomKind.Club, RoomKind.Vault, RoomKind.Retail, RoomKind.Conference, RoomKind.Utility,
        };

        public static readonly Color[] FloorColors =
        {
            new Color(0.36f, 0.42f, 0.52f), new Color(0.72f, 0.7f, 0.66f), new Color(0.6f, 0.6f, 0.62f), new Color(0.48f, 0.48f, 0.47f),
            new Color(0.3f, 0.33f, 0.38f), new Color(0.75f, 0.8f, 0.82f), new Color(0.7f, 0.64f, 0.55f), new Color(0.46f, 0.46f, 0.45f),
            new Color(0.45f, 0.32f, 0.2f), new Color(0.82f, 0.86f, 0.86f), new Color(0.2f, 0.18f, 0.24f), new Color(0.45f, 0.47f, 0.5f),
            new Color(0.82f, 0.82f, 0.8f), new Color(0.45f, 0.4f, 0.36f), new Color(0.42f, 0.42f, 0.42f),
        };

        public static string RoomName(CustomLevel level, int index)
        {
            var room = level.rooms[index];
            if (!string.IsNullOrEmpty(room.name)) return room.name;
            return RoomTypeNames[Mathf.Clamp((int)room.type, 0, RoomTypeNames.Length - 1)] + " " + (index + 1);
        }

        public static string RoomId(int index) { return "room" + (index + 1); }

        public static LevelLayout Build(Transform parent, CustomLevel level)
        {
            var geo = new CustomLevelGeometry(level);
            float w = geo.Width, h = geo.Height;
            var b = new LevelBuilder(parent, "custom", string.IsNullOrEmpty(level.name) ? "Custom Level" : level.name);
            var outerWall = new Color(0.7f, 0.66f, 0.6f);
            var innerWall = new Color(0.86f, 0.84f, 0.79f);

            b.Area(b.Layout.displayName, -12f, -12f, w + 12f, h + 12f);
            b.Layout.navBounds = new Bounds(new Vector3(w * 0.5f, 1f, h * 0.5f - 2f), new Vector3(w + 32f, 6f, h + 38f));
            b.Slab("Ground", -40f, -40f, w + 40f, h + 40f, -0.2f, 0f, level.grassGround ? new Color(0.3f, 0.42f, 0.27f) : new Color(0.24f, 0.25f, 0.27f));
            b.Boundary(-11f, -11f, w + 11f, h + 11f);

            // Team start, van and zones.
            var start = level.Find(CustomObjectType.TeamStart) ?? new CustomObject { type = CustomObjectType.TeamStart, x = w * 0.5f, z = -6f };
            Vector3 parking, back, right;
            CustomLevelGeometry.VanPath(start, out parking, out back, out right);
            b.Arrival(new Vector3(start.x, 0.05f, start.z), start.yaw, parking, start.yaw, parking + back * 26f);
            var exit = level.Find(CustomObjectType.Extraction) ?? start;
            b.Extraction(exit.x - 2.5f, exit.z - 2.75f, exit.x + 2.5f, exit.z + 2.75f);

            // Rooms, with the look and sound of the chosen type.
            for (int i = 0; i < level.rooms.Count; i++)
            {
                var r = level.rooms[i];
                int type = Mathf.Clamp((int)r.type, 0, RoomKinds.Length - 1);
                var room = b.Room(RoomId(i), RoomName(level, i), r.x, r.z, r.x + r.w, r.z + r.h, FloorColors[type]);
                room.Style = RoomStyle.For(RoomKinds[type]);
                if (room.Floor != null)
                {
                    Shapes.ApplySurface(room.Floor, FloorColors[type], room.Style.floor);
                    SurfaceTag.Set(room.Floor, room.Style.footsteps);
                }
            }

            // Walls with their doors.
            var doorIds = new Dictionary<CustomDoor, string>();
            for (int i = 0; i < level.doors.Count; i++) doorIds[level.doors[i]] = "door" + (i + 1);
            foreach (var segment in geo.Segments())
            {
                var gaps = new List<Gap>();
                foreach (var door in level.doors)
                {
                    if (door.alongZ != segment.alongZ) continue;
                    int line = segment.alongZ ? door.x : door.z;
                    int along = segment.alongZ ? door.z : door.x;
                    if (line != segment.line || along - 1 < segment.from || along + 1 > segment.to || !geo.DoorFits(door)) continue;
                    gaps.Add(MakeGap(door, along, doorIds[door]));
                }
                var color = segment.exterior ? outerWall : innerWall;
                if (segment.alongZ) b.WallZ(segment.line, segment.from, segment.to, color, segment.exterior, gaps.ToArray());
                else b.WallX(segment.line, segment.from, segment.to, color, segment.exterior, gaps.ToArray());
            }

            // Everything placed.
            int consoles = 0, suspects = 0, civilians = 0;
            bool alarm = false;
            foreach (var o in level.objects)
            {
                var p = new Vector3(o.x, 0f, o.z);
                switch (o.type)
                {
                    case CustomObjectType.ArmedSuspect:
                    case CustomObjectType.UnarmedSuspect:
                    case CustomObjectType.NervousSuspect:
                    case CustomObjectType.Guard:
                    case CustomObjectType.ArmoredSuspect:
                    case CustomObjectType.Leader:
                        b.EnemySpot("cs" + (suspects++), o.x, o.z, o.yaw);
                        break;
                    case CustomObjectType.Civilian:
                    case CustomObjectType.Hostage:
                    case CustomObjectType.InjuredCivilian:
                    case CustomObjectType.HidingCivilian:
                        b.CivilianSpot("cc" + (civilians++), o.x, o.z, o.yaw);
                        break;
                    case CustomObjectType.Evidence: b.Evidence(o.x, o.z); break;
                    case CustomObjectType.Console: b.Console("console" + (++consoles), "Security Console", o.x, o.z, o.yaw, true, true); break;
                    case CustomObjectType.Camera: b.Camera(o.x, o.z, o.yaw); break;
                    case CustomObjectType.AlarmPanel:
                        if (alarm) break;
                        alarm = true;
                        b.AlarmPanel(o.x, o.z, o.yaw);
                        b.Beacon(o.x, o.z);
                        break;
                    case CustomObjectType.SafeZone: b.SafeZone(o.x - 2f, o.z - 2f, o.x + 2f, o.z + 2f); break;
                    case CustomObjectType.Desk: b.Desk(p, o.yaw); break;
                    case CustomObjectType.Table: b.Table(p, new Vector3(1.6f, 0.75f, 0.9f), new Color(0.5f, 0.38f, 0.26f), o.yaw); break;
                    case CustomObjectType.Shelf: b.Shelf(p, o.yaw, 2f); break;
                    case CustomObjectType.Crate: b.Crate(p, 1.1f, (Mathf.RoundToInt(o.x * 3f + o.z) & 1) == 0); break;
                    case CustomObjectType.Couch: b.Couch(p, o.yaw, new Color(0.32f, 0.36f, 0.45f)); break;
                    case CustomObjectType.Plant: b.Plant(p); break;
                    case CustomObjectType.Counter: b.Prop("Counter", p, new Vector3(2.4f, 1.05f, 0.7f), new Color(0.5f, 0.4f, 0.3f), true, o.yaw); break;
                    case CustomObjectType.Bed: b.Bed(p, o.yaw); break;
                    case CustomObjectType.Car: b.Car(p, o.yaw, new Color(0.3f, 0.32f, 0.4f)); break;
                    case CustomObjectType.Lamp: b.Lamp(p); break;
                }
            }
            if (alarm)
            {
                // A couple of beacons in the biggest rooms so the alarm is visible.
                var order = new List<int>();
                for (int i = 0; i < level.rooms.Count; i++) order.Add(i);
                order.Sort((x, y) => (level.rooms[y].w * level.rooms[y].h).CompareTo(level.rooms[x].w * level.rooms[x].h));
                for (int i = 0; i < order.Count && i < 3; i++)
                {
                    var r = level.rooms[order[i]];
                    b.Beacon(r.x + r.w * 0.5f, r.z + r.h * 0.5f);
                }
            }

            // Escape routes for a fleeing leader, and street lamps at the corners.
            b.Escape(-8f, h * 0.5f);
            b.Escape(w + 8f, h * 0.5f);
            foreach (var corner in new[] { new Vector3(-2f, 0f, -2f), new Vector3(w + 2f, 0f, -2f), new Vector3(-2f, 0f, h + 2f), new Vector3(w + 2f, 0f, h + 2f) })
                if (!geo.InsideAnyRoom(corner, 0.5f)) b.Lamp(corner);
            return b.Finish();
        }

        static Gap MakeGap(CustomDoor door, float center, string id)
        {
            switch (door.type)
            {
                case CustomDoorType.Doorway: return Gap.Open(center, 1.8f);
                case CustomDoorType.LockedPickable: return Gap.Locked(center, id, true, true);
                case CustomDoorType.LockedBreachable: return Gap.Locked(center, id, true, false);
                case CustomDoorType.Electronic: return Gap.Electronic(center, id);
                default: return Gap.Door(center, id);
            }
        }

        static string EnemyId(CustomObjectType type)
        {
            switch (type)
            {
                case CustomObjectType.UnarmedSuspect: return "suspect_unarmed";
                case CustomObjectType.NervousSuspect: return "nervous";
                case CustomObjectType.Guard: return "guard";
                case CustomObjectType.ArmoredSuspect: return "armored";
                case CustomObjectType.Leader: return "leader";
                default: return "hostile";
            }
        }

        static CivilianType CivilianKind(CustomObjectType type)
        {
            switch (type)
            {
                case CustomObjectType.Hostage: return CivilianType.Hostage;
                case CustomObjectType.InjuredCivilian: return CivilianType.Injured;
                case CustomObjectType.HidingCivilian: return CivilianType.Hiding;
                default: return CivilianType.Visitor;
            }
        }

        static ObjectiveDefinition O(ObjectiveType type, string text, int points, string target = null, int count = 0)
        {
            return new ObjectiveDefinition(type, text, points, target, count);
        }

        // A mission built from what the level contains.
        public static MissionData ToMission(CustomLevel level)
        {
            var m = ScriptableObject.CreateInstance<MissionData>();
            m.id = "custom_" + level.id;
            m.name = m.id;
            m.displayName = string.IsNullOrEmpty(level.name) ? "Custom Level" : level.name;
            m.location = string.IsNullOrEmpty(level.author) ? "Custom level" : "Custom level by " + level.author;
            m.mapId = CustomLevelStore.MapPrefix + level.id;
            m.isCustom = true;
            m.difficulty = Mathf.Clamp(level.difficulty, 1, 3);
            m.maxSquad = Mathf.Clamp(level.squad, 0, 3);
            m.parTime = Mathf.Clamp(level.parTime, 60, 3600);
            m.timeOfDay = (TimeOfDay)Mathf.Clamp(level.timeOfDay, 0, 2);
            m.powerOutageChance = level.powerOutage ? 1f : 0f;
            m.alarmArmedChance = 1f;
            m.camerasActiveChance = 1f;
            m.randomLockChance = 0f;
            m.seed = 0;
            m.sortOrder = 1000;
            m.optionalCount = 2;
            m.thumbnailColor = m.timeOfDay == TimeOfDay.Night ? new Color(0.2f, 0.2f, 0.38f) : m.timeOfDay == TimeOfDay.Evening ? new Color(0.42f, 0.3f, 0.22f) : new Color(0.24f, 0.38f, 0.42f);

            int suspects = level.SuspectCount, civilians = level.CivilianCount;
            int evidence = level.Count(CustomObjectType.Evidence);
            bool leader = level.Count(CustomObjectType.Leader) > 0;
            m.missionType = civilians > suspects ? MissionType.CivilianRescue : evidence > 0 ? MissionType.Investigation : MissionType.BuildingClearance;
            m.description = string.IsNullOrEmpty(level.description) ? "A custom level made in the level creator." : level.description;
            m.briefing = m.description + " Intel: " + suspects + " suspect(s), " + civilians + " civilian(s)" + (evidence > 0 ? ", " + evidence + " piece(s) of evidence" : "")
                + ". Clear the building, then return to the van.";

            m.objectives.Add(O(ObjectiveType.EnterBuilding, "Enter the building", 100));
            if (suspects > 0) m.objectives.Add(O(ObjectiveType.SecureSuspects, "Secure all suspects", 300));
            if (leader) m.objectives.Add(O(ObjectiveType.ApprehendLeader, "Arrest the leader", 250));
            if (civilians > 0) m.objectives.Add(O(ObjectiveType.RescueCivilians, "Evacuate all civilians", 300));
            if (evidence > 0) m.objectives.Add(O(ObjectiveType.SecureEvidence, "Secure " + evidence + " piece(s) of evidence", 200, null, evidence));
            m.objectives.Add(O(ObjectiveType.ReachExtraction, "Return to the SWAT van", 150));

            if (civilians > 0) m.optionalPool.Add(O(ObjectiveType.NoCivilianCasualties, "No civilian casualties", 150));
            m.optionalPool.Add(O(ObjectiveType.NoOfficerDown, "No officer goes down", 150));
            if (suspects >= 2) m.optionalPool.Add(O(ObjectiveType.ArrestSuspects, "Arrest at least " + Mathf.Min(3, suspects) + " suspects", 150, null, Mathf.Min(3, suspects)));
            m.optionalPool.Add(O(ObjectiveType.TimeLimit, "Finish within par time", 100, null, m.parTime > 0 ? Mathf.RoundToInt(m.parTime) : 480));
            if (level.Count(CustomObjectType.Camera) > 0) m.optionalPool.Add(O(ObjectiveType.DisableCameras, "Disable the security cameras", 100));
            if (level.Count(CustomObjectType.AlarmPanel) > 0) m.optionalPool.Add(O(ObjectiveType.AlarmNotTriggered, "Don't let the alarm go off", 150));

            int s = 0, c = 0;
            foreach (var o in level.objects)
            {
                if (CustomLevel.IsSuspect(o.type)) m.enemies.Add(new EnemyGroup(EnemyId(o.type), 1, "cs" + (s++)));
                else if (CustomLevel.IsCivilian(o.type)) m.civilians.Add(new CivilianGroup(CivilianKind(o.type), 1, "cc" + (c++)));
            }
            return m;
        }
    }
}
