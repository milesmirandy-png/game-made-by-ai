using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    public enum SurfaceKind { Plain, Concrete, DirtyConcrete, PaintedWall, Brick, Carpet, Tile, Asphalt, Wood, Metal, Grass, Plastic, Fabric, Glass, Rubber }

    // Small tiling textures generated at startup (no image files): mostly
    // light gray detail that the material color tints, so each room keeps its
    // color and gains surface character. Size follows the texture quality
    // setting (32, 64 or 128 pixels). Also builds the soft gradient textures
    // used by decals, light pools and flashlight cones.
    public static class ProceduralTextures
    {
        static readonly Dictionary<int, Texture2D> surfaces = new Dictionary<int, Texture2D>();
        static Texture2D radial, edge, cone, dot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            surfaces.Clear();
            radial = edge = cone = dot = null;
        }

        // Pixel art uses small textures with hard-edged texels so they match the chunky screen pixels.
        public static int Size
        {
            get
            {
                int quality = QualityManager.TextureLevel;
                if (QualityManager.PixelArt) return quality <= 0 ? 16 : 32;
                return quality <= 0 ? 32 : quality == 1 ? 64 : 128;
            }
        }

        static FilterMode Filter { get { return QualityManager.PixelArt ? FilterMode.Point : FilterMode.Bilinear; } }

        // Switches already-made surface textures between crisp and smooth sampling.
        public static void ApplyFilter()
        {
            foreach (var texture in surfaces.Values)
                if (texture != null) texture.filterMode = Filter;
        }

        public static Texture2D Surface(SurfaceKind kind)
        {
            if (kind == SurfaceKind.Plain) return null;
            int size = Size;
            int key = (int)kind * 1000 + size;
            Texture2D texture;
            if (surfaces.TryGetValue(key, out texture) && texture != null) return texture;
            texture = Generate(kind, size);
            surfaces[key] = texture;
            return texture;
        }

        // World meters covered by one repeat of the texture.
        public static float TileMeters(SurfaceKind kind)
        {
            switch (kind)
            {
                case SurfaceKind.Tile: return 1.2f;
                case SurfaceKind.Carpet: return 2f;
                case SurfaceKind.Wood: return 2.4f;
                case SurfaceKind.Brick: return 2f;
                case SurfaceKind.Asphalt: return 5f;
                case SurfaceKind.Grass: return 4f;
                case SurfaceKind.Metal: return 1.5f;
                case SurfaceKind.PaintedWall: return 3f;
                default: return 3f;
            }
        }

        static Texture2D Generate(SurfaceKind kind, int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "SWAT " + kind,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = Filter,
                anisoLevel = 2,
                hideFlags = HideFlags.DontSave,
            };
            var random = new System.Random((int)kind * 7919 + 13);
            var pixels = new Color[size * size];
            float s = size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / s, v = y / s;
                    float n = Fbm(u, v, (int)kind);
                    float value;
                    switch (kind)
                    {
                        case SurfaceKind.Concrete:
                            value = 0.84f + n * 0.14f + Grain(random) * 0.04f;
                            break;
                        case SurfaceKind.DirtyConcrete:
                            value = 0.72f + n * 0.24f + Grain(random) * 0.06f;
                            break;
                        case SurfaceKind.PaintedWall:
                            value = 0.93f + n * 0.05f + Grain(random) * 0.015f;
                            break;
                        case SurfaceKind.Brick:
                        {
                            float row = Mathf.Floor(v * 8f);
                            float bu = u * 4f + (row % 2f) * 0.5f;
                            bool mortar = Mathf.Repeat(v * 8f, 1f) < 0.12f || Mathf.Repeat(bu, 1f) < 0.06f;
                            value = mortar ? 0.62f : 0.86f + n * 0.12f + Grain(random) * 0.05f;
                            break;
                        }
                        case SurfaceKind.Carpet:
                            value = 0.86f + n * 0.08f + Grain(random) * 0.1f;
                            break;
                        case SurfaceKind.Tile:
                        {
                            bool grout = Mathf.Repeat(u * 2f, 1f) < 0.04f || Mathf.Repeat(v * 2f, 1f) < 0.04f;
                            value = grout ? 0.66f : 0.95f + n * 0.04f;
                            break;
                        }
                        case SurfaceKind.Asphalt:
                            value = 0.8f + n * 0.12f + Grain(random) * 0.16f;
                            break;
                        case SurfaceKind.Wood:
                        {
                            float plank = Mathf.Floor(v * 6f);
                            bool seam = Mathf.Repeat(v * 6f, 1f) < 0.05f || Mathf.Repeat(u * 1.5f + plank * 0.37f, 1f) < 0.015f;
                            float grain = Mathf.Sin((u * 40f + Fbm(u * 2f, v * 6f, 3) * 6f) * 3.1f) * 0.05f;
                            value = seam ? 0.6f : 0.85f + grain + (plank % 3f) * 0.03f;
                            break;
                        }
                        case SurfaceKind.Metal:
                            value = 0.86f + Fbm(u * 0.2f, v * 8f, 9) * 0.1f + Grain(random) * 0.03f;
                            break;
                        case SurfaceKind.Grass:
                            value = 0.78f + n * 0.18f + Grain(random) * 0.12f;
                            break;
                        case SurfaceKind.Fabric:
                            value = 0.88f + ((x + y) % 2 == 0 ? 0.04f : 0f) + n * 0.06f;
                            break;
                        case SurfaceKind.Rubber:
                            value = 0.82f + n * 0.06f + Grain(random) * 0.05f;
                            break;
                        default:
                            value = 0.9f + n * 0.08f;
                            break;
                    }
                    value = Mathf.Clamp01(value);
                    pixels[y * size + x] = new Color(value, value, value, 1f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, true); // mipmaps, then free the CPU copy
            return texture;
        }

        static float Grain(System.Random random)
        {
            return (float)random.NextDouble() - 0.5f;
        }

        // Tileable value noise: a few octaves on a periodic lattice.
        static float Fbm(float u, float v, int seed)
        {
            float total = 0f, amplitude = 0.5f;
            int period = 4;
            for (int octave = 0; octave < 3; octave++)
            {
                total += amplitude * (ValueNoise(u * period, v * period, period, seed + octave * 31) - 0.5f) * 2f;
                amplitude *= 0.5f;
                period *= 2;
            }
            return total;
        }

        static float ValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(x0, y0, period, seed), b = Hash(x0 + 1, y0, period, seed);
            float c = Hash(x0, y0 + 1, period, seed), d = Hash(x0 + 1, y0 + 1, period, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Hash(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            int h = x * 374761393 + y * 668265263 + seed * 2147483;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }

        // ---- Decal and light textures ----

        // Soft round blob: alpha 1 in the middle fading to 0 at the edge.
        public static Texture2D Radial
        {
            get
            {
                if (radial == null) radial = Gradient("SWAT Radial", 64, (u, v) =>
                {
                    float d = Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f);
                    return Mathf.Pow(1f - d, 1.6f);
                });
                return radial;
            }
        }

        // Linear falloff along v (1 at v = 0, 0 at v = 1), soft at the u ends: contact shadow strips.
        public static Texture2D Edge
        {
            get
            {
                if (edge == null) edge = Gradient("SWAT Edge", 32, (u, v) =>
                {
                    float ends = Mathf.Clamp01(Mathf.Min(u, 1f - u) * 8f);
                    return Mathf.Pow(1f - v, 2f) * ends;
                });
                return edge;
            }
        }

        // Flashlight cone: a fan from the bottom-center with soft edges and distance falloff.
        public static Texture2D Cone
        {
            get
            {
                if (cone == null) cone = Gradient("SWAT Cone", 64, (u, v) =>
                {
                    Vector2 p = new Vector2(u - 0.5f, v);
                    float distance = p.magnitude;
                    if (distance < 0.0001f) return 0f;
                    float angle = Mathf.Abs(Mathf.Atan2(p.x, p.y)) * Mathf.Rad2Deg;
                    float sides = 1f - Smooth(18f, 30f, angle);
                    float reach = 1f - Smooth(0.55f, 1f, distance);
                    float near = Smooth(0f, 0.12f, distance);
                    return sides * reach * near;
                });
                return cone;
            }
        }

        // Small hard-edged dot (bullet marks, dust motes).
        public static Texture2D Dot
        {
            get
            {
                if (dot == null) dot = Gradient("SWAT Dot", 16, (u, v) =>
                {
                    float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                    return Mathf.Clamp01((1f - d) * 3f);
                });
                return dot;
            }
        }

        // GLSL-style smoothstep (Unity's Mathf.SmoothStep has different arguments).
        static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        static Texture2D Gradient(string name, int size, System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha((x + 0.5f) / size, (y + 0.5f) / size)));
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
