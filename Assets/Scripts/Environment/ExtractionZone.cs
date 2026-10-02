using UnityEngine;

namespace Swat
{
    // A marked area outside: the extraction point by the van, and the safe
    // zone where escorted civilians are counted as evacuated.
    public class ExtractionZone : MonoBehaviour
    {
        public Bounds Bounds { get; private set; }
        public bool IsSafeZone { get; private set; }
        public int Area { get; private set; }

        public static ExtractionZone Create(Transform parent, string name, Bounds bounds, bool safeZone, int area, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = bounds.center;
            var zone = go.AddComponent<ExtractionZone>();
            zone.Bounds = bounds;
            zone.IsSafeZone = safeZone;
            zone.Area = area;
            Shapes.Box("Marking", go.transform, new Vector3(0f, 0.025f - bounds.center.y, 0f), new Vector3(bounds.size.x, 0.01f, bounds.size.z), color, false, 0.4f);
            Shapes.Box("Beacon", go.transform, new Vector3(bounds.extents.x - 0.2f, 1.2f - bounds.center.y, bounds.extents.z - 0.2f), new Vector3(0.12f, 2.4f, 0.12f), color, false, 1.5f);
            return zone;
        }

        public bool Contains(Vector3 point)
        {
            var b = Bounds;
            return point.x >= b.min.x && point.x <= b.max.x && point.z >= b.min.z && point.z <= b.max.z;
        }
    }
}
