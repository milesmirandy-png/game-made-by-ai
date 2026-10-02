using UnityEngine;

namespace Swat
{
    public enum DoorKind { Opening, Closed, Locked, Breachable }

    public struct Gap
    {
        public float center;
        public float width;
        public DoorKind kind;

        public Gap(float center, DoorKind kind, float width = 1.6f)
        {
            this.center = center;
            this.kind = kind;
            this.width = width;
        }
    }

    // Small toolkit for building levels out of boxes: floors, walls with
    // doorways, props that count as cover. New buildings can be made by
    // writing another Build method like BuildClearTheBuilding.
    public class LevelBuilder
    {
        // Walls are drawn low so you can see into rooms from above, but their
        // colliders are full height so they still block bullets and sight.
        const float WallVisualHeight = 1.4f;
        const float WallSolidHeight = 2.6f;
        const float WallThickness = 0.2f;

        readonly LevelLayout layout;
        readonly Transform geometry;
        readonly Transform props;

        LevelBuilder(Transform parent)
        {
            layout = new LevelLayout();
            layout.root = new GameObject("Level").transform;
            layout.root.SetParent(parent, false);
            geometry = Group("Geometry");
            props = Group("Props");
        }

        Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(layout.root, false);
            return t;
        }

        // ---- Mission 1: Clear the Building ----
        //
        //  z=24 +---------+-----------+------------+
        //       | Storage | Break Room|  Security  |
        //  z=13 +---D-----+-----D-----+-----D------+
        //  D    |              Hallway             |
        //  z=10 +---  ----+-----D-----+-----D------+
        //       |  Lobby  | Office A  D  Office B  |
        //   z=0 +---D-----+-----------+------------+
        //      x=0       x=14        x=22         x=30
        //            (parking lot to the south)
        public static LevelLayout BuildClearTheBuilding(Transform parent)
        {
            var b = new LevelBuilder(parent);
            var L = b.layout;
            L.missionName = "Clear the Building";
            L.briefing = "Armed suspects have taken over the Halvorsen Logistics office and are holding staff inside. "
                + "Enter the building, rescue the civilians, neutralize or arrest the suspects, and secure the storage and security rooms. "
                + "Then return to the SWAT van.\n\nArrests and rescues earn the most points. Locked doors with yellow stripes need a breaching charge.";

            L.playerSpawn = new Vector3(7f, 0.05f, -8f);
            L.playerYaw = 0f;
            L.navBounds = new Bounds(new Vector3(15f, 1f, 2f), new Vector3(64f, 6f, 64f));
            L.buildingBounds = new Bounds(new Vector3(15f, 1f, 12f), new Vector3(30f, 4f, 24f));
            L.extractionPoint = new Vector3(9f, 0f, -8f);
            L.extractionZone = new Bounds(new Vector3(9.5f, 1f, -9f), new Vector3(8f, 4f, 7f));

            var grass = new Color(0.32f, 0.44f, 0.27f);
            var asphalt = new Color(0.23f, 0.24f, 0.26f);
            var outsideWall = new Color(0.6f, 0.57f, 0.52f);
            var insideWall = new Color(0.86f, 0.84f, 0.79f);

            // Outside
            b.Slab("Ground", -40f, -40f, 70f, 45f, -0.2f, 0f, grass);
            b.Slab("Parking Lot", -8f, -22f, 38f, -2f, -0.05f, 0.01f, asphalt);
            b.Slab("Sidewalk", -2f, -2f, 32f, 0f, -0.05f, 0.02f, new Color(0.58f, 0.58f, 0.56f));
            for (float x = -4f; x <= 34f; x += 3.6f)
                b.Decal("Parking Line", new Vector3(x, 0.015f, -15f), new Vector3(0.12f, 0.01f, 4.5f), new Color(0.9f, 0.9f, 0.88f));
            b.Car(new Vector3(-2.2f, 0f, -15f), 0f, new Color(0.7f, 0.15f, 0.12f));
            b.Car(new Vector3(1.4f, 0f, -15f), 0f, new Color(0.2f, 0.3f, 0.55f));
            b.Car(new Vector3(19.4f, 0f, -15f), 0f, new Color(0.8f, 0.8f, 0.78f));
            b.Car(new Vector3(26.6f, 0f, -15f), 0f, new Color(0.15f, 0.15f, 0.16f));
            b.Car(new Vector3(30f, 0f, -6f), 90f, new Color(0.45f, 0.5f, 0.35f));
            b.Van(new Vector3(12.5f, 0f, -9f));
            b.Car(new Vector3(3f, 0f, -6.5f), 20f, new Color(0.95f, 0.95f, 0.95f), true);
            b.Boundary(-14f, -26f, 44f, 32f);

            // Floors (each is also a named room for the HUD)
            b.Room("Lobby", 0f, 0f, 14f, 10f, new Color(0.72f, 0.7f, 0.66f));
            b.Room("Office A", 14f, 0f, 22f, 10f, new Color(0.36f, 0.42f, 0.52f));
            b.Room("Office B", 22f, 0f, 30f, 10f, new Color(0.36f, 0.42f, 0.52f));
            b.Room("Hallway", 0f, 10f, 30f, 13f, new Color(0.6f, 0.6f, 0.62f));
            b.Room("Storage Room", 0f, 13f, 10f, 24f, new Color(0.48f, 0.48f, 0.47f), true);
            b.Room("Break Room", 10f, 13f, 20f, 24f, new Color(0.62f, 0.55f, 0.45f));
            b.Room("Security Room", 20f, 13f, 30f, 24f, new Color(0.3f, 0.33f, 0.38f), true);

            // Outside walls
            b.WallX(0f, 0f, 30f, outsideWall, true, new Gap(7f, DoorKind.Closed, 1.8f));
            b.WallX(24f, 0f, 30f, outsideWall, true, new Gap(5f, DoorKind.Breachable));
            b.WallZ(0f, 0f, 24f, outsideWall, true, new Gap(11.5f, DoorKind.Breachable));
            b.WallZ(30f, 0f, 24f, outsideWall, true);

            // Inside walls
            b.WallZ(14f, 0f, 10f, insideWall, false, new Gap(5f, DoorKind.Closed));
            b.WallZ(22f, 0f, 10f, insideWall, false, new Gap(5f, DoorKind.Closed));
            b.WallX(10f, 0f, 14f, insideWall, false, new Gap(7f, DoorKind.Opening, 2.4f));
            b.WallX(10f, 14f, 22f, insideWall, false, new Gap(18f, DoorKind.Closed));
            b.WallX(10f, 22f, 30f, insideWall, false, new Gap(26f, DoorKind.Breachable));
            b.WallX(13f, 0f, 10f, insideWall, false, new Gap(5f, DoorKind.Breachable));
            b.WallX(13f, 10f, 20f, insideWall, false, new Gap(15f, DoorKind.Closed));
            b.WallX(13f, 20f, 30f, insideWall, false, new Gap(25f, DoorKind.Breachable));
            b.WallZ(10f, 13f, 24f, insideWall, false, new Gap(18.5f, DoorKind.Closed));
            b.WallZ(20f, 13f, 24f, insideWall, false);

            // Lobby
            b.Desk(new Vector3(3.2f, 0f, 6.8f), 0f, 3.2f, true);
            b.Couch(new Vector3(11f, 0f, 2f), 0f);
            b.Couch(new Vector3(10.5f, 0f, 7.8f), 180f);
            b.Prop("Coffee Table", new Vector3(11f, 0f, 3.3f), new Vector3(1.2f, 0.45f, 0.6f), new Color(0.4f, 0.28f, 0.18f), false);
            b.Plant(new Vector3(0.8f, 0f, 0.8f));
            b.Plant(new Vector3(13.2f, 0f, 0.8f));
            b.Plant(new Vector3(0.8f, 0f, 9.2f));

            // Office A
            b.Desk(new Vector3(16.5f, 0f, 3f), 0f, 1.6f, true);
            b.Desk(new Vector3(19.5f, 0f, 3f), 0f, 1.6f, true);
            b.Desk(new Vector3(19.5f, 0f, 7.5f), 180f, 1.6f, true);
            b.Shelf(new Vector3(21.6f, 0f, 1.5f), 90f);
            b.Shelf(new Vector3(21.6f, 0f, 8.5f), 90f);

            // Office B
            b.Desk(new Vector3(25f, 0f, 3f), 0f, 1.6f, true);
            b.Desk(new Vector3(28f, 0f, 3f), 0f, 1.6f, true);
            b.Desk(new Vector3(28f, 0f, 7f), 180f, 1.6f, true);
            b.Shelf(new Vector3(29.6f, 0f, 5f), 90f);

            // Hallway
            b.Prop("Water Cooler", new Vector3(19.6f, 0f, 12.6f), new Vector3(0.4f, 1.2f, 0.4f), new Color(0.75f, 0.85f, 0.95f), false);
            b.Plant(new Vector3(29.4f, 0f, 12.4f));

            // Storage room
            b.Shelf(new Vector3(2f, 0f, 17f), 90f, 3f);
            b.Shelf(new Vector3(2f, 0f, 21f), 90f, 3f);
            b.Crate(new Vector3(6.5f, 0f, 16f), 1.2f, false);
            b.Crate(new Vector3(7.5f, 0f, 21.5f), 1.2f, true);
            b.Crate(new Vector3(4.6f, 0f, 20f), 1f, false);

            // Break room
            b.Prop("Table", new Vector3(15f, 0f, 19f), new Vector3(2.4f, 0.75f, 1.2f), new Color(0.75f, 0.72f, 0.66f), true);
            b.Couch(new Vector3(17.5f, 0f, 23.2f), 180f);
            b.Prop("Counter", new Vector3(12.5f, 0f, 23.5f), new Vector3(4f, 0.95f, 0.7f), new Color(0.55f, 0.55f, 0.58f), true);
            b.Prop("Vending Machine", new Vector3(19.5f, 0f, 15.5f), new Vector3(0.8f, 2f, 1f), new Color(0.7f, 0.15f, 0.15f), true);

            // Security room
            b.Desk(new Vector3(25f, 0f, 22.8f), 180f, 4f, true);
            b.Prop("Lockers", new Vector3(29.6f, 0f, 18f), new Vector3(0.6f, 2f, 3f), new Color(0.4f, 0.45f, 0.5f), true);
            b.Prop("Server Rack", new Vector3(21f, 0f, 22.6f), new Vector3(0.8f, 2.2f, 0.8f), new Color(0.12f, 0.12f, 0.14f), true);
            b.Crate(new Vector3(23f, 0f, 17f), 1f, false);

            // Suspects
            b.Enemy("Suspect", new Vector3(4f, 0f, 3f), 0f, new Vector3(4f, 0f, 3f), new Vector3(10f, 0f, 5.5f), new Vector3(5f, 0f, 8.5f));
            b.Enemy("Suspect", new Vector3(2.5f, 0f, 11.5f), 90f, new Vector3(2.5f, 0f, 11.5f), new Vector3(28f, 0f, 11.5f));
            b.Enemy("Suspect", new Vector3(20.5f, 0f, 5.5f), 270f);
            b.Enemy("Suspect", new Vector3(24f, 0f, 7.5f), 180f, new Vector3(24f, 0f, 7.5f), new Vector3(28.5f, 0f, 8.8f));
            b.Enemy("Suspect", new Vector3(4f, 0f, 15f), 0f, new Vector3(4f, 0f, 15f), new Vector3(6.5f, 0f, 22.8f));
            b.Enemy("Suspect", new Vector3(16f, 0f, 21.5f), 180f);
            b.Enemy("Heavy", new Vector3(25f, 0f, 20.5f), 180f);
            b.Enemy("Suspect", new Vector3(22f, 0f, 15.5f), 90f, new Vector3(22f, 0f, 15.5f), new Vector3(28.5f, 0f, 15f));

            // Civilians
            b.Civilian(new Vector3(12.8f, 0f, 3.4f), 200f);
            b.Civilian(new Vector3(15.2f, 0f, 1f), 30f);
            b.Civilian(new Vector3(29f, 0f, 1f), 300f);
            b.Civilian(new Vector3(11.2f, 0f, 21.5f), 120f);
            b.Civilian(new Vector3(28.6f, 0f, 22.4f), 220f);

            return L;
        }

