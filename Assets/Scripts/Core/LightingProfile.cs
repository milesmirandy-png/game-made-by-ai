using UnityEngine;
using UnityEngine.Rendering;

namespace Swat
{
    public enum TimeOfDay { Day, Evening, Night }

    // A mission's lighting setup: sun, ambient light, haze, how strongly the
    // interior fixtures and exterior lamps read, and how dark unpowered rooms
    // look. Profiles replace any dynamic time-of-day simulation.
    public struct LightingProfile
    {
        public TimeOfDay time;
        public Color sunColor;
        public float sunIntensity;
        public Vector3 sunAngles;
        public float shadowStrength;
        public Color ambientSky, ambientEquator, ambientGround;
        public Color fogColor;
        public float fogStart, fogEnd;
        public float interior;        // multiplier on fixture light intensity
        public float pool;            // strength of the light pools under fixtures
        public bool lamps;            // exterior lamps and building lights on
        public float flashlight;      // how visible a flashlight's cone is in lit areas
        public float darkness;        // opacity of the shading over unpowered rooms

        public static readonly string[] Names = { "Day", "Evening", "Night" };

        public static LightingProfile For(TimeOfDay time)
        {
            switch (time)
            {
                case TimeOfDay.Evening:
                    return new LightingProfile
                    {
                        time = time,
                        sunColor = new Color(1f, 0.62f, 0.38f), sunIntensity = 0.62f, sunAngles = new Vector3(22f, -62f, 0f), shadowStrength = 0.75f,
                        ambientSky = new Color(0.38f, 0.4f, 0.52f), ambientEquator = new Color(0.36f, 0.31f, 0.32f), ambientGround = new Color(0.17f, 0.15f, 0.15f),
                        fogColor = new Color(0.36f, 0.31f, 0.34f), fogStart = 40f, fogEnd = 115f,
                        interior = 1f, pool = 0.26f, lamps = true, flashlight = 0.35f, darkness = 0.55f,
                    };
                case TimeOfDay.Night:
                    return new LightingProfile
                    {
                        time = time,
                        sunColor = new Color(0.5f, 0.6f, 0.9f), sunIntensity = 0.16f, sunAngles = new Vector3(62f, 28f, 0f), shadowStrength = 0.5f,
                        ambientSky = new Color(0.12f, 0.15f, 0.24f), ambientEquator = new Color(0.09f, 0.1f, 0.15f), ambientGround = new Color(0.05f, 0.05f, 0.07f),
                        fogColor = new Color(0.035f, 0.045f, 0.08f), fogStart = 35f, fogEnd = 100f,
                        interior = 1.4f, pool = 0.4f, lamps = true, flashlight = 0.75f, darkness = 0.7f,
                    };
                default:
                    return new LightingProfile
                    {
                        time = TimeOfDay.Day,
                        sunColor = new Color(1f, 0.95f, 0.86f), sunIntensity = 1.05f, sunAngles = new Vector3(52f, -35f, 0f), shadowStrength = 0.8f,
                        ambientSky = new Color(0.62f, 0.68f, 0.78f), ambientEquator = new Color(0.5f, 0.52f, 0.54f), ambientGround = new Color(0.32f, 0.31f, 0.3f),
                        fogColor = new Color(0.6f, 0.66f, 0.73f), fogStart = 50f, fogEnd = 130f,
                        interior = 0.55f, pool = 0.12f, lamps = false, flashlight = 0.12f, darkness = 0.35f,
                    };
            }
        }

        // Sun, ambient, haze and background. Without dynamic lights (lowest tiers) the
        // ambient light is raised so night missions stay readable; light pools still show.
        public void ApplyEnvironment(Light sun, Camera cam, bool dynamicLights, float viewDistance)
        {
            sun.color = sunColor;
            sun.intensity = sunIntensity;
            sun.transform.rotation = Quaternion.Euler(sunAngles);
            sun.shadowStrength = shadowStrength;

            float boost = dynamicLights || time == TimeOfDay.Day ? 1f : time == TimeOfDay.Night ? 1.8f : 1.3f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky * boost;
            RenderSettings.ambientEquatorColor = ambientEquator * boost;
            RenderSettings.ambientGroundColor = ambientGround * boost;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart * viewDistance;
            RenderSettings.fogEndDistance = fogEnd * viewDistance;
            cam.backgroundColor = fogColor;
            cam.farClipPlane = Mathf.Max(60f, 160f * viewDistance);
        }

        // Neutral daylight for the HQ diorama behind the menus.
        public static void ApplyHeadquarters(Light sun, Camera cam)
        {
            sun.color = new Color(1f, 0.95f, 0.88f);
            sun.intensity = 1f;
            sun.transform.rotation = Quaternion.Euler(50f, -40f, 0f);
            sun.shadowStrength = 0.75f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.54f, 0.62f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.43f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.25f, 0.26f);
            RenderSettings.fog = false;
            cam.backgroundColor = UITheme.Background;
            cam.farClipPlane = 160f;
        }
    }
}
