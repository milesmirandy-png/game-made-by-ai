using System.Collections.Generic;

namespace Swat
{
    // The weapon icons are side-view pixel-art guns from a free-to-use sprite
    // pack the project owner supplied (see CREDITS.md), shown pixel for pixel.
    // This class knows where each gun's muzzle, optic rail and light mount are,
    // and draws the loadout's attachments (optics, muzzle devices, grips, light
    // and laser) onto a copy of the sprite in the pack's own colours. Pure C#
    // with no Unity types, so it can be checked outside the editor;
    // WeaponSprites loads the images. Magazines and stocks aren't drawn.
    public static class WeaponSpritePack
    {
        // What to draw. optic: 0 none, 1 red dot, 2 reflex, 3 holographic, 4 scope; muzzle: 0 none,
        // 1 suppressor, 2 flash hider, 3 compensator, 4 brake; grip: 0 none, 1 vertical, 2 angled.
        public struct Fitting
        {
            public int optic, muzzle, grip;
            public bool light, laser;
            public bool Any { get { return optic > 0 || muzzle > 0 || grip > 0 || light || laser; } }
            public string Key { get { return "o" + optic + "m" + muzzle + "g" + grip + (light ? "l" : "") + (laser ? "z" : ""); } }
        }

        // Sprite pixel positions, row 0 at the top; -1 where the gun has no such
        // mount (a built-in suppressor or scope, or a launcher).
        struct Mounts
        {
            public int muzzleX, muzzleY;   // last barrel column and the barrel's centre row
            public int opticX, opticY;     // centre column and the top row of the receiver there
            public int lightX, lightY;     // centre column and the first empty row under the handguard
        }

        static Mounts M(int muzzleX, int muzzleY, int opticX, int opticY, int lightX, int lightY)
        {
            return new Mounts { muzzleX = muzzleX, muzzleY = muzzleY, opticX = opticX, opticY = opticY, lightX = lightX, lightY = lightY };
        }

        static readonly Dictionary<string, Mounts> mounts = new Dictionary<string, Mounts>
        {
            { "smg_compact", M(54, 6, 20, 2, 48, 9) },
            { "smg_v10", M(103, 12, 66, 6, 96, 22) },
            { "pdw_x4", M(109, 10, 62, 4, 100, 15) },
            { "smg_kv", M(-1, -1, 47, 5, 64, 24) },
            { "rifle_compact", M(74, 10, 36, 2, 53, 17) },
            { "rifle_service", M(113, 9, 48, 2, 80, 15) },
            { "rifle_b4", M(114, 10, 72, 4, 78, 19) },
            { "rifle_cx", M(114, 14, 52, 2, 85, 22) },
            { "carbine_pc9", M(114, 11, -1, -1, 100, 16) },
            { "dmr_dm2", M(114, 14, -1, -1, 84, 18) },
            { "lmg_lm8", M(104, 27, 82, 23, 97, 32) },
            { "rotary_rg6", M(-1, -1, 33, 9, 95, 32) },
            { "shotgun_ts8", M(114, 5, 50, 2, 84, 13) },
            { "shotgun_as12", M(110, 14, 58, 7, 100, 16) },
            { "shotgun_d20", M(97, 12, 50, 0, 88, 18) },
            { "launcher_ll40", M(-1, -1, 80, 4, 110, 20) },
            { "pepper_pb3", M(-1, -1, 68, 4, 82, 20) },
            { "launcher_gl6", M(-1, -1, -1, -1, 106, 31) },
        };

        // The pack's palette: a near-black outline, charcoal steel ramp and the orange wood tones.
        static readonly Dictionary<char, uint> palette = new Dictionary<char, uint>
        {
            { 'K', 0x090909FFu }, { 'D', 0x292722FFu }, { 'M', 0x3D3F3BFFu }, { 'N', 0x51544FFFu },
            { 'O', 0x656A60FFu }, { 'P', 0x7E807BFFu }, { 'L', 0x969896FFu }, { 'H', 0xB0B2B0FFu },
            { 'r', 0x7A3215FFu }, { 'o', 0xC16320FFu }, { 'Y', 0xD88435FFu }, { 'y', 0xE2C063FFu },
        };

