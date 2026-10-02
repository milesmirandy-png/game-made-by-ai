using UnityEngine;

namespace Swat
{
    public enum QualityTier { Potato, Low, Medium, High, Ultra }

    // Makes the game run on anything from a potato to a gaming beast.
    //  * Picks a graphics preset from the hardware on first launch.
    //  * In Auto mode, drops a preset if the frame rate stays low.
    //  * Individual options (shadows, anti-aliasing, effects, VSync, ambient
    //    occlusion, post-processing, view distance) can override the preset.
    //  * Performance Mode trims the expensive parts while keeping the look coherent.
    //  * Low presets render the 3D view at reduced resolution (the HUD stays
    //    sharp), turn off shadows and real-time lights, and update AI less often.
    public class QualityManager : MonoBehaviour
    {
        public struct Profile
        {
            public string name;
            public float renderScale;     // fraction of screen resolution the world is drawn at
            public int shadows;           // 0 off, 1 low, 2 medium, 3 high, 4 very high
            public int msaa;
            public int pixelLights;
            public float particleScale;   // multiplier on debris, shells and smoke puffs
            public bool dynamicLights;    // muzzle flashes, explosions and flashlights light the scene
            public bool fixtureLights;    // room fixtures and street lamps are real lights (pools always show)
            public bool contactShadows;   // baked-style ambient occlusion decals
            public bool post;             // post-processing (grading, vignette, bloom)
            public bool bloom;
            public float aiThinkInterval; // seconds between AI decisions
            public int targetFps;
            public bool vSync;
            public int textures;          // generated surface texture size: 0 low, 1 medium, 2 high
        }

        public static readonly string[] ShadowNames = { "Off", "Low", "Medium", "High", "Very High" };
        public static readonly string[] EffectsNames = { "Low", "Medium", "High" };

        static readonly Profile[] Profiles =
        {
            new Profile { name = "Potato", renderScale = 0.5f, shadows = 0, msaa = 0, pixelLights = 0, particleScale = 0.35f, dynamicLights = false, fixtureLights = false, contactShadows = false, post = false, bloom = false, aiThinkInterval = 0.3f, targetFps = 60, vSync = false, textures = 0 },
            new Profile { name = "Low", renderScale = 0.75f, shadows = 1, msaa = 0, pixelLights = 1, particleScale = 0.6f, dynamicLights = false, fixtureLights = false, contactShadows = false, post = false, bloom = false, aiThinkInterval = 0.22f, targetFps = 60, vSync = false, textures = 0 },
            new Profile { name = "Medium", renderScale = 1f, shadows = 2, msaa = 0, pixelLights = 2, particleScale = 1f, dynamicLights = true, fixtureLights = true, contactShadows = true, post = true, bloom = false, aiThinkInterval = 0.15f, targetFps = 60, vSync = false, textures = 1 },
            new Profile { name = "High", renderScale = 1f, shadows = 3, msaa = 2, pixelLights = 4, particleScale = 1f, dynamicLights = true, fixtureLights = true, contactShadows = true, post = true, bloom = true, aiThinkInterval = 0.12f, targetFps = 0, vSync = true, textures = 2 },
            new Profile { name = "Ultra", renderScale = 1f, shadows = 4, msaa = 4, pixelLights = 6, particleScale = 1.3f, dynamicLights = true, fixtureLights = true, contactShadows = true, post = true, bloom = true, aiThinkInterval = 0.1f, targetFps = 0, vSync = true, textures = 2 },
        };

        const float LowFpsThreshold = 40f;
        const float LowFpsSeconds = 4f;

        public static QualityManager Instance { get; private set; }
        // The preset with the player's overrides and Performance Mode applied.
        public static Profile Current { get { return Instance != null ? Instance.effective : Profiles[(int)QualityTier.Medium]; } }
        public static int TierCount { get { return Profiles.Length; } }
        public static string TierName(int tier) { return Profiles[tier].name; }
        public static Profile PresetProfile(QualityTier tier) { return Profiles[(int)tier]; }
        public static bool AmbientOcclusionOn { get { return Current.contactShadows && SaveManager.Settings.ambientOcclusion; } }
        public static bool PostProcessingOn { get { return Current.post && SaveManager.Settings.postProcessing; } }
        // Texture detail: the setting when overridden, otherwise the preset's.
        public static int TextureLevel { get { int s = SaveManager.Settings.textureQuality; return s >= 0 ? Mathf.Clamp(s, 0, 2) : Current.textures; } }

        public QualityTier Tier { get; private set; }
        public bool IsAuto { get; private set; }
        public bool ShowFps { get; set; }
        public float Fps { get; private set; }
        public float FrameMs { get; private set; }
        public RenderTexture ScaledView { get; private set; }
        public string Hardware { get; private set; }
        public string Notice { get; private set; }
        public float NoticeTime { get; private set; }
        public event System.Action Changed;

