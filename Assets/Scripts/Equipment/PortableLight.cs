using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // A battery lamp placed on the floor. Lights a radius for a while, then switches off.
    public class PortableLight : MonoBehaviour
    {
        static readonly List<PortableLight> active = new List<PortableLight>();

        float radius, expires;
        Light lamp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            active.Clear();
        }

        public static void Place(Vector3 position, float radius, float duration, Transform parent)
        {
            var go = new GameObject("Portable Light");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            Shapes.Box("Lamp Body", go.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.22f, 0.24f, 0.22f), new Color(0.2f, 0.2f, 0.22f), false);
            Shapes.Box("Lamp Lens", go.transform, new Vector3(0f, 0.27f, 0f), new Vector3(0.18f, 0.06f, 0.18f), new Color(1f, 0.95f, 0.8f), false, 3f);
            var light = go.AddComponent<PortableLight>();
            light.radius = radius;
            light.expires = Time.time + duration;
            if (QualityManager.Current.dynamicLights) light.lamp = Shapes.PointLight(go.transform, new Vector3(0f, 0.8f, 0f), new Color(1f, 0.95f, 0.85f), 1.8f, radius);
            active.Add(light);
            AudioManager.Play(Sound.LightPlace, position, 0.6f);
        }

        public static bool Illuminates(Vector3 point)
        {
            for (int i = 0; i < active.Count; i++)
            {
                var light = active[i];
                if (light == null) continue;
                Vector3 offset = point - light.transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude < light.radius * light.radius) return true;
            }
            return false;
        }

        void Update()
        {
            if (Time.time < expires) return;
            active.Remove(this);
            if (lamp != null) lamp.enabled = false;
            enabled = false;
        }

        void OnDestroy()
        {
            active.Remove(this);
        }
    }
}
