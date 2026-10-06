using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Low-poly gear pieces that fit the imported soldier: shells grown out from the model's own
    // surface (hair, beards, cap crowns, shoulder guards, a gas mask), plus brims and a curved face
    // shield. A shell copies the chosen triangles of a part, splits each one in four with the new
    // points pushed out a little (so it's rounder than the original), then moves everything out along
    // the surface normal. It follows the head exactly, so nothing floats or pokes through like a box
    // would. Meshes are made once and shared.
    public static class MeshKit
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, float> measured = new Dictionary<string, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cache.Clear();
            measured.Clear();
        }

        // How far forward (+z) a slot's surface is at (x, y) in a part: where eyes and brows go on the face.
        public static float FrontZ(ModelLibrary.Model model, string partName, string slot, float x, float y, float fallback)
        {
            string key = model.id + "/" + partName + "/" + slot + "/z/" + x + "/" + y;
            float z;
            if (measured.TryGetValue(key, out z)) return z;
            z = fallback;
            bool any = false;
            var part = model.Find(partName);
            if (part != null && part.mesh != null)
            {
                var v = part.mesh.vertices;
                for (int s = 0; s < part.slots.Length; s++)
                {
                    if (model.slotNames[part.slots[s]] != slot) continue;
                    var t = part.mesh.GetTriangles(s);
                    for (int i = 0; i + 2 < t.Length; i += 3)
                    {
                        Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                        float d = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                        if (Mathf.Abs(d) < 1e-12f) continue;
                        float w0 = ((b.y - c.y) * (x - c.x) + (c.x - b.x) * (y - c.y)) / d;
                        float w1 = ((c.y - a.y) * (x - c.x) + (a.x - c.x) * (y - c.y)) / d;
                        float w2 = 1f - w0 - w1;
                        if (w0 < -1e-5f || w1 < -1e-5f || w2 < -1e-5f) continue;
                        float hz = w0 * a.z + w1 * b.z + w2 * c.z;
                        if (!any || hz > z) z = hz;
                        any = true;
                    }
                }
            }
            measured[key] = z;
            return z;
        }

        // The outermost x of a part's vertices between two heights (sign -1 for the left side).
        public static float OuterX(ModelLibrary.Model model, string partName, float y0, float y1, float sign, float fallback)
        {
            string key = model.id + "/" + partName + "/x/" + y0 + "/" + y1 + "/" + sign;
            float x;
            if (measured.TryGetValue(key, out x)) return x;
            x = fallback;
            var part = model.Find(partName);
            if (part != null && part.mesh != null)
            {
                bool any = false;
                foreach (var p in part.mesh.vertices)
                {
                    if (p.y < y0 || p.y > y1) continue;
                    if (!any || p.x * sign > x * sign) x = p.x;
                    any = true;
                }
            }
            measured[key] = x;
            return x;
        }

        // keep(centre, highest y) picks triangles of the given slots (positions relative to the part's pivot).
        public static Mesh Shell(ModelLibrary.Model model, string partName, string[] slots, System.Func<Vector3, float, bool> keep, float offset, float bulge, string key)
        {
            if (model == null) return null;
            key = model.id + "/" + key;
            Mesh mesh;
            if (cache.TryGetValue(key, out mesh) && mesh != null) return mesh;
            var part = model != null ? model.Find(partName) : null;
            if (part == null || part.mesh == null) return null;
            var vertices = part.mesh.vertices;
            var chosen = new List<int>();
            for (int s = 0; s < part.slots.Length; s++)
            {
                if (System.Array.IndexOf(slots, model.slotNames[part.slots[s]]) < 0) continue;
                var triangles = part.mesh.GetTriangles(s);
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                    if (!keep((a + b + c) / 3f, Mathf.Max(a.y, Mathf.Max(b.y, c.y)))) continue;
                    chosen.Add(triangles[i]);
                    chosen.Add(triangles[i + 1]);
                    chosen.Add(triangles[i + 2]);
                }
            }
            if (chosen.Count == 0) return null;
            // Smooth normals over welded positions (the model's halves meet with tiny gaps, so weld coarsely).
            var normals = new Dictionary<Vector3Int, Vector3>();
            for (int i = 0; i < chosen.Count; i += 3)
            {
                Vector3 a = vertices[chosen[i]], b = vertices[chosen[i + 1]], c = vertices[chosen[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                Accumulate(normals, a, n);
                Accumulate(normals, b, n);
                Accumulate(normals, c, n);
            }
            var output = new List<Vector3>(chosen.Count * 4);
            for (int i = 0; i < chosen.Count; i += 3)
            {
                Vector3 a = vertices[chosen[i]], b = vertices[chosen[i + 1]], c = vertices[chosen[i + 2]];
                Vector3 na = Normal(normals, a), nb = Normal(normals, b), nc = Normal(normals, c);
                Vector3 nab, nbc, nca;
                Vector3 ab = Middle(a, b, na, nb, bulge, out nab), bc = Middle(b, c, nb, nc, bulge, out nbc), ca = Middle(c, a, nc, na, bulge, out nca);
                Vector3 A = a + na * offset, B = b + nb * offset, C = c + nc * offset;
                Vector3 AB = ab + nab * offset, BC = bc + nbc * offset, CA = ca + nca * offset;
                output.Add(A); output.Add(AB); output.Add(CA);
                output.Add(AB); output.Add(B); output.Add(BC);
                output.Add(CA); output.Add(BC); output.Add(C);
                output.Add(AB); output.Add(BC); output.Add(CA);
            }
            mesh = Flat(key, output, false);
            cache[key] = mesh;
            return mesh;
        }

        static Vector3Int Weld(Vector3 p)
        {
            return new Vector3Int(Mathf.RoundToInt(p.x / 0.004f), Mathf.RoundToInt(p.y / 0.004f), Mathf.RoundToInt(p.z / 0.004f));
        }

        static void Accumulate(Dictionary<Vector3Int, Vector3> normals, Vector3 p, Vector3 n)
        {
            var key = Weld(p);
            Vector3 sum;
            normals.TryGetValue(key, out sum);
            normals[key] = sum + n;
        }

        static Vector3 Normal(Dictionary<Vector3Int, Vector3> normals, Vector3 p)
        {
            Vector3 n;
            return normals.TryGetValue(Weld(p), out n) && n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
        }

        static Vector3 Middle(Vector3 p, Vector3 q, Vector3 np, Vector3 nq, float bulge, out Vector3 n)
        {
            n = (np + nq).sqrMagnitude > 1e-8f ? (np + nq).normalized : np;
            return (p + q) * 0.5f + n * Vector3.Distance(p, q) * bulge;
        }

        // A brim: a fan (inner = 0) or a ring between two radii, from angle a0 to a1 (0 = straight ahead, +z),
        // drooping by droop at the outer edge. Both faces, so it shows from above and below.
        public static Mesh Brim(float radius, float a0, float a1, float droop, int segments, float inner, string key)
        {
            Mesh mesh;
            if (cache.TryGetValue(key, out mesh) && mesh != null) return mesh;
            var points = new List<Vector3>();
            for (int i = 0; i < segments; i++)
            {
                float u0 = Mathf.Lerp(a0, a1, i / (float)segments) * Mathf.Deg2Rad, u1 = Mathf.Lerp(a0, a1, (i + 1) / (float)segments) * Mathf.Deg2Rad;
                if (inner <= 0f)
                {
                    points.Add(Vector3.zero); points.Add(Rim(u0, radius, radius, droop)); points.Add(Rim(u1, radius, radius, droop));
                }
                else
                {
                    Vector3 p0 = Rim(u0, inner, radius, droop), p1 = Rim(u0, radius, radius, droop), p2 = Rim(u1, radius, radius, droop), p3 = Rim(u1, inner, radius, droop);
                    points.Add(p0); points.Add(p1); points.Add(p2);
                    points.Add(p0); points.Add(p2); points.Add(p3);
                }
            }
            mesh = Flat(key, points, true);
            cache[key] = mesh;
            return mesh;
        }

        static Vector3 Rim(float angle, float r, float radius, float droop)
        {
            float k = r / radius;
            return new Vector3(Mathf.Sin(angle) * r, -droop * k * k, Mathf.Cos(angle) * r);
        }

        // A curved face shield: part of an upright cylinder round the face, a0..a1 degrees, both faces.
        public static Mesh Arc(float radius, float a0, float a1, float height, int segments, string key)
        {
            Mesh mesh;
            if (cache.TryGetValue(key, out mesh) && mesh != null) return mesh;
            var points = new List<Vector3>();
            for (int i = 0; i < segments; i++)
            {
                float u0 = Mathf.Lerp(a0, a1, i / (float)segments) * Mathf.Deg2Rad, u1 = Mathf.Lerp(a0, a1, (i + 1) / (float)segments) * Mathf.Deg2Rad;
                Vector3 b0 = new Vector3(Mathf.Sin(u0) * radius, 0f, Mathf.Cos(u0) * radius), b1 = new Vector3(Mathf.Sin(u1) * radius, 0f, Mathf.Cos(u1) * radius);
                Vector3 t0 = b0 + Vector3.up * height, t1 = b1 + Vector3.up * height;
                points.Add(b0); points.Add(t0); points.Add(t1);
                points.Add(b0); points.Add(t1); points.Add(b1);
            }
            mesh = Flat(key, points, true);
            cache[key] = mesh;
            return mesh;
        }

        // Flat-shaded mesh from a triangle list (optionally with every triangle also facing the other way).
        static Mesh Flat(string name, List<Vector3> points, bool twoSided)
        {
            if (twoSided)
            {
                int count = points.Count;
                for (int i = 0; i < count; i += 3)
                {
                    points.Add(points[i]);
                    points.Add(points[i + 2]);
                    points.Add(points[i + 1]);
                }
            }
            var triangles = new int[points.Count];
            for (int i = 0; i < triangles.Length; i++) triangles[i] = i;
            var mesh = new Mesh { name = name };
            mesh.SetVertices(points);
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // A renderer for one of these meshes, in one colour.
        public static GameObject Piece(string name, Transform parent, Mesh mesh, Color color, Vector3 position, Quaternion rotation)
        {
            if (mesh == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Shapes.Mat(color);
            return go;
        }
    }
}
