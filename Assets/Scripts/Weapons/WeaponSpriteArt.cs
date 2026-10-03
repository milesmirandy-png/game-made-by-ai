using System;

namespace Swat
{
    // Side-view pixel-art weapon sprites, drawn by code (no image files): chunky
    // parts with three or four shades each (light top edge, dark bottom edge),
    // blue-grey steel, coloured furniture taken from the weapon's accent colour,
    // a dark outline and a white "sticker" border. Muzzle on the left, stock on
    // the right. Pure C# with no Unity types, so the same code can be run
    // outside the editor to check what it draws; WeaponSprites turns the result
    // into textures.
    public static class WeaponSpriteArt
    {
        public sealed class Image
        {
            public int Width, Height;
            public uint[] Pixels; // RGBA packed as 0xRRGGBBAA, row 0 at the top
        }

        public struct Options
        {
            public float r, g, b;       // furniture (stock, grip, handguard) colour
            public bool suppressor, optic, light;
        }

        struct Mat
        {
            public uint Hi, Light, Mid, Dark;
        }

        const uint Empty = 0u, Hole = 1u;
        static readonly uint Outline = Pack(0.07f, 0.08f, 0.12f);
        static readonly uint Border = Pack(1f, 1f, 1f);
        static readonly Mat Steel = Make(0.25f, 0.3f, 0.42f);
        static readonly Mat DarkSteel = Make(0.15f, 0.17f, 0.23f);
        static readonly Mat Polymer = Make(0.17f, 0.19f, 0.24f);
        static readonly Mat Orange = Make(0.92f, 0.45f, 0.1f);
        static readonly Mat Yellow = Make(0.95f, 0.78f, 0.12f);
        static readonly uint Glass = Pack(0.38f, 0.72f, 1f);
        static readonly uint RedDot = Pack(1f, 0.25f, 0.2f);

        // ---- Canvas ----

        sealed class Canvas
        {
            public const int W = 112, H = 40;
            public readonly uint[] Px = new uint[W * H];
            public int OX = 6, OY = 6; // room for muzzle devices and the borders

            public void Set(int x, int y, uint c)
            {
                x += OX;
                y += OY;
                if (x < 0 || y < 0 || x >= W || y >= H) return;
                Px[y * W + x] = c;
            }

            public uint Get(int x, int y)
            {
                x += OX;
                y += OY;
                if (x < 0 || y < 0 || x >= W || y >= H) return Empty;
                return Px[y * W + x];
            }

            // A horizontal part: light top row, dark bottom row.
            public void Fill(int x, int y, int w, int h, Mat m)
            {
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                        Set(x + i, y + j, h == 1 ? m.Mid : j == 0 ? m.Light : j == h - 1 ? m.Dark : m.Mid);
            }

            // A vertical part (grip, magazine): light left column, dark right column and bottom.
            // Each row moves sideways so the part leans: shift > 0 leans the bottom toward the stock.
            public void Lean(int x, int y, int w, int h, int shift, Mat m)
            {
                for (int j = 0; j < h; j++)
                {
                    int offset = h > 1 ? (int)Math.Round(j * shift / (double)(h - 1)) : 0;
                    for (int i = 0; i < w; i++)
                        Set(x + offset + i, y + j, i == 0 ? m.Light : (i == w - 1 || j == h - 1) ? m.Dark : m.Mid);
                }
            }

            // A part whose height changes along its length (stocks): top edge fixed.
            public void Taper(int x, int y, int w, int h0, int h1, Mat m)
            {
                for (int i = 0; i < w; i++)
                {
                    int h = (int)Math.Round(h0 + (h1 - h0) * (w > 1 ? i / (double)(w - 1) : 0.0));
                    for (int j = 0; j < h; j++)
                        Set(x + i, y + j, j == 0 ? m.Light : j == h - 1 ? m.Dark : m.Mid);
                }
            }

