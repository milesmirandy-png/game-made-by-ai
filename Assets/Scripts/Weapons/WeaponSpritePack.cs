using System.Collections.Generic;

namespace Swat
{
    // The weapon icons are side-view pixel-art guns from a free-to-use sprite
    // pack the project owner supplied (see CREDITS.md), shown pixel for pixel.
    // This class knows where each gun's muzzle, optic rail and light mount are,
    // and draws the loadout's suppressor, red dot and weapon light onto a copy
    // of the sprite in the pack's own colours. Pure C# with no Unity types, so
    // it can be checked outside the editor; WeaponSprites loads the images.
    public static class WeaponSpritePack
    {
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
            Mounts m;
            if (gun == null || weaponId == null || !mounts.TryGetValue(weaponId, out m)) return gun;
            suppressor &= m.muzzleX >= 0;
            optic &= m.opticX >= 0;
            light &= m.lightX >= 0;
            if (!suppressor && !optic && !light) return gun;

            // Where each part goes, in sprite pixels.
            int sx = m.muzzleX - 1, sy = m.muzzleY - Suppressor.Length / 2;
            int ox = m.opticX - RedDot[0].Length / 2, oy = m.opticY - (RedDot.Length - 1);
            int lx = m.lightX - Light[0].Length / 2, ly = m.lightY - 1;

            int minX = 0, minY = 0, maxX = gun.Width - 1, maxY = gun.Height - 1;
            if (suppressor) Grow(ref minX, ref minY, ref maxX, ref maxY, sx, sy, Suppressor);
            if (optic) Grow(ref minX, ref minY, ref maxX, ref maxY, ox, oy, RedDot);
            if (light) Grow(ref minX, ref minY, ref maxX, ref maxY, lx, ly, Light);

            var image = new WeaponSpriteArt.Image { Width = maxX - minX + 1, Height = maxY - minY + 1 };
            image.Pixels = new uint[image.Width * image.Height];
            for (int y = 0; y < gun.Height; y++)
                for (int x = 0; x < gun.Width; x++)
                    image.Pixels[(y - minY) * image.Width + x - minX] = gun.Pixels[y * gun.Width + x];
            if (light) Paste(image, lx - minX, ly - minY, Light);
            if (optic) Paste(image, ox - minX, oy - minY, RedDot);
            if (suppressor) Paste(image, sx - minX, sy - minY, Suppressor);
            return image;
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
