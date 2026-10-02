using System;
using System.Collections.Generic;

namespace Swat
{
    public enum CustomRoomType { Office, Lobby, Hallway, Storage, Security, Restroom, Breakroom, Warehouse, Residential, Medical, Club, Vault, Retail, Conference, Utility }

    public enum CustomDoorType { Doorway, Door, LockedPickable, LockedBreachable, Electronic }

    public enum CustomObjectType
    {
        TeamStart, ArmedSuspect, UnarmedSuspect, NervousSuspect, Guard, ArmoredSuspect, Leader,
        Civilian, Hostage, InjuredCivilian, HidingCivilian,
        Evidence, Console, Camera, AlarmPanel, Extraction, SafeZone,
        Desk, Table, Shelf, Crate, Couch, Plant, Counter, Bed, Car, Lamp,
    }

    // A rectangular room on the 1 m grid. Walls are generated wherever a room
    // meets another room or the outside.
    [Serializable]
    public class CustomRoom
    {
        public int x, z, w, h;
        public CustomRoomType type;
        public string name;
    }

    // A door centered on a grid point of a wall, two cells wide. alongZ = the
    // wall runs north-south (a vertical line on the editor grid).
    [Serializable]
    public class CustomDoor
    {
        public int x, z;
        public bool alongZ;
        public CustomDoorType type = CustomDoorType.Door;
    }

    // Anything placed at a point: the team start, people, objectives, devices and props.
    [Serializable]
    public class CustomObject
    {
        public CustomObjectType type;
        public float x, z;
        public int yaw;
    }

    // A level made in the level creator, saved as JSON in
    // <persistentDataPath>/CustomLevels/<id>.json.
    [Serializable]
    public class CustomLevel
    {
        public const int MinSize = 16, MaxSize = 64;

        public int version = 1;
        public string id;
        public string name = "Untitled Level";
        public string author = "";
        public string description = "";
        public int width = 40, height = 30;
        public int timeOfDay;          // TimeOfDay
        public bool powerOutage;
        public int difficulty = 2;     // 1-3 stars
        public int squad = 3;          // squadmates allowed
        public int parTime = 480;
        public bool grassGround = true;
        public string modified;
        public List<CustomRoom> rooms = new List<CustomRoom>();
        public List<CustomDoor> doors = new List<CustomDoor>();
        public List<CustomObject> objects = new List<CustomObject>();

        public int Count(CustomObjectType type)
        {
            int n = 0;
            foreach (var o in objects) if (o.type == type) n++;
            return n;
        }

        public CustomObject Find(CustomObjectType type)
        {
            foreach (var o in objects) if (o.type == type) return o;
            return null;
        }

        public int SuspectCount
        {
            get
            {
                int n = 0;
                foreach (var o in objects) if (IsSuspect(o.type)) n++;
                return n;
            }
        }

        public int CivilianCount
        {
            get
            {
                int n = 0;
                foreach (var o in objects) if (IsCivilian(o.type)) n++;
                return n;
            }
        }

        public static bool IsSuspect(CustomObjectType t) { return t >= CustomObjectType.ArmedSuspect && t <= CustomObjectType.Leader; }
        public static bool IsCivilian(CustomObjectType t) { return t >= CustomObjectType.Civilian && t <= CustomObjectType.HidingCivilian; }
        public static bool IsProp(CustomObjectType t) { return t >= CustomObjectType.Desk; }
        public static bool IsZone(CustomObjectType t) { return t == CustomObjectType.Extraction || t == CustomObjectType.SafeZone; }

        public CustomLevel Clone()
        {
            return UnityEngine.JsonUtility.FromJson<CustomLevel>(UnityEngine.JsonUtility.ToJson(this));
        }
    }
}