            // Same with the bottom edge fixed at y + h0 - 1 on the left (stocks that rise toward the butt).
            public void TaperUp(int x, int bottom, int w, int h0, int h1, Mat m)
            {
                for (int i = 0; i < w; i++)
                {
                    int h = (int)Math.Round(h0 + (h1 - h0) * (w > 1 ? i / (double)(w - 1) : 0.0));
                    for (int j = 0; j < h; j++)
                        Set(x + i, bottom - h + 1 + j, j == 0 ? m.Light : j == h - 1 ? m.Dark : m.Mid);
                }
            }

            public void HLine(int x, int y, int w, uint c) { for (int i = 0; i < w; i++) Set(x + i, y, c); }
            public void VLine(int x, int y, int h, uint c) { for (int j = 0; j < h; j++) Set(x, y + j, c); }

            // A see-through gap (trigger guard, stock frame); the outline pass leaves it alone.
            public void Cut(int x, int y, int w, int h)
            {
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                        Set(x + i, y + j, Hole);
            }

            // A dark dotted row (vents, grip texture, rail teeth).
            public void Dots(int x, int y, int w, int step, uint c)
            {
                for (int i = 0; i < w; i += step) Set(x + i, y, c);
            }
        }

        // ---- Shared parts ----

        static void Barrel(Canvas c, int x, int y, int length, int thickness)
        {
            c.Fill(x, y, length, thickness, Steel);
            c.Set(x, y, DarkSteel.Dark); // bore
        }

        static void FrontSight(Canvas c, int x, int y)
        {
            c.VLine(x, y - 2, 2, DarkSteel.Mid);
        }

        static void TriggerGuard(Canvas c, int x, int y, int w)
        {
            // A small loop under the receiver: sides and bottom in dark steel, open in the middle.
            c.VLine(x, y, 3, DarkSteel.Mid);
            c.HLine(x, y + 3, w, DarkSteel.Dark);
            c.Cut(x + 1, y, w - 1, 3);
            c.Set(x + 2, y, DarkSteel.Light); // trigger
            c.Set(x + 2, y + 1, DarkSteel.Light);
        }

        static void Rail(Canvas c, int x, int y, int w)
        {
            c.Fill(x, y, w, 1, DarkSteel);
            c.Dots(x, y - 1, w, 2, DarkSteel.Mid);
        }

        static void Glint(Canvas c, int x, int y, int w)
        {
            c.HLine(x, y, w, Steel.Hi);
        }

        // ---- Entry point ----

