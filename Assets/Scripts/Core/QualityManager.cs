using UnityEngine;

namespace Swat
{
    public enum QualityTier { Potato, Low, Medium, High, Ultra }

    // Makes the game run on anything from a potato to a gaming beast.
    //  * Picks a graphics tier from the hardware on first launch.
    //  * In Auto mode, drops a tier if the frame rate stays low.
    //  * Low tiers render the 3D view at reduced resolution (the HUD stays sharp),
    //    turn off shadows and dynamic lights, and think less often for the AI.
    public class QualityManager : MonoBehaviour
    {
        public struct Profile
        {
            public string name;
            public float renderScale;     // fraction of screen resolution the world is drawn at
            public int shadows;           // 0 off, 1 hard, 2 soft
            public float shadowDistance;
            public int msaa;
            public int pixelLights;
            public float particleScale;   // multiplier on debris and smoke puffs
            public bool dynamicLights;    // muzzle flashes and explosions light the scene
            public float aiThinkInterval; // seconds between AI decisions
            public int targetFps;
            public bool vSync;
        }

        static readonly Profile[] Profiles =
        {
            new Profile { name = "Potato", renderScale = 0.5f, shadows = 0, shadowDistance = 0f, msaa = 0, pixelLights = 0, particleScale = 0.35f, dynamicLights = false, aiThinkInterval = 0.3f, targetFps = 60, vSync = false },
            new Profile { name = "Low", renderScale = 0.75f, shadows = 0, shadowDistance = 0f, msaa = 0, pixelLights = 1, particleScale = 0.6f, dynamicLights = false, aiThinkInterval = 0.22f, targetFps = 60, vSync = false },
            new Profile { name = "Medium", renderScale = 1f, shadows = 1, shadowDistance = 35f, msaa = 0, pixelLights = 2, particleScale = 1f, dynamicLights = true, aiThinkInterval = 0.15f, targetFps = 60, vSync = false },
            new Profile { name = "High", renderScale = 1f, shadows = 2, shadowDistance = 45f, msaa = 2, pixelLights = 4, particleScale = 1f, dynamicLights = true, aiThinkInterval = 0.12f, targetFps = 0, vSync = true },
            new Profile { name = "Ultra", renderScale = 1f, shadows = 2, shadowDistance = 60f, msaa = 4, pixelLights = 6, particleScale = 1.3f, dynamicLights = true, aiThinkInterval = 0.1f, targetFps = 0, vSync = true },
        };

        const float LowFpsThreshold = 40f;
        const float LowFpsSeconds = 4f;

        public static QualityManager Instance { get; private set; }
        public static Profile Current { get { return Instance != null ? Profiles[(int)Instance.Tier] : Profiles[(int)QualityTier.Medium]; } }
        public static int TierCount { get { return Profiles.Length; } }
        public static string TierName(int tier) { return Profiles[tier].name; }

        public QualityTier Tier { get; private set; }
        public bool IsAuto { get; private set; }
        public bool ShowFps { get; set; }
        public float Fps { get; private set; }
        public RenderTexture ScaledView { get; private set; }
        public string Hardware { get; private set; }
        public string Notice { get; private set; }
        public float NoticeTime { get; private set; }

        Camera worldCamera;
        Camera presentCamera;
        Light sun;
        float lowFpsTimer;
        float settleUntil;

        void Awake()
        {
            Instance = this;
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

        void Apply()
        {
            var p = Profiles[(int)Tier];
            QualitySettings.vSyncCount = p.vSync ? 1 : 0;
            Application.targetFrameRate = p.vSync ? -1 : p.targetFps;
            QualitySettings.antiAliasing = p.msaa;
            QualitySettings.pixelLightCount = p.pixelLights;
            QualitySettings.shadows = p.shadows == 0 ? ShadowQuality.Disable : p.shadows == 1 ? ShadowQuality.HardOnly : ShadowQuality.All;
            QualitySettings.shadowDistance = p.shadowDistance;
            QualitySettings.shadowResolution = p.shadows == 2 ? ShadowResolution.High : ShadowResolution.Medium;
            QualitySettings.shadowCascades = p.shadows == 2 ? 2 : 1;
            QualitySettings.softParticles = false;
            QualitySettings.realtimeReflectionProbes = false;
            QualitySettings.anisotropicFiltering = p.shadows == 0 ? AnisotropicFiltering.Disable : AnisotropicFiltering.Enable;
            Time.fixedDeltaTime = (int)Tier <= (int)QualityTier.Low ? 0.04f : 0.02f;

            if (sun != null)
                sun.shadows = p.shadows == 0 ? LightShadows.None : p.shadows == 1 ? LightShadows.Hard : LightShadows.Soft;

            UpdateRenderTarget(true);
            settleUntil = Time.unscaledTime + 3f;
            lowFpsTimer = 0f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) Fps = Mathf.Lerp(Fps, 1f / dt, 0.05f);
            if (GameInput.Down(InputAction.ToggleFps))
            {
                ShowFps = !ShowFps;
                SaveFpsSetting();
            }
            UpdateRenderTarget(false);

            // Auto mode: if the game is struggling for a few seconds, step down a tier.
            var game = GameManager.Instance;
            bool measuring = IsAuto && game != null && game.IsPlaying && Time.unscaledTime > settleUntil;
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

        // Draws the 3D world into a smaller texture when the tier asks for it.
        void UpdateRenderTarget(bool force)
        {
            if (worldCamera == null) return;
            float scale = Profiles[(int)Tier].renderScale;
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
