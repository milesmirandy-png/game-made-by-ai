using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Imported low-poly models (the soldier, the police shield and the weapon
    // pack), converted to a small format the game reads itself, so no model
    // importer or package is needed and they get the same lit / pixel-art
    // materials as everything else. Each file is
    // Resources/SWAT/Models/<id>.bytes:
    //   "SWM1", colour slots (name + RGB), then parts: a name, a pivot and
    //   flat-shaded triangles (16-bit positions relative to the pivot), each
    //   triangle tagged with a colour slot.
    // Parts let the soldier be split on the joints the procedural animator
    // moves (head, arms, legs); a part with no triangles is just a point
    // (a gun's muzzle). Slot names let a character recolour the model (the
    // vest, shirt, helmet...) for each officer and team.
    public static class ModelLibrary
    {
        public class Part
        {
            public string name;
            public Vector3 pivot;     // where the part sits in the model (metres)
            public Mesh mesh;         // null for a point-only part; one sub-mesh per used colour slot
            public int[] slots;       // colour slot of each sub-mesh
        }

        public class Model
        {
            public string id;
            public string[] slotNames;
            public Color[] slotColors;
            public Part[] parts;

            public Part Find(string name)
            {
                foreach (var part in parts) if (part.name == name) return part;
                return null;
            }
        }

        static readonly Dictionary<string, Model> cache = new Dictionary<string, Model>();
        static readonly HashSet<string> missing = new HashSet<string>();
        static readonly Dictionary<string, Variant> variants = new Dictionary<string, Variant>();

        // Low-poly camouflage for a uniform: its triangles are split in four and each piece takes one of a
        // few colours from smooth 3D noise in model space (so the blotches carry on across arms, body and legs).
        public class Camo
        {
            public string id;          // names the pattern (meshes are cached by it)
            public string[] slots;     // the colour slots it covers
            public Color[] colors;     // base colour first, then the blotches
            public float[] cuts;       // noise level where each blotch colour starts (rising)
            public float scale;        // blotch size, metres
            public int seed;
        }

        // A part's mesh with some slots left out and/or camouflage applied: sub-meshes and what colours them.
        class Variant
        {
            public Mesh mesh;
            public int[] slots;    // colour slot of each sub-mesh
            public int[] layers;   // camouflage colour of each sub-mesh, or -1 for the slot's own colour
        }

        // The model, or null if its file isn't there (callers fall back to the built-in shapes).
        public static Model Get(string id)
        {
            if (string.IsNullOrEmpty(id) || missing.Contains(id)) return null;
            Model model;
            if (cache.TryGetValue(id, out model)) return model;
            var asset = Resources.Load<TextAsset>("SWAT/Models/" + id);
            model = asset != null ? Parse(id, asset.bytes) : null;
            if (model == null)
            {
                missing.Add(id);
                return null;
            }
            cache[id] = model;
            return model;
        }

        static Model Parse(string id, byte[] data)
        {
            try
            {
                int pos = 0;
                if (data.Length < 6 || data[0] != 'S' || data[1] != 'W' || data[2] != 'M' || data[3] != '1') return null;
                pos = 4;
                int slotCount = data[pos++];
                var model = new Model { id = id, slotNames = new string[slotCount], slotColors = new Color[slotCount] };
                for (int i = 0; i < slotCount; i++)
                {
                    model.slotNames[i] = ReadString(data, ref pos);
                    model.slotColors[i] = new Color32(data[pos], data[pos + 1], data[pos + 2], 255);
                    pos += 3;
                }
                int partCount = data[pos++];
                model.parts = new Part[partCount];
                for (int p = 0; p < partCount; p++)
                {
                    var part = new Part { name = ReadString(data, ref pos) };
                    part.pivot = new Vector3(ReadFloat(data, ref pos), ReadFloat(data, ref pos), ReadFloat(data, ref pos));
                    float step = ReadFloat(data, ref pos);
                    int triCount = System.BitConverter.ToInt32(data, pos);
                    pos += 4;
                    if (triCount > 0) part.mesh = BuildMesh(id + " " + part.name, data, ref pos, triCount, step, slotCount, out part.slots);
                    else part.slots = new int[0];
                    model.parts[p] = part;
                }
                return model;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("SWAT: couldn't read model '" + id + "': " + e.Message);
                return null;
            }
        }

        // Flat shading: every triangle gets its own three vertices, so the facets stay crisp.
        static Mesh BuildMesh(string name, byte[] data, ref int pos, int triCount, float step, int slotCount, out int[] usedSlots)
        {
            var vertices = new Vector3[triCount * 3];
            var bySlot = new List<int>[slotCount];
            for (int t = 0; t < triCount; t++)
            {
                int slot = data[pos++];
                for (int k = 0; k < 3; k++)
                {
                    float x = System.BitConverter.ToInt16(data, pos) * step;
                    float y = System.BitConverter.ToInt16(data, pos + 2) * step;
                    float z = System.BitConverter.ToInt16(data, pos + 4) * step;
                    pos += 6;
                    vertices[t * 3 + k] = new Vector3(x, y, z);
                }
                if (slot >= slotCount) slot = 0;
                if (bySlot[slot] == null) bySlot[slot] = new List<int>();
                bySlot[slot].Add(t * 3);
                bySlot[slot].Add(t * 3 + 1);
                bySlot[slot].Add(t * 3 + 2);
            }
            var mesh = new Mesh { name = name };
            if (vertices.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            var used = new List<int>();
            for (int s = 0; s < slotCount; s++) if (bySlot[s] != null) used.Add(s);
            mesh.subMeshCount = used.Count;
            for (int i = 0; i < used.Count; i++) mesh.SetTriangles(bySlot[used[i]], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            usedSlots = used.ToArray();
            return mesh;
        }

        static string ReadString(byte[] data, ref int pos)
        {
            int length = data[pos++];
            string text = System.Text.Encoding.UTF8.GetString(data, pos, length);
            pos += length;
            return text;
        }

        static float ReadFloat(byte[] data, ref int pos)
        {
            float value = System.BitConverter.ToSingle(data, pos);
            pos += 4;
            return value;
        }

        // A renderer for one part under a parent, coloured slot by slot (recolor may change any slot,
        // by its name; return the colour you're given to keep it).
        public static GameObject Spawn(Model model, string partName, Transform parent, Vector3 localPosition, System.Func<string, Color, Color> recolor = null)
        {
            return Spawn(model, partName, parent, localPosition, recolor, null);
        }

        // hidden: colour slots to leave out (the soldier's helmet, goggles, vest...), so one model
        // covers many kits. The trimmed mesh is made once per combination.
        public static GameObject Spawn(Model model, string partName, Transform parent, Vector3 localPosition, System.Func<string, Color, Color> recolor, ICollection<string> hidden)
        {
            return Spawn(model, partName, parent, localPosition, recolor, hidden, null);
        }

        public static GameObject Spawn(Model model, string partName, Transform parent, Vector3 localPosition, System.Func<string, Color, Color> recolor, ICollection<string> hidden, Camo camo)
        {
            var part = model != null ? model.Find(partName) : null;
            if (part == null || part.mesh == null) return null;
            Mesh mesh = part.mesh;
            int[] slots = part.slots;
            int[] layers = null;
            if ((hidden != null && hidden.Count > 0) || camo != null)
            {
                var variant = MakeVariant(model, part, hidden, camo);
                mesh = variant.mesh;
                slots = variant.slots;
                layers = variant.layers;
            }
            if (slots.Length == 0) return null;
            var go = new GameObject(model.id + " " + partName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            var materials = new Material[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                int slot = slots[i];
                Color color = model.slotColors[slot];
                if (recolor != null) color = recolor(model.slotNames[slot], color);
                if (layers != null && layers[i] >= 0) color = camo.colors[layers[i]];
                materials[i] = Shapes.Mat(color);
            }
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        static Variant MakeVariant(Model model, Part part, ICollection<string> hidden, Camo camo)
        {
            var key = new System.Text.StringBuilder(model.id).Append('/').Append(part.name);
            for (int i = 0; i < part.slots.Length; i++)
                if (hidden != null && hidden.Contains(model.slotNames[part.slots[i]])) key.Append('-').Append(i);
            bool camouflaged = false;
            if (camo != null)
                for (int i = 0; i < part.slots.Length; i++)
                    if (System.Array.IndexOf(camo.slots, model.slotNames[part.slots[i]]) >= 0) camouflaged = true;
            if (camouflaged) key.Append('/').Append(camo.id);
            Variant made;
            if (variants.TryGetValue(key.ToString(), out made) && made.mesh != null) return made;

            var vertices = new List<Vector3>(part.mesh.vertices);
            var groups = new List<List<int>>();
            var slots = new List<int>();
            var layers = new List<int>();
            for (int s = 0; s < part.slots.Length; s++)
            {
                string name = model.slotNames[part.slots[s]];
                if (hidden != null && hidden.Contains(name)) continue;
                var triangles = part.mesh.GetTriangles(s);
                if (!camouflaged || System.Array.IndexOf(camo.slots, name) < 0)
                {
                    groups.Add(new List<int>(triangles));
                    slots.Add(part.slots[s]);
                    layers.Add(-1);
                    continue;
                }
                // One sub-mesh per camouflage colour used on this slot.
                var byLayer = new List<int>[camo.colors.Length];
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                    Vector3 ab = (a + b) * 0.5f, bc = (b + c) * 0.5f, ca = (c + a) * 0.5f;
                    AddCamo(vertices, byLayer, camo, part.pivot, a, ab, ca, part.slots[s]);
                    AddCamo(vertices, byLayer, camo, part.pivot, ab, b, bc, part.slots[s]);
                    AddCamo(vertices, byLayer, camo, part.pivot, ca, bc, c, part.slots[s]);
                    AddCamo(vertices, byLayer, camo, part.pivot, ab, bc, ca, part.slots[s]);
                }
                for (int l = 0; l < byLayer.Length; l++)
                {
                    if (byLayer[l] == null) continue;
                    groups.Add(byLayer[l]);
                    slots.Add(part.slots[s]);
                    layers.Add(l);
                }
            }
            var mesh = new Mesh { name = part.mesh.name + " (variant)" };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.subMeshCount = groups.Count;
            for (int i = 0; i < groups.Count; i++) mesh.SetTriangles(groups[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            made = new Variant { mesh = mesh, slots = slots.ToArray(), layers = layers.ToArray() };
            variants[key.ToString()] = made;
            return made;
        }

        static void AddCamo(List<Vector3> vertices, List<int>[] byLayer, Camo camo, Vector3 pivot, Vector3 a, Vector3 b, Vector3 c, int slotSalt)
        {
            Vector3 centre = (a + b + c) / 3f + pivot;
            float v = Noise(centre / camo.scale, camo.seed + slotSalt * 3) * 0.65f + Noise(centre * (2.3f / camo.scale), camo.seed + slotSalt * 3 + 7) * 0.35f;
            int layer = 0;
            for (int k = 0; k < camo.cuts.Length && k + 1 < camo.colors.Length; k++) if (v > camo.cuts[k]) layer = k + 1;
            if (byLayer[layer] == null) byLayer[layer] = new List<int>();
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            byLayer[layer].Add(start);
            byLayer[layer].Add(start + 1);
            byLayer[layer].Add(start + 2);
        }

        // Smooth 3D value noise in 0..1.
        static float Noise(Vector3 p, int seed)
        {
            int i = Mathf.FloorToInt(p.x), j = Mathf.FloorToInt(p.y), k = Mathf.FloorToInt(p.z);
            float fx = p.x - i, fy = p.y - j, fz = p.z - k;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy); fz = fz * fz * (3f - 2f * fz);
            float x00 = Mathf.Lerp(Hash(i, j, k, seed), Hash(i + 1, j, k, seed), fx);
            float x10 = Mathf.Lerp(Hash(i, j + 1, k, seed), Hash(i + 1, j + 1, k, seed), fx);
            float x01 = Mathf.Lerp(Hash(i, j, k + 1, seed), Hash(i + 1, j, k + 1, seed), fx);
            float x11 = Mathf.Lerp(Hash(i, j + 1, k + 1, seed), Hash(i + 1, j + 1, k + 1, seed), fx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, fy), Mathf.Lerp(x01, x11, fy), fz);
        }

        static float Hash(int i, int j, int k, int seed)
        {
            unchecked
            {
                uint h = (uint)(i * 374761393 + j * 668265263 + k * 2147483647 + seed * 144269504);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cache.Clear();
            missing.Clear();
            variants.Clear();
        }
    }
}