        public static Image Draw(WeaponCategory category, Options o)
        {
            var c = new Canvas();
            var wood = Make(o.r, o.g, o.b);
            // Space at the muzzle for a suppressor: the whole gun moves right.
            int suppressor = o.suppressor ? 10 : 0;
            c.OX += suppressor;
            int muzzleX = 0, barrelY = 0, railX = -1, railY = 0, railW = 0, lightX = -1, lightY = 0;
            switch (category)
            {
                case WeaponCategory.CompactSMG:
                    // Boxy steel receiver with the magazine inside the grip.
                    Barrel(c, 6, 11, 6, 2);
                    c.Fill(12, 8, 30, 7, Steel);
                    Glint(c, 13, 9, 20);
                    c.HLine(16, 11, 10, Steel.Dark);
                    c.Fill(30, 6, 3, 2, DarkSteel);
                    c.Fill(14, 6, 2, 2, DarkSteel);
                    c.Lean(22, 15, 6, 14, 1, Polymer);
                    c.Lean(23, 21, 4, 9, 1, DarkSteel);
                    TriggerGuard(c, 17, 15, 5);
                    c.Fill(42, 9, 8, 3, DarkSteel);
                    c.VLine(49, 9, 5, DarkSteel.Mid);
                    muzzleX = 6; barrelY = 11; railX = 18; railY = 7; railW = 0;
                    break;

                case WeaponCategory.SMG:
                    // Slim handguard, tall receiver, slightly curved magazine, wire stock.
                    Barrel(c, 4, 11, 8, 2);
                    FrontSight(c, 12, 9);
                    c.Fill(10, 9, 12, 5, Polymer);
                    c.Dots(12, 11, 9, 2, Polymer.Dark);
                    c.Fill(22, 7, 22, 7, Steel);
                    Glint(c, 23, 8, 14);
                    c.HLine(30, 10, 6, DarkSteel.Dark);
                    c.Lean(26, 14, 4, 11, -2, DarkSteel);
                    c.Lean(36, 14, 5, 9, 2, Polymer);
                    TriggerGuard(c, 31, 14, 5);
                    // Folding wire stock: a frame with a gap in the middle.
                    c.Fill(44, 8, 18, 6, DarkSteel);
                    c.Cut(46, 10, 14, 2);
                    c.Fill(60, 7, 3, 8, Polymer);
                    muzzleX = 4; barrelY = 11; railX = 24; railY = 6; railW = 14; lightX = 12; lightY = 14;
                    break;

                case WeaponCategory.CompactRifle:
                case WeaponCategory.Rifle:
                case WeaponCategory.BurstRifle:
                {
                    bool full = category == WeaponCategory.Rifle;
                    bool burst = category == WeaponCategory.BurstRifle;
                    int barrel = full ? 18 : burst ? 14 : 10;
                    int x = 2 + (full ? 0 : burst ? 4 : 8);
                    Barrel(c, x, 11, barrel, 2);
                    FrontSight(c, x + 4, 9);
                    c.HLine(x + 6, 10, barrel - 2, DarkSteel.Mid); // gas tube
                    int guard = x + barrel - 2;
                    c.Fill(guard, 9, 16, 5, wood);
                    c.Dots(guard + 2, 11, 12, 3, wood.Dark);
                    c.Fill(guard + 16, 8, 22, 6, Steel);
                    Glint(c, guard + 17, 9, 12);
                    c.HLine(guard + 26, 11, 5, DarkSteel.Dark);
                    c.Set(guard + 17, 7, DarkSteel.Mid);
                    // Curved magazine leaning toward the muzzle; pistol grip leaning back.
                    if (burst) c.Lean(guard + 19, 14, 5, 9, -1, Polymer);
                    else c.Lean(guard + 19, 14, 5, 10, -3, full ? DarkSteel : Steel);
                    c.Lean(guard + 30, 14, 4, 8, 2, wood);
                    TriggerGuard(c, guard + 25, 14, 4);
                    int stock = guard + 38;
                    if (full) c.Taper(stock, 9, 22, 5, 9, wood);
                    else if (burst)
                    {
                        c.Fill(stock, 9, 18, 6, Polymer);
                        c.Fill(stock + 18, 8, 2, 8, DarkSteel);
                    }
                    else
                    {
                        // Collapsible stock: a tube and a short butt.
                        c.Fill(stock, 9, 8, 3, DarkSteel);
                        c.Taper(stock + 8, 8, 10, 6, 8, wood);
                    }
                    muzzleX = x; barrelY = 11; railX = guard + 17; railY = 7; railW = burst || !full ? 14 : 0; lightX = guard + 4; lightY = 14;
                    break;
                }

                case WeaponCategory.Bullpup:
                    // Magazine behind the grip, carry rail on top, one long body.
                    Barrel(c, 4, 11, 12, 2);
                    FrontSight(c, 8, 9);
                    c.Fill(16, 8, 50, 8, wood);
                    c.HLine(16, 8, 50, wood.Light);
                    c.HLine(20, 11, 12, wood.Dark);
                    c.Fill(40, 9, 12, 3, Steel);
                    Glint(c, 41, 9, 8);
                    c.Lean(26, 16, 4, 8, 2, Polymer);
                    TriggerGuard(c, 21, 16, 4);
                    c.Lean(44, 16, 5, 8, -1, DarkSteel);
                    c.Fill(62, 7, 4, 10, DarkSteel);
                    muzzleX = 4; barrelY = 11; railX = 20; railY = 7; railW = 18; lightX = 18; lightY = 16;
                    break;

                case WeaponCategory.Carbine:
                    // Long wooden stock running most of the way to the muzzle.
                    Barrel(c, 2, 10, 22, 2);
                    FrontSight(c, 5, 8);
                    c.Fill(22, 9, 22, 4, wood);
                    c.Fill(44, 8, 14, 5, Steel);
                    Glint(c, 45, 9, 8);
                    c.HLine(48, 10, 6, DarkSteel.Dark);
                    c.Fill(46, 13, 5, 3, DarkSteel);
                    TriggerGuard(c, 52, 13, 4);
                    c.Taper(58, 9, 26, 5, 9, wood);
                    c.HLine(58, 12, 8, wood.Dark);
                    c.Fill(84, 9, 2, 9, DarkSteel);
                    muzzleX = 2; barrelY = 10; railX = 44; railY = 7; railW = 0;
                    break;

                case WeaponCategory.Marksman:
                    // Long barrel, scope, box magazine, stock with a cheek rest.
                    Barrel(c, 2, 11, 26, 2);
                    c.Fill(26, 9, 16, 5, wood);
                    c.Dots(28, 11, 12, 3, wood.Dark);
                    c.Fill(42, 8, 18, 6, Steel);
                    Glint(c, 43, 9, 10);
                    c.Fill(46, 14, 5, 6, DarkSteel);
                    c.Lean(54, 14, 4, 8, 2, wood);
                    TriggerGuard(c, 50, 14, 4);
                    c.Taper(60, 9, 22, 5, 9, wood);
                    c.Fill(62, 7, 12, 2, wood);
                    c.Fill(82, 8, 2, 10, DarkSteel);
                    // Scope with glass at both ends.
                    c.Fill(38, 4, 22, 3, DarkSteel);
                    c.Fill(36, 3, 3, 5, DarkSteel);
                    c.Fill(58, 3, 3, 5, DarkSteel);
                    c.VLine(36, 4, 3, Glass);
                    c.VLine(46, 7, 1, DarkSteel.Mid);
                    c.VLine(52, 7, 1, DarkSteel.Mid);
                    muzzleX = 2; barrelY = 11; railX = -1;
                    break;

                case WeaponCategory.LMG:
                    // Heavy barrel with bipod, carry handle, big box magazine.
                    Barrel(c, 2, 10, 22, 3);
                    FrontSight(c, 5, 8);
                    c.VLine(8, 13, 9, DarkSteel.Mid);
                    c.VLine(11, 13, 9, DarkSteel.Mid);
                    c.Set(7, 21, DarkSteel.Dark);
                    c.Set(12, 21, DarkSteel.Dark);
                    c.Fill(22, 9, 14, 5, Polymer);
                    c.Dots(24, 11, 10, 2, Polymer.Dark);
                    c.Fill(36, 7, 24, 7, Steel);
                    Glint(c, 37, 8, 16);
                    c.Fill(40, 4, 12, 2, DarkSteel);
                    c.VLine(40, 5, 2, DarkSteel.Mid);
                    c.VLine(51, 5, 2, DarkSteel.Mid);
                    c.Fill(38, 14, 12, 8, wood);
                    c.HLine(38, 17, 12, wood.Dark);
                    c.Lean(54, 14, 4, 8, 2, Polymer);
                    TriggerGuard(c, 50, 14, 4);
                    c.Taper(60, 8, 22, 6, 9, Polymer);
                    c.Fill(82, 8, 2, 9, DarkSteel);
                    muzzleX = 2; barrelY = 10; railX = -1;
                    break;

                case WeaponCategory.Shotgun:
                    // Barrel over a tube magazine, ribbed pump, wooden stock that drops at the butt.
                    Barrel(c, 2, 9, 30, 2);
                    c.Fill(4, 11, 26, 2, DarkSteel);
                    FrontSight(c, 3, 9);
                    c.Fill(12, 11, 14, 4, wood);
                    c.Dots(13, 12, 12, 2, wood.Dark);
                    c.Fill(32, 8, 16, 6, Steel);
                    Glint(c, 33, 9, 10);
                    c.HLine(36, 11, 6, DarkSteel.Dark);
                    TriggerGuard(c, 40, 14, 4);
                    c.Taper(48, 9, 30, 5, 10, wood);
                    c.Fill(78, 9, 2, 10, DarkSteel);
                    muzzleX = 2; barrelY = 9; railX = 33; railY = 7; railW = 0;
                    break;

                case WeaponCategory.AutoShotgun:
                    // Boxy body, straight box magazine, muzzle brake.
                    c.Fill(2, 9, 4, 4, DarkSteel);
                    Barrel(c, 6, 10, 14, 2);
                    c.Fill(18, 8, 16, 6, Polymer);
                    c.Dots(20, 10, 12, 2, Polymer.Dark);
                    c.Fill(34, 7, 18, 7, Steel);
                    Glint(c, 35, 8, 12);
                    c.Fill(36, 14, 7, 9, DarkSteel);
                    c.HLine(36, 17, 7, DarkSteel.Light);
                    c.HLine(36, 20, 7, DarkSteel.Light);
                    c.Lean(47, 14, 4, 8, 2, wood);
                    TriggerGuard(c, 43, 14, 4);
                    c.Fill(52, 8, 18, 6, wood);
                    c.Fill(70, 7, 2, 9, DarkSteel);
                    muzzleX = 2; barrelY = 10; railX = 35; railY = 6; railW = 14; lightX = 22; lightY = 14;
                    break;

                case WeaponCategory.PDW:
                    // Smooth polymer body with a magazine along the top and a thumbhole grip.
                    Barrel(c, 6, 11, 6, 2);
                    c.Fill(12, 7, 42, 11, wood);
                    c.HLine(12, 7, 42, wood.Light);
                    c.Fill(16, 5, 30, 3, Polymer);
                    c.HLine(17, 5, 28, Polymer.Light);
                    c.Cut(22, 13, 7, 4);
                    c.Cut(38, 12, 6, 6);
                    c.Fill(54, 8, 3, 10, DarkSteel);
                    c.HLine(14, 10, 6, wood.Dark);
                    muzzleX = 6; barrelY = 11; railX = -1;
                    break;

                case WeaponCategory.LessLethal:
                    // Fat launcher tube with an orange muzzle band.
                    c.Fill(4, 7, 22, 7, DarkSteel);
                    c.HLine(4, 7, 22, DarkSteel.Light);
                    c.Fill(2, 7, 3, 7, Orange);
                    c.Fill(26, 8, 14, 6, Steel);
                    Glint(c, 27, 9, 8);
                    c.Lean(30, 14, 4, 8, 2, Orange);
                    TriggerGuard(c, 26, 14, 4);
                    c.Fill(40, 9, 14, 4, DarkSteel);
                    c.Cut(42, 10, 10, 2);
                    c.Fill(54, 8, 3, 7, Orange);
                    muzzleX = 2; barrelY = 10; railX = 27; railY = 7; railW = 10;
                    break;

                case WeaponCategory.Pepperball:
                    // Thin barrel, round hopper on top showing the coloured rounds.
                    Barrel(c, 4, 11, 12, 2);
                    c.Fill(16, 9, 26, 5, Polymer);
                    c.Fill(20, 2, 14, 7, wood);
                    c.HLine(22, 1, 10, wood.Light);
                    c.HLine(23, 4, 8, Pack(0.85f, 0.35f, 0.95f));
                    c.HLine(23, 6, 8, Pack(0.85f, 0.35f, 0.95f));
                    c.Lean(32, 14, 4, 8, 2, Polymer);
                    TriggerGuard(c, 27, 14, 4);
                    c.Fill(42, 9, 12, 4, wood);
                    c.Fill(54, 8, 2, 7, DarkSteel);
                    muzzleX = 4; barrelY = 11; railX = -1;
                    break;

                case WeaponCategory.MachinePistol:
                    // Pistol with a long magazine and a compensator.
                    c.Fill(4, 8, 3, 4, DarkSteel);
                    c.Fill(7, 8, 22, 4, Steel);
                    Glint(c, 8, 8, 14);
                    c.Dots(23, 9, 6, 2, Steel.Dark);
                    c.Fill(8, 12, 19, 3, Polymer);
                    c.Lean(19, 15, 6, 8, 2, Polymer);
                    c.Lean(20, 22, 4, 8, 1, DarkSteel);
                    TriggerGuard(c, 12, 15, 5);
                    muzzleX = 4; barrelY = 9; railX = -1;
                    break;

                case WeaponCategory.Revolver:
                    // Round barrel with an ejector rod, fluted cylinder, curved wooden grip and hammer.
                    Barrel(c, 2, 8, 20, 3);
                    c.HLine(6, 11, 14, DarkSteel.Mid);
                    FrontSight(c, 3, 8);
                    c.Fill(20, 7, 10, 7, Steel);
                    c.VLine(23, 8, 5, Steel.Dark);
                    c.VLine(26, 8, 5, Steel.Dark);
                    Glint(c, 21, 7, 8);
                    c.Fill(30, 7, 6, 6, Steel);
                    c.Set(35, 6, DarkSteel.Mid);
                    c.Set(36, 5, DarkSteel.Mid);
                    c.Lean(31, 13, 6, 11, 3, wood);
                    TriggerGuard(c, 26, 13, 4);
                    muzzleX = 2; barrelY = 9; railX = -1;
                    break;

                case WeaponCategory.StunPistol:
                    // Chunky body with a yellow cartridge at the front.
                    c.Fill(4, 7, 8, 7, Yellow);
                    c.HLine(4, 9, 8, Yellow.Dark);
                    c.Fill(12, 7, 16, 7, Polymer);
                    c.HLine(13, 8, 12, Yellow.Mid);
                    c.Lean(18, 14, 6, 9, 2, Polymer);
                    c.HLine(19, 17, 5, Yellow.Mid);
                    TriggerGuard(c, 13, 14, 5);
                    muzzleX = 4; barrelY = 10; railX = -1;
                    break;

                case WeaponCategory.HeavyPistol:
                    c.Fill(4, 7, 28, 6, Steel);
                    Glint(c, 5, 7, 18);
                    c.Dots(25, 9, 7, 2, Steel.Dark);
                    FrontSight(c, 5, 7);
                    c.Set(30, 6, DarkSteel.Mid);
                    c.Fill(6, 13, 24, 3, DarkSteel);
                    c.Lean(21, 16, 7, 10, 3, wood);
                    c.Dots(23, 19, 4, 2, wood.Dark);
                    TriggerGuard(c, 13, 16, 6);
                    muzzleX = 4; barrelY = 9; railX = -1;
                    break;

                default: // service and backup pistols
                {
                    bool backup = category == WeaponCategory.BackupPistol;
                    int length = backup ? 18 : 24;
                    c.Fill(4, 8, length, 5, Steel);
                    Glint(c, 5, 8, length - 8);
                    c.Dots(4 + length - 6, 10, 6, 2, Steel.Dark);
                    FrontSight(c, 5, 8);
                    c.Set(4 + length - 2, 7, DarkSteel.Mid);
                    c.Fill(6, 13, length - 4, 3, Polymer);
                    c.Lean(4 + length - 10, 16, 6, backup ? 7 : 9, 2, Polymer);
                    TriggerGuard(c, 11, 16, 5);
                    muzzleX = 4; barrelY = 9; railX = -1;
                    break;
                }
            }

            if (o.suppressor)
            {
                int x = muzzleX - 10;
                c.Fill(x, barrelY - 1, 11, 4, DarkSteel);
                c.HLine(x + 1, barrelY - 1, 9, DarkSteel.Light);
                c.VLine(x + 7, barrelY, 2, Steel.Mid);
            }
            if (o.optic && railX >= 0)
            {
                if (railW > 0) Rail(c, railX, railY, railW);
                int ox = railX + Math.Max(2, railW / 2 - 3);
                c.Fill(ox, railY - 4, 7, 4, DarkSteel);
                c.Set(ox, railY - 3, Glass);
                c.Set(ox + 6, railY - 3, RedDot);
            }
            else if (railW > 0) Rail(c, railX, railY, railW);
            if (o.light && lightX >= 0)
            {
                c.Fill(lightX, lightY, 6, 2, DarkSteel);
                c.Set(lightX, lightY, Pack(1f, 0.95f, 0.7f));
            }
            return Finish(c);
        }