        Profile effective;
        Camera worldCamera;
        Camera presentCamera;
        Light sun;
        float lowFpsTimer;
        float settleUntil;

        void Awake()
        {
            Instance = this;
            effective = Profiles[(int)QualityTier.Medium];
            Fps = 60f;
            Hardware = SystemInfo.graphicsDeviceName + "  |  " + SystemInfo.graphicsMemorySize + " MB VRAM  |  "
                + SystemInfo.systemMemorySize + " MB RAM  |  " + SystemInfo.processorCount + " cores";
        }

        public void Init(Camera cam, Light sunLight)
        {
            worldCamera = cam;
            sun = sunLight;

            // A camera that draws nothing, so the screen is cleared when the world is drawn to a texture.
            presentCamera = new GameObject("Present Camera").AddComponent<Camera>();
            presentCamera.transform.SetParent(transform, false);
            presentCamera.cullingMask = 0;
            presentCamera.clearFlags = CameraClearFlags.SolidColor;
            presentCamera.backgroundColor = Color.black;
            presentCamera.depth = cam.depth + 1;
            presentCamera.enabled = false;

            // Saved in the settings file; -1 means Auto.
            ShowFps = SaveManager.Settings.showFps;
            ApplyDisplay();
            int saved = SaveManager.Settings.qualityTier;
            if (saved < 0 || saved >= Profiles.Length) SetTier(DetectTier(), true);
            else SetTier((QualityTier)saved, false);
        }

        public void SetTier(QualityTier tier, bool auto)
        {
            Tier = tier;
            IsAuto = auto;
            SaveManager.Settings.qualityTier = auto ? -1 : (int)tier;
            SaveManager.Settings.showFps = ShowFps;
            SaveManager.Save();
            Apply();
        }

        public void SaveFpsSetting()
        {
            SaveManager.Settings.showFps = ShowFps;
            SaveManager.Save();
        }

        // Rough guess from the hardware. Auto mode corrects it later if the game runs slowly.
        public static QualityTier DetectTier()
        {
            int vram = SystemInfo.graphicsMemorySize;
            int ram = SystemInfo.systemMemorySize;
            int cores = SystemInfo.processorCount;
            string gpu = (SystemInfo.graphicsDeviceName ?? string.Empty).ToLowerInvariant();
            bool integrated = (gpu.Contains("intel") && !gpu.Contains("arc")) || gpu.Contains("uhd") || gpu.Contains("iris")
                || gpu.Contains("radeon(tm) graphics") || gpu.Contains("vega 8") || gpu.Contains("mali") || gpu.Contains("adreno")
                || gpu.Contains("llvmpipe") || gpu.Contains("software");

            if (SystemInfo.deviceType == DeviceType.Handheld) return QualityTier.Low;
            if (ram < 3000 || cores <= 2 || vram < 768 || SystemInfo.graphicsShaderLevel < 35) return QualityTier.Potato;
            if (integrated) return vram >= 2048 && ram >= 8000 ? QualityTier.Medium : QualityTier.Low;
            if (vram < 2048) return QualityTier.Low;
            if (vram < 4096) return QualityTier.Medium;
            if (vram < 8000) return QualityTier.High;
            return QualityTier.Ultra;
        }

        // Re-applies everything (call after changing any graphics setting).
        public void Apply()
        {
            effective = Effective(Profiles[(int)Tier], SaveManager.Settings);
            var p = effective;
            QualitySettings.vSyncCount = p.vSync ? 1 : 0;
            Application.targetFrameRate = p.vSync ? -1 : p.targetFps;
            QualitySettings.antiAliasing = p.msaa;
            QualitySettings.pixelLightCount = p.pixelLights;
            ApplyShadows(p.shadows);
            QualitySettings.softParticles = false;
            QualitySettings.realtimeReflectionProbes = false;
            QualitySettings.anisotropicFiltering = p.textures >= 2 ? AnisotropicFiltering.Enable : AnisotropicFiltering.Disable;
            Time.fixedDeltaTime = (int)Tier <= (int)QualityTier.Low ? 0.04f : 0.02f;
            if (worldCamera != null) worldCamera.allowMSAA = p.msaa > 0;

            UpdateRenderTarget(true);
            settleUntil = Time.unscaledTime + 3f;
            lowFpsTimer = 0f;
            if (Changed != null) Changed();
        }

