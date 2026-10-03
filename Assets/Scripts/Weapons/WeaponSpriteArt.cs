using System;

namespace Swat
{
    // Side-view pixel-art weapon sprites drawn by code, the fallback for a
    // weapon with no sprite in the gun pack (see WeaponSprites): navy and slate
    // steel, coloured furniture with grain and hue-shifted shading, an outline in
    // a darker shade of each part's own colour and a white border. Muzzle on the
    // left, stock on the right (WeaponSprites mirrors it to match the pack). Pure
    // C# with no Unity types, so the same code can be run outside the editor.
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
            public uint Hi, Light, Mid, Shade, Dark, Line;
            public bool Grain;
        }

        const uint Empty = 0u, Hole = 1u;
        static readonly uint Border = Pack(1f, 1f, 1f);
        static readonly Mat Steel = Make(0.27f, 0.33f, 0.48f, false);
        static readonly Mat DarkSteel = Make(0.15f, 0.18f, 0.28f, false);
        static readonly Mat Polymer = Make(0.2f, 0.22f, 0.3f, false);
        static readonly Mat Teal = Make(0.16f, 0.36f, 0.42f, false);
        static readonly Mat Orange = Make(0.92f, 0.45f, 0.1f, false);
        static readonly Mat Yellow = Make(0.95f, 0.78f, 0.12f, false);
        static readonly uint Glass = Pack(0.45f, 0.8f, 1f);
        static readonly uint RedDot = Pack(1f, 0.25f, 0.25f);

        // ---- Canvas ----

        sealed class Canvas
        {
            public const int W = 120, H = 44;
            public readonly uint[] Px = new uint[W * H];
            public readonly uint[] Ln = new uint[W * H]; // outline colour belonging to each pixel's part
            public int OX = 8, OY = 8; // room for muzzle devices and the borders

            public void Put(int x, int y, uint c, uint line)
            {
                x += OX;
                y += OY;
                if (x < 0 || y < 0 || x >= W || y >= H) return;
                Px[y * W + x] = c;
                Ln[y * W + x] = line;
            }

            public void Set(int x, int y, uint c) { Put(x, y, c, DarkSteel.Line); }

            // Wood gets a little grain: short darker streaks along the length, the odd lighter pixel.
            static uint Grain(Mat m, int x, int y, uint c)
            {
                if (!m.Grain || c != m.Mid) return c;
                int streak = (((x / 4) * 73856093) ^ (y * 19349663)) & 0x7fffffff;
                if (streak % 6 == 0) return m.Shade;
                int speck = ((x * 83492791) ^ (y * 19349669)) & 0x7fffffff;
                return speck % 23 == 0 ? m.Light : c;
            }

            // A horizontal part: highlight on top with a bright glint toward the front,
            // a slightly darker lower half, dark bottom row, rounded corners.
            public void Fill(int x, int y, int w, int h, Mat m, bool round = true)
            {
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                    {
                        if (round && w >= 3 && h >= 3 && (i == 0 || i == w - 1) && (j == 0 || j == h - 1)) continue;
                        uint c = h == 1 ? m.Mid : j == 0 ? m.Light : j == h - 1 ? m.Dark : (h >= 5 && j > h / 2) ? m.Shade : m.Mid;
                        Put(x + i, y + j, Grain(m, x + i, y + j, c), m.Line);
                    }
                if (h >= 3 && w >= 6)
                    for (int i = 2; i < Math.Min(w - 2, 2 + Math.Max(2, w / 4)); i++) Put(x + i, y, m.Hi, m.Line);
            }

            // A vertical part (grip, magazine) that curves: each row moves sideways a little more than the last.
            // shift > 0 curves the bottom toward the stock, < 0 toward the muzzle.
            public void Lean(int x, int y, int w, int h, int shift, Mat m)
            {
                for (int j = 0; j < h; j++)
                {
                    double t = h > 1 ? j / (double)(h - 1) : 0.0;
                    int offset = (int)Math.Round(shift * Math.Pow(t, 1.5));
                    for (int i = 0; i < w; i++)
                    {
                        if (w >= 3 && j == h - 1 && (i == 0 || i == w - 1)) continue;
                        uint c = i == 0 ? m.Light : (i == w - 1 || j == h - 1) ? m.Dark : (i == w - 2 && w >= 4) ? m.Shade : m.Mid;
                        Put(x + offset + i, y + j, Grain(m, x + offset + i, y + j, c), m.Line);
                    }
                }
                if (h >= 4) Put(x, y + 1, m.Hi, m.Line);
            }

            // A part whose height changes along its length (stocks): top edge fixed.
            public void Taper(int x, int y, int w, int h0, int h1, Mat m)
            {
                for (int i = 0; i < w; i++)
                {
                    int h = (int)Math.Round(h0 + (h1 - h0) * (w > 1 ? i / (double)(w - 1) : 0.0));
                    for (int j = 0; j < h; j++)
                    {
                        if (i == w - 1 && (j == 0 || j == h - 1)) continue;
                        uint c = j == 0 ? m.Light : j == h - 1 ? m.Dark : (h >= 5 && j > h / 2) ? m.Shade : m.Mid;
                        Put(x + i, y + j, Grain(m, x + i, y + j, c), m.Line);
                    }
                }
                for (int i = 2; i < Math.Min(w - 2, 2 + w / 3); i++) Put(x + i, y, m.Hi, m.Line);
            }

            public void HLine(int x, int y, int w, uint c) { for (int i = 0; i < w; i++) Set(x + i, y, c); }
            public void VLine(int x, int y, int h, uint c) { for (int j = 0; j < h; j++) Set(x, y + j, c); }

            // A see-through gap (trigger guard, stock frame); the outline pass leaves it alone.
            public void Cut(int x, int y, int w, int h)
            {
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                        Put(x + i, y + j, Hole, 0u);
            }

            // A dotted row (vents, grip texture, rail teeth, magazine ribs).
            public void Dots(int x, int y, int w, int step, uint c)
            {
                for (int i = 0; i < w; i += step) Set(x + i, y, c);
            }
        }

        // ---- Shared parts ----

        static void Barrel(Canvas c, int x, int y, int length, int thickness)
        {
            c.Fill(x, y, length, thickness, Steel, false);
            c.Set(x, y + thickness - 1, DarkSteel.Dark); // bore
        }

        static void FrontSight(Canvas c, int x, int y)
        {
            c.VLine(x, y - 2, 2, DarkSteel.Mid);
            c.Set(x, y - 2, Steel.Light);
        }

        static void TriggerGuard(Canvas c, int x, int y, int w)
        {
            // A small loop under the receiver, open in the middle, with the trigger inside.
            c.VLine(x, y, 3, DarkSteel.Mid);
            c.HLine(x, y + 3, w, DarkSteel.Shade);
            c.Cut(x + 1, y, w - 1, 3);
            c.Set(x + 2, y, Steel.Light);
            c.Set(x + 2, y + 1, Steel.Mid);
        }

        static void Rail(Canvas c, int x, int y, int w)
        {
            c.Fill(x, y, w, 1, DarkSteel, false);
            c.Dots(x, y - 1, w, 2, DarkSteel.Mid);
        }

        // Ribs across a magazine (a lighter pixel line every few rows).
        static void Ribs(Canvas c, int x, int y, int w, int h, int shift)
        {
            for (int j = 2; j < h - 1; j += 3)
            {
                int offset = (int)Math.Round(shift * Math.Pow(j / (double)Math.Max(1, h - 1), 1.5));
                c.HLine(x + offset + 1, y + j, Math.Max(1, w - 2), DarkSteel.Light);
            }
        }

        // ---- Entry point ----

        public static Image Draw(WeaponCategory category, Options o)
        {
            var c = new Canvas();
            // Warm colours read as wood and get grain; cool ones (polymer, olive) stay smooth.
            var wood = Make(o.r, o.g, o.b, o.r > o.b + 0.12f);
            // Space at the muzzle for a suppressor: the whole gun moves right.
            int suppressor = o.suppressor ? 13 : 0;
            c.OX += suppressor;
            int muzzleX = 0, barrelY = 0, railX = -1, railY = 0, railW = 0, lightX = -1, lightY = 0;
            switch (category)
            {
                case WeaponCategory.CompactSMG:
                    // Boxy two-tone receiver with the magazine running down through the grip.
                    Barrel(c, 5, 11, 7, 2);
                    FrontSight(c, 13, 8);
                    c.Fill(12, 7, 30, 8, Steel);
                    c.HLine(15, 11, 12, Steel.Dark);
                    c.Fill(28, 9, 6, 2, DarkSteel, false);
                    c.Fill(31, 5, 3, 2, DarkSteel, false);
                    c.Lean(22, 15, 7, 8, 1, Polymer);
                    c.Fill(23, 23, 5, 9, DarkSteel);
                    Ribs(c, 23, 23, 5, 9, 0);
                    TriggerGuard(c, 16, 15, 5);
                    c.Fill(42, 8, 7, 4, DarkSteel);
                    c.VLine(48, 8, 7, Steel.Mid);
                    c.Set(48, 8, Steel.Light);
                    muzzleX = 5; barrelY = 11; railX = 18; railY = 6; railW = 0;
                    break;

                case WeaponCategory.SMG:
                    // Slim handguard, tall receiver, curved magazine, light folding stock frame.
                    Barrel(c, 3, 11, 9, 2);
                    FrontSight(c, 11, 9);
                    c.Fill(10, 9, 13, 5, Polymer);
                    c.Dots(12, 11, 10, 2, Polymer.Dark);
                    c.Fill(22, 6, 23, 8, Steel);
                    c.HLine(30, 9, 7, DarkSteel.Dark);
                    c.Set(42, 10, RedDot);
                    c.Lean(26, 14, 5, 11, -3, DarkSteel);
                    Ribs(c, 26, 14, 5, 11, -3);
                    c.Lean(36, 14, 5, 9, 2, Polymer);
                    TriggerGuard(c, 31, 14, 5);
                    // Folding stock: a pale steel frame with an open middle.
                    c.Fill(45, 7, 19, 7, Make(0.55f, 0.6f, 0.72f, false));
                    c.Cut(47, 9, 14, 3);
                    c.Fill(62, 6, 3, 9, Polymer);
                    muzzleX = 3; barrelY = 11; railX = 24; railY = 5; railW = 14; lightX = 12; lightY = 14;
                    break;

                case WeaponCategory.CompactRifle:
                case WeaponCategory.Rifle:
                case WeaponCategory.BurstRifle:
                {
                    bool full = category == WeaponCategory.Rifle;
                    bool burst = category == WeaponCategory.BurstRifle;
                    int barrel = full ? 20 : burst ? 15 : 11;
                    int x = 2 + (full ? 0 : burst ? 5 : 9);
                    Barrel(c, x, 11, barrel, 2);
                    c.Fill(x - 1, 10, 3, 4, DarkSteel, false); // muzzle device
                    FrontSight(c, x + 4, 10);
                    c.HLine(x + 6, 10, barrel - 2, DarkSteel.Mid); // gas tube
                    int guard = x + barrel - 2;
                    c.Fill(guard, 9, 17, 6, wood);
                    c.Dots(guard + 2, 12, 13, 3, wood.Dark);
                    c.Fill(guard + 17, 7, 23, 7, Steel);
                    c.HLine(guard + 27, 10, 6, DarkSteel.Dark);
                    c.Set(guard + 18, 6, DarkSteel.Mid);
                    c.Set(guard + 38, 6, DarkSteel.Mid);
                    // Curved magazine sweeping toward the muzzle; pistol grip leaning back.
                    if (burst)
                    {
                        c.Lean(guard + 20, 14, 6, 10, -2, Polymer);
                        Ribs(c, guard + 20, 14, 6, 10, -2);
                    }
                    else
                    {
                        c.Lean(guard + 20, 14, 6, 12, -5, full ? DarkSteel : Steel);
                        Ribs(c, guard + 20, 14, 6, 12, -5);
                    }
                    c.Lean(guard + 31, 14, 5, 9, 3, wood);
                    TriggerGuard(c, guard + 26, 14, 4);
                    int stock = guard + 40;
                    if (full) c.Taper(stock, 8, 24, 6, 11, wood);
                    else if (burst)
                    {
                        c.Fill(stock, 8, 20, 7, Polymer);
                        c.Cut(stock + 4, 10, 10, 2);
                        c.Fill(stock + 20, 7, 3, 9, DarkSteel);
                    }
                    else
                    {
                        // Collapsible stock: a tube and a short butt.
                        c.Fill(stock, 9, 9, 3, DarkSteel, false);
                        c.Taper(stock + 9, 7, 11, 7, 9, wood);
                    }
                    muzzleX = x - 1; barrelY = 11; railX = guard + 18; railY = 6; railW = burst || !full ? 15 : 0; lightX = guard + 4; lightY = 15;
                    break;
                }

                case WeaponCategory.Bullpup:
                    // Magazine behind the grip, rail on top, one long body.
                    Barrel(c, 3, 11, 13, 2);
                    FrontSight(c, 7, 10);
                    c.Fill(16, 7, 52, 10, wood);
                    c.HLine(20, 11, 14, wood.Dark);
                    c.Fill(40, 9, 14, 3, Steel);
                    c.Lean(26, 17, 5, 8, 2, Polymer);
                    TriggerGuard(c, 20, 17, 5);
                    c.Lean(45, 17, 6, 9, -1, DarkSteel);
                    Ribs(c, 45, 17, 6, 9, -1);
                    c.Fill(64, 6, 4, 12, DarkSteel);
                    muzzleX = 3; barrelY = 11; railX = 20; railY = 6; railW = 18; lightX = 18; lightY = 17;
                    break;

                case WeaponCategory.Carbine:
                    // Long wooden stock running most of the way to the muzzle, with a folded blade under the barrel.
                    Barrel(c, 2, 10, 24, 2);
                    FrontSight(c, 5, 9);
                    c.HLine(6, 12, 12, Pack(0.78f, 0.82f, 0.9f));
                    c.Set(17, 12, DarkSteel.Mid);
                    c.Fill(20, 9, 26, 5, wood);
                    c.Fill(46, 7, 15, 6, Steel);
                    c.HLine(50, 9, 7, DarkSteel.Dark);
                    c.Fill(48, 13, 6, 4, DarkSteel);
                    TriggerGuard(c, 54, 13, 4);
                    c.Taper(60, 8, 28, 6, 11, wood);
                    c.HLine(61, 12, 10, wood.Shade);
                    c.Fill(88, 8, 2, 11, DarkSteel, false);
                    muzzleX = 2; barrelY = 10; railX = 46; railY = 6; railW = 0;
                    break;

                case WeaponCategory.Marksman:
                    // Long barrel, scope, box magazine, stock with a cheek rest.
                    Barrel(c, 2, 11, 27, 2);
                    c.Fill(1, 10, 3, 4, DarkSteel, false);
                    c.Fill(27, 9, 17, 6, wood);
                    c.Dots(29, 12, 13, 3, wood.Dark);
                    c.Fill(44, 8, 19, 6, Steel);
                    c.Fill(48, 14, 6, 7, DarkSteel);
                    Ribs(c, 48, 14, 6, 7, 0);
                    c.Lean(57, 14, 5, 9, 3, wood);
                    TriggerGuard(c, 53, 14, 4);
                    c.Taper(63, 9, 24, 6, 11, wood);
                    c.Fill(65, 6, 13, 3, wood);
                    c.Fill(87, 8, 2, 11, DarkSteel, false);
                    // Scope with glass at both ends.
                    c.Fill(40, 3, 24, 4, DarkSteel);
                    c.Fill(37, 2, 4, 6, DarkSteel);
                    c.Fill(62, 2, 4, 6, DarkSteel);
                    c.VLine(37, 3, 4, Glass);
                    c.Set(38, 3, Pack(0.85f, 0.95f, 1f));
                    c.VLine(47, 7, 1, DarkSteel.Mid);
                    c.VLine(56, 7, 1, DarkSteel.Mid);
                    muzzleX = 1; barrelY = 11; railX = -1;
                    break;

                case WeaponCategory.LMG:
                    // Heavy barrel with bipod, carry handle, big box magazine.
                    Barrel(c, 2, 10, 23, 3);
                    c.Fill(0, 9, 3, 5, DarkSteel, false);
                    FrontSight(c, 5, 9);
                    c.Lean(8, 13, 2, 10, -2, DarkSteel);
                    c.Lean(12, 13, 2, 10, 2, DarkSteel);
                    c.Fill(23, 8, 15, 6, Polymer);
                    c.Dots(25, 11, 11, 2, Polymer.Dark);
                    c.Fill(38, 6, 25, 8, Steel);
                    c.Fill(41, 3, 14, 2, DarkSteel, false);
                    c.VLine(41, 4, 2, DarkSteel.Mid);
                    c.VLine(54, 4, 2, DarkSteel.Mid);
                    c.Fill(40, 14, 13, 9, wood);
                    c.HLine(41, 18, 11, wood.Shade);
                    c.Lean(56, 14, 5, 9, 3, Polymer);
                    TriggerGuard(c, 52, 14, 4);
                    c.Taper(63, 7, 23, 7, 10, Polymer);
                    c.Fill(86, 7, 2, 10, DarkSteel, false);
                    muzzleX = 0; barrelY = 10; railX = -1;
                    break;

                case WeaponCategory.Shotgun:
                    // Barrel over a tube magazine, ribbed pump, wooden stock that drops at the butt.
                    Barrel(c, 2, 9, 32, 2);
                    c.Fill(4, 11, 28, 2, DarkSteel, false);
                    FrontSight(c, 3, 9);
                    c.Fill(11, 11, 16, 5, wood);
                    c.Dots(12, 13, 14, 2, wood.Dark);
                    c.Fill(34, 7, 17, 7, Steel);
                    c.HLine(38, 10, 7, DarkSteel.Dark);
                    TriggerGuard(c, 42, 14, 4);
                    c.Taper(51, 8, 31, 6, 12, wood);
                    c.Fill(82, 8, 2, 12, DarkSteel, false);
                    muzzleX = 2; barrelY = 9; railX = 35; railY = 6; railW = 0;
                    break;

                case WeaponCategory.AutoShotgun:
                    // Boxy body, straight box magazine, muzzle brake.
                    c.Fill(1, 8, 5, 6, DarkSteel);
                    Barrel(c, 6, 10, 14, 2);
                    c.Fill(18, 8, 17, 6, Polymer);
                    c.Dots(20, 11, 13, 2, Polymer.Dark);
                    c.Fill(35, 6, 19, 8, Steel);
                    c.Fill(37, 14, 8, 10, DarkSteel);
                    Ribs(c, 37, 14, 8, 10, 0);
                    c.Lean(49, 14, 5, 9, 3, wood);
                    TriggerGuard(c, 45, 14, 4);
                    c.Fill(54, 7, 19, 7, wood);
                    c.Fill(73, 6, 3, 10, DarkSteel);
                    muzzleX = 1; barrelY = 10; railX = 36; railY = 5; railW = 15; lightX = 22; lightY = 14;
                    break;

                case WeaponCategory.PDW:
                    // Smooth body with a magazine along the top and a thumbhole grip.
                    Barrel(c, 5, 11, 7, 2);
                    c.Fill(12, 7, 44, 12, wood);
                    c.Fill(16, 4, 31, 4, Teal);
                    c.Cut(22, 13, 7, 4);
                    c.Cut(40, 12, 6, 6);
                    c.Set(24, 13, Steel.Light);
                    c.Fill(56, 8, 3, 11, DarkSteel);
                    c.HLine(14, 10, 6, wood.Dark);
                    muzzleX = 5; barrelY = 11; railX = -1;
                    break;

                case WeaponCategory.LessLethal:
                    // Fat launcher tube with an orange muzzle band.
                    c.Fill(4, 6, 23, 8, DarkSteel);
                    c.Fill(1, 6, 4, 8, Orange);
                    c.Fill(27, 7, 15, 7, Steel);
                    c.Lean(31, 14, 5, 9, 3, Orange);
                    TriggerGuard(c, 27, 14, 4);
                    c.Fill(42, 8, 15, 5, DarkSteel);
                    c.Cut(44, 9, 11, 3);
                    c.Fill(57, 7, 3, 8, Orange);
                    muzzleX = 1; barrelY = 10; railX = 28; railY = 6; railW = 11;
                    break;

                case WeaponCategory.Pepperball:
                    // Thin barrel, round hopper on top showing the coloured rounds.
                    Barrel(c, 3, 11, 14, 2);
                    c.Fill(16, 9, 27, 6, Polymer);
                    c.Fill(20, 1, 15, 8, wood);
                    c.HLine(23, 3, 9, Pack(0.95f, 0.45f, 1f));
                    c.HLine(23, 5, 9, Pack(0.95f, 0.45f, 1f));
                    c.Lean(33, 15, 5, 8, 3, Polymer);
                    TriggerGuard(c, 28, 15, 4);
                    c.Fill(43, 9, 13, 5, wood);
                    c.Fill(56, 8, 3, 8, DarkSteel);
                    muzzleX = 3; barrelY = 11; railX = -1;
                    break;

                case WeaponCategory.Rotary:
                    // A cluster of barrels with clamps, motor housing, carry handle, spade grip and ammo box.
                    for (int i = 0; i < 3; i++) c.Fill(2, 8 + i * 2, 30, 2, i == 1 ? DarkSteel : Steel, false);
                    c.Fill(6, 7, 3, 8, DarkSteel);
                    c.Fill(20, 7, 3, 8, DarkSteel);
                    c.Fill(0, 8, 3, 6, DarkSteel);
                    c.Fill(31, 5, 16, 11, Steel);
                    c.HLine(33, 9, 12, Steel.Dark);
                    c.Fill(34, 2, 10, 2, DarkSteel, false);
                    c.VLine(34, 3, 2, DarkSteel.Mid);
                    c.VLine(43, 3, 2, DarkSteel.Mid);
                    c.Fill(35, 16, 12, 8, wood);
                    c.HLine(36, 19, 10, wood.Shade);
                    c.Fill(47, 7, 6, 3, DarkSteel, false);
                    c.Lean(50, 10, 4, 8, 2, Polymer);
                    TriggerGuard(c, 46, 16, 4);
                    muzzleX = 0; barrelY = 10; railX = -1;
                    break;

                case WeaponCategory.DrumShotgun:
                    // Barrel with a brake, ribbed handguard, boxy receiver, big drum magazine, straight stock.
                    c.Fill(1, 9, 4, 5, DarkSteel);
                    Barrel(c, 5, 10, 14, 2);
                    c.Fill(15, 8, 16, 6, Polymer);
                    c.Dots(17, 11, 12, 2, Polymer.Dark);
                    c.Fill(31, 6, 22, 8, Steel);
                    c.HLine(36, 9, 8, DarkSteel.Dark);
                    c.Fill(31, 14, 14, 13, wood);
                    c.Fill(35, 18, 6, 5, DarkSteel); // drum hub
                    c.Lean(47, 14, 5, 9, 3, Polymer);
                    TriggerGuard(c, 44, 14, 4);
                    c.Fill(53, 7, 19, 7, Polymer);
                    c.Fill(72, 6, 3, 10, DarkSteel);
                    muzzleX = 1; barrelY = 10; railX = 33; railY = 5; railW = 16; lightX = 18; lightY = 14;
                    break;

                case WeaponCategory.VectorSMG:
                    // Long shroud, angular upper, slanted lower body, magazine at the front, folding stock.
                    c.Fill(1, 9, 18, 4, DarkSteel);
                    c.HLine(3, 9, 14, DarkSteel.Light);
                    c.Fill(18, 6, 28, 7, wood);
                    c.Lean(21, 13, 18, 7, 4, wood);
                    c.Fill(23, 19, 5, 10, DarkSteel);
                    Ribs(c, 23, 19, 5, 10, 0);
                    c.Lean(38, 13, 5, 9, 2, Polymer);
                    TriggerGuard(c, 33, 18, 4);
                    c.Fill(46, 7, 15, 5, Polymer);
                    c.Cut(48, 8, 10, 2);
                    c.Fill(60, 6, 3, 8, DarkSteel);
                    muzzleX = 1; barrelY = 10; railX = 20; railY = 5; railW = 20; lightX = 6; lightY = 13;
                    break;

                case WeaponCategory.GrenadeLauncher:
                    // Short fat barrel, a six-shot revolving drum, ladder sight, foregrip, pistol grip, stock.
                    c.Fill(2, 6, 15, 7, DarkSteel);
                    c.HLine(3, 6, 12, DarkSteel.Light);
                    c.Lean(8, 13, 4, 7, 1, Polymer);
                    c.Fill(16, 3, 16, 15, wood);
                    for (int i = 0; i < 3; i++) c.HLine(17, 6 + i * 4, 14, wood.Dark);
                    c.Fill(19, 0, 3, 3, DarkSteel, false);
                    c.Fill(32, 6, 8, 8, Steel);
                    c.Lean(34, 14, 5, 9, 3, wood);
                    TriggerGuard(c, 30, 14, 4);
                    c.Fill(40, 8, 17, 5, DarkSteel);
                    c.Cut(42, 9, 12, 3);
                    c.Fill(56, 6, 4, 10, wood);
                    muzzleX = 2; barrelY = 9; railX = -1;
                    break;

                case WeaponCategory.MachinePistol:
                    // Pistol with a long magazine and a compensator.
                    c.Fill(3, 7, 4, 5, DarkSteel);
                    c.Fill(7, 7, 23, 5, Steel);
                    c.Dots(23, 9, 7, 2, Steel.Dark);
                    c.Fill(8, 12, 20, 3, Polymer, false);
                    c.Lean(19, 15, 6, 8, 3, Polymer);
                    c.Fill(22, 23, 4, 9, DarkSteel);
                    Ribs(c, 22, 23, 4, 9, 0);
                    TriggerGuard(c, 12, 15, 5);
                    muzzleX = 3; barrelY = 9; railX = -1;
                    break;

                case WeaponCategory.Revolver:
                    // Round barrel with an ejector rod, fluted cylinder, curved wooden grip and hammer.
                    Barrel(c, 2, 8, 21, 3);
                    c.HLine(6, 11, 15, DarkSteel.Mid);
                    FrontSight(c, 3, 8);
                    c.Fill(21, 6, 11, 8, Steel);
                    c.VLine(24, 7, 6, Steel.Dark);
                    c.VLine(28, 7, 6, Steel.Dark);
                    c.Fill(32, 6, 6, 7, Steel);
                    c.Set(37, 5, DarkSteel.Mid);
                    c.Set(38, 4, DarkSteel.Mid);
                    c.Lean(32, 13, 7, 12, 4, wood);
                    TriggerGuard(c, 27, 13, 4);
                    muzzleX = 2; barrelY = 9; railX = -1;
                    break;

                case WeaponCategory.StunPistol:
                    // Chunky body with a yellow cartridge at the front.
                    c.Fill(3, 6, 10, 8, Yellow);
                    c.HLine(4, 9, 8, Yellow.Dark);
                    c.Fill(12, 6, 17, 8, Polymer);
                    c.HLine(14, 8, 12, Yellow.Mid);
                    c.Lean(18, 14, 7, 10, 3, Polymer);
                    c.HLine(20, 18, 5, Yellow.Mid);
                    TriggerGuard(c, 13, 14, 5);
                    muzzleX = 3; barrelY = 10; railX = -1;
                    break;

                case WeaponCategory.HeavyPistol:
                    c.Fill(3, 6, 30, 7, Steel);
                    c.Dots(26, 8, 7, 2, Steel.Dark);
                    FrontSight(c, 5, 6);
                    c.Set(31, 5, DarkSteel.Mid);
                    c.Fill(6, 13, 25, 3, DarkSteel, false);
                    c.Lean(21, 16, 8, 11, 4, wood);
                    TriggerGuard(c, 13, 16, 6);
                    muzzleX = 3; barrelY = 9; railX = -1;
                    break;

                default: // service and backup pistols
                {
                    bool backup = category == WeaponCategory.BackupPistol;
                    int length = backup ? 19 : 25;
                    c.Fill(3, 7, length, 6, Steel);
                    c.Dots(3 + length - 7, 9, 7, 2, Steel.Dark);
                    FrontSight(c, 5, 7);
                    c.Set(3 + length - 2, 6, DarkSteel.Mid);
                    c.Fill(5, 13, length - 4, 3, Polymer, false);
                    c.Lean(3 + length - 11, 16, 7, backup ? 8 : 10, 3, Polymer);
                    TriggerGuard(c, 10, 16, 5);
                    muzzleX = 3; barrelY = 9; railX = -1;
                    break;
                }
            }

            if (o.suppressor)
            {
                // A long can with a teal band along the top, like a tinted finish.
                int x = muzzleX - 13;
                c.Fill(x, barrelY - 1, 14, 5, DarkSteel);
                c.HLine(x + 2, barrelY - 1, 10, Teal.Light);
                c.HLine(x + 2, barrelY, 10, Teal.Mid);
                c.VLine(x + 10, barrelY, 3, DarkSteel.Dark);
            }
            if (o.optic && railX >= 0)
            {
                if (railW > 0) Rail(c, railX, railY, railW);
                int ox = railX + Math.Max(2, railW / 2 - 3);
                c.Fill(ox, railY - 5, 8, 5, DarkSteel);
                c.Set(ox, railY - 3, Glass);
                c.Set(ox + 7, railY - 3, RedDot);
            }
            else if (railW > 0) Rail(c, railX, railY, railW);
            if (o.light && lightX >= 0)
            {
                c.Fill(lightX, lightY, 7, 3, DarkSteel, false);
                c.Set(lightX, lightY + 1, Pack(1f, 0.95f, 0.7f));
            }
            return Finish(c);
        }

        // Outline every part in a darker shade of its own colour, then a two-pixel white border, then crop.
        static Image Finish(Canvas c)
        {
            int w = Canvas.W, h = Canvas.H;
            var px = c.Px;
            var outlined = (uint[])px.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x] != Empty) continue;
                    int n = SolidNeighbour(px, w, h, x, y);
                    if (n >= 0) outlined[y * w + x] = c.Ln[n] != 0u ? c.Ln[n] : DarkSteel.Line;
                }
            var bordered = Grow(outlined, w, h, true);
            bordered = Grow(bordered, w, h, false);

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

        // One ring of white border: 8-neighbour (rounder) or 4-neighbour.
        static uint[] Grow(uint[] source, int w, int h, bool diagonal)
        {
            var result = (uint[])source.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (source[y * w + x] != Empty) continue;
                    bool near = false;
                    for (int dy = -1; dy <= 1 && !near; dy++)
                        for (int dx = -1; dx <= 1 && !near; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            if (!diagonal && dx != 0 && dy != 0) continue;
                            if (Solid(source, w, h, x + dx, y + dy)) near = true;
                        }
                    if (near) result[y * w + x] = Border;
                }
            return result;
        }

        static int SolidNeighbour(uint[] px, int w, int h, int x, int y)
        {
            if (Solid(px, w, h, x - 1, y)) return y * w + x - 1;
            if (Solid(px, w, h, x + 1, y)) return y * w + x + 1;
            if (Solid(px, w, h, x, y - 1)) return (y - 1) * w + x;
            if (Solid(px, w, h, x, y + 1)) return (y + 1) * w + x;
            return -1;
        }

        static bool Solid(uint[] px, int w, int h, int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return false;
            uint p = px[y * w + x];
            return p != Empty && p != Hole;
        }

        // ---- Colour ----

        // Six shades from one colour: highlights lighter and slightly paler, shadows deeper and
        // leaning toward navy-purple, and an outline colour darker still, as in hand-drawn pixel art.
        static Mat Make(float r, float g, float b, bool grain)
        {
            return new Mat
            {
                Hi = Pack(Mix(r * 1.9f, 0.95f, 0.38f), Mix(g * 1.9f, 0.96f, 0.38f), Mix(b * 1.9f, 1f, 0.38f)),
                Light = Pack(Mix(r * 1.4f, 1f, 0.06f), Mix(g * 1.4f, 0.97f, 0.06f), Mix(b * 1.4f, 0.92f, 0.06f)),
                Mid = Pack(r, g, b),
                Shade = Pack(Mix(r * 0.78f, 0.12f, 0.12f), Mix(g * 0.78f, 0.1f, 0.12f), Mix(b * 0.78f, 0.24f, 0.12f)),
                Dark = Pack(Mix(r * 0.52f, 0.08f, 0.3f), Mix(g * 0.52f, 0.06f, 0.3f), Mix(b * 0.52f, 0.18f, 0.3f)),
                Line = Pack(Mix(r * 0.28f, 0.05f, 0.5f), Mix(g * 0.28f, 0.04f, 0.5f), Mix(b * 0.28f, 0.1f, 0.5f)),
                Grain = grain,
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
