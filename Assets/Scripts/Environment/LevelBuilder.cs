using UnityEngine;

namespace Swat
{
    public enum DoorKind { Opening, Door, Locked, Electronic, Sealed }

    // A gap in a wall: an open doorway or a door of some kind.
    public struct Gap
    {
        public float center, width;
        public DoorKind kind;
        public string id;
        public bool breachable, pickable, lockable;

        public static Gap Open(float center, float width = 2.4f) { return new Gap { center = center, width = width, kind = DoorKind.Opening }; }
        // An unlocked door. 'lockable' doors may be randomly locked by a mission (always pickable or breachable then).
        public static Gap Door(float center, string id, bool lockable = false, float width = 1.6f) { return new Gap { center = center, width = width, kind = DoorKind.Door, id = id, lockable = lockable, breachable = lockable }; }
        public static Gap Locked(float center, string id, bool breachable = true, bool pickable = false, float width = 1.6f) { return new Gap { center = center, width = width, kind = DoorKind.Locked, id = id, breachable = breachable, pickable = pickable }; }
        public static Gap Electronic(float center, string id) { return new Gap { center = center, width = 1.6f, kind = DoorKind.Electronic, id = id, breachable = true }; }
        public static Gap Sealed(float center, string id) { return new Gap { center = center, width = 1.6f, kind = DoorKind.Sealed, id = id }; }
    }

    // Toolkit for building maps out of boxes: floors, rooms, walls with
    // doors, props that double as cover, spawn point pools and security
    // mounts. Each map is one static Build method in its own file.
    public class LevelBuilder
    {
        // Walls are drawn low for the top-down view (you see into rooms from above) and full height in
        // first person; their colliders are full height either way. A level is built for the top-down
        // view and the first-person parts are registered in Layout.view, so the view can switch any time.
        const float WallLowHeight = 1.4f;
        const float WallHighHeight = ViewMode.FirstPersonWallHeight;
        const float WallSolidHeight = 2.6f;
        const float WallThickness = 0.2f;

        public LevelLayout Layout { get; private set; }
        public Transform Geometry { get; private set; }
        public Transform Props { get; private set; }
        public int CurrentArea { get; set; }

        public LevelBuilder(Transform parent, string mapId, string displayName)
        {
            Layout = new LevelLayout { mapId = mapId, displayName = displayName };
            Layout.root = new GameObject("Map " + displayName).transform;
            Layout.root.SetParent(parent, false);
            Geometry = Group("Geometry");
            Props = Group("Props");
            ExteriorWall = mapId == "warehouse" || mapId == "training" ? SurfaceKind.Concrete : SurfaceKind.Brick;
        }

        Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(Layout.root, false);
            return t;
        }

        public int Area(string name, float x0, float z0, float x1, float z1)
        {
            Layout.areas.Add(new MapArea { name = name, bounds = new Bounds(new Vector3((x0 + x1) * 0.5f, 1f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 6f, z1 - z0)) });
            CurrentArea = Layout.areas.Count - 1;
            return CurrentArea;
        }

        // Finishes the map: works out which rooms each door connects.
        public LevelLayout Finish()
        {
            foreach (var door in Layout.doors)
            {
                Vector3 p = door.transform.position;
                door.RoomFront = Layout.RoomAt(p + door.transform.forward * 0.8f);
                door.RoomBack = Layout.RoomAt(p - door.transform.forward * 0.8f);
                door.Area = Layout.AreaAt(p);
            }
            return Layout;
        }

        // ---- Floors and rooms ----

        public GameObject Slab(string name, float x0, float z0, float x1, float z1, float bottom, float top, Color color)
        {
            var slab = Shapes.Box(name, Geometry, new Vector3((x0 + x1) * 0.5f, (bottom + top) * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, top - bottom, z1 - z0), color);
            // Open ground: grass if it's green, otherwise asphalt.
            if (name.Contains("Ground"))
            {
                bool grass = color.g > color.r * 1.12f && color.g > color.b * 1.12f;
                Shapes.ApplySurface(slab, color, grass ? SurfaceKind.Grass : SurfaceKind.Asphalt);
                SurfaceTag.Set(slab, grass ? Swat.Surface.Grass : Swat.Surface.Asphalt);
            }
            return slab;
        }

        public void Decal(Vector3 position, Vector3 size, Color color, float glow = 0f)
        {
            Shapes.Box("Marking", Geometry, position, size, color, false, glow);
        }