        // ---- Building blocks ----

        void Slab(string name, float x0, float z0, float x1, float z1, float bottom, float top, Color color)
        {
            Shapes.Box(name, geometry, new Vector3((x0 + x1) * 0.5f, (bottom + top) * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, top - bottom, z1 - z0), color);
        }

        void Decal(string name, Vector3 position, Vector3 size, Color color)
        {
            Shapes.Box(name, geometry, position, size, color, false);
        }

        void Room(string name, float x0, float z0, float x1, float z1, Color floor, bool mustSecure = false)
        {
            Slab(name + " Floor", x0, z0, x1, z1, -0.05f, 0.03f, floor);
            layout.rooms.Add(new RoomArea
            {
                name = name,
                bounds = new Bounds(new Vector3((x0 + x1) * 0.5f, 1f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 4f, z1 - z0)),
                mustSecure = mustSecure,
            });
        }

        // A wall along the X axis at height z, from x0 to x1, with optional doorways.
        void WallX(float z, float x0, float x1, Color color, bool exterior, params Gap[] gaps)
        {
            Wall(new Vector3(x0, 0f, z), new Vector3(x1, 0f, z), true, color, exterior, gaps);
        }

        // A wall along the Z axis at x, from z0 to z1.
        void WallZ(float x, float z0, float z1, Color color, bool exterior, params Gap[] gaps)
        {
            Wall(new Vector3(x, 0f, z0), new Vector3(x, 0f, z1), false, color, exterior, gaps);
        }

