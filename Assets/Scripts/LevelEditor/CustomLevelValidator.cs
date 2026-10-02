using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Checks a custom level before it can be played. Errors block playing;
    // warnings are shown but the level still runs.
    public static class CustomLevelValidator
    {
        public const int MaxRooms = 48, MaxSuspects = 24, MaxCivilians = 16, MaxObjects = 260;
        // The team start, zones and cars may also sit in a strip below the map (z down to -Margin).
        public const float Margin = 8f;

        public static bool AllowedInMargin(CustomObjectType type)
        {
            return type == CustomObjectType.TeamStart || type == CustomObjectType.Extraction || type == CustomObjectType.SafeZone || type == CustomObjectType.Car || type == CustomObjectType.Lamp;
        }

        public static bool Validate(CustomLevel level, List<string> errors, List<string> warnings)
        {
            errors.Clear();
            warnings.Clear();
            var geo = new CustomLevelGeometry(level);

            if (level.rooms.Count == 0) errors.Add("Add at least one room (Room tool: drag a rectangle).");
            if (level.rooms.Count > MaxRooms) errors.Add("Too many rooms (max " + MaxRooms + ").");
            if (level.SuspectCount > MaxSuspects) errors.Add("Too many suspects (max " + MaxSuspects + ").");
            if (level.CivilianCount > MaxCivilians) errors.Add("Too many civilians (max " + MaxCivilians + ").");
            if (level.objects.Count > MaxObjects) errors.Add("Too many objects (max " + MaxObjects + ").");
            if (level.Count(CustomObjectType.Leader) > 1) errors.Add("Only one Leader per level.");
            if (level.SuspectCount == 0 && level.CivilianCount == 0 && level.Count(CustomObjectType.Evidence) == 0)
                errors.Add("Give the team something to do: place suspects, civilians or evidence.");

            // Team start and the van.
            var start = level.Find(CustomObjectType.TeamStart);
            if (start == null) errors.Add("Place the Team Start outside the building.");
            else if (level.Count(CustomObjectType.TeamStart) > 1) errors.Add("Only one Team Start per level.");
            else
            {
                var p = new Vector3(start.x, 0f, start.z);
                if (geo.InsideAnyRoom(p, 0.8f)) errors.Add("The Team Start must be outside, at least a metre from the building.");
                Vector3 parking, back, right;
                CustomLevelGeometry.VanPath(start, out parking, out back, out right);
                bool blocked = false;
                for (float d = -3.2f; d <= 26f && !blocked; d += 0.5f)
                    for (float side = -1.3f; side <= 1.31f && !blocked; side += 1.3f)
                        if (geo.InsideAnyRoom(parking + back * d + right * side, 0.3f)) blocked = true;
                if (blocked) errors.Add("No room for the van: it parks to the right of the Team Start and drives in from behind. Move or turn the start (R).");
                Vector3 behind = Quaternion.Euler(0f, start.yaw, 0f) * Vector3.back;
                foreach (var offset in new[] { right * 1.3f + behind * 0.8f, -right * 1.3f + behind * 0.8f, behind * 1.8f })
                    if (geo.InsideAnyRoom(p + offset, 0.3f)) { errors.Add("The squad spawns around the Team Start: leave a few metres of space."); break; }
            }

            // Every room must connect to the outside through doors.
            if (level.rooms.Count > 0)
            {
                var reached = new bool[level.rooms.Count];
                var queue = new Queue<int>();
                queue.Enqueue(-1);
                while (queue.Count > 0)
                {
                    int node = queue.Dequeue();
                    foreach (var door in level.doors)
                    {
                        if (!geo.DoorFits(door)) continue;
                        int a, b;
                        geo.DoorSides(door, out a, out b);
                        int other = a == node ? b : b == node ? a : -2;
                        if (other == -2 || other == -1) continue;
                        if (other >= 0 && !reached[other]) { reached[other] = true; queue.Enqueue(other); }
                    }
                }
                var unreachable = new List<string>();
                for (int i = 0; i < reached.Length; i++) if (!reached[i]) unreachable.Add(CustomLevelBuilder.RoomName(level, i));
                if (unreachable.Count > 0)
                    errors.Add("Can't reach " + Join(unreachable, 3) + " from outside: add doors (Door tool) so every room connects.");
            }

            int badDoors = 0;
            foreach (var door in level.doors) if (!geo.DoorFits(door)) badDoors++;
            if (badDoors > 0) warnings.Add(badDoors + " door(s) no longer sit on a wall and will be left out.");

            // Objects.
            int outsideGrid = 0, devicesOutside = 0, blockedDoors = 0;
            foreach (var o in level.objects)
            {
                float minZ = AllowedInMargin(o.type) ? -Margin : 0f;
                if (o.x < 0f || o.z < minZ || o.x > geo.Width || o.z > geo.Height) outsideGrid++;
                if ((o.type == CustomObjectType.Console || o.type == CustomObjectType.Camera || o.type == CustomObjectType.AlarmPanel) && geo.RoomAt(o.x, o.z) < 0) devicesOutside++;
                if (CustomLevel.IsProp(o.type) && o.type != CustomObjectType.Lamp && o.type != CustomObjectType.Plant)
                    foreach (var door in level.doors)
                        if (Vector2.Distance(new Vector2(o.x, o.z), new Vector2(door.x, door.z)) < 1.4f) { blockedDoors++; break; }
            }
            if (outsideGrid > 0) errors.Add(outsideGrid + " object(s) are off the map.");
            if (devicesOutside > 0) warnings.Add("Cameras, consoles and alarm panels work best inside rooms.");
            if (blockedDoors > 0) warnings.Add(blockedDoors + " prop(s) may block a door.");
            if (level.Count(CustomObjectType.Extraction) == 0) warnings.Add("No extraction zone placed: one is added around the Team Start.");
            if (level.Count(CustomObjectType.Extraction) > 1) warnings.Add("Only the first extraction zone is used.");
            if (level.Count(CustomObjectType.Leader) == 1 && level.SuspectCount == 1) warnings.Add("The Leader is the only suspect.");
            int electronic = 0;
            foreach (var door in level.doors) if (door.type == CustomDoorType.Electronic) electronic++;
            if (electronic > 0 && level.Count(CustomObjectType.Console) == 0) warnings.Add("Electronic doors without a console can only be breached with charges.");
            return errors.Count == 0;
        }

        static string Join(List<string> items, int max)
        {
            var parts = new List<string>();
            for (int i = 0; i < items.Count && i < max; i++) parts.Add(items[i]);
            string text = string.Join(", ", parts.ToArray());
            if (items.Count > max) text += " and " + (items.Count - max) + " more";
            return text;
        }
    }
}
