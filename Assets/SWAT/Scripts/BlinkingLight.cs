using UnityEngine;

namespace Swat
{
    // Police sirens (alternating lights) and broken flickering ceiling lamps.
    public class BlinkingLight : MonoBehaviour
    {
        public Light[] lights;
        public Renderer[] glows;
        public bool siren;

        float seed;
        float[] baseIntensity;

        void Start()
        {
            seed = Random.value * 100f;
            baseIntensity = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) baseIntensity[i] = lights[i].intensity;
        }

        void Update()
        {
            if (siren)
            {
                bool firstOn = Mathf.Repeat(Time.time * 2.5f + seed, 1f) < 0.5f;
                for (int i = 0; i < lights.Length; i++)
                {
                    bool on = (i % 2 == 0) == firstOn;
                    lights[i].enabled = on;
                    if (glows != null && i < glows.Length) glows[i].enabled = on;
                }
            }
            else
            {
                bool on = Mathf.PerlinNoise(Time.time * 4f, seed) > 0.35f;
                for (int i = 0; i < lights.Length; i++) lights[i].intensity = on ? baseIntensity[i] : baseIntensity[i] * 0.15f;
                if (glows != null) foreach (var glow in glows) glow.enabled = on;
            }
        }
    }
}
