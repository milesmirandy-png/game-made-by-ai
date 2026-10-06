using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    public class MapArea
    {
        public string name;
        public Bounds bounds;
    }

    public class EnemySpawnPoint
    {
        public Vector3 position;
        public float yaw;
        public string tag;
        public Vector3[] patrol;
    }

    public class CivilianSpawnPoint
    {
        public Vector3 position;
        public float yaw;
        public string tag;
    }

    public struct WallSegment
    {
        public Vector2 a, b;
        public int area;
        public bool exterior;
    }

    public struct Mount
    {
        public string id, title;
        public Vector3 position;
        public float yaw;
        public bool unlockDoors, reviewFootage;
    }

    // Everything the game needs to know about a built map: rooms, doors,
    // walls (for the tactical map), spawn point pools, security devices,
    // zones and where the team arrives. Missions pick from the pools.
    public class LevelLayout
    {
        public string mapId;
        public string displayName;
        public Transform root;
        // What changes between the top-down and first-person views (switched with V).
        public readonly ViewParts view = new ViewParts();
        public readonly List<MapArea> areas = new List<MapArea>();
        public Vector3 playerSpawn;
        public float playerYaw;
        public readonly List<Vector3> squadSpawns = new List<Vector3>();
        public Vector3 vanParking, vanArrivalStart;
        public float vanYaw;
        public Bounds navBounds;
        public readonly List<RoomController> rooms = new List<RoomController>();
        public readonly List<DoorController> doors = new List<DoorController>();
        public readonly List<DoorController> lockableDoors = new List<DoorController>(); // a mission may lock these at random
        public readonly List<Stairwell> stairs = new List<Stairwell>();
        public readonly List<WallSegment> walls = new List<WallSegment>();
        public readonly List<EnemySpawnPoint> enemySpawns = new List<EnemySpawnPoint>();
        public readonly List<CivilianSpawnPoint> civilianSpawns = new List<CivilianSpawnPoint>();
        public readonly List<Vector3> coverPoints = new List<Vector3>();
        public readonly List<Vector3> evidenceSpots = new List<Vector3>();
        public readonly List<Vector3> escapePoints = new List<Vector3>();
        public readonly List<Mount> cameraMounts = new List<Mount>();
        public readonly List<Mount> consoleMounts = new List<Mount>();
        public readonly List<Vector3> alarmBeacons = new List<Vector3>();
        public readonly List<Vector3> trainingTargets = new List<Vector3>();
        public readonly List<Light> outdoorLights = new List<Light>();
        public readonly Dictionary<string, Bounds> zones = new Dictionary<string, Bounds>();
        public Mount alarmPanel;
        public bool hasAlarm;
        public ExtractionZone extraction;
        public readonly List<ExtractionZone> safeZones = new List<ExtractionZone>();

        // Created by the mission from the mounts above.
        public readonly List<SecurityConsole> consoles = new List<SecurityConsole>();
        public readonly List<EvidenceItem> evidence = new List<EvidenceItem>();
        public AlarmSystem alarm;

        // The room a point is in. A closet inside a corridor's rectangle is its own room, so the
        // smallest room that contains the point wins.
        public RoomController RoomAt(Vector3 point)
        {
            RoomController best = null;
            float bestArea = float.MaxValue;
            for (int i = 0; i < rooms.Count; i++)
            {
                if (!rooms[i].Contains(point)) continue;
                var size = rooms[i].Bounds.size;
                float area = size.x * size.z;
                if (area >= bestArea) continue;
                best = rooms[i];
                bestArea = area;
            }
            return best;
        }

        public RoomController Room(string id)
        {
            foreach (var room in rooms) if (room.Id == id) return room;
            return null;
        }

        public DoorController Door(string id)
        {
            foreach (var door in doors) if (door.Id == id) return door;
            return null;
        }

        public int AreaAt(Vector3 point)
        {
            for (int i = 0; i < areas.Count; i++)
            {
                var b = areas[i].bounds;
                if (point.x >= b.min.x && point.x <= b.max.x && point.z >= b.min.z && point.z <= b.max.z) return i;
            }
            return 0;
        }

        public bool InSafeZone(Vector3 point)
        {
            if (extraction != null && extraction.Contains(point)) return true;
            foreach (var zone in safeZones) if (zone.Contains(point)) return true;
            return false;
        }

        public bool InZone(string id, Vector3 point)
        {
            Bounds b;
            if (!zones.TryGetValue(id, out b)) return false;
            return point.x >= b.min.x && point.x <= b.max.x && point.z >= b.min.z && point.z <= b.max.z;
        }
    }
}
