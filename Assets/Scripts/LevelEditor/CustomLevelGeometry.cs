using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Works out the walls of a custom level from its rooms. Every cell of the
    // 1 m grid belongs to one room or to the outside; a wall runs along every
    // cell edge where the two sides differ. Shared by the builder, the
    // validator and the editor's drawing, so what you see is what you play.
    public class CustomLevelGeometry
    {
        public struct Segment
        {
            public bool alongZ;     // runs north-south (vertical on the editor grid)
            public int line;        // z for east-west walls, x for north-south walls
            public int from, to;    // grid range along the wall
            public bool exterior;
        }

        public readonly CustomLevel Level;
        public readonly int Width, Height;
        readonly int[,] owner;

        public CustomLevelGeometry(CustomLevel level)
        {
            Level = level;
            Width = Mathf.Clamp(level.width, CustomLevel.MinSize, CustomLevel.MaxSize);
            Height = Mathf.Clamp(level.height, CustomLevel.MinSize, CustomLevel.MaxSize);
            owner = new int[Width, Height];
            for (int x = 0; x < Width; x++)
                for (int z = 0; z < Height; z++) owner[x, z] = -1;
            for (int i = 0; i < level.rooms.Count; i++)
            {
                var r = level.rooms[i];
                for (int x = Mathf.Max(0, r.x); x < Mathf.Min(Width, r.x + r.w); x++)
                    for (int z = Mathf.Max(0, r.z); z < Mathf.Min(Height, r.z + r.h); z++)
                        if (owner[x, z] < 0) owner[x, z] = i;
            }
        }

        // Room index of a cell, or -1 for outside (including off the grid).
        public int Owner(int x, int z)
        {
            if (x < 0 || z < 0 || x >= Width || z >= Height) return -1;
            return owner[x, z];
        }

        public int RoomAt(float x, float z) { return Owner(Mathf.FloorToInt(x), Mathf.FloorToInt(z)); }

        // Wall on the east-west line z, over the cell column x.
        public bool HWall(int x, int z) { int a = Owner(x, z - 1), b = Owner(x, z); return a != b && (a >= 0 || b >= 0); }
        public bool HExterior(int x, int z) { return Owner(x, z - 1) < 0 || Owner(x, z) < 0; }
        // Wall on the north-south line x, over the cell row z.
        public bool VWall(int x, int z) { int a = Owner(x - 1, z), b = Owner(x, z); return a != b && (a >= 0 || b >= 0); }
        public bool VExterior(int x, int z) { return Owner(x - 1, z) < 0 || Owner(x, z) < 0; }

        // A door is two cells wide, centered on grid point (x, z) of a straight wall.
        public bool DoorFits(int x, int z, bool alongZ, out string reason)
        {
            reason = null;
            if (!alongZ)
            {
                if (!HWall(x - 1, z) || !HWall(x, z)) { reason = "Doors go on walls (two cells of straight wall)."; return false; }
                if (HExterior(x - 1, z) != HExterior(x, z)) { reason = "A door can't sit where an outside wall meets an inside wall."; return false; }
                if (VWall(x, z - 1) || VWall(x, z)) { reason = "Too close to a corner or a joining wall."; return false; }
            }
            else
            {
                if (!VWall(x, z - 1) || !VWall(x, z)) { reason = "Doors go on walls (two cells of straight wall)."; return false; }
                if (VExterior(x, z - 1) != VExterior(x, z)) { reason = "A door can't sit where an outside wall meets an inside wall."; return false; }
                if (HWall(x - 1, z) || HWall(x, z)) { reason = "Too close to a corner or a joining wall."; return false; }
            }
            return true;
        }

        public bool DoorFits(CustomDoor door)
        {
            string reason;
            return DoorFits(door.x, door.z, door.alongZ, out reason);
        }

        // The two sides a door connects (room indexes, -1 = outside).
        public void DoorSides(CustomDoor door, out int a, out int b)
        {
            if (!door.alongZ) { a = Owner(door.x, door.z - 1); b = Owner(door.x, door.z); }
            else { a = Owner(door.x - 1, door.z); b = Owner(door.x, door.z); }
        }

        // Straight wall runs, split where the wall changes between inside and outside.
        public List<Segment> Segments()
        {
            var list = new List<Segment>();
            for (int z = 0; z <= Height; z++)
            {
                int start = -1;
                bool ext = false;
                for (int x = 0; x <= Width; x++)
                {
                    bool wall = x < Width && HWall(x, z);
                    bool e = wall && HExterior(x, z);
                    if (start >= 0 && (!wall || e != ext))
                    {
                        list.Add(new Segment { alongZ = false, line = z, from = start, to = x, exterior = ext });
                        start = -1;
                    }
                    if (wall && start < 0) { start = x; ext = e; }
                }
            }
            for (int x = 0; x <= Width; x++)
            {
                int start = -1;
                bool ext = false;
                for (int z = 0; z <= Height; z++)
                {
                    bool wall = z < Height && VWall(x, z);
                    bool e = wall && VExterior(x, z);
                    if (start >= 0 && (!wall || e != ext))
                    {
                        list.Add(new Segment { alongZ = true, line = x, from = start, to = z, exterior = ext });
                        start = -1;
                    }
                    if (wall && start < 0) { start = z; ext = e; }
                }
            }
            return list;
        }

        // Where the van parks and the strip it drives along, from the team start.
        public static void VanPath(CustomObject start, out Vector3 parking, out Vector3 back, out Vector3 right)
        {
            var rotation = Quaternion.Euler(0f, start.yaw, 0f);
            right = rotation * Vector3.right;
            back = rotation * Vector3.back;
            parking = new Vector3(start.x, 0f, start.z) + right * 4.5f + back * 1f;
        }

        public bool InsideAnyRoom(Vector3 p, float margin)
        {
            foreach (var r in Level.rooms)
                if (p.x > r.x - margin && p.x < r.x + r.w + margin && p.z > r.z - margin && p.z < r.z + r.h + margin) return true;
            return false;
        }
    }
}
