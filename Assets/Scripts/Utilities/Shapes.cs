using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Swat
{
    // Builds placeholder visuals from Unity's primitive shapes. Materials are
    // copies of the render pipeline's default material (one per colour, cached),
    // so they work in both the Built-in pipeline and URP with no shader setup.
    public static class Shapes
    {
        static Material template;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        static Shader unlitShader, decalShader, glowShader;
        static bool shadersLoaded;
        static readonly Dictionary<int, Mesh> tiledCubes = new Dictionary<int, Mesh>();
        static readonly Dictionary<Texture, Material> decalMaterials = new Dictionary<Texture, Material>();
        static readonly Dictionary<Texture, Material> glowMaterials = new Dictionary<Texture, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            template = null;
            cache.Clear();
            tiledCubes.Clear();
            decalMaterials.Clear();
            glowMaterials.Clear();
            shadersLoaded = false;
        }

        // The game's own small shaders live in Resources so they are always included in builds.
        static void LoadShaders()
        {
            if (shadersLoaded) return;
            shadersLoaded = true;
            unlitShader = Resources.Load<Shader>("SWAT/Shaders/SwatUnlit") ?? Shader.Find("SWAT/Unlit");
            decalShader = Resources.Load<Shader>("SWAT/Shaders/SwatDecal") ?? Shader.Find("SWAT/Decal") ?? Shader.Find("Sprites/Default");
            glowShader = Resources.Load<Shader>("SWAT/Shaders/SwatGlow") ?? Shader.Find("SWAT/Glow") ?? decalShader;
            if (unlitShader != null && !unlitShader.isSupported) unlitShader = null;
            if (decalShader != null && !decalShader.isSupported) decalShader = Shader.Find("Sprites/Default");
            if (glowShader != null && !glowShader.isSupported) glowShader = decalShader;
        }

        static Material Template
        {
            get
            {
                if (template == null)
                {
                    var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    template = probe.GetComponent<Renderer>().sharedMaterial;
                    Object.DestroyImmediate(probe);
                }
                return template;
            }
        }

        // Lit material of one color. Glowing ones (lamps, screens, signs) use the
        // game's unlit shader, so they read as light sources on every tier.
        public static Material Mat(Color color, float glow = 0f)
        {
            string key = ColorUtility.ToHtmlStringRGB(color) + "/" + glow.ToString("0.0");
            Material material;
            if (cache.TryGetValue(key, out material) && material != null) return material;
            LoadShaders();

            if (glow > 0f && unlitShader != null)
            {
                material = new Material(unlitShader) { name = "SWAT Glow " + key };
                float boost = Mathf.Clamp(0.8f + glow * 0.2f, 0.85f, 1.4f);
                material.color = new Color(Mathf.Min(1f, color.r * boost), Mathf.Min(1f, color.g * boost), Mathf.Min(1f, color.b * boost), 1f);
            }
            else
            {
                material = new Material(Template) { name = "SWAT " + key };
                material.color = color;
                material.SetFloat("_Glossiness", 0.05f); // Built-in Standard shader
                material.SetFloat("_Smoothness", 0.05f); // URP Lit shader
                if (glow > 0f)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * glow);
                }
            }
            cache[key] = material;
            return material;
        }

        // Lit material with a tiling surface texture (concrete, carpet, tile, wood, metal...).
        public static Material Surface(Color color, SurfaceKind kind)
        {
            if (kind == SurfaceKind.Plain) return Mat(color);
            var texture = ProceduralTextures.Surface(kind);
            string key = ColorUtility.ToHtmlStringRGB(color) + "/" + kind + "/" + (texture != null ? texture.width : 0);
            Material material;
            if (cache.TryGetValue(key, out material) && material != null) return material;
            material = new Material(Template) { name = "SWAT " + key };
            material.color = color;
            material.mainTexture = texture;
            float smooth = 0.06f, metal = 0f;
            switch (kind)
            {
                case SurfaceKind.Tile: smooth = 0.32f; break;
                case SurfaceKind.Metal: smooth = 0.4f; metal = 0.35f; break;
                case SurfaceKind.Glass: smooth = 0.75f; break;
                case SurfaceKind.Plastic: smooth = 0.28f; break;
                case SurfaceKind.Wood: smooth = 0.14f; break;
                case SurfaceKind.PaintedWall: smooth = 0.08f; break;
            }
            material.SetFloat("_Glossiness", smooth);
            material.SetFloat("_Smoothness", smooth);
            material.SetFloat("_Metallic", metal);
            cache[key] = material;
            return material;
        }

        // Swaps a box's mesh for a cube whose UVs are in world meters, so a
        // shared tiling material repeats at the same density on every size.
        public static void ApplySurface(GameObject box, Color color, SurfaceKind kind)
        {
            var renderer = box.GetComponent<Renderer>();
            if (renderer == null) return;
            renderer.sharedMaterial = Surface(color, kind);
            var filter = box.GetComponent<MeshFilter>();
            if (filter != null && kind != SurfaceKind.Plain) filter.sharedMesh = TiledCube(box.transform.localScale, ProceduralTextures.TileMeters(kind));
        }

        public static Mesh TiledCube(Vector3 size, float tileMeters)
        {
            int key = Mathf.RoundToInt(size.x * 20f) * 73856093 ^ Mathf.RoundToInt(size.y * 20f) * 19349663 ^ Mathf.RoundToInt(size.z * 20f) * 83492791 ^ Mathf.RoundToInt(tileMeters * 10f);
            Mesh mesh;
            if (tiledCubes.TryGetValue(key, out mesh) && mesh != null) return mesh;
            mesh = BuildTiledCube(size / Mathf.Max(0.1f, tileMeters));
            tiledCubes[key] = mesh;
            return mesh;
        }

        static Mesh BuildTiledCube(Vector3 repeats)
        {
            var vertices = new List<Vector3>(24);
            var normals = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var triangles = new List<int>(36);
            // Each face: normal, the two in-plane axes, and how many repeats along each.
            AddFace(vertices, normals, uvs, triangles, Vector3.up, Vector3.right, Vector3.forward, repeats.x, repeats.z);
            AddFace(vertices, normals, uvs, triangles, Vector3.down, Vector3.right, Vector3.back, repeats.x, repeats.z);
            AddFace(vertices, normals, uvs, triangles, Vector3.forward, Vector3.left, Vector3.up, repeats.x, repeats.y);
            AddFace(vertices, normals, uvs, triangles, Vector3.back, Vector3.right, Vector3.up, repeats.x, repeats.y);
            AddFace(vertices, normals, uvs, triangles, Vector3.right, Vector3.forward, Vector3.up, repeats.z, repeats.y);
            AddFace(vertices, normals, uvs, triangles, Vector3.left, Vector3.back, Vector3.up, repeats.z, repeats.y);
            var mesh = new Mesh { name = "Tiled Cube" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddFace(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 normal, Vector3 right, Vector3 up, float repeatU, float repeatV)
        {
            int start = v.Count;
            Vector3 center = normal * 0.5f;
            v.Add(center - right * 0.5f - up * 0.5f);
            v.Add(center + right * 0.5f - up * 0.5f);
            v.Add(center + right * 0.5f + up * 0.5f);
            v.Add(center - right * 0.5f + up * 0.5f);
            for (int i = 0; i < 4; i++) n.Add(normal);
            uv.Add(new Vector2(0f, 0f));
            uv.Add(new Vector2(repeatU, 0f));
            uv.Add(new Vector2(repeatU, repeatV));
            uv.Add(new Vector2(0f, repeatV));
            // Clockwise when seen from outside (Unity's front face winding).
            t.Add(start); t.Add(start + 2); t.Add(start + 1);
            t.Add(start); t.Add(start + 3); t.Add(start + 2);
        }

        // Alpha-blended unlit material for decals (contact shadows, darkness, marks).
        public static Material DecalMaterial(Texture texture)
        {
            LoadShaders();
            Material material;
            if (decalMaterials.TryGetValue(texture, out material) && material != null) return material;
            material = new Material(decalShader) { name = "SWAT Decal " + texture.name, mainTexture = texture };
            decalMaterials[texture] = material;
            return material;
        }

        // Additive unlit material for light (pools under lamps, flashlight cones, glows).
        public static Material GlowMaterial(Texture texture)
        {
            LoadShaders();
            Material material;
            if (glowMaterials.TryGetValue(texture, out material) && material != null) return material;
            material = new Material(glowShader) { name = "SWAT Glow " + texture.name, mainTexture = texture };
            glowMaterials[texture] = material;
            return material;
        }

        public static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Color color, bool solid = true, float glow = 0f)
        {
            return Make(PrimitiveType.Cube, name, parent, localPosition, size, color, solid, glow);
        }

        public static GameObject Make(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Color color, bool solid = true, float glow = 0f)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = Mat(color, glow);
            if (!solid)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            return go;
        }

        public static Light PointLight(Transform parent, Vector3 localPosition, Color color, float intensity, float range)
        {
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = localPosition;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity * PointLightScale;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        // URP point lights fade with distance faster than Built-in ones, so they
        // need extra intensity to light a room about as brightly.
        public static float PointLightScale
        {
            get { return GraphicsSettings.currentRenderPipeline != null ? 2.5f : 1f; }
        }

        public static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayer(child.gameObject, layer);
        }

        public static Color Shade(Color color, float amount)
        {
            return new Color(color.r * amount, color.g * amount, color.b * amount, 1f);
        }
    }
}
