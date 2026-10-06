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
            var part = model != null ? model.Find(partName) : null;
            if (part == null || part.mesh == null) return null;
            var go = new GameObject(model.id + " " + partName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<MeshFilter>().sharedMesh = part.mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            var materials = new Material[part.slots.Length];
            for (int i = 0; i < part.slots.Length; i++)
            {
                int slot = part.slots[i];
                Color color = model.slotColors[slot];
                if (recolor != null) color = recolor(model.slotNames[slot], color);
                materials[i] = Shapes.Mat(color);
            }
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cache.Clear();
            missing.Clear();
        }
    }
}