        // A banded can, its left end over the muzzle.
        static readonly string[] Suppressor =
        {
            " KKKKKKKKKKKKKKKKKK ",
            "KHLHLHLHLHLHLHLHLHPK",
            "KPPPPPPPPPPPPPPPPPOK",
            "KONONONONONONONONONK",
            "KMMMMMMMMMMMMMMMMMMK",
            "KDMDMDMDMDMDMDMDMDDK",
            " KKKKKKKKKKKKKKKKKK ",
        };

        // A tube sight on a low mount, a red glint on the lens.
        static readonly string[] RedDot =
        {
            " KKKKKKKK ",
            "KHLHLHLHrK",
            "KPPPPPPPoK",
            "KNONONONMK",
            " KKMMMMKK ",
            "  KDDDDK  ",
            "  KKKKKK  ",
        };

        // A tiny open sight: a frame round a red dot.
        static readonly string[] Reflex =
        {
            " KKKKKK ",
            "KM    MK",
            "KM  r MK",
            "KKMMMMKK",
            " KDDDDK ",
            " KKKKKK ",
        };

        // A boxy holographic sight with a wide window.
        static readonly string[] Holo =
        {
            " KKKKKKKKKK ",
            " KHLHLHLHLK ",
            "KK    r   KK",
            "KM        MK",
            "KNONONONONMK",
            " KDDDDDDDDK ",
            " KKKKKKKKKK ",
        };

        // A magnified scope on two rings, lens forward.
        static readonly string[] Scope =
        {
            "KKKK            KKKK",
            "KHLHKKKKKKKKKKKKHLHK",
            "KPPPLHLHLHLHLHLHPPPy",
            "KNONONONONONONONONMK",
            "KKKK KKMMKKKKMMKK KK",
            "      KDDK  KDDK    ",
            "      KKKK  KKKK    ",
        };

        // A short slotted flash hider.
        static readonly string[] FlashHider =
        {
            "KKKKKKK",
            "KMKMKMK",
            "KNNNNNK",
            "KDKDKDK",
            "KKKKKKK",
        };

        // A compensator with ports on top.
        static readonly string[] Compensator =
        {
            " KKKKKK ",
            "KHKLKHLK",
            "KPPPPPPK",
            "KNONONOK",
            "KMKMKMMK",
            " KKKKKK ",
        };

        // A wide muzzle brake with side ports.
        static readonly string[] Brake =
        {
            " KKKKKKK ",
            "KHLHLHLHK",
            "KKDKKDKKK",
            "KNONONONK",
            "KKDKKDKKK",
            "KMMMMMMMK",
            " KKKKKKK ",
        };

        // A vertical foregrip under the handguard.
        static readonly string[] VerticalGrip =
        {
            "KKKKK",
            "KMMMK",
            "KNMNK",
            "KNMNK",
            "KNMNK",
            "KNMNK",
            "KNMNK",
            "KNMNK",
            "KDDDK",
            " KKK ",
        };

        // An angled foregrip.
        static readonly string[] AngledGrip =
        {
            "KKKKKKKKK",
            "KPPPPPPMK",
            " KNNNNMK ",
            "  KMMDK  ",
            "   KKK   ",
        };

        // A small laser box, emitter forward.
        static readonly string[] Laser =
        {
            "KKKKKKKK",
            "KMNONONr",
            "KDMMMMMK",
            "KKKKKKKK",
        };

        // A short flashlight hanging from the handguard, lens forward.
        static readonly string[] Light =
        {
            "  KKKK    ",
            " KKMMKKKK ",
            "KHLHLHLHyK",
            "KNONONONYK",
            " KKKKKKKK ",
        };

        public static bool Has(string weaponId)
        {
            return weaponId != null && mounts.ContainsKey(weaponId);
        }

