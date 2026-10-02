using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // A character's flashlight. Turning it on lets you (and your squad) spot
    // people in dark rooms at full range, but it also makes you easier to see.
    // The actual Light is only enabled on tiers with dynamic lights; the
    // gameplay effect works on every tier.
    public class FlashlightController : MonoBehaviour
    {
        static readonly List<FlashlightController> active = new List<FlashlightController>();

        public bool On { get; private set; }
        public float Range { get; private set; }
        const float HalfAngle = 28f;
        Light beam;
        bool showBeam;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            active.Clear();
        }

        public void Init(Light light, float range, bool visibleBeam)
        {
            beam = light;
            Range = range;
            showBeam = visibleBeam;
        }

        public void Set(bool on)
        {
            On = on;
            if (On && !active.Contains(this)) active.Add(this);
            if (!On) active.Remove(this);
            if (beam != null) beam.enabled = On && showBeam && QualityManager.Current.dynamicLights;
        }

        public void Toggle()
        {
            Set(!On);
            AudioManager.Play(Sound.Click, transform.position, 0.4f, 1.4f);
        }

        public static bool Illuminates(Vector3 point)
        {
            for (int i = 0; i < active.Count; i++)
            {
                var light = active[i];
                if (light == null) continue;
                Vector3 to = point - light.transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > light.Range * light.Range) continue;
                if (Vector3.Angle(light.transform.forward, to) < HalfAngle) return true;
            }
            return false;
        }

        void OnDisable()
        {
            active.Remove(this);
        }
    }
}
