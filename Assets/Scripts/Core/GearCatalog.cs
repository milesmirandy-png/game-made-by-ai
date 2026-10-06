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
        public enum Patch { None, Badge, Stripes, Chevron, Star, Cross, Diamond, Target }

        public static readonly string[] HeadgearNames =
        {
            "Helmet, NVG and goggles", "Helmet and goggles", "Helmet", "Helmet and face shield",
            "Ops cap", "Beanie", "Boonie hat", "Bare head",
        };
        public static readonly string[] FaceNames = { "Balaclava", "Bare face", "Gas mask", "Shades" };
        public static readonly string[] PatchNames = { "None", "Unit badge", "Stripes", "Chevron", "Star", "Medic cross", "Diamond", "Target" };
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
