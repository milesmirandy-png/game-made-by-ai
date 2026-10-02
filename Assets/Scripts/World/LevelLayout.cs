using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    public class RoomArea
    {
        public string name;
        public Bounds bounds;
        public bool mustSecure;
    }

    public struct EnemySpawn
    {
        public Vector3 position;
        public float yaw;
        public Vector3[] patrol;
        public string profile;
    }

    public struct CivilianSpawn
    {
        public Vector3 position;
        public float yaw;
    }

    // Everything the game needs to know about a built level: where people
    // start, which rooms matter, cover spots, doors and so on.
    public class LevelLayout
    {
        public string missionName;
        public string briefing;
        public Transform root;
        public Vector3 playerSpawn;
        public float playerYaw;
        public Bounds navBounds;
        public Bounds buildingBounds;
        public Bounds extractionZone;
        public Vector3 extractionPoint;
        public readonly List<RoomArea> rooms = new List<RoomArea>();
        public readonly List<EnemySpawn> enemies = new List<EnemySpawn>();
        public readonly List<CivilianSpawn> civilians = new List<CivilianSpawn>();
        public readonly List<Vector3> coverPoints = new List<Vector3>();
        public readonly List<DoorController> doors = new List<DoorController>();

        public RoomArea RoomAt(Vector3 position)
        {
            foreach (var room in rooms)
                if (room.bounds.Contains(new Vector3(position.x, room.bounds.center.y, position.z))) return room;
            return null;
        }
    }
}
