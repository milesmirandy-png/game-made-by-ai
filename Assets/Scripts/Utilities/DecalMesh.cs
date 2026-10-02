using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Swat
{
    // Collects many flat quads (contact shadows, light pools, markings) and
    // bakes them into a single mesh, so a whole map's worth of decals costs
    // one draw call per material instead of one per quad.
    public class DecalMesh
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Color32> colors = new List<Color32>();
        readonly List<int> triangles = new List<int>();

        public int QuadCount { get { return vertices.Count / 4; } }

        // A quad lying flat at 'center', 'size' in meters, rotated by 'yaw' degrees.
        public void AddFlat(Vector3 center, Vector2 size, float yaw, Color color)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            AddQuad(center, rotation * Vector3.right * size.x * 0.5f, rotation * Vector3.forward * size.y * 0.5f, color);
        }

        // A general quad from its center and two half-extent vectors (u along 'halfRight', v along 'halfUp').
        public void AddQuad(Vector3 center, Vector3 halfRight, Vector3 halfUp, Color color)
        {
            int start = vertices.Count;
            vertices.Add(center - halfRight - halfUp);
            vertices.Add(center + halfRight - halfUp);
            vertices.Add(center + halfRight + halfUp);
            vertices.Add(center - halfRight + halfUp);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 1f));
            Color32 c = color;
            for (int i = 0; i < 4; i++) colors.Add(c);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
        }

        public GameObject Build(string name, Transform parent, Material material)
        {
            if (vertices.Count == 0) return null;
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        // One standalone quad (for things that change on their own, like a flickering light pool).
        public static MeshRenderer Single(string name, Transform parent, Vector3 center, Vector2 size, float yaw, Color color, Material material)
        {
            var decal = new DecalMesh();
            decal.AddFlat(Vector3.zero, size, 0f, color);
            var go = decal.Build(name, parent, material);
            go.transform.position = center;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go.GetComponent<MeshRenderer>();
        }
    }
}
