using UnityEngine;

namespace Swat
{
    public enum RoomKind
    {
        Exterior, Lobby, Office, Conference, Hallway, Storage, Security, Restroom, Breakroom,
        Utility, Warehouse, Residential, Stairwell, Range, Training, Garage, Roof,
        Medical, Club, Vault, Retail,
    }

    // What kind of space a room is, worked out from its id and name, and what
    // that means for how it looks and sounds: floor surface, light color and
    // strength, footsteps, reverb, how likely its lights are to flicker and
    // which small props dress it. This gives every map distinct lighting zones
    // without editing each map by hand.
    public struct RoomStyle
    {
        public RoomKind kind;
        public SurfaceKind floor;
        public Surface footsteps;
        public Color light;
        public float lightStrength;   // 1 = a normally lit office
        public float flickerChance;
        public AudioReverbPreset reverb;
        public bool hum;              // fluorescent hum in the ambience

        public static RoomKind Classify(string id, string name, bool indoor)
        {
            string key = ((id ?? "") + " " + (name ?? "")).ToLowerInvariant();
            if (!indoor) return key.Contains("roof") ? RoomKind.Roof : RoomKind.Exterior;
            if (Has(key, "vault", "safe deposit")) return RoomKind.Vault;
            if (Has(key, "ward", "exam", "clinic", "pharmacy", "triage", "treatment")) return RoomKind.Medical;
            if (Has(key, "dance", "club", "vip", "stage")) return RoomKind.Club;
            if (Has(key, "shop floor", "sales floor", "showroom")) return RoomKind.Retail;
            if (Has(key, "lobby", "reception", "waiting", "banking hall", "coat check")) return RoomKind.Lobby;
            if (Has(key, "hall", "corridor", "walkway")) return RoomKind.Hallway;
            if (Has(key, "stair", "roofaccess", "roof access")) return RoomKind.Stairwell;
            if (Has(key, "security", "booth", "control room")) return RoomKind.Security;
            if (Has(key, "restroom", "toilet", "lockers", "locker room", "bathroom")) return RoomKind.Restroom;
            if (Has(key, "conference", "briefing", "meeting")) return RoomKind.Conference;
            if (Has(key, "break", "lounge", "kitchen", "bar")) return RoomKind.Breakroom;
            if (Has(key, "maintenance", "laundry", "utility", "boiler", "electrical")) return RoomKind.Utility;
            if (Has(key, "bays", "aisles", "assembly", "production", "loading dock", "tool shop")) return RoomKind.Warehouse;
            if (Has(key, "storage", "restricted", "armory", "stock room", "cooler", "freezer")) return RoomKind.Storage;
            if (Has(key, "unit", "apartment", "motel", "guest room")) return RoomKind.Residential;
            if (Has(key, "range")) return RoomKind.Range;
            if (Has(key, "garage")) return RoomKind.Garage;
            if (Has(key, "office", "manager", "records", "loan", "foreman")) return RoomKind.Office;
            return RoomKind.Training;
        }

        static bool Has(string key, params string[] words)
        {
            foreach (var word in words) if (key.Contains(word)) return true;
            return false;
        }

