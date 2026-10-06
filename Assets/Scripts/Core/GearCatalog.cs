using UnityEngine;

namespace Swat
{
    // How the vest looks, from the armor you pick: no armor (barebones: shirt, belt and kneepads), a slick
    // plate carrier, the full carrier with pouches and pack, or that plus shoulder guards, collar and groin
    // protector. Standard is first so a default Appearance keeps the full kit.
    public enum ArmorStyle { Standard, Light, Heavy, None }

    // The looks an officer can pick in the loadout (Look tab): headgear, face, a patch and its colour.
    // They're cosmetic: protection comes from the armor. Indexes are stored in OfficerLoadout.
    public static class GearCatalog
    {
        public enum Headgear { HelmetFull, HelmetGoggles, Helmet, HelmetVisor, OpsCap, Beanie, Boonie, BareHead }
        public enum Face { Balaclava, Bare, GasMask, Shades }
        public enum Patch { None, Badge, Stripes, Chevron, Star, Cross, Diamond, Target, Flag }
        public enum FacialHair { None, Stubble, Beard, Moustache }

        public static readonly string[] HeadgearNames =
        {
            "Helmet, NVG and goggles", "Helmet and goggles", "Helmet", "Helmet and face shield",
            "Ops cap", "Beanie", "Boonie hat", "Bare head",
        };
        public static readonly string[] FaceNames = { "Balaclava", "Bare face", "Gas mask", "Shades" };
        public static readonly string[] PatchNames = { "None", "Unit badge", "Stripes", "Chevron", "Star", "Medic cross", "Diamond", "Target", "Flag" };
        public static readonly string[] FacialHairNames = { "None", "Stubble", "Beard", "Moustache" };
        public static readonly string[] HairColorNames = { "Black", "Dark brown", "Brown", "Blond", "Red", "Gray" };
        static readonly Color[] HairColors =
        {
            new Color(0.06f, 0.05f, 0.05f), new Color(0.16f, 0.1f, 0.06f), new Color(0.3f, 0.19f, 0.1f),
            new Color(0.62f, 0.5f, 0.3f), new Color(0.48f, 0.22f, 0.1f), new Color(0.5f, 0.5f, 0.5f),
        };

        public static Color HairColor(int index)
        {
            return HairColors[Mathf.Clamp(index, 0, HairColors.Length - 1)];
        }
        public static readonly string[] PatchColorNames = { "Red", "White", "Blue", "Yellow", "Green", "Orange", "Black", "Tan" };
        static readonly Color[] PatchColors =
        {
            new Color(0.8f, 0.14f, 0.12f), new Color(0.92f, 0.92f, 0.9f), new Color(0.2f, 0.42f, 0.85f), new Color(0.95f, 0.78f, 0.15f),
            new Color(0.3f, 0.7f, 0.25f), new Color(0.95f, 0.5f, 0.12f), new Color(0.08f, 0.08f, 0.09f), new Color(0.66f, 0.55f, 0.38f),
        };

        public static Color PatchColor(int index)
        {
            return PatchColors[Mathf.Clamp(index, 0, PatchColors.Length - 1)];
        }

        // ---- Camouflage for the camo uniforms (Progression.Kit sets the number) ----

        static ModelLibrary.Camo[] camos;

        public static ModelLibrary.Camo CamoFor(int camo)
        {
            if (camo <= 0) return null;
            if (camos == null)
            {
                string[] uniform = { "Shirt", "Pants" };
                camos = new[]
                {
                    // Arid: a multi-colour pattern, tan with khaki, olive and brown blotches.
                    new ModelLibrary.Camo { id = "arid", slots = uniform, scale = 0.07f, seed = 3, cuts = new[] { 0.42f, 0.56f, 0.66f },
                        colors = new[] { new Color(0.62f, 0.53f, 0.37f), new Color(0.71f, 0.62f, 0.45f), new Color(0.45f, 0.45f, 0.28f), new Color(0.44f, 0.31f, 0.19f) } },
                    new ModelLibrary.Camo { id = "woodland", slots = uniform, scale = 0.08f, seed = 11, cuts = new[] { 0.45f, 0.58f, 0.7f },
                        colors = new[] { new Color(0.33f, 0.38f, 0.22f), new Color(0.39f, 0.29f, 0.17f), new Color(0.19f, 0.25f, 0.14f), new Color(0.09f, 0.09f, 0.08f) } },
                    new ModelLibrary.Camo { id = "urban", slots = uniform, scale = 0.07f, seed = 5, cuts = new[] { 0.45f, 0.58f, 0.7f },
                        colors = new[] { new Color(0.5f, 0.51f, 0.52f), new Color(0.7f, 0.71f, 0.72f), new Color(0.32f, 0.33f, 0.35f), new Color(0.12f, 0.12f, 0.13f) } },
                    new ModelLibrary.Camo { id = "night", slots = uniform, scale = 0.07f, seed = 17, cuts = new[] { 0.46f, 0.62f },
                        colors = new[] { new Color(0.12f, 0.12f, 0.13f), new Color(0.2f, 0.2f, 0.22f), new Color(0.06f, 0.06f, 0.07f) } },
                };
            }
            return camos[Mathf.Clamp(camo - 1, 0, camos.Length - 1)];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            camos = null;
        }

        public static ArmorStyle StyleFor(ArmorData armor)
        {
            if (armor == null || armor.tier <= 0) return ArmorStyle.None;
            if (armor.tier == 1) return ArmorStyle.Light;
            return armor.tier >= 3 ? ArmorStyle.Heavy : ArmorStyle.Standard;
        }

        public static bool HasHelmet(int headgear)
        {
            return headgear <= (int)Headgear.HelmetVisor;
        }
    }
}
