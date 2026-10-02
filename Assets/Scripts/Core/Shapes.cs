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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            template = null;
            cache.Clear();
        }

        public static Material Mat(Color color, float glow = 0f)
        {
            string key = ColorUtility.ToHtmlStringRGB(color) + "/" + glow.ToString("0.0");
            Material material;
            if (cache.TryGetValue(key, out material) && material != null) return material;

            if (template == null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                template = probe.GetComponent<Renderer>().sharedMaterial;
                Object.DestroyImmediate(probe);
            }

            material = new Material(template) { name = "SWAT " + key };
            material.color = color;
            material.SetFloat("_Glossiness", 0.05f); // Built-in Standard shader
            material.SetFloat("_Smoothness", 0.05f); // URP Lit shader
            if (glow > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * glow);
            }
            cache[key] = material;
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
