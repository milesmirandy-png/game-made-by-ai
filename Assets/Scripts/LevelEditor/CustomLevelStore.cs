using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Swat
{
    // Saves and loads level creator levels as JSON files in
    // <persistentDataPath>/CustomLevels/. Files are written to a temporary
    // file first and then moved into place, so a crash can't half-write one.
    public static class CustomLevelStore
    {
        public const string MapPrefix = "custom:";
        static readonly Dictionary<string, CustomLevel> cache = new Dictionary<string, CustomLevel>();

        public static string Folder { get { return Path.Combine(Application.persistentDataPath, "CustomLevels"); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cache.Clear();
        }

        static string PathFor(string id) { return Path.Combine(Folder, id + ".json"); }

        // All saved levels, newest first. Creates the example level the first time.
        public static List<CustomLevel> List()
        {
            var list = new List<CustomLevel>();
            try
            {
                if (!Directory.Exists(Folder))
                {
                    Directory.CreateDirectory(Folder);
                    string error;
                    Save(Sample(), out error);
                }
                foreach (var file in Directory.GetFiles(Folder, "*.json"))
                {
                    var level = Read(file);
                    if (level == null) continue;
                    level.id = Path.GetFileNameWithoutExtension(file);
                    cache[level.id] = level;
                    list.Add(level);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("SWAT: could not list custom levels. " + e.Message);
            }
            list.Sort((a, b) => string.CompareOrdinal(b.modified ?? "", a.modified ?? ""));
            return list;
        }

        public static CustomLevel Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            CustomLevel level;
            if (cache.TryGetValue(id, out level) && level != null) return level;
            level = Read(PathFor(id));
            if (level != null)
            {
                level.id = id;
                cache[id] = level;
            }
            return level;
        }

        // Levels being played straight from the editor are kept here too, so restarts rebuild the same layout.
        public static void Remember(CustomLevel level)
        {
            if (level != null && !string.IsNullOrEmpty(level.id)) cache[level.id] = level.Clone();
        }

        static CustomLevel Read(string file)
        {
            try
            {
                if (!File.Exists(file)) return null;
                var level = JsonUtility.FromJson<CustomLevel>(File.ReadAllText(file));
                if (level == null) return null;
                if (level.rooms == null) level.rooms = new List<CustomRoom>();
                if (level.doors == null) level.doors = new List<CustomDoor>();
                if (level.objects == null) level.objects = new List<CustomObject>();
                level.width = Mathf.Clamp(level.width, CustomLevel.MinSize, CustomLevel.MaxSize);
                level.height = Mathf.Clamp(level.height, CustomLevel.MinSize, CustomLevel.MaxSize);
                return level;
            }
            catch (Exception e)
            {
                Debug.LogWarning("SWAT: could not read custom level " + file + ". " + e.Message);
                return null;
            }
        }

        public static bool Save(CustomLevel level, out string error)
        {
            error = null;
            try
            {
                Directory.CreateDirectory(Folder);
                if (string.IsNullOrEmpty(level.id)) level.id = NewId(level.name);
                level.modified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string path = PathFor(level.id);
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(level, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                cache[level.id] = level.Clone();
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.LogWarning("SWAT: could not save custom level. " + e.Message);
                return false;
            }
        }

        public static bool Delete(string id)
        {
            try
            {
                cache.Remove(id);
                string path = PathFor(id);
                if (File.Exists(path)) File.Delete(path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("SWAT: could not delete custom level. " + e.Message);
                return false;
            }
        }

        // A file-name-safe id from the level name plus a short random suffix.
        static string NewId(string name)
        {
            var chars = new System.Text.StringBuilder();
            foreach (char c in (name ?? "level").ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c)) chars.Append(c);
                else if (chars.Length > 0 && chars[chars.Length - 1] != '_') chars.Append('_');
                if (chars.Length >= 24) break;
            }
            if (chars.Length == 0) chars.Append("level");
            return chars.ToString().Trim('_') + "_" + UnityEngine.Random.Range(1000, 9999);
        }

        public static CustomLevel NewLevel()
        {
            return new CustomLevel { name = "Untitled Level", author = "", width = 40, height = 30, timeOfDay = 0, difficulty = 2, squad = 3, parTime = 480 };
        }

        // The example that ships with the level creator: a small pawn shop.
        public static CustomLevel Sample()
        {
            var level = new CustomLevel
            {
                id = "example_pawn_shop",
                name = "Example: Pawn Shop",
                author = "TRU",
                description = "An example level made with the level creator. Open it in the editor to see how it's built.",
                width = 32, height = 24, timeOfDay = (int)TimeOfDay.Evening, difficulty = 1, squad = 3, parTime = 300,
            };
            level.rooms.Add(new CustomRoom { x = 6, z = 8, w = 12, h = 8, type = CustomRoomType.Retail, name = "Pawn Shop" });
            level.rooms.Add(new CustomRoom { x = 18, z = 8, w = 6, h = 5, type = CustomRoomType.Office, name = "Back Office" });
            level.rooms.Add(new CustomRoom { x = 18, z = 13, w = 6, h = 6, type = CustomRoomType.Storage, name = "Storage" });
            level.rooms.Add(new CustomRoom { x = 6, z = 16, w = 5, h = 3, type = CustomRoomType.Restroom, name = "Restroom" });
            level.doors.Add(new CustomDoor { x = 12, z = 8, alongZ = false, type = CustomDoorType.Door });
            level.doors.Add(new CustomDoor { x = 18, z = 10, alongZ = true, type = CustomDoorType.Door });
            level.doors.Add(new CustomDoor { x = 21, z = 13, alongZ = false, type = CustomDoorType.LockedPickable });
            level.doors.Add(new CustomDoor { x = 8, z = 16, alongZ = false, type = CustomDoorType.Door });
            level.doors.Add(new CustomDoor { x = 21, z = 19, alongZ = false, type = CustomDoorType.LockedBreachable });
            Add(level, CustomObjectType.TeamStart, 12.5f, 3.5f, 0);
            Add(level, CustomObjectType.Extraction, 11.5f, 3.5f, 0);
            Add(level, CustomObjectType.SafeZone, 4.5f, 2.5f, 0);
            Add(level, CustomObjectType.ArmedSuspect, 9.5f, 12.5f, 180);
            Add(level, CustomObjectType.ArmedSuspect, 15.5f, 10.5f, 270);
            Add(level, CustomObjectType.UnarmedSuspect, 20.5f, 10.5f, 180);
            Add(level, CustomObjectType.NervousSuspect, 8.5f, 17.5f, 180);
            Add(level, CustomObjectType.Leader, 21.5f, 16.5f, 180);
            Add(level, CustomObjectType.Hostage, 16.5f, 14.5f, 180);
            Add(level, CustomObjectType.Civilian, 7.5f, 9.5f, 90);
            Add(level, CustomObjectType.Evidence, 22.5f, 17.5f, 0);
            Add(level, CustomObjectType.Evidence, 19f, 9f, 0);
            Add(level, CustomObjectType.Console, 22.5f, 9.5f, 180);
            Add(level, CustomObjectType.Camera, 6.5f, 15.5f, 135);
            Add(level, CustomObjectType.Counter, 12.5f, 13.5f, 0);
            Add(level, CustomObjectType.Shelf, 6.5f, 11.5f, 90);
            Add(level, CustomObjectType.Desk, 20.5f, 11.5f, 0);
            Add(level, CustomObjectType.Crate, 19.5f, 17.5f, 0);
            Add(level, CustomObjectType.Plant, 17.5f, 8.5f, 0);
            Add(level, CustomObjectType.Car, 25.5f, 3.5f, 0);
            return level;
        }

        static void Add(CustomLevel level, CustomObjectType type, float x, float z, int yaw)
        {
            level.objects.Add(new CustomObject { type = type, x = x, z = z, yaw = yaw });
        }
    }
}
