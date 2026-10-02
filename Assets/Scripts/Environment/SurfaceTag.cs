using UnityEngine;

namespace Swat
{
    // Marks what a collider is made of, for bullet impacts and footsteps.
    public class SurfaceTag : MonoBehaviour
    {
        public Surface surface = Surface.Concrete;

        public static Surface Of(Collider collider)
        {
            if (collider == null) return Surface.Concrete;
            var tag = collider.GetComponent<SurfaceTag>();
            return tag != null ? tag.surface : Surface.Concrete;
        }

        public static void Set(GameObject go, Surface surface)
        {
            var tag = go.GetComponent<SurfaceTag>();
            if (tag == null) tag = go.AddComponent<SurfaceTag>();
            tag.surface = surface;
        }
    }
}