        void Wall(Vector3 a, Vector3 b, bool alongX, Color color, bool exterior, Gap[] gaps)
        {
            Vector3 direction = (b - a).normalized;
            // Outside walls overlap at the corners; inside walls end inside the wall they meet.
            float stretch = exterior ? WallThickness * 0.5f : 0f;
            Vector3 cursor = a - direction * stretch;
            Vector3 end = b + direction * stretch;
            float startCoord = alongX ? a.x : a.z;

            System.Array.Sort(gaps, (g1, g2) => g1.center.CompareTo(g2.center));
            foreach (var gap in gaps)
            {
                Vector3 gapCenter = a + direction * (gap.center - startCoord);
                Vector3 gapStart = gapCenter - direction * gap.width * 0.5f;
                WallPiece(cursor, gapStart, color, exterior);
                cursor = gapCenter + direction * gap.width * 0.5f;

                if (gap.kind == DoorKind.Opening) continue;
                var state = gap.kind == DoorKind.Closed ? DoorState.Closed : DoorState.Locked;
                var door = DoorController.Create(geometry, gapCenter, alongX, gap.width, state, gap.kind == DoorKind.Breachable);
                layout.doors.Add(door);
            }
            WallPiece(cursor, end, color, exterior);
        }

        void WallPiece(Vector3 from, Vector3 to, Color color, bool exterior)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < 0.0001f) return;
            // Inside walls are a touch lower so their tops never overlap the outside walls' tops.
            float height = exterior ? WallVisualHeight : WallVisualHeight - 0.04f;
            var wall = Shapes.Box("Wall", geometry, Vector3.zero, new Vector3(WallThickness, height, delta.magnitude), color);
            wall.transform.localPosition = (from + to) * 0.5f + Vector3.up * height * 0.5f;
            wall.transform.localRotation = Quaternion.LookRotation(delta);
            var box = wall.GetComponent<BoxCollider>();
            box.size = new Vector3(1f, WallSolidHeight / height, 1f);
            box.center = new Vector3(0f, box.size.y * 0.5f - 0.5f, 0f);
            // Cap the wall with a darker strip so its outline reads clearly from above.
            Shapes.Box("Wall Top", wall.transform, new Vector3(0f, 0.5f, 0f), new Vector3(1.05f, 0.02f, 1f), Shapes.Shade(color, 0.55f), false);
        }

        // A solid box standing on the floor. Cover props add spots the AI can hide behind.
        GameObject Prop(string name, Vector3 position, Vector3 size, Color color, bool cover, float yaw = 0f)
        {
            var go = Shapes.Box(name, props, position + Vector3.up * size.y * 0.5f, size, color);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (cover)
            {
                bool turned = Mathf.Abs(Mathf.DeltaAngle(yaw, 90f)) < 45f || Mathf.Abs(Mathf.DeltaAngle(yaw, 270f)) < 45f;
                float halfX = (turned ? size.z : size.x) * 0.5f + 0.6f;
                float halfZ = (turned ? size.x : size.z) * 0.5f + 0.6f;
                layout.coverPoints.Add(position + new Vector3(halfX, 0f, 0f));
                layout.coverPoints.Add(position + new Vector3(-halfX, 0f, 0f));
                layout.coverPoints.Add(position + new Vector3(0f, 0f, halfZ));
                layout.coverPoints.Add(position + new Vector3(0f, 0f, -halfZ));
            }
            return go;
        }

        void Desk(Vector3 position, float yaw, float width, bool withMonitor)
        {
            var desk = Prop("Desk", position, new Vector3(width, 0.8f, 0.85f), new Color(0.5f, 0.38f, 0.26f), true, yaw);
            if (!withMonitor) return;
            var monitor = Shapes.Box("Monitor", desk.transform, new Vector3(0f, 0.85f, 0.15f), new Vector3(0.35f / width, 0.5f, 0.06f), new Color(0.08f, 0.08f, 0.1f), false);
            Shapes.Box("Screen", monitor.transform, new Vector3(0f, 0f, -0.6f), new Vector3(0.9f, 0.8f, 0.2f), new Color(0.35f, 0.6f, 0.9f), false, 1.2f);
        }

        void Couch(Vector3 position, float yaw)
        {
            var couch = Prop("Couch", position, new Vector3(2.2f, 0.5f, 0.9f), new Color(0.3f, 0.36f, 0.45f), true, yaw);
            Shapes.Box("Back", couch.transform, new Vector3(0f, 0.6f, 0.4f), new Vector3(1f, 1.2f, 0.25f), new Color(0.27f, 0.32f, 0.4f), false);
        }

        void Shelf(Vector3 position, float yaw, float length = 2f)
        {
            Prop("Shelf", position, new Vector3(length, 2f, 0.5f), new Color(0.35f, 0.37f, 0.4f), true, yaw);
        }

        void Crate(Vector3 position, float size, bool stacked)
        {
            var wood = new Color(0.55f, 0.4f, 0.22f);
            var crate = Prop("Crate", position, Vector3.one * size, wood, true);
            if (stacked)
                Shapes.Box("Crate", crate.transform, new Vector3(0.05f, 0.9f, 0f), Vector3.one * 0.8f, Shapes.Shade(wood, 0.85f), false)
                    .transform.localRotation = Quaternion.Euler(0f, 15f, 0f);
        }

        void Plant(Vector3 position)
        {
            Shapes.Make(PrimitiveType.Cylinder, "Pot", props, position + Vector3.up * 0.25f, new Vector3(0.45f, 0.25f, 0.45f), new Color(0.5f, 0.3f, 0.2f));
            Shapes.Make(PrimitiveType.Sphere, "Leaves", props, position + Vector3.up * 0.85f, new Vector3(0.8f, 0.8f, 0.8f), new Color(0.2f, 0.5f, 0.22f), false);
        }

        void Car(Vector3 position, float yaw, Color color, bool police = false)
        {
            var car = Prop(police ? "Police Car" : "Car", position, new Vector3(1.9f, 0.75f, 4.3f), color, true, yaw);
            Shapes.Box("Cabin", car.transform, new Vector3(0f, 0.9f, -0.05f), new Vector3(0.9f, 0.8f, 0.5f), new Color(0.15f, 0.18f, 0.22f), false);
            if (!police) return;
            Shapes.Box("Siren Red", car.transform, new Vector3(-0.2f, 1.35f, -0.05f), new Vector3(0.3f, 0.12f, 0.06f), new Color(1f, 0.1f, 0.1f), false, 3f);
            Shapes.Box("Siren Blue", car.transform, new Vector3(0.2f, 1.35f, -0.05f), new Vector3(0.3f, 0.12f, 0.06f), new Color(0.15f, 0.35f, 1f), false, 3f);
        }

        void Van(Vector3 position)
        {
            var van = Prop("SWAT Van", position, new Vector3(2.4f, 2.4f, 5.6f), new Color(0.08f, 0.09f, 0.13f), true);
            Shapes.Box("Stripe", van.transform, new Vector3(0f, 0.05f, 0f), new Vector3(1.01f, 0.1f, 1.01f), new Color(0.8f, 0.8f, 0.82f), false);
            Shapes.Box("Roof Mark", van.transform, new Vector3(0f, 0.505f, 0f), new Vector3(0.5f, 0.01f, 0.3f), new Color(0.95f, 0.95f, 0.95f), false);
            // Highlight the extraction spot next to the van.
            Decal("Extraction Zone", new Vector3(9f, 0.02f, -8.5f), new Vector3(3.5f, 0.01f, 5f), new Color(0.2f, 0.5f, 0.9f));
        }

        // Invisible walls around the playable area.
        void Boundary(float x0, float z0, float x1, float z1)
        {
            float midX = (x0 + x1) * 0.5f, midZ = (z0 + z1) * 0.5f;
            InvisibleWall(new Vector3(x0, 1.5f, midZ), new Vector3(1f, 3f, z1 - z0));
            InvisibleWall(new Vector3(x1, 1.5f, midZ), new Vector3(1f, 3f, z1 - z0));
            InvisibleWall(new Vector3(midX, 1.5f, z0), new Vector3(x1 - x0, 3f, 1f));
            InvisibleWall(new Vector3(midX, 1.5f, z1), new Vector3(x1 - x0, 3f, 1f));
        }

        void InvisibleWall(Vector3 position, Vector3 size)
        {
            Shapes.Box("Boundary", geometry, position, size, Color.black).GetComponent<Renderer>().enabled = false;
        }

        void Enemy(string profile, Vector3 position, float yaw, params Vector3[] patrol)
        {
            layout.enemies.Add(new EnemySpawn { profile = profile, position = position, yaw = yaw, patrol = patrol });
        }

        void Civilian(Vector3 position, float yaw)
        {
            layout.civilians.Add(new CivilianSpawn { position = position, yaw = yaw });
        }
    }
}