        // Dark outline around every part, then a white border around that, then crop.
        static Image Finish(Canvas c)
        {
            int w = Canvas.W, h = Canvas.H;
            var px = c.Px;
            var outlined = (uint[])px.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x] != Empty) continue;
                    if (Solid(px, w, h, x - 1, y) || Solid(px, w, h, x + 1, y) || Solid(px, w, h, x, y - 1) || Solid(px, w, h, x, y + 1))
                        outlined[y * w + x] = Outline;
                }
            var bordered = (uint[])outlined.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (outlined[y * w + x] != Empty) continue;
                    bool near = false;
                    for (int dy = -1; dy <= 1 && !near; dy++)
                        for (int dx = -1; dx <= 1 && !near; dx++)
                            if ((dx != 0 || dy != 0) && Solid(outlined, w, h, x + dx, y + dy)) near = true;
                    if (near) bordered[y * w + x] = Border;
                }

            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    uint p = bordered[y * w + x];
                    if (p == Empty || p == Hole) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            if (maxX < 0) return new Image { Width = 1, Height = 1, Pixels = new uint[1] };
            var image = new Image { Width = maxX - minX + 1, Height = maxY - minY + 1 };
            image.Pixels = new uint[image.Width * image.Height];
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    uint p = bordered[(y + minY) * w + x + minX];
                    image.Pixels[y * image.Width + x] = p == Hole ? Empty : p;
                }
            return image;
        }

        static bool Solid(uint[] px, int w, int h, int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return false;
            uint p = px[y * w + x];
            return p != Empty && p != Hole;
        }

        // ---- Colour ----

        // Four shades from one colour; shadows lean blue and highlights lean warm, as in hand-drawn pixel art.
        static Mat Make(float r, float g, float b)
        {
            return new Mat
            {
                Hi = Pack(Mix(r * 1.7f, 1f, 0.35f), Mix(g * 1.7f, 0.97f, 0.35f), Mix(b * 1.7f, 0.9f, 0.35f)),
                Light = Pack(Mix(r * 1.35f, 1f, 0.1f), Mix(g * 1.35f, 0.95f, 0.1f), Mix(b * 1.35f, 0.85f, 0.1f)),
                Mid = Pack(r, g, b),
                Dark = Pack(Mix(r * 0.58f, 0.1f, 0.25f), Mix(g * 0.58f, 0.1f, 0.25f), Mix(b * 0.58f, 0.25f, 0.25f)),
            };
        }

        static float Mix(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        static uint Pack(float r, float g, float b)
        {
            uint R = (uint)Math.Round(Math.Max(0f, Math.Min(1f, r)) * 255f);
            uint G = (uint)Math.Round(Math.Max(0f, Math.Min(1f, g)) * 255f);
            uint B = (uint)Math.Round(Math.Max(0f, Math.Min(1f, b)) * 255f);
            return (R << 24) | (G << 16) | (B << 8) | 0xFFu;
        }
    }
}
