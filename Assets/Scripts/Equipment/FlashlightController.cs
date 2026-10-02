using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // A character's flashlight. Turning it on lets you (and your squad) spot
    // people in dark rooms at full range, but it also makes you easier to see.
    // A soft cone of light is drawn on the floor on every graphics tier; the
    // real spot light is only used on tiers with dynamic lights (and casts a
    // soft shadow for the team leader on Very High shadows).
    public class FlashlightController : MonoBehaviour
    {
        static readonly List<FlashlightController> active = new List<FlashlightController>();

        public bool On { get; private set; }
        public float Range { get; private set; }
        const float HalfAngle = 28f;
        Light beam;
        bool showBeam, isPlayer;
        float baseIntensity, nextRefresh, coneStrength;
        MeshRenderer cone;
        MaterialPropertyBlock block;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            active.Clear();
        }

        public void Init(Light light, float range, bool visibleBeam, bool player = false)
        {
            beam = light;
            Range = range;
            showBeam = visibleBeam;
            isPlayer = player;
            baseIntensity = light != null ? light.intensity : 0f;
            block = new MaterialPropertyBlock();
            float length = Mathf.Min(range * 0.75f, 9f);
            cone = DecalMesh.Single("Flashlight Cone", transform, Vector3.zero, new Vector2(length * 1.05f, length), 0f, new Color(1f, 0.95f, 0.82f, 1f), Shapes.GlowMaterial(ProceduralTextures.Cone));
            cone.transform.localPosition = new Vector3(0.1f, 0.06f, 0.35f + length * 0.5f);
            cone.transform.localRotation = Quaternion.identity;
            cone.enabled = false;
        }

        public void Set(bool on)
        {
            On = on;
            if (On && !active.Contains(this)) active.Add(this);
            if (!On) active.Remove(this);
            ApplyLight();
            nextRefresh = 0f;
            if (cone != null) cone.enabled = On;
        }

        void ApplyLight()
        {
            if (beam == null) return;
            var quality = QualityManager.Current;
            beam.enabled = On && showBeam && quality.dynamicLights;
            beam.intensity = baseIntensity * SaveManager.Settings.flashlightBrightness;
            beam.shadows = isPlayer && quality.shadows >= 4 ? LightShadows.Soft : LightShadows.None;
        }

        public void Toggle()
        {
            Set(!On);
            AudioManager.Play(Sound.FlashlightClick, transform.position, 0.5f, Random.Range(0.95f, 1.05f));
        }

        // The cone is faint in lit areas and strong in the dark; refreshed a few times a second.
        void LateUpdate()
        {
            if (!On || cone == null || Time.time < nextRefresh) return;
            nextRefresh = Time.time + 0.25f;
            var game = GameManager.Instance;
            float strength = 0.35f;
            if (game != null && game.Level != null)
            {
                var room = game.Level.RoomAt(transform.position + transform.forward * 2f);
                strength = room != null && room.IsDark ? 0.9f : game.Lighting.flashlight;
            }
            strength *= Mathf.Clamp(SaveManager.Settings.flashlightBrightness, 0.4f, 1.6f);
            if (!Mathf.Approximately(strength, coneStrength))
            {
                coneStrength = strength;
                block.SetColor("_Color", new Color(1f, 1f, 1f, Mathf.Clamp01(strength)));
                cone.SetPropertyBlock(block);
            }
            ApplyLight();
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
