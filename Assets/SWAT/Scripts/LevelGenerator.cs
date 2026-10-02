using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    public class Level
    {
        public Transform root;
        public string location;
        public Vector3 playerSpawn;
        public float playerYaw;
        public readonly List<Suspect> suspects = new List<Suspect>();
        public readonly List<Hostage> hostages = new List<Hostage>();
    }

    // Builds a new random building for every mission: a grid of rooms joined by
    // doorways, furniture for cover, suspects and hostages inside, and a street
    // with police vehicles outside the front door.
    public static class LevelGenerator
    {
        const float RoomSize = 8f;
        const float WallHeight = 3f;
        const float WallThickness = 0.2f;
        const float DoorWidth = 1.6f;
        const float DoorHeight = 2.3f;

        enum Furniture { Crate, CrateStack, Barrel, Shelf, Table, Desk, Couch }

        class Theme
        {
            public string name;
            public Color wall, outside, ceiling;
            public Color[] floors;
            public Furniture[] furniture;
        }

        struct Spot
        {
            public Vector3 position;
            public float radius;
        }

        static readonly Theme[] Themes =
        {
            new Theme
            {
                name = "Warehouse", wall = C(0.55f, 0.55f, 0.52f), outside = C(0.36f, 0.35f, 0.33f), ceiling = C(0.35f, 0.35f, 0.37f),
                floors = new[] { C(0.32f, 0.32f, 0.32f), C(0.38f, 0.37f, 0.34f) },
                furniture = new[] { Furniture.Crate, Furniture.CrateStack, Furniture.Barrel, Furniture.Shelf },
            },
            new Theme
            {
                name = "Apartment Building", wall = C(0.78f, 0.72f, 0.62f), outside = C(0.48f, 0.28f, 0.22f), ceiling = C(0.85f, 0.84f, 0.8f),
                floors = new[] { C(0.45f, 0.3f, 0.18f), C(0.33f, 0.36f, 0.44f), C(0.68f, 0.68f, 0.66f) },
                furniture = new[] { Furniture.Couch, Furniture.Table, Furniture.Shelf, Furniture.Crate },
            },
            new Theme
            {
                name = "Office Building", wall = C(0.74f, 0.76f, 0.8f), outside = C(0.3f, 0.33f, 0.38f), ceiling = C(0.85f, 0.86f, 0.88f),
                floors = new[] { C(0.24f, 0.29f, 0.4f), C(0.5f, 0.5f, 0.5f) },
                furniture = new[] { Furniture.Desk, Furniture.Desk, Furniture.Shelf, Furniture.Table },
            },
        };

        static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        public static Level Build(Transform parent, int mission)
        {
            var level = new Level();
            level.root = new GameObject("Level " + mission).transform;
            level.root.SetParent(parent, false);

            var theme = Themes[Random.Range(0, Themes.Length)];
            level.location = theme.name;

            int cols = Mathf.Min(3 + mission / 2, 5);
            int rows = Mathf.Min(2 + (mission + 1) / 2, 4);
            int entrance = Random.Range(0, cols);

            var doorEast = new bool[cols, rows];  // doorway between (c, r) and (c + 1, r)
            var doorNorth = new bool[cols, rows]; // doorway between (c, r) and (c, r + 1)
            CarveDoors(cols, rows, entrance, doorEast, doorNorth);

            BuildOutside(level.root, cols, rows, entrance);
            BuildWalls(level.root, theme, cols, rows, entrance, doorEast, doorNorth);
            BuildFrontDoor(level.root, entrance);
            Populate(level, theme, cols, rows, entrance, doorEast, doorNorth, mission);

            level.playerSpawn = new Vector3((entrance + 0.5f) * RoomSize, 0.05f, -6f);
            level.playerYaw = 0f;
            return level;
        }

        // A random maze (depth-first search) makes sure every room can be
        // reached, then a few extra doorways add loops so there's more than one way around.
        static void CarveDoors(int cols, int rows, int startCol, bool[,] east, bool[,] north)
        {
            var visited = new bool[cols, rows];
            var stack = new List<Vector2Int> { new Vector2Int(startCol, 0) };
            visited[startCol, 0] = true;
            var options = new List<Vector2Int>(4);

            while (stack.Count > 0)
            {
                var cell = stack[stack.Count - 1];
                options.Clear();
                foreach (var direction in Directions)
                {
                    var next = cell + direction;
                    if (next.x >= 0 && next.y >= 0 && next.x < cols && next.y < rows && !visited[next.x, next.y]) options.Add(next);
                }
                if (options.Count == 0)
                {
                    stack.RemoveAt(stack.Count - 1);
                    continue;
                }
                var chosen = options[Random.Range(0, options.Count)];
                if (chosen.y == cell.y) east[Mathf.Min(chosen.x, cell.x), cell.y] = true;
                else north[cell.x, Mathf.Min(chosen.y, cell.y)] = true;
                visited[chosen.x, chosen.y] = true;
                stack.Add(chosen);
            }

            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                {
                    if (c + 1 < cols && Random.value < 0.25f) east[c, r] = true;
                    if (r + 1 < rows && Random.value < 0.25f) north[c, r] = true;
                }
        }

        static void BuildWalls(Transform root, Theme theme, int cols, int rows, int entrance, bool[,] east, bool[,] north)
        {
            var building = new GameObject("Building").transform;
            building.SetParent(root, false);

            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                    BuildRoom(building, theme, RoomCenter(c, r));

            // Walls running north-south.
            for (int c = 0; c <= cols; c++)
                for (int r = 0; r < rows; r++)
                {
                    bool exterior = c == 0 || c == cols;
                    bool door = !exterior && east[c - 1, r];
                    var a = new Vector3(c * RoomSize, 0f, r * RoomSize);
                    BuildWall(building, a, a + Vector3.forward * RoomSize, door, exterior, exterior ? theme.outside : theme.wall);
                }

            // Walls running east-west. The front entrance is in the south wall.
            for (int r = 0; r <= rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    bool exterior = r == 0 || r == rows;
                    bool door = exterior ? (r == 0 && c == entrance) : north[c, r - 1];
                    var a = new Vector3(c * RoomSize, 0f, r * RoomSize);
                    BuildWall(building, a, a + Vector3.right * RoomSize, door, exterior, exterior ? theme.outside : theme.wall);
                }
        }

        static void BuildRoom(Transform parent, Theme theme, Vector3 center)
        {
            var room = new GameObject("Room").transform;
            room.SetParent(parent, false);
            room.localPosition = center;

            Shapes.Box("Floor", room, Vector3.zero, new Vector3(RoomSize, 0.04f, RoomSize), theme.floors[Random.Range(0, theme.floors.Length)]);
            Shapes.Box("Ceiling", room, new Vector3(0f, WallHeight + 0.1f, 0f), new Vector3(RoomSize, 0.2f, RoomSize), theme.ceiling);
            var lamp = Shapes.Box("Ceiling Lamp", room, new Vector3(0f, WallHeight - 0.03f, 0f), new Vector3(1.2f, 0.06f, 0.3f), new Color(1f, 0.95f, 0.85f), false, 2f);

            var light = PointLight(room, new Vector3(0f, WallHeight - 0.4f, 0f), new Color(1f, 0.92f, 0.78f), Random.value < 0.2f ? 0.7f : 1.5f, 9f);
            if (Random.value < 0.15f)
            {
                var flicker = light.gameObject.AddComponent<BlinkingLight>();
                flicker.lights = new[] { light };
                flicker.glows = new[] { lamp.GetComponent<Renderer>() };
            }
        }

        static void BuildWall(Transform parent, Vector3 a, Vector3 b, bool door, bool exterior, Color color)
        {
            Vector3 direction = (b - a).normalized;
            // Outside walls are stretched by half their thickness so the building's
            // corners close up. Inside walls already end within the wall they meet.
            float stretch = exterior ? WallThickness * 0.5f : 0f;
            Vector3 start = a - direction * stretch;
            Vector3 end = b + direction * stretch;
            if (!door)
            {
                WallPiece(parent, start, end, 0f, WallHeight, color);
                return;
            }
            Vector3 middle = (a + b) * 0.5f;
            Vector3 doorStart = middle - direction * DoorWidth * 0.5f;
            Vector3 doorEnd = middle + direction * DoorWidth * 0.5f;
            WallPiece(parent, start, doorStart, 0f, WallHeight, color);
            WallPiece(parent, doorEnd, end, 0f, WallHeight, color);
            WallPiece(parent, doorStart, doorEnd, DoorHeight, WallHeight, color);
        }

        static void WallPiece(Transform parent, Vector3 from, Vector3 to, float bottom, float top, Color color)
        {
            Vector3 delta = to - from;
            var wall = Shapes.Box("Wall", parent, Vector3.zero, new Vector3(WallThickness, top - bottom, delta.magnitude), color);
            wall.transform.localPosition = (from + to) * 0.5f + Vector3.up * (bottom + top) * 0.5f;
            wall.transform.localRotation = Quaternion.LookRotation(delta);
        }

        static void BuildFrontDoor(Transform parent, int entrance)
        {
            float x = (entrance + 0.5f) * RoomSize;
            var hinge = new GameObject("Front Door").transform;
            hinge.SetParent(parent, false);
            hinge.localPosition = new Vector3(x - DoorWidth * 0.5f, 0f, 0f);
            Shapes.Box("Door", hinge, new Vector3(DoorWidth * 0.5f, DoorHeight * 0.5f, 0f), new Vector3(DoorWidth - 0.04f, DoorHeight - 0.02f, 0.07f), new Color(0.32f, 0.2f, 0.12f));
            Shapes.Box("Handle", hinge, new Vector3(DoorWidth - 0.18f, 1f, -0.06f), new Vector3(0.12f, 0.04f, 0.04f), new Color(0.8f, 0.75f, 0.5f), false);
            hinge.gameObject.AddComponent<BreachDoor>();
        }

        static void BuildOutside(Transform root, int cols, int rows, int entrance)
        {
            var outside = new GameObject("Street").transform;
            outside.SetParent(root, false);

            float width = cols * RoomSize, depth = rows * RoomSize;
            float midX = width * 0.5f;
            float doorX = (entrance + 0.5f) * RoomSize;

            Shapes.Box("Ground", outside, new Vector3(midX, -0.1f, depth * 0.5f), new Vector3(160f, 0.2f, 160f), C(0.16f, 0.17f, 0.18f));
            Shapes.Box("Sidewalk", outside, new Vector3(midX, 0f, -1.5f), new Vector3(width + 8f, 0.06f, 3f), C(0.42f, 0.42f, 0.4f));
            for (float x = midX - 40f; x <= midX + 40f; x += 4f)
                Shapes.Box("Road Line", outside, new Vector3(x, 0.005f, -11f), new Vector3(2f, 0.02f, 0.15f), C(0.85f, 0.7f, 0.2f), false);

            BuildVan(outside, new Vector3(doorX + 5.5f, 0f, -8.5f), 90f);
            BuildPoliceCar(outside, new Vector3(doorX - 6f, 0f, -9f), -75f);
            BuildStreetLamp(outside, new Vector3(doorX - 4.5f, 0f, -2.7f));
            BuildStreetLamp(outside, new Vector3(doorX + 4.5f, 0f, -2.7f));

            // Invisible walls so nobody wanders off into the dark.
            float left = midX - 40f, right = midX + 40f, back = -25f, front = depth + 20f;
            InvisibleWall(outside, new Vector3(left, 2f, (back + front) * 0.5f), new Vector3(1f, 4f, front - back));
            InvisibleWall(outside, new Vector3(right, 2f, (back + front) * 0.5f), new Vector3(1f, 4f, front - back));
            InvisibleWall(outside, new Vector3(midX, 2f, back), new Vector3(right - left, 4f, 1f));
            InvisibleWall(outside, new Vector3(midX, 2f, front), new Vector3(right - left, 4f, 1f));
        }

        static void InvisibleWall(Transform parent, Vector3 position, Vector3 size)
        {
            var wall = Shapes.Box("Boundary", parent, position, size, Color.black);
            wall.GetComponent<Renderer>().enabled = false;
        }

        static void BuildVan(Transform parent, Vector3 position, float yaw)
        {
            var van = Holder("SWAT Van", parent, position, yaw);
            var body = C(0.07f, 0.08f, 0.12f);
            Shapes.Box("Body", van, new Vector3(0f, 1.45f, -0.4f), new Vector3(2.3f, 2.3f, 4.6f), body);
            Shapes.Box("Cab", van, new Vector3(0f, 1.1f, 2.5f), new Vector3(2.3f, 1.6f, 1.4f), body);
            Shapes.Box("Windshield", van, new Vector3(0f, 1.45f, 3.21f), new Vector3(2f, 0.6f, 0.02f), C(0.15f, 0.2f, 0.25f), false);
            Shapes.Box("Stripe", van, new Vector3(0f, 1.3f, -0.4f), new Vector3(2.32f, 0.25f, 4.62f), C(0.75f, 0.75f, 0.78f), false);
            Wheels(van, 1.1f, -1.8f, 2.4f);
            Siren(van, new Vector3(0f, 2.68f, 0.6f), 1.6f);
        }

        static void BuildPoliceCar(Transform parent, Vector3 position, float yaw)
        {
            var car = Holder("Police Car", parent, position, yaw);
            Shapes.Box("Body", car, new Vector3(0f, 0.65f, 0f), new Vector3(1.9f, 0.7f, 4.4f), C(0.92f, 0.92f, 0.92f));
            Shapes.Box("Doors", car, new Vector3(0f, 0.6f, -0.1f), new Vector3(1.92f, 0.4f, 1.8f), C(0.08f, 0.08f, 0.1f), false);
            Shapes.Box("Cabin", car, new Vector3(0f, 1.28f, -0.2f), new Vector3(1.7f, 0.56f, 2.2f), C(0.12f, 0.14f, 0.18f));
            Wheels(car, 0.95f, -1.4f, 1.4f);
            Siren(car, new Vector3(0f, 1.64f, -0.1f), 1.2f);
        }

        static void Wheels(Transform vehicle, float x, float rearZ, float frontZ)
        {
            foreach (float side in new[] { -x, x })
                foreach (float z in new[] { rearZ, frontZ })
                {
                    var wheel = Shapes.Make(PrimitiveType.Cylinder, "Wheel", vehicle, new Vector3(side, 0.38f, z), new Vector3(0.76f, 0.13f, 0.76f), C(0.05f, 0.05f, 0.05f), false);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
        }

        static void Siren(Transform vehicle, Vector3 position, float width)
        {
            var size = new Vector3(width * 0.45f, 0.14f, 0.3f);
            var red = Shapes.Box("Siren Red", vehicle, position + Vector3.left * width * 0.25f, size, new Color(1f, 0.1f, 0.1f), false, 4f);
            var blue = Shapes.Box("Siren Blue", vehicle, position + Vector3.right * width * 0.25f, size, new Color(0.15f, 0.35f, 1f), false, 4f);
            var blink = vehicle.gameObject.AddComponent<BlinkingLight>();
            blink.siren = true;
            blink.lights = new[]
            {
                PointLight(vehicle, red.transform.localPosition + Vector3.up * 0.3f, new Color(1f, 0.15f, 0.1f), 3f, 12f),
                PointLight(vehicle, blue.transform.localPosition + Vector3.up * 0.3f, new Color(0.2f, 0.4f, 1f), 3f, 12f),
            };
            blink.glows = new[] { red.GetComponent<Renderer>(), blue.GetComponent<Renderer>() };
        }

        static void BuildStreetLamp(Transform parent, Vector3 position)
        {
            var metal = C(0.2f, 0.2f, 0.22f);
            Shapes.Box("Lamp Post", parent, position + new Vector3(0f, 2.25f, 0f), new Vector3(0.14f, 4.5f, 0.14f), metal);
            Shapes.Box("Lamp Arm", parent, position + new Vector3(0f, 4.45f, 0.5f), new Vector3(0.1f, 0.1f, 1f), metal, false);
            Shapes.Box("Lamp Head", parent, position + new Vector3(0f, 4.38f, 0.95f), new Vector3(0.35f, 0.08f, 0.5f), new Color(1f, 0.85f, 0.6f), false, 3f);
            PointLight(parent, position + new Vector3(0f, 4.1f, 0.95f), new Color(1f, 0.8f, 0.55f), 2f, 15f);
        }

        static void Populate(Level level, Theme theme, int cols, int rows, int entrance, bool[,] east, bool[,] north, int mission)
        {
            int suspectCount = Mathf.Min(3 + mission, (cols * rows - 1) * 2);
            int hostageCount = Mathf.Min(1 + mission / 2, 4);
            float accuracy = Mathf.Min(0.4f + mission * 0.03f, 0.65f);
            float reaction = Mathf.Max(0.9f - mission * 0.06f, 0.45f);

            // Nobody waits in the entrance room.
            var rooms = new List<Vector2Int>();
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                    if (c != entrance || r != 0) rooms.Add(new Vector2Int(c, r));
            Shuffle(rooms);

            var taken = new List<Spot>[cols, rows];
            var doors = new List<Vector3>[cols, rows];
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                {
                    taken[c, r] = new List<Spot>();
                    doors[c, r] = DoorsOf(c, r, cols, rows, entrance, east, north);
                }

            for (int i = 0; i < suspectCount; i++)
            {
                var room = rooms[i % rooms.Count];
                Vector3 spot;
                if (!FindSpot(RoomCenter(room.x, room.y), doors[room.x, room.y], taken[room.x, room.y], 0.45f, out spot)) continue;
                taken[room.x, room.y].Add(new Spot { position = spot, radius = 0.45f });

                // Most suspects watch a doorway; some face a random way.
                var roomDoors = doors[room.x, room.y];
                float yaw = Random.Range(0f, 360f);
                if (roomDoors.Count > 0 && Random.value < 0.65f)
                {
                    Vector3 to = roomDoors[Random.Range(0, roomDoors.Count)] - spot;
                    yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg + Random.Range(-25f, 25f);
                }
                level.suspects.Add(Suspect.Spawn(level.root, spot, yaw, accuracy, reaction));
            }

            // Hostages are kept in the same rooms as the suspects guarding them.
            for (int i = 0; i < hostageCount; i++)
            {
                var room = rooms[i % rooms.Count];
                Vector3 spot;
                if (!FindSpot(RoomCenter(room.x, room.y), doors[room.x, room.y], taken[room.x, room.y], 0.45f, out spot)) continue;
                taken[room.x, room.y].Add(new Spot { position = spot, radius = 0.45f });
                level.hostages.Add(Hostage.Spawn(level.root, spot, Random.Range(0f, 360f)));
            }

            var furnitureRoot = new GameObject("Furniture").transform;
            furnitureRoot.SetParent(level.root, false);
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                {
                    int count = (c == entrance && r == 0) ? Random.Range(1, 3) : Random.Range(2, 5);
                    for (int i = 0; i < count; i++)
                    {
                        var kind = theme.furniture[Random.Range(0, theme.furniture.Length)];
                        float radius = FurnitureRadius(kind);
                        Vector3 spot;
                        if (!FindSpot(RoomCenter(c, r), doors[c, r], taken[c, r], radius, out spot)) continue;
                        taken[c, r].Add(new Spot { position = spot, radius = radius });
                        PlaceFurniture(furnitureRoot, kind, spot, Random.Range(0, 4) * 90f + Random.Range(-8f, 8f));
                    }
                }
        }

        static List<Vector3> DoorsOf(int c, int r, int cols, int rows, int entrance, bool[,] east, bool[,] north)
        {
            var list = new List<Vector3>();
            Vector3 center = RoomCenter(c, r);
            float half = RoomSize * 0.5f;
            if (c > 0 && east[c - 1, r]) list.Add(center + Vector3.left * half);
            if (c < cols - 1 && east[c, r]) list.Add(center + Vector3.right * half);
            if (r > 0 && north[c, r - 1]) list.Add(center + Vector3.back * half);
            if (r < rows - 1 && north[c, r]) list.Add(center + Vector3.forward * half);
            if (r == 0 && c == entrance) list.Add(center + Vector3.back * half);
            return list;
        }

        // Finds a free place in a room that keeps doorways clear and doesn't overlap anything else.
        static bool FindSpot(Vector3 center, List<Vector3> doors, List<Spot> taken, float radius, out Vector3 spot)
        {
            float extent = RoomSize * 0.5f - WallThickness - radius - 0.2f;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var p = center + new Vector3(Random.Range(-extent, extent), 0f, Random.Range(-extent, extent));
                bool free = true;
                foreach (var door in doors)
                    if (Vector3.Distance(p, door) < 1.9f + radius) { free = false; break; }
                if (free)
                    foreach (var other in taken)
                        if (Vector3.Distance(p, other.position) < other.radius + radius + 0.6f) { free = false; break; }
                if (free)
                {
                    spot = p;
                    return true;
                }
            }
            spot = center;
            return false;
        }

        static float FurnitureRadius(Furniture kind)
        {
            switch (kind)
            {
                case Furniture.Barrel: return 0.45f;
                case Furniture.Crate:
                case Furniture.CrateStack: return 0.75f;
                case Furniture.Desk: return 0.9f;
                case Furniture.Table: return 1f;
                default: return 1.1f;
            }
        }

        static void PlaceFurniture(Transform parent, Furniture kind, Vector3 position, float yaw)
        {
            var item = Holder(kind.ToString(), parent, position, yaw);
            var wood = C(0.5f, 0.34f, 0.2f);
            var metal = C(0.3f, 0.32f, 0.35f);
            switch (kind)
            {
                case Furniture.Crate:
                    Shapes.Box("Crate", item, new Vector3(0f, 0.5f, 0f), Vector3.one, wood);
                    break;
                case Furniture.CrateStack:
                    Shapes.Box("Crate", item, new Vector3(0f, 0.5f, 0f), Vector3.one, wood);
                    Shapes.Box("Crate", item, new Vector3(0.1f, 1.4f, -0.05f), Vector3.one * 0.8f, Shapes.Shade(wood, 0.85f))
                        .transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
                    break;
                case Furniture.Barrel:
                    var colors = new[] { C(0.15f, 0.3f, 0.6f), C(0.6f, 0.15f, 0.1f), C(0.2f, 0.4f, 0.2f) };
                    Shapes.Make(PrimitiveType.Cylinder, "Barrel", item, new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.45f, 0.6f), colors[Random.Range(0, colors.Length)]);
                    break;
                case Furniture.Shelf:
                    Shapes.Box("Shelf", item, new Vector3(0f, 1f, 0f), new Vector3(2f, 2f, 0.5f), metal);
                    for (int i = 0; i < 3; i++)
                        Shapes.Box("Box", item, new Vector3(Random.Range(-0.7f, 0.7f), 0.5f + i * 0.6f, 0.2f), new Vector3(0.35f, 0.3f, 0.2f), C(0.6f, 0.5f, 0.35f), false);
                    break;
                case Furniture.Table:
                    Shapes.Box("Top", item, new Vector3(0f, 0.76f, 0f), new Vector3(1.8f, 0.08f, 0.9f), wood);
                    foreach (float x in new[] { -0.8f, 0.8f })
                        foreach (float z in new[] { -0.38f, 0.38f })
                            Shapes.Box("Leg", item, new Vector3(x, 0.36f, z), new Vector3(0.08f, 0.72f, 0.08f), Shapes.Shade(wood, 0.7f));
                    break;
                case Furniture.Desk:
                    Shapes.Box("Top", item, new Vector3(0f, 0.74f, 0f), new Vector3(1.4f, 0.06f, 0.7f), C(0.55f, 0.55f, 0.57f));
                    Shapes.Box("Side", item, new Vector3(-0.67f, 0.36f, 0f), new Vector3(0.06f, 0.72f, 0.7f), metal);
                    Shapes.Box("Side", item, new Vector3(0.67f, 0.36f, 0f), new Vector3(0.06f, 0.72f, 0.7f), metal);
                    Shapes.Box("Monitor", item, new Vector3(0f, 0.97f, 0.15f), new Vector3(0.5f, 0.32f, 0.04f), C(0.05f, 0.05f, 0.06f), false);
                    Shapes.Box("Screen", item, new Vector3(0f, 0.97f, 0.128f), new Vector3(0.45f, 0.27f, 0.01f), C(0.3f, 0.5f, 0.8f), false, 1.5f);
                    break;
                case Furniture.Couch:
                    var fabric = new[] { C(0.35f, 0.2f, 0.15f), C(0.2f, 0.3f, 0.35f), C(0.4f, 0.38f, 0.3f) }[Random.Range(0, 3)];
                    Shapes.Box("Seat", item, new Vector3(0f, 0.225f, 0f), new Vector3(2f, 0.45f, 0.85f), fabric);
                    Shapes.Box("Back", item, new Vector3(0f, 0.7f, -0.33f), new Vector3(2f, 0.5f, 0.2f), fabric);
                    Shapes.Box("Arm", item, new Vector3(-0.9f, 0.6f, 0f), new Vector3(0.2f, 0.3f, 0.85f), Shapes.Shade(fabric, 0.85f));
                    Shapes.Box("Arm", item, new Vector3(0.9f, 0.6f, 0f), new Vector3(0.2f, 0.3f, 0.85f), Shapes.Shade(fabric, 0.85f));
                    break;
            }
        }

        static Transform Holder(string name, Transform parent, Vector3 position, float yaw)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.localPosition = position;
            holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return holder;
        }

        static Light PointLight(Transform parent, Vector3 localPosition, Color color, float intensity, float range)
        {
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = localPosition;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity * Shapes.PointLightScale;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        static Vector3 RoomCenter(int c, int r)
        {
            return new Vector3((c + 0.5f) * RoomSize, 0f, (r + 0.5f) * RoomSize);
        }

        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                T swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
        }

        static Color C(float r, float g, float b)
        {
            return new Color(r, g, b);
        }
    }
}