        static Profile Effective(Profile p, SettingsData s)
        {
            if (s.shadowQuality >= 0) p.shadows = Mathf.Clamp(s.shadowQuality, 0, 4);
            if (s.antiAliasing >= 0) p.msaa = s.antiAliasing >= 8 ? 8 : s.antiAliasing >= 4 ? 4 : s.antiAliasing >= 2 ? 2 : 0;
            if (s.effectsQuality >= 0) p.particleScale = s.effectsQuality == 0 ? 0.4f : s.effectsQuality == 1 ? 0.8f : 1.2f;
            if (s.vSync >= 0) p.vSync = s.vSync == 1;
            if (s.textureQuality >= 0) p.textures = Mathf.Clamp(s.textureQuality, 0, 2);
            if (s.performanceMode)
            {
                // Same look, less work: low shadows, no AO or post-processing, fewer effects,
                // light pools instead of real fixture lights.
                p.shadows = Mathf.Min(p.shadows, 1);
                p.contactShadows = false;
                p.post = false;
                p.bloom = false;
                p.particleScale *= 0.5f;
                p.fixtureLights = false;
                p.pixelLights = Mathf.Min(p.pixelLights, 1);
                p.msaa = Mathf.Min(p.msaa, 2);
                p.renderScale = Mathf.Min(p.renderScale, 0.85f);
            }
            return p;
        }

        void ApplyShadows(int level)
        {
            switch (level)
            {
                case 0:
                    QualitySettings.shadows = ShadowQuality.Disable;
                    break;
                case 1:
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowResolution = ShadowResolution.Low;
                    QualitySettings.shadowDistance = 25f;
                    QualitySettings.shadowCascades = 1;
                    break;
                case 2:
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowResolution = ShadowResolution.Medium;
                    QualitySettings.shadowDistance = 35f;
                    QualitySettings.shadowCascades = 1;
                    break;
                case 3:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.High;
                    QualitySettings.shadowDistance = 45f;
                    QualitySettings.shadowCascades = 2;
                    break;
                default:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
                    QualitySettings.shadowDistance = 60f;
                    QualitySettings.shadowCascades = 4;
                    break;
            }
            if (sun != null)
                sun.shadows = level == 0 ? LightShadows.None : level <= 2 ? LightShadows.Hard : LightShadows.Soft;
        }

        // Resolution and window mode (only meaningful in a built game).
        public void ApplyDisplay()
        {
            var s = SaveManager.Settings;
            if (Application.isEditor) return;
            int width = s.resolutionWidth > 0 ? s.resolutionWidth : Screen.width;
            int height = s.resolutionHeight > 0 ? s.resolutionHeight : Screen.height;
            var mode = s.fullscreenMode >= 0 ? (FullScreenMode)s.fullscreenMode : Screen.fullScreenMode;
            if (width != Screen.width || height != Screen.height || mode != Screen.fullScreenMode) Screen.SetResolution(width, height, mode);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
            {
                Fps = Mathf.Lerp(Fps, 1f / dt, 0.05f);
                FrameMs = Mathf.Lerp(FrameMs, dt * 1000f, 0.05f);
            }
            if (GameInput.Down(InputAction.ToggleFps))
            {
                ShowFps = !ShowFps;
                SaveFpsSetting();
            }
            UpdateRenderTarget(false);

            // Auto mode: if the game is struggling for a few seconds, step down a preset.
            var game = GameManager.Instance;
            bool measuring = IsAuto && game != null && game.IsPlaying && Time.timeScale > 0f && Time.unscaledTime > settleUntil;
            if (!measuring) return;
            lowFpsTimer = Fps < LowFpsThreshold ? lowFpsTimer + dt : 0f;
            if (lowFpsTimer > LowFpsSeconds && Tier > QualityTier.Potato)
            {
                Tier = Tier - 1;
                Apply();
                Notice = "Graphics lowered to " + Profiles[(int)Tier].name + " to keep the game smooth";
                NoticeTime = Time.unscaledTime;
            }
        }

        // Draws the 3D world into a smaller texture when the preset asks for it.
        void UpdateRenderTarget(bool force)
        {
            if (worldCamera == null) return;
            float scale = effective.renderScale <= 0f ? 1f : effective.renderScale;
            int width = Mathf.Max(64, Mathf.RoundToInt(Screen.width * scale));
            int height = Mathf.Max(64, Mathf.RoundToInt(Screen.height * scale));
            bool wantScaled = scale < 0.99f;

            if (!force && wantScaled == (ScaledView != null) && (!wantScaled || (ScaledView.width == width && ScaledView.height == height)))
                return;

            worldCamera.targetTexture = null;
            if (ScaledView != null)
            {
                ScaledView.Release();
                Destroy(ScaledView);
                ScaledView = null;
            }
            if (wantScaled)
            {
                ScaledView = new RenderTexture(width, height, 24) { name = "Scaled View", filterMode = FilterMode.Bilinear };
                worldCamera.targetTexture = ScaledView;
            }
            presentCamera.enabled = wantScaled;
        }

        void OnDestroy()
        {
            if (ScaledView != null) ScaledView.Release();
        }
    }
}