        public static RoomStyle For(RoomKind kind)
        {
            var style = new RoomStyle
            {
                kind = kind,
                floor = SurfaceKind.Concrete,
                footsteps = Surface.Concrete,
                light = new Color(1f, 0.96f, 0.88f),
                lightStrength = 1f,
                flickerChance = 0.05f,
                reverb = AudioReverbPreset.Room,
                hum = true,
            };
            switch (kind)
            {
                case RoomKind.Exterior:
                    style.floor = SurfaceKind.Asphalt; style.footsteps = Surface.Asphalt; style.lightStrength = 0f;
                    style.reverb = AudioReverbPreset.Off; style.hum = false; style.flickerChance = 0f;
                    break;
                case RoomKind.Roof:
                    style.floor = SurfaceKind.DirtyConcrete; style.footsteps = Surface.Concrete; style.lightStrength = 0f;
                    style.reverb = AudioReverbPreset.Off; style.hum = false; style.flickerChance = 0f;
                    break;
                case RoomKind.Lobby:
                    style.floor = SurfaceKind.Tile; style.footsteps = Surface.Tile; style.light = new Color(1f, 0.9f, 0.74f);
                    style.lightStrength = 1.1f; style.reverb = AudioReverbPreset.Hallway; style.flickerChance = 0f;
                    break;
                case RoomKind.Office:
                    style.floor = SurfaceKind.Carpet; style.footsteps = Surface.Carpet; style.light = new Color(0.94f, 0.97f, 1f);
                    style.reverb = AudioReverbPreset.Livingroom;
                    break;
                case RoomKind.Conference:
                    style.floor = SurfaceKind.Carpet; style.footsteps = Surface.Carpet; style.light = new Color(1f, 0.93f, 0.82f);
                    style.reverb = AudioReverbPreset.Livingroom;
                    break;
                case RoomKind.Hallway:
                    style.floor = SurfaceKind.Tile; style.footsteps = Surface.Tile; style.light = new Color(0.92f, 0.95f, 0.98f);
                    style.lightStrength = 0.8f; style.reverb = AudioReverbPreset.Hallway; style.flickerChance = 0.18f;
                    break;
                case RoomKind.Storage:
                    style.floor = SurfaceKind.DirtyConcrete; style.light = new Color(1f, 0.82f, 0.58f);
                    style.lightStrength = 0.55f; style.flickerChance = 0.35f; style.reverb = AudioReverbPreset.Room;
                    break;
                case RoomKind.Security:
                    style.floor = SurfaceKind.Carpet; style.footsteps = Surface.Carpet; style.light = new Color(0.62f, 0.76f, 1f);
                    style.lightStrength = 0.75f; style.reverb = AudioReverbPreset.Room; style.flickerChance = 0f;
                    break;
                case RoomKind.Restroom:
                    style.floor = SurfaceKind.Tile; style.footsteps = Surface.Tile; style.light = new Color(0.85f, 1f, 0.98f);
                    style.reverb = AudioReverbPreset.Bathroom; style.flickerChance = 0.2f;
                    break;
                case RoomKind.Breakroom:
                    style.floor = SurfaceKind.Tile; style.footsteps = Surface.Tile; style.light = new Color(1f, 0.88f, 0.7f);
                    break;
                case RoomKind.Utility:
                    style.floor = SurfaceKind.DirtyConcrete; style.light = new Color(0.86f, 1f, 0.84f);
                    style.lightStrength = 0.7f; style.flickerChance = 0.45f;
                    break;
                case RoomKind.Warehouse:
                    style.floor = SurfaceKind.DirtyConcrete; style.light = new Color(1f, 0.86f, 0.62f);
                    style.lightStrength = 1.2f; style.reverb = AudioReverbPreset.Hangar; style.flickerChance = 0.08f;
                    break;
                case RoomKind.Residential:
                    style.floor = SurfaceKind.Wood; style.footsteps = Surface.Wood; style.light = new Color(1f, 0.84f, 0.62f);
                    style.lightStrength = 0.85f; style.reverb = AudioReverbPreset.Livingroom; style.hum = false; style.flickerChance = 0.03f;
                    break;
                case RoomKind.Stairwell:
                    style.floor = SurfaceKind.Concrete; style.light = new Color(0.95f, 0.95f, 0.9f);
                    style.lightStrength = 0.7f; style.reverb = AudioReverbPreset.StoneCorridor; style.flickerChance = 0.25f;
                    break;
                case RoomKind.Range:
                    style.floor = SurfaceKind.Concrete; style.lightStrength = 1.2f; style.reverb = AudioReverbPreset.Hangar;
                    break;
                case RoomKind.Garage:
                    style.floor = SurfaceKind.DirtyConcrete; style.reverb = AudioReverbPreset.Hangar;
                    break;
                case RoomKind.Medical:
                    style.floor = SurfaceKind.Tile; style.footsteps = Surface.Tile; style.light = new Color(0.9f, 0.97f, 1f);
                    style.lightStrength = 1.15f; style.reverb = AudioReverbPreset.Room; style.flickerChance = 0.06f;
                    break;
                case RoomKind.Club:
                    style.floor = SurfaceKind.Wood; style.footsteps = Surface.Wood; style.light = new Color(0.75f, 0.45f, 1f);
                    style.lightStrength = 0.6f; style.reverb = AudioReverbPreset.Auditorium; style.hum = false; style.flickerChance = 0.12f;
                    break;
                case RoomKind.Vault:
                    style.floor = SurfaceKind.Metal; style.footsteps = Surface.Metal; style.light = new Color(0.85f, 0.92f, 1f);
                    style.lightStrength = 0.8f; style.reverb = AudioReverbPreset.Stoneroom; style.flickerChance = 0f;
                    break;
                case RoomKind.Retail:
                    style.floor = SurfaceKind.Tile; style.footsteps = Surface.Tile; style.light = new Color(0.96f, 1f, 1f);
                    style.lightStrength = 1.25f; style.reverb = AudioReverbPreset.Room; style.flickerChance = 0.04f;
                    break;
            }
            return style;
        }

        public static SurfaceKind ForProp(string name)
        {
            string key = (name ?? "").ToLowerInvariant();
            if (Has(key, "desk", "table", "crate", "pallet", "bench", "counter", "bed", "wardrobe")) return SurfaceKind.Wood;
            if (Has(key, "shelf", "rack", "locker", "cabinet", "machine", "washer", "dryer", "panel", "container", "dumpster", "forklift", "rail", "barrier", "ac unit", "vent")) return SurfaceKind.Metal;
            if (Has(key, "couch", "sofa", "chair", "seat", "armchair")) return SurfaceKind.Fabric;
            if (Has(key, "car", "truck", "van", "plant", "bin", "cooler", "printer", "monitor")) return SurfaceKind.Plastic;
            if (Has(key, "planter", "pillar", "column", "kerb", "block", "barricade", "step")) return SurfaceKind.Concrete;
            return SurfaceKind.Plain;
        }

        public static Surface ImpactFor(SurfaceKind kind)
        {
            switch (kind)
            {
                case SurfaceKind.Metal: return Surface.Metal;
                case SurfaceKind.Wood: return Surface.Wood;
                case SurfaceKind.Glass: return Surface.Glass;
                case SurfaceKind.Carpet:
                case SurfaceKind.Fabric: return Surface.Carpet;
                default: return Surface.Concrete;
            }
        }
    }
}