        public RoomController Room(string id, string name, float x0, float z0, float x1, float z1, Color floor, bool indoor = true)
        {
            // A room inside another one's rectangle (a closet jutting into a corridor; add it after the
            // corridor) gets its floor a few millimetres higher so the two don't flicker, and shares
            // the corridor's ceiling.
            int nested = 0;
            foreach (var other in Layout.rooms)
            {
                var ob = other.Bounds;
                if (other.Indoor == indoor && ob.min.x <= x0 + 0.01f && ob.max.x >= x1 - 0.01f && ob.min.z <= z0 + 0.01f && ob.max.z >= z1 - 0.01f) nested++;
            }
            float top = (indoor ? 0.03f : 0.015f) + nested * 0.006f;
            var slab = Slab(name + " Floor", x0, z0, x1, z1, -0.05f, top, floor);
            var bounds = new Bounds(new Vector3((x0 + x1) * 0.5f, 1f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 4f, z1 - z0));
            var room = RoomController.Create(Geometry, id, name, bounds, CurrentArea, indoor);
            room.Style = RoomStyle.For(RoomStyle.Classify(id, name, indoor));
            room.Floor = slab;
            Shapes.ApplySurface(slab, floor, room.Style.floor);
            SurfaceTag.Set(slab, room.Style.footsteps);
            if (indoor)
            {
                room.Fixture = Shapes.PointLight(room.transform, new Vector3(0f, 2.6f, 0f), new Color(1f, 0.93f, 0.8f), 1.6f, Mathf.Max(x1 - x0, z1 - z0) * 0.9f + 2f);
                room.Fixture.enabled = false;
                if (nested == 0) Ceiling(x0, z0, x1, z1);
            }
            Layout.rooms.Add(room);
            return room;
        }

        // ---- Walls ----

        public void WallX(float z, float x0, float x1, Color color, bool exterior, params Gap[] gaps)
        {
            Wall(new Vector3(x0, 0f, z), new Vector3(x1, 0f, z), true, color, exterior, gaps);
        }

        public void WallZ(float x, float z0, float z1, Color color, bool exterior, params Gap[] gaps)
        {
            Wall(new Vector3(x, 0f, z0), new Vector3(x, 0f, z1), false, color, exterior, gaps);
        }

        void Wall(Vector3 a, Vector3 b, bool alongX, Color color, bool exterior, Gap[] gaps)
        {
            Vector3 direction = (b - a).normalized;
            float stretch = exterior ? WallThickness * 0.5f : 0f;
            Vector3 cursor = a - direction * stretch;
            Vector3 end = b + direction * stretch;
            float startCoord = alongX ? a.x : a.z;

            System.Array.Sort(gaps, (g1, g2) => g1.center.CompareTo(g2.center));
            foreach (var gap in gaps)
            {
                Vector3 gapCenter = a + direction * (gap.center - startCoord);
                WallPiece(cursor, gapCenter - direction * gap.width * 0.5f, color, exterior);
                cursor = gapCenter + direction * gap.width * 0.5f;
                Header(gapCenter, direction, gap.width, color, exterior);
                if (gap.kind == DoorKind.Opening) continue;

                DoorState state = gap.kind == DoorKind.Door ? DoorState.Closed : gap.kind == DoorKind.Sealed ? DoorState.Disabled : DoorState.Locked;
                var door = DoorController.Create(Geometry, gap.id ?? ("door" + Layout.doors.Count), gapCenter, alongX, gap.width, state,
                    gap.breachable, gap.kind == DoorKind.Electronic);
                door.Pickable = gap.pickable;
                door.RegisterView(Layout.view);
                if (gap.lockable) Layout.lockableDoors.Add(door);
                Layout.doors.Add(door);
            }
            WallPiece(cursor, end, color, exterior);
        }

