using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Runs once after a map is built and gives it intentional lighting and
    // detail without touching the gameplay layout:
    //  * light fixtures per room, colored by room kind (cool office, warm
    //    lobby, dim amber storage, blue security...), with light pools on the
    //    floor that work even on tiers without dynamic lights;
    //  * emergency lights and shading in rooms without power;
    //  * exterior lamp pools, exit signs, monitor glow;
    //  * contact shadows (cheap baked-style ambient occlusion) along walls
    //    and under furniture, merged into a couple of meshes;
    //  * small non-colliding props that make each kind of room recognizable.
    public class MapDresser : MonoBehaviour
    {
        class Fixture
        {
            public RoomController room;
            public Light light;
        }

        readonly List<Fixture> fixtures = new List<Fixture>();
        readonly List<Light> exteriorLights = new List<Light>();
        readonly List<GameObject> contactShadows = new List<GameObject>();
        LevelLayout level;
        LightingProfile profile;
        Transform props;

        public static MapDresser Dress(LevelLayout level, LightingProfile profile, int seed, bool decorate)
        {
            var go = new GameObject("Dressing");
            go.transform.SetParent(level.root, false);
            var dresser = go.AddComponent<MapDresser>();
            dresser.Build(level, profile, seed, decorate);
            return dresser;
        }

        void Build(LevelLayout layout, LightingProfile lighting, int seed, bool decorate)
        {
            level = layout;
            profile = lighting;
            props = new GameObject("Decor").transform;
            props.SetParent(transform, false);
            Physics.SyncTransforms();
            var random = new System.Random(seed);
            var pools = new DecalMesh();
            var darkness = new DecalMesh();
            var blobs = new DecalMesh();

            foreach (var room in level.rooms)
            {
                if (!room.Indoor) continue;
                LightRoom(room, random, pools, darkness);
                if (decorate) Decorate(room, random, blobs);
            }
            DressExterior(pools, random, decorate);
            AddExitSigns(pools);
            ContactShadows(blobs);

            pools.Build("Light Pools", transform, Shapes.GlowMaterial(ProceduralTextures.Radial));
            darkness.Build("Unpowered Rooms", transform, Shapes.DecalMaterial(Texture2D.whiteTexture));
            var blobObject = blobs.Build("Contact Shadows (props)", transform, Shapes.DecalMaterial(ProceduralTextures.Radial));
            if (blobObject != null) contactShadows.Add(blobObject);
            ApplyQuality();
        }

        // Turns real lights and the contact shadows on or off to match the current graphics settings.
        public void ApplyQuality()
        {
            var quality = QualityManager.Current;
            bool realLights = quality.dynamicLights && quality.fixtureLights;
            foreach (var fixture in fixtures)
                if (fixture.light != null) fixture.light.enabled = realLights && !fixture.room.IsDark;
            foreach (var light in exteriorLights)
                if (light != null) light.enabled = realLights && profile.lamps;
            bool ao = QualityManager.AmbientOcclusionOn;
            foreach (var go in contactShadows) if (go != null) go.SetActive(ao);
        }

        // ---- Interior lighting ----

        void LightRoom(RoomController room, System.Random random, DecalMesh pools, DecalMesh darkness)
        {
            var style = room.Style;
            var b = room.Bounds;
            bool dark = room.IsDark;
            float strength = style.lightStrength * profile.interior;

            if (room.Fixture != null)
            {
                room.Fixture.color = style.light;
                room.Fixture.intensity = 1.25f * strength * Shapes.PointLightScale;
                fixtures.Add(new Fixture { room = room, light = room.Fixture });
            }

            // Lamp panels in a grid about 5 m apart, hung at wall-top height.
            int nx = Mathf.Max(1, Mathf.RoundToInt(b.size.x / 5f));
            int nz = Mathf.Max(1, Mathf.RoundToInt(b.size.z / 5f));
            float stepX = b.size.x / nx, stepZ = b.size.z / nz;
            float poolSize = Mathf.Clamp(Mathf.Min(stepX, stepZ) * 1.5f, 3f, 7.5f);
            var litMaterial = Shapes.Mat(style.light, 1.6f);
            var offMaterial = Shapes.Mat(new Color(0.32f, 0.33f, 0.35f));
            bool flicker = !dark && random.NextDouble() < style.flickerChance;
            var lampRenderers = new List<Renderer>();
            var flickerPools = new List<Renderer>();
            Color poolColor = style.light;
            poolColor.a = Mathf.Clamp01(profile.pool * style.lightStrength);

            for (int ix = 0; ix < nx; ix++)
            {
                for (int iz = 0; iz < nz; iz++)
                {
                    Vector3 p = new Vector3(b.min.x + stepX * (ix + 0.5f), 0f, b.min.z + stepZ * (iz + 0.5f));
                    bool alongX = b.size.x >= b.size.z;
                    // Hung at wall-top height from above, just under the ceiling in first person.
                    var lamp = Shapes.Box("Ceiling Lamp", props, p + Vector3.up * 1.43f, alongX ? new Vector3(1f, 0.04f, 0.3f) : new Vector3(0.3f, 0.04f, 1f), Color.white, false);
                    level.view.AddLift(lamp.transform, ViewMode.FirstPersonWallHeight - 0.03f - 1.43f);
                    var renderer = lamp.GetComponent<Renderer>();
                    renderer.sharedMaterial = dark ? offMaterial : litMaterial;
                    lampRenderers.Add(renderer);
                    if (dark) continue;
                    if (flicker)
                        flickerPools.Add(DecalMesh.Single("Flickering Pool", props, p + Vector3.up * 0.05f, Vector2.one * poolSize, 0f, poolColor, Shapes.GlowMaterial(ProceduralTextures.Radial)));
                    else
                        pools.AddFlat(p + Vector3.up * 0.05f, Vector2.one * poolSize, 0f, poolColor);
                }
            }

            if (flicker)
            {
                var flickerer = new GameObject("Flicker").AddComponent<LightFlicker>();
                flickerer.transform.SetParent(props, false);
                flickerer.Init(room.Fixture, lampRenderers.ToArray(), litMaterial, offMaterial, flickerPools.ToArray());
            }

            if (dark)
            {
                // Unpowered: shade the room and light it only with emergency lamps by the doors.
                darkness.AddFlat(new Vector3(b.center.x, 0.045f, b.center.z), new Vector2(b.size.x, b.size.z), 0f, new Color(0.01f, 0.015f, 0.03f, profile.darkness));
                foreach (var door in level.doors)
                {
                    if (door.RoomFront != room && door.RoomBack != room) continue;
                    Vector3 inward = (door.RoomFront == room ? 1f : -1f) * door.transform.forward;
                    Vector3 spot = door.transform.position + inward * 0.12f;
                    var emergency = Shapes.Box("Emergency Light", props, spot + Vector3.up * 1.3f + door.transform.right * (door.Width * 0.5f + 0.25f), new Vector3(0.25f, 0.08f, 0.08f), new Color(1f, 0.45f, 0.12f), false, 2f);
                    emergency.transform.rotation = door.transform.rotation;
                    level.view.AddLift(emergency.transform, 0.9f);
                    pools.AddFlat(spot + inward * 0.9f + Vector3.up * 0.05f, new Vector2(2.6f, 2.6f), 0f, new Color(1f, 0.4f, 0.12f, 0.3f));
                    break;
                }
                if (style.kind == RoomKind.Hallway)
                {
                    // Floor-level emergency strip lights along the corridor.
                    bool alongX = b.size.x >= b.size.z;
                    int count = Mathf.Max(1, Mathf.RoundToInt((alongX ? b.size.x : b.size.z) / 6f));
                    for (int i = 0; i < count; i++)
                    {
                        float t = (i + 0.5f) / count;
                        Vector3 p = alongX ? new Vector3(b.min.x + b.size.x * t, 0.05f, b.min.z + 0.25f) : new Vector3(b.min.x + 0.25f, 0.05f, b.min.z + b.size.z * t);
                        pools.AddFlat(p, new Vector2(1.6f, 1.6f), 0f, new Color(1f, 0.35f, 0.1f, 0.35f));
                    }
                }
            }
        }

        // ---- Exterior ----

        void DressExterior(DecalMesh pools, System.Random random, bool decorate)
        {
            foreach (var light in level.outdoorLights)
            {
                if (light == null) continue;
                exteriorLights.Add(light);
                light.color = new Color(1f, 0.82f, 0.56f);
                light.intensity = 1.6f * Shapes.PointLightScale;
                if (profile.lamps)
                {
                    Vector3 p = light.transform.position;
                    pools.AddFlat(new Vector3(p.x, 0.04f, p.z), new Vector2(9f, 9f), 0f, new Color(1f, 0.78f, 0.5f, profile.pool * 0.9f));
                }
            }
            if (!decorate || level.extraction == null) return;
            // Bollards mark the extraction point's corners.
            var e = level.extraction.Bounds;
            EnvironmentProps.Bollard(props, new Vector3(e.min.x, 0f, e.min.z));
            EnvironmentProps.Bollard(props, new Vector3(e.max.x, 0f, e.min.z));
            EnvironmentProps.Bollard(props, new Vector3(e.min.x, 0f, e.max.z));
            EnvironmentProps.Bollard(props, new Vector3(e.max.x, 0f, e.max.z));
        }

        void AddExitSigns(DecalMesh pools)
        {
            foreach (var door in level.doors)
            {
                bool frontIn = door.RoomFront != null && door.RoomFront.Indoor;
                bool backIn = door.RoomBack != null && door.RoomBack.Indoor;
                if (frontIn == backIn) continue; // only doors between inside and outside
                Vector3 inward = (frontIn ? 1f : -1f) * door.transform.forward;
                Vector3 spot = door.transform.position + inward * 0.13f + Vector3.up * 1.3f;
                float yaw = Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg;
                // Over the door frame in first person.
                level.view.AddLift(EnvironmentProps.ExitSign(props, spot, yaw), ViewMode.FirstPersonDoorHeight + 0.15f - 1.3f);
                pools.AddFlat(door.transform.position + inward * 0.8f + Vector3.up * 0.05f, new Vector2(1.6f, 1.6f), 0f, new Color(0.2f, 1f, 0.4f, 0.12f + profile.pool * 0.2f));
            }
        }

        // ---- Contact shadows ----

        void ContactShadows(DecalMesh blobs)
        {
            var strips = new DecalMesh();
            var shadow = new Color(0f, 0f, 0f, 0.34f);
            foreach (var wall in level.walls)
            {
                Vector3 a = new Vector3(wall.a.x, 0.05f, wall.a.y), b = new Vector3(wall.b.x, 0.05f, wall.b.y);
                Vector3 along = b - a;
                if (along.sqrMagnitude < 0.01f) continue;
                Vector3 normal = new Vector3(-along.z, 0f, along.x).normalized;
                Vector3 mid = (a + b) * 0.5f;
                // One strip each side, darkest against the wall (v = 0) fading out over 0.6 m.
                strips.AddQuad(mid + normal * 0.4f, along * 0.5f, normal * 0.3f, shadow);
                strips.AddQuad(mid - normal * 0.4f, along * 0.5f, -normal * 0.3f, shadow);
            }
            var stripObject = strips.Build("Contact Shadows (walls)", transform, Shapes.DecalMaterial(ProceduralTextures.Edge));
            if (stripObject != null) contactShadows.Add(stripObject);

            var propRoot = level.root.Find("Props");
            if (propRoot == null) return;
            foreach (Transform child in propRoot)
            {
                var collider = child.GetComponent<Collider>();
                var renderer = child.GetComponent<Renderer>();
                if (collider == null || renderer == null || !renderer.enabled) continue;
                var bounds = renderer.bounds;
                if (bounds.size.y > 4f || bounds.size.x * bounds.size.z < 0.05f) continue;
                blobs.AddFlat(new Vector3(bounds.center.x, 0.05f, bounds.center.z), new Vector2(bounds.size.x + 0.6f, bounds.size.z + 0.6f), 0f, new Color(0f, 0f, 0f, 0.42f));
            }
        }

        // ---- Room decor ----

        enum Mount { Floor, Wall }

        struct DecorItem
        {
            public Mount mount;
            public System.Func<Transform, Vector3, float, Transform> build;
            public float lift;   // how much higher it hangs in first person
        }

        static DecorItem Floor(System.Func<Transform, Vector3, float, Transform> build) { return new DecorItem { mount = Mount.Floor, build = build }; }
        static DecorItem Wall(System.Func<Transform, Vector3, float, Transform> build) { return new DecorItem { mount = Mount.Wall, build = build }; }
        // Posters, boards, clocks and panels hang low enough to show over the low top-down walls; in first
        // person they go up to eye level.
        static DecorItem Hung(System.Func<Transform, Vector3, float, Transform> build) { return new DecorItem { mount = Mount.Wall, build = build, lift = 0.55f }; }

        static readonly Color[] PosterColors = { new Color(0.25f, 0.45f, 0.75f), new Color(0.75f, 0.55f, 0.2f), new Color(0.3f, 0.6f, 0.4f), new Color(0.6f, 0.25f, 0.3f) };

        List<DecorItem> ItemsFor(RoomKind kind, System.Random random)
        {
            Color poster = PosterColors[random.Next(PosterColors.Length)];
            var items = new List<DecorItem>();
            switch (kind)
            {
                case RoomKind.Office:
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.FilingCabinet(p, at, yaw)));
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.TrashCan(p, at)));
                    items.Add(Hung((p, at, yaw) => EnvironmentProps.Poster(p, at, yaw, poster)));
                    items.Add(Hung((p, at, yaw) => EnvironmentProps.Clock(p, at, yaw)));
                    break;
                case RoomKind.Conference:
                    items.Add(Hung((p, at, yaw) => EnvironmentProps.Whiteboard(p, at, yaw)));
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.TrashCan(p, at)));
                    items.Add(Hung((p, at, yaw) => EnvironmentProps.Clock(p, at, yaw)));
                    break;
                case RoomKind.Lobby:
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.WaterCooler(p, at, yaw)));
                    items.Add(Hung((p, at, yaw) => EnvironmentProps.Poster(p, at, yaw, poster)));
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.TrashCan(p, at)));
                    items.Add(Wall((p, at, yaw) => EnvironmentProps.FireExtinguisher(p, at, yaw)));
                    items.Add(Wall((p, at, yaw) => EnvironmentProps.FireAlarm(p, at, yaw)));
                    break;
                case RoomKind.Hallway:
                case RoomKind.Stairwell:
                    items.Add(Wall((p, at, yaw) => EnvironmentProps.FireExtinguisher(p, at, yaw)));
                    items.Add(Wall((p, at, yaw) => EnvironmentProps.FireAlarm(p, at, yaw)));
                    items.Add(Hung((p, at, yaw) => EnvironmentProps.Poster(p, at, yaw, poster)));
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.TrashCan(p, at)));
                    break;
                case RoomKind.Storage:
                case RoomKind.Warehouse:
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.BoxStack(p, at, yaw)));
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.BoxStack(p, at, yaw + 40f)));
                    items.Add(Hung((p, at, yaw) => EnvironmentProps.ElectricalPanel(p, at, yaw)));
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.Toolbox(p, at, yaw)));
                    items.Add(Wall((p, at, yaw) => EnvironmentProps.FireExtinguisher(p, at, yaw)));
                    items.Add(Wall((p, at, yaw) => EnvironmentProps.FireAlarm(p, at, yaw)));
                    break;
                case RoomKind.Security:
                    items.Add(Wall((p, at, yaw) => EnvironmentProps.MonitorBank(p, at, yaw)));
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.FilingCabinet(p, at, yaw)));
                    items.Add(Hung((p, at, yaw) => EnvironmentProps.Poster(p, at, yaw, new Color(0.2f, 0.3f, 0.5f))));
                    break;
                case RoomKind.Restroom:
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.TrashCan(p, at)));
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.CleaningCart(p, at, yaw)));
                    break;
                case RoomKind.Breakroom:
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.VendingMachine(p, at, yaw)));
                    items.Add(Wall((p, at, yaw) => EnvironmentProps.CoffeeStation(p, at, yaw)));
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.TrashCan(p, at)));
                    break;
                case RoomKind.Utility:
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.CleaningCart(p, at, yaw)));
                    items.Add(Hung((p, at, yaw) => EnvironmentProps.ElectricalPanel(p, at, yaw)));
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.Toolbox(p, at, yaw)));
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.BoxStack(p, at, yaw)));
                    break;
                case RoomKind.Residential:
                    items.Add(Hung((p, at, yaw) => EnvironmentProps.Poster(p, at, yaw, poster)));
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.TrashCan(p, at)));
                    items.Add(Hung((p, at, yaw) => EnvironmentProps.Clock(p, at, yaw)));
                    break;
                case RoomKind.Medical:
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.FilingCabinet(p, at, yaw)));
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.TrashCan(p, at)));
                    items.Add(Hung((p, at, yaw) => EnvironmentProps.Poster(p, at, yaw, new Color(0.3f, 0.6f, 0.7f))));
                    items.Add(Hung((p, at, yaw) => EnvironmentProps.Clock(p, at, yaw)));
                    break;
                case RoomKind.Club:
                    items.Add(Hung((p, at, yaw) => EnvironmentProps.Poster(p, at, yaw, new Color(0.6f, 0.2f, 0.7f))));
                    items.Add(Hung((p, at, yaw) => EnvironmentProps.Poster(p, at, yaw, new Color(0.15f, 0.5f, 0.75f))));
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.TrashCan(p, at)));
                    items.Add(Wall((p, at, yaw) => EnvironmentProps.FireExtinguisher(p, at, yaw)));
                    break;
                case RoomKind.Vault:
                    items.Add(Hung((p, at, yaw) => EnvironmentProps.ElectricalPanel(p, at, yaw)));
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.BoxStack(p, at, yaw)));
                    break;
                case RoomKind.Retail:
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.TrashCan(p, at)));
                    items.Add(Hung((p, at, yaw) => EnvironmentProps.Poster(p, at, yaw, poster)));
                    items.Add(Wall((p, at, yaw) => EnvironmentProps.FireExtinguisher(p, at, yaw)));
                    break;
                case RoomKind.Garage:
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.Toolbox(p, at, yaw)));
                    items.Add(Wall((p, at, yaw) => EnvironmentProps.FireExtinguisher(p, at, yaw)));
                    items.Add(Floor((p, at, yaw) => EnvironmentProps.BoxStack(p, at, yaw)));
                    break;
                default:
                    items.Add(Wall((p, at, yaw) => EnvironmentProps.FireExtinguisher(p, at, yaw)));
                    items.Add(Hung((p, at, yaw) => EnvironmentProps.Poster(p, at, yaw, poster)));
                    break;
            }
            return items;
        }

        void Decorate(RoomController room, System.Random random, DecalMesh blobs)
        {
            var b = room.Bounds;
            if (b.size.x < 3f || b.size.z < 3f) return;
            var floorSpots = new List<Vector3>
            {
                new Vector3(b.min.x + 0.5f, 0f, b.min.z + 0.5f), new Vector3(b.max.x - 0.5f, 0f, b.min.z + 0.5f),
                new Vector3(b.min.x + 0.5f, 0f, b.max.z - 0.5f), new Vector3(b.max.x - 0.5f, 0f, b.max.z - 0.5f),
            };
            Shuffle(floorSpots, random);
            var wallSpots = WallSpots(room);
            Shuffle(wallSpots, random);

            foreach (var item in ItemsFor(room.Style.kind, random))
            {
                if (item.mount == Mount.Floor)
                {
                    for (int i = 0; i < floorSpots.Count; i++)
                    {
                        Vector3 spot = floorSpots[i];
                        if (!FloorFree(spot, 0.32f)) continue;
                        floorSpots.RemoveAt(i);
                        // Face into the room.
                        Vector3 toCenter = new Vector3(b.center.x - spot.x, 0f, b.center.z - spot.z);
                        float yaw = Mathf.Round(Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg / 90f) * 90f;
                        item.build(props, spot, yaw);
                        blobs.AddFlat(spot + Vector3.up * 0.05f, new Vector2(0.9f, 0.9f), 0f, new Color(0f, 0f, 0f, 0.35f));
                        break;
                    }
                }
                else
                {
                    for (int i = 0; i < wallSpots.Count; i++)
                    {
                        var spot = wallSpots[i];
                        if (!NearDoorOrSpawn(spot.position, 1.3f))
                        {
                            wallSpots.RemoveAt(i);
                            var built = item.build(props, spot.position, spot.yaw);
                            if (item.lift > 0f) level.view.AddLift(built, item.lift);
                            break;
                        }
                    }
                }
            }
        }

        struct WallSpot
        {
            public Vector3 position;
            public float yaw;
        }

        // Points on the inside face of the room's walls (only where a wall is actually there).
        List<WallSpot> WallSpots(RoomController room)
        {
            var spots = new List<WallSpot>();
            var b = room.Bounds;
            var edges = new[]
            {
                new { inward = Vector3.forward, from = new Vector3(b.min.x, 0f, b.min.z), to = new Vector3(b.max.x, 0f, b.min.z) },
                new { inward = Vector3.back, from = new Vector3(b.min.x, 0f, b.max.z), to = new Vector3(b.max.x, 0f, b.max.z) },
                new { inward = Vector3.right, from = new Vector3(b.min.x, 0f, b.min.z), to = new Vector3(b.min.x, 0f, b.max.z) },
                new { inward = Vector3.left, from = new Vector3(b.max.x, 0f, b.min.z), to = new Vector3(b.max.x, 0f, b.max.z) },
            };
            foreach (var edge in edges)
            {
                foreach (float t in new[] { 0.3f, 0.5f, 0.7f })
                {
                    Vector3 p = Vector3.Lerp(edge.from, edge.to, t) + edge.inward * 0.1f;
                    RaycastHit hit;
                    if (!Physics.Raycast(p + Vector3.up * 0.8f + edge.inward * 0.4f, -edge.inward, out hit, 0.7f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.collider.name != "Wall") continue;
                    spots.Add(new WallSpot { position = new Vector3(hit.point.x, 0f, hit.point.z), yaw = Mathf.Atan2(edge.inward.x, edge.inward.z) * Mathf.Rad2Deg });
                }
            }
            return spots;
        }

        bool FloorFree(Vector3 spot, float radius)
        {
            if (NearDoorOrSpawn(spot, 1.7f)) return false;
            foreach (var cover in level.coverPoints)
                if (AIManager.FlatDistance(cover, spot) < 0.5f) return false;
            return !Physics.CheckBox(spot + Vector3.up * 0.6f, new Vector3(radius, 0.45f, radius), Quaternion.identity, Layers.WorldMask, QueryTriggerInteraction.Ignore);
        }

        bool NearDoorOrSpawn(Vector3 spot, float doorDistance)
        {
            foreach (var door in level.doors)
                if (AIManager.FlatDistance(door.transform.position, spot) < doorDistance) return true;
            foreach (var spawn in level.enemySpawns)
                if (AIManager.FlatDistance(spawn.position, spot) < 1f) return true;
            foreach (var spawn in level.civilianSpawns)
                if (AIManager.FlatDistance(spawn.position, spot) < 1f) return true;
            foreach (var mount in level.consoleMounts)
                if (AIManager.FlatDistance(mount.position, spot) < 1.2f) return true;
            foreach (var mount in level.cameraMounts)
                if (AIManager.FlatDistance(mount.position, spot) < 0.8f) return true;
            if (level.hasAlarm && AIManager.FlatDistance(level.alarmPanel.position, spot) < 1.2f) return true;
            foreach (var stairs in level.stairs)
                if (AIManager.FlatDistance(stairs.transform.position, spot) < 2f) return true;
            return false;
        }

        static void Shuffle<T>(List<T> list, System.Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }
    }
}
