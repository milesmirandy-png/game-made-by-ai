using UnityEngine;
using UnityEngine.Rendering;

namespace Swat
{
    // Restrained post-processing for the Built-in render pipeline: subtle
    // bloom on light sources, gentle color grading per lighting profile and a
    // mild vignette (see Resources/SWAT/Shaders/SwatPostFX.shader). Turned
    // off by the Post Processing setting, Performance Mode and low presets.
    // Under URP this component isn't used; UIManager draws a vignette overlay instead.
    [RequireComponent(typeof(Camera))]
    public class PostEffects : MonoBehaviour
    {
        public static bool Active { get; private set; }

        struct Grade
        {
            public float exposure, contrast, saturation, vignette, vignetteSize;
            public Color lift, gain;
        }

        Material material;
        Grade grade = Neutral();

        public static PostEffects Attach(Camera cam)
        {
            if (GraphicsSettings.currentRenderPipeline != null) return null;
            var effects = cam.GetComponent<PostEffects>();
            if (effects == null) effects = cam.gameObject.AddComponent<PostEffects>();
            return effects;
        }

        void Awake()
        {
            var shader = Resources.Load<Shader>("SWAT/Shaders/SwatPostFX");
            if (shader == null || !shader.isSupported)
            {
                enabled = false;
                return;
            }
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnEnable() { Active = material != null; }
        void OnDisable() { Active = false; }

        static Grade Neutral()
        {
            return new Grade { exposure = 1f, contrast = 1.04f, saturation = 1f, vignette = 0.18f, vignetteSize = 0.55f, lift = new Color(0.98f, 0.99f, 1.02f), gain = new Color(1.01f, 1f, 0.99f) };
        }

        public void SetProfile(TimeOfDay? time)
        {
            if (time == null) { grade = Neutral(); return; }
            switch (time.Value)
            {
                case TimeOfDay.Evening:
                    grade = new Grade { exposure = 1f, contrast = 1.08f, saturation = 1f, vignette = 0.24f, vignetteSize = 0.5f, lift = new Color(0.94f, 0.96f, 1.06f), gain = new Color(1.05f, 0.99f, 0.92f) };
                    break;
                case TimeOfDay.Night:
                    grade = new Grade { exposure = 1.08f, contrast = 1.1f, saturation = 0.86f, vignette = 0.3f, vignetteSize = 0.45f, lift = new Color(0.92f, 0.96f, 1.1f), gain = new Color(1f, 1f, 1.02f) };
                    break;
                default:
                    grade = new Grade { exposure = 1f, contrast = 1.06f, saturation = 1.05f, vignette = 0.2f, vignetteSize = 0.55f, lift = new Color(0.97f, 0.99f, 1.03f), gain = new Color(1.02f, 1f, 0.97f) };
                    break;
            }
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null || !QualityManager.PostProcessingOn)
            {
                Graphics.Blit(source, destination);
                return;
            }
            RenderTexture bloom = null;
            if (QualityManager.Current.bloom)
            {
                int w = Mathf.Max(16, source.width / 4), h = Mathf.Max(16, source.height / 4);
                var a = RenderTexture.GetTemporary(w, h, 0, source.format);
                var b = RenderTexture.GetTemporary(w, h, 0, source.format);
                material.SetFloat("_Threshold", 0.78f);
                Graphics.Blit(source, a, material, 0);
                material.SetFloat("_BlurOffset", 1f);
                Graphics.Blit(a, b, material, 1);
                material.SetFloat("_BlurOffset", 2f);
                Graphics.Blit(b, a, material, 1);
                RenderTexture.ReleaseTemporary(b);
                bloom = a;
                material.SetTexture("_BloomTex", bloom);
                material.SetFloat("_BloomIntensity", SaveManager.Settings.reduceFlashes ? 0.18f : 0.32f);
            }
            else
            {
                material.SetTexture("_BloomTex", Texture2D.blackTexture);
                material.SetFloat("_BloomIntensity", 0f);
            }
            material.SetFloat("_Exposure", grade.exposure);
            material.SetFloat("_Contrast", grade.contrast);
            material.SetFloat("_Saturation", grade.saturation);
            material.SetVector("_Lift", grade.lift);
            material.SetVector("_Gain", grade.gain);
            material.SetFloat("_VignetteStrength", grade.vignette);
            material.SetFloat("_VignetteSize", grade.vignetteSize);
            Graphics.Blit(source, destination, material, 2);
            if (bloom != null) RenderTexture.ReleaseTemporary(bloom);
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
