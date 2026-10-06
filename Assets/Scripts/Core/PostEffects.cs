using UnityEngine;
using UnityEngine.Rendering;

namespace Swat
{
    // Restrained post-processing for the Built-in render pipeline: subtle
    // bloom on light sources, gentle color grading per lighting profile and a
    // mild vignette (see Resources/SWAT/Shaders/SwatPostFX.shader). Turned
    // off by the Post Processing setting, Performance Mode and low presets.
    // In the pixel-art style it always adds sprite outlines (optional) and a
    // reduced, dithered palette, even when the rest of post-processing is off.
    // The first-person body cam lens (distortion, fringing, grain) is applied
    // whenever that view and the Body cam look setting are on.
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

        bool Outlines
        {
            get
            {
                var cam = GetComponent<Camera>();
                return material != null && QualityManager.PixelArt && SaveManager.Settings.pixelOutlines && cam != null && cam.orthographic;
            }
        }

        // The outline pass needs the camera's depth texture (one extra cheap depth pass at pixel resolution).
        void Update()
        {
            var cam = GetComponent<Camera>();
            if (cam == null) return;
            if (Outlines) cam.depthTextureMode |= DepthTextureMode.Depth;
            else cam.depthTextureMode &= ~DepthTextureMode.Depth;
        }

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
            bool post = QualityManager.PostProcessingOn;
            bool pixel = QualityManager.PixelArt;
            bool bodyCam = FirstPersonRig.Active && SaveManager.Settings.bodyCamLook;
            if (material == null || (!post && !pixel && !bodyCam))
            {
                Graphics.Blit(source, destination);
                return;
            }
            RenderTexture bloom = null;
            if (post && QualityManager.Current.bloom)
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
            // Without post-processing the grade is neutral and only the pixel-art steps apply.
            var g = post ? grade : new Grade { exposure = 1f, contrast = 1f, saturation = 1f, vignette = 0f, vignetteSize = 1f, lift = Color.white, gain = Color.white };
            if (bodyCam)
            {
                // A small wide-angle camera: darker corners, slightly flatter colour.
                g.vignette = Mathf.Max(g.vignette, 0.45f);
                g.vignetteSize = Mathf.Min(g.vignetteSize, 0.38f);
                g.saturation *= 0.92f;
            }
            // Through a scope (or on the way to the sights) the lens straightens out.
            float lens = bodyCam && !FirstPersonRig.Scoped ? 1f - 0.6f * FirstPersonRig.AimBlend : 0f;
            material.SetFloat("_BodyCam", bodyCam ? 1f : 0f);
            material.SetFloat("_Barrel", 0.24f * lens);
            material.SetFloat("_Aberration", 0.012f * lens);
            material.SetFloat("_Grain", bodyCam ? (pixel ? 0.025f : 0.045f) : 0f);
            material.SetFloat("_Exposure", g.exposure);
            material.SetFloat("_Contrast", g.contrast);
            material.SetFloat("_Saturation", g.saturation * (pixel ? 1.12f : 1f));
            material.SetVector("_Lift", g.lift);
            material.SetVector("_Gain", g.gain);
            material.SetFloat("_VignetteStrength", g.vignette);
            material.SetFloat("_VignetteSize", g.vignetteSize);
            var cam = GetComponent<Camera>();
            material.SetFloat("_PixelArt", pixel ? 1f : 0f);
            material.SetFloat("_Levels", 18f);
            material.SetFloat("_Outline", Outlines ? 1f : 0f);
            // About half a metre of depth difference, in 0-1 depth units.
            material.SetFloat("_OutlineDepth", cam != null ? 0.45f / Mathf.Max(1f, cam.farClipPlane - cam.nearClipPlane) : 0.003f);
            Graphics.Blit(source, destination, material, 2);
            if (bloom != null) RenderTexture.ReleaseTemporary(bloom);
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