        // A copy of the sprite with the attachments drawn on, grown to fit them;
        // the sprite itself when nothing applies.
        public static WeaponSpriteArt.Image Fit(WeaponSpriteArt.Image gun, string weaponId, bool suppressor, bool optic, bool light)
        {
            return Fit(gun, weaponId, new Fitting { muzzle = suppressor ? 1 : 0, optic = optic ? 1 : 0, light = light });
        }

        public static WeaponSpriteArt.Image Fit(WeaponSpriteArt.Image gun, string weaponId, Fitting f)
        {
            Mounts m;
            if (gun == null || weaponId == null || !mounts.TryGetValue(weaponId, out m)) return gun;
            if (m.muzzleX < 0) f.muzzle = 0;
            if (m.opticX < 0) f.optic = 0;
            if (m.lightX < 0) { f.light = false; f.laser = false; f.grip = 0; }
            if (!f.Any) return gun;

            // Each part and where it goes, in sprite pixels: muzzle devices over the muzzle, optics on the
            // receiver, and under the handguard the grip, then the light, then the laser, front to back.
            var parts = new List<KeyValuePair<string[], int[]>>();
            if (f.light) parts.Add(Part(Light, m.lightX - Light[0].Length / 2, m.lightY - 1));
            if (f.grip > 0)
            {
                var art = f.grip == 1 ? VerticalGrip : AngledGrip;
                parts.Add(Part(art, m.lightX - (f.light ? 9 : 2) - art[0].Length / 2, m.lightY - 1));
            }
            if (f.laser) parts.Add(Part(Laser, m.lightX + (f.light ? 6 : 0) - (f.light ? 0 : Laser[0].Length / 2), m.lightY + (f.light ? 0 : -1)));
            if (f.optic > 0)
            {
                var art = f.optic == 2 ? Reflex : f.optic == 3 ? Holo : f.optic == 4 ? Scope : RedDot;
                parts.Add(Part(art, m.opticX - art[0].Length / 2, m.opticY - (art.Length - 1)));
            }
            if (f.muzzle > 0)
            {
                var art = f.muzzle == 2 ? FlashHider : f.muzzle == 3 ? Compensator : f.muzzle == 4 ? Brake : Suppressor;
                parts.Add(Part(art, m.muzzleX - 1, m.muzzleY - art.Length / 2));
            }

            int minX = 0, minY = 0, maxX = gun.Width - 1, maxY = gun.Height - 1;
            foreach (var part in parts) Grow(ref minX, ref minY, ref maxX, ref maxY, part.Value[0], part.Value[1], part.Key);

            var image = new WeaponSpriteArt.Image { Width = maxX - minX + 1, Height = maxY - minY + 1 };
            image.Pixels = new uint[image.Width * image.Height];
            for (int y = 0; y < gun.Height; y++)
                for (int x = 0; x < gun.Width; x++)
                    image.Pixels[(y - minY) * image.Width + x - minX] = gun.Pixels[y * gun.Width + x];
            foreach (var part in parts) Paste(image, part.Value[0] - minX, part.Value[1] - minY, part.Key);
            return image;
        }

        static KeyValuePair<string[], int[]> Part(string[] art, int left, int top)
        {
            return new KeyValuePair<string[], int[]>(art, new[] { left, top });
        }

        static void Grow(ref int minX, ref int minY, ref int maxX, ref int maxY, int x, int y, string[] art)
        {
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x + art[0].Length - 1 > maxX) maxX = x + art[0].Length - 1;
            if (y + art.Length - 1 > maxY) maxY = y + art.Length - 1;
        }

        static void Paste(WeaponSpriteArt.Image image, int left, int top, string[] art)
        {
            for (int row = 0; row < art.Length; row++)
                for (int column = 0; column < art[row].Length; column++)
                {
                    uint colour;
                    if (!palette.TryGetValue(art[row][column], out colour)) continue;
                    int x = left + column, y = top + row;
                    if (x < 0 || y < 0 || x >= image.Width || y >= image.Height) continue;
                    image.Pixels[y * image.Width + x] = colour;
                }
        }
    }
}