        void WallPiece(Vector3 from, Vector3 to, Color color, bool exterior)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < 0.0001f) return;
            // Walls are drawn low so you can see into rooms from above, but their colliders are full height.
            float height = exterior ? WallLowHeight : WallLowHeight - 0.04f;
            var wall = Shapes.Box("Wall", Geometry, Vector3.zero, new Vector3(WallThickness, height, delta.magnitude), color);
            wall.transform.localPosition = (from + to) * 0.5f + Vector3.up * height * 0.5f;
            wall.transform.localRotation = Quaternion.LookRotation(delta);
            var surface = exterior ? ExteriorWall : SurfaceKind.PaintedWall;
            Shapes.ApplySurface(wall, color, surface);
            SurfaceTag.Set(wall, Swat.Surface.Concrete);
            var box = wall.GetComponent<BoxCollider>();
            box.size = new Vector3(1f, WallSolidHeight / height, 1f);
            box.center = new Vector3(0f, box.size.y * 0.5f - 0.5f, 0f);
            Layout.view.AddStretch(wall, height, height - WallLowHeight + WallHighHeight, WallSolidHeight, 0f, ProceduralTextures.TileMeters(surface));
            if (!exterior) wall.AddComponent<ThinWall>();
            Shapes.Box("Wall Top", wall.transform, new Vector3(0f, 0.5f, 0f), new Vector3(1.05f, 0.02f, 1f), Shapes.Shade(color, 0.55f), false);
            Layout.walls.Add(new WallSegment { a = new Vector2(from.x, from.z), b = new Vector2(to.x, to.z), area = CurrentArea, exterior = exterior });
        }

        // First person: the wall above a doorway or opening, up to the ceiling (visual only).
        void Header(Vector3 center, Vector3 direction, float width, Color color, bool exterior)
        {
            float bottom = ViewMode.FirstPersonDoorHeight, top = WallHighHeight;
            var header = Shapes.Box("Door Header", Geometry, center + Vector3.up * (bottom + top) * 0.5f, new Vector3(WallThickness, top - bottom, width + 0.02f), color, false);
            header.transform.localRotation = Quaternion.LookRotation(direction);
            header.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            Shapes.ApplySurface(header, color, exterior ? ExteriorWall : SurfaceKind.PaintedWall);
            Layout.view.AddFirstPersonOnly(header);
        }

        // First person: a plain ceiling over an indoor room. It doesn't block the sun (rooms would be
        // pitch dark), it has no collider (so it's not part of the NavMesh) and it hides nothing from
        // the top-down view because it's switched off there.
        void Ceiling(float x0, float z0, float x1, float z1)
        {
            var ceiling = Shapes.Box("Ceiling", Geometry, new Vector3((x0 + x1) * 0.5f, WallHighHeight + 0.03f, (z0 + z1) * 0.5f), new Vector3(x1 - x0 + 0.2f, 0.06f, z1 - z0 + 0.2f), new Color(0.82f, 0.81f, 0.78f), false);
            var renderer = ceiling.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Shapes.ApplySurface(ceiling, new Color(0.82f, 0.81f, 0.78f), SurfaceKind.PaintedWall);
            Layout.view.AddFirstPersonOnly(ceiling);
        }

        // ---- Props ----

        // Brick for offices and homes, plain concrete for industrial buildings.
        public SurfaceKind ExteriorWall { get; set; }

        public GameObject Prop(string name, Vector3 position, Vector3 size, Color color, bool cover, float yaw = 0f)
        {
            var go = Shapes.Box(name, Props, position + Vector3.up * size.y * 0.5f, size, color);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var surface = RoomStyle.ForProp(name);
            if (surface != SurfaceKind.Plain) Shapes.ApplySurface(go, color, surface);
            SurfaceTag.Set(go, RoomStyle.ImpactFor(surface));
            if (cover)
            {
                bool turned = Mathf.Abs(Mathf.DeltaAngle(yaw, 90f)) < 45f || Mathf.Abs(Mathf.DeltaAngle(yaw, 270f)) < 45f;
                float halfX = (turned ? size.z : size.x) * 0.5f + 0.6f;
                float halfZ = (turned ? size.x : size.z) * 0.5f + 0.6f;
                Layout.coverPoints.Add(position + new Vector3(halfX, 0f, 0f));
                Layout.coverPoints.Add(position + new Vector3(-halfX, 0f, 0f));
                Layout.coverPoints.Add(position + new Vector3(0f, 0f, halfZ));
                Layout.coverPoints.Add(position + new Vector3(0f, 0f, -halfZ));
            }
            return go;
        }

        // A structural column: cover all round that breaks up a big room or a long corridor. Like the
        // walls it's drawn low from above and full height in first person, and always solid to full height.
        public void Pillar(float x, float z, float size = 0.6f)
        {
            var color = new Color(0.62f, 0.62f, 0.6f);
            var pillar = Prop("Pillar", new Vector3(x, 0f, z), new Vector3(size, WallLowHeight, size), color, true);
            var surface = ExteriorWall == SurfaceKind.Concrete ? SurfaceKind.Concrete : SurfaceKind.PaintedWall;
            Shapes.ApplySurface(pillar, color, surface);
            SurfaceTag.Set(pillar, Swat.Surface.Concrete);
            var box = pillar.GetComponent<BoxCollider>();
            box.size = new Vector3(1f, WallSolidHeight / WallLowHeight, 1f);
            box.center = new Vector3(0f, box.size.y * 0.5f - 0.5f, 0f);
            Layout.view.AddStretch(pillar, WallLowHeight, WallHighHeight, WallSolidHeight, 0f, ProceduralTextures.TileMeters(surface));
        }

        // A free-standing partition panel from (x0, z0) to (x1, z1): cubicle walls, a screen, a low
        // divider. Tall enough (1.7 m by default) to hide someone standing behind it, and cover.
        public void Partition(float x0, float z0, float x1, float z1, float height = 1.7f, Color? color = null)
        {
            float dx = x1 - x0, dz = z1 - z0;
            float length = Mathf.Sqrt(dx * dx + dz * dz);
            if (length < 0.05f) return;
            float yaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg - 90f;
            var panel = Prop("Partition", new Vector3((x0 + x1) * 0.5f, 0f, (z0 + z1) * 0.5f), new Vector3(length, height, 0.08f), color ?? new Color(0.46f, 0.5f, 0.56f), true, yaw);
            SurfaceTag.Set(panel, Swat.Surface.Wood);
        }

        public void Desk(Vector3 position, float yaw, float width = 1.6f, bool monitor = true)
        {
            var desk = Prop("Desk", position, new Vector3(width, 0.8f, 0.85f), new Color(0.5f, 0.38f, 0.26f), true, yaw);
            if (!monitor) return;
            // A 1999 desk: a deep beige CRT monitor.
            var screen = Shapes.Box("Monitor", desk.transform, new Vector3(0f, 0.725f, 0.12f), new Vector3(0.4f / width, 0.45f, 0.45f), new Color(0.78f, 0.75f, 0.66f), false);
            Shapes.Box("Screen", screen.transform, new Vector3(0f, 0.05f, -0.51f), new Vector3(0.78f, 0.7f, 0.02f), new Color(0.3f, 0.55f, 0.75f), false, 1.2f);
            // Desk clutter: keyboard, mug, papers (visual only).
            Shapes.Box("Keyboard", desk.transform, new Vector3(0f, 0.53f, -0.15f), new Vector3(0.28f / width, 0.04f, 0.18f), new Color(0.12f, 0.12f, 0.14f), false);
            Shapes.Make(PrimitiveType.Cylinder, "Mug", desk.transform, new Vector3(0.32f, 0.56f, -0.1f), new Vector3(0.05f / width, 0.07f, 0.08f), new Color(0.85f, 0.85f, 0.8f), false);
            Shapes.Box("Papers", desk.transform, new Vector3(-0.3f, 0.51f, -0.05f), new Vector3(0.18f / width, 0.02f, 0.3f), new Color(0.92f, 0.92f, 0.88f), false)
                .transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
            // Some desks also get a phone or a forgotten ID badge (picked from the position, so it's stable).
            int pick = Mathf.Abs(Mathf.RoundToInt(position.x * 7f + position.z * 13f)) % 3;
            if (pick != 1)
            {
                Shapes.Box("Phone", desk.transform, new Vector3(0.3f, 0.535f, 0.22f), new Vector3(0.13f / width, 0.06f, 0.14f), new Color(0.1f, 0.1f, 0.11f), false);
                Shapes.Box("Phone Light", desk.transform, new Vector3(0.3f, 0.57f, 0.17f), new Vector3(0.02f / width, 0.01f, 0.02f), new Color(0.3f, 1f, 0.4f), false, 1.5f);
            }
            if (pick != 0)
                Shapes.Box("ID Badge", desk.transform, new Vector3(-0.12f, 0.51f, -0.3f), new Vector3(0.05f / width, 0.01f, 0.08f), new Color(0.85f, 0.9f, 1f), false)
                    .transform.localRotation = Quaternion.Euler(0f, -20f, 0f);
        }

        public void Couch(Vector3 position, float yaw, Color color)
        {
            var couch = Prop("Couch", position, new Vector3(2.2f, 0.5f, 0.9f), color, true, yaw);
            Shapes.Box("Back", couch.transform, new Vector3(0f, 0.6f, 0.4f), new Vector3(1f, 1.2f, 0.25f), Shapes.Shade(color, 0.85f), false);
        }

        public void Table(Vector3 position, Vector3 size, Color color, float yaw = 0f)
        {
            Prop("Table", position, size, color, true, yaw);
        }

        public void Shelf(Vector3 position, float yaw, float length = 2f, float height = 2f)
        {
            var shelf = Prop("Shelf", position, new Vector3(length, height, 0.5f), new Color(0.35f, 0.37f, 0.4f), true, yaw);
            for (int i = 0; i < 3; i++)
                Shapes.Box("Box", shelf.transform, new Vector3(Random.Range(-0.35f, 0.35f), -0.25f + i * 0.3f, -0.55f), new Vector3(0.15f, 0.12f, 0.4f), new Color(0.6f, 0.5f, 0.35f), false);
        }

        public void Crate(Vector3 position, float size, bool stacked)
        {
            var wood = new Color(0.55f, 0.4f, 0.22f);
            var crate = Prop("Crate", position, Vector3.one * size, wood, true);
            if (stacked)
                Shapes.Box("Crate", crate.transform, new Vector3(0.05f, 0.9f, 0f), Vector3.one * 0.8f, Shapes.Shade(wood, 0.85f), false)
                    .transform.localRotation = Quaternion.Euler(0f, 15f, 0f);
        }

        public void Plant(Vector3 position)
        {
            Shapes.Make(PrimitiveType.Cylinder, "Pot", Props, position + Vector3.up * 0.25f, new Vector3(0.45f, 0.25f, 0.45f), new Color(0.5f, 0.3f, 0.2f));
            Shapes.Make(PrimitiveType.Sphere, "Leaves", Props, position + Vector3.up * 0.85f, new Vector3(0.8f, 0.8f, 0.8f), new Color(0.2f, 0.5f, 0.22f), false);
        }

        public void Bed(Vector3 position, float yaw)
        {
            var bed = Prop("Bed", position, new Vector3(1.5f, 0.55f, 2.1f), new Color(0.75f, 0.75f, 0.8f), true, yaw);
            Shapes.Box("Pillow", bed.transform, new Vector3(0f, 0.6f, 0.38f), new Vector3(0.8f, 0.3f, 0.15f), Color.white, false);
        }

        public void Car(Vector3 position, float yaw, Color color, bool police = false)
        {
            var car = Prop(police ? "Police Car" : "Car", position, new Vector3(1.9f, 0.75f, 4.3f), color, true, yaw);
            Shapes.Box("Cabin", car.transform, new Vector3(0f, 0.9f, -0.05f), new Vector3(0.9f, 0.8f, 0.5f), new Color(0.15f, 0.18f, 0.22f), false);
            if (!police) return;
            Shapes.Box("Siren Red", car.transform, new Vector3(-0.2f, 1.35f, -0.05f), new Vector3(0.3f, 0.12f, 0.06f), new Color(1f, 0.1f, 0.1f), false, 3f);
            Shapes.Box("Siren Blue", car.transform, new Vector3(0.2f, 1.35f, -0.05f), new Vector3(0.3f, 0.12f, 0.06f), new Color(0.15f, 0.35f, 1f), false, 3f);
        }

        public void Truck(Vector3 position, float yaw)
        {
            var truck = Prop("Truck", position, new Vector3(2.5f, 3f, 8f), new Color(0.8f, 0.8f, 0.78f), true, yaw);
            Shapes.Box("Cab", truck.transform, new Vector3(0f, -0.15f, 0.62f), new Vector3(1f, 0.7f, 0.25f), new Color(0.2f, 0.3f, 0.5f), false);
        }

        public void Lamp(Vector3 position)
        {
            Shapes.Box("Lamp Post", Props, position + Vector3.up * 2.25f, new Vector3(0.14f, 4.5f, 0.14f), new Color(0.2f, 0.2f, 0.22f));
            Shapes.Box("Lamp Head", Props, position + new Vector3(0f, 4.4f, 0f), new Vector3(0.4f, 0.1f, 0.4f), new Color(1f, 0.85f, 0.6f), false, 2f);
            var light = Shapes.PointLight(Props, position + Vector3.up * 4f, new Color(1f, 0.85f, 0.6f), 1.8f, 14f);
            light.enabled = false;
            Layout.outdoorLights.Add(light);
        }

        public void Boundary(float x0, float z0, float x1, float z1)
        {
            float midX = (x0 + x1) * 0.5f, midZ = (z0 + z1) * 0.5f;
            Invisible(new Vector3(x0, 1.5f, midZ), new Vector3(1f, 3f, z1 - z0));
            Invisible(new Vector3(x1, 1.5f, midZ), new Vector3(1f, 3f, z1 - z0));
            Invisible(new Vector3(midX, 1.5f, z0), new Vector3(x1 - x0, 3f, 1f));
            Invisible(new Vector3(midX, 1.5f, z1), new Vector3(x1 - x0, 3f, 1f));
        }

        void Invisible(Vector3 position, Vector3 size)
        {
            Shapes.Box("Boundary", Geometry, position, size, Color.black).GetComponent<Renderer>().enabled = false;
        }

        // ---- Spawns, zones and mounts ----

        public void EnemySpot(string tag, float x, float z, float yaw, params Vector3[] patrol)
        {
            Layout.enemySpawns.Add(new EnemySpawnPoint { tag = tag, position = new Vector3(x, 0f, z), yaw = yaw, patrol = patrol });
        }

        public void CivilianSpot(string tag, float x, float z, float yaw)
        {
            Layout.civilianSpawns.Add(new CivilianSpawnPoint { tag = tag, position = new Vector3(x, 0f, z), yaw = yaw });
        }

        public void Camera(float x, float z, float yaw) { Layout.cameraMounts.Add(new Mount { position = new Vector3(x, 0f, z), yaw = yaw }); }

        public void Console(string id, string title, float x, float z, float yaw, bool unlockDoors, bool reviewFootage)
        {
            Layout.consoleMounts.Add(new Mount { id = id, title = title, position = new Vector3(x, 0f, z), yaw = yaw, unlockDoors = unlockDoors, reviewFootage = reviewFootage });
        }

        public void AlarmPanel(float x, float z, float yaw)
        {
            Layout.hasAlarm = true;
            Layout.alarmPanel = new Mount { position = new Vector3(x, 0f, z), yaw = yaw };
        }

        public void Beacon(float x, float z) { Layout.alarmBeacons.Add(new Vector3(x, 1.38f, z)); }
        public void Evidence(float x, float z) { Layout.evidenceSpots.Add(new Vector3(x, 0.03f, z)); }
        public void Escape(float x, float z) { Layout.escapePoints.Add(new Vector3(x, 0f, z)); }
        public void Zone(string id, float x0, float z0, float x1, float z1) { Layout.zones[id] = new Bounds(new Vector3((x0 + x1) * 0.5f, 1f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 4f, z1 - z0)); }

        public void Extraction(float x0, float z0, float x1, float z1)
        {
            var bounds = new Bounds(new Vector3((x0 + x1) * 0.5f, 1f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 4f, z1 - z0));
            Layout.extraction = ExtractionZone.Create(Geometry, "Extraction Zone", bounds, true, CurrentArea, new Color(0.2f, 0.5f, 0.95f));
        }

        public void SafeZone(float x0, float z0, float x1, float z1)
        {
            var bounds = new Bounds(new Vector3((x0 + x1) * 0.5f, 1f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 4f, z1 - z0));
            Layout.safeZones.Add(ExtractionZone.Create(Geometry, "Safe Zone", bounds, true, CurrentArea, new Color(0.2f, 0.85f, 0.45f)));
        }

        public void Arrival(Vector3 playerSpawn, float yaw, Vector3 vanParking, float vanYaw, Vector3 vanStart)
        {
            Layout.playerSpawn = playerSpawn;
            Layout.playerYaw = yaw;
            Layout.vanParking = vanParking;
            Layout.vanYaw = vanYaw;
            Layout.vanArrivalStart = vanStart;
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            Vector3 back = Quaternion.Euler(0f, yaw, 0f) * Vector3.back;
            Layout.squadSpawns.Add(playerSpawn + right * 1.3f + back * 0.8f);
            Layout.squadSpawns.Add(playerSpawn - right * 1.3f + back * 0.8f);
            Layout.squadSpawns.Add(playerSpawn + back * 1.8f);
        }

        public Stairwell Stairs(float x, float z, string label, Vector3 destination, float yaw, int destinationArea)
        {
            var stairs = Stairwell.Create(Props, new Vector3(x, 0f, z), label, destination, yaw, destinationArea);
            Layout.stairs.Add(stairs);
            return stairs;
        }
    }
}
