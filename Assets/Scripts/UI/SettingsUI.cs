using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Settings window (from the main menu or pause menu): gameplay, camera and
    // aiming, graphics (preset plus per-option overrides, display, performance
    // mode), audio mix, accessibility and controls. Every option is applied as
    // soon as it changes and saved to the JSON settings file.
    public class SettingsUI
    {
        static readonly string[] Tabs = { "Gameplay", "Camera", "Graphics", "Audio", "Accessibility", "Controls" };
        public const int ControlsTab = 5;
        static readonly string[] ViewNames = { "Top-down (tactical)", "First person (body cam)" };
        static readonly string[] ShakeNames = { "Off", "Low", "Medium" };
        static readonly string[] TextureNames = { "Low", "Medium", "High" };
        static readonly float[] UiScales = { 0.75f, 0.9f, 1f, 1.1f, 1.25f, 1.5f };
        static readonly string[] UiScaleNames = { "75%", "90%", "100%", "110%", "125%", "150%" };
        static readonly float[] TextSizes = { 0.85f, 1f, 1.15f, 1.3f, 1.4f };
        static readonly string[] TextSizeNames = { "Small", "Normal", "Large", "Larger", "Largest" };
        static readonly float[] SubtitleSizes = { 0.8f, 1f, 1.25f, 1.5f };
        static readonly string[] SubtitleSizeNames = { "Small", "Normal", "Large", "Extra large" };
        static readonly int[] AaValues = { -1, 0, 2, 4, 8 };
        static readonly int[] DisplayModes = { -1, (int)FullScreenMode.ExclusiveFullScreen, (int)FullScreenMode.FullScreenWindow, (int)FullScreenMode.Windowed };
        static readonly string[] DisplayModeNames = { "Keep current", "Fullscreen", "Borderless fullscreen", "Windowed" };

        public bool Open { get; private set; }
        public bool Rebinding { get { return rebindIndex >= 0; } }
        int tab, rebindIndex = -1, closedFrame = -1;
        float rebindArmedAt;
        bool confirmReset, dirty, showGamepad;
        string message;
        readonly List<Vector2Int> resolutions = new List<Vector2Int>();

        public bool JustClosed { get { return closedFrame == Time.frameCount; } }

        public void Show(int startTab)
        {
            Open = true;
            tab = Mathf.Clamp(startTab, 0, Tabs.Length - 1);
            rebindIndex = -1;
            confirmReset = false;
            message = null;
            CollectResolutions();
        }

        void Close()
        {
            Open = false;
            rebindIndex = -1;
            closedFrame = Time.frameCount;
            if (dirty) SaveManager.Save();
            dirty = false;
        }

        public void Draw(GameManager game)
        {
            float w = UITheme.Width, h = UITheme.Height;
            var e = Event.current;
            if (Rebinding) CaptureKey(e);
            else if ((e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) || (e.type == EventType.Repaint && GameInput.PadDown(PadButton.East)))
            {
                Close();
                if (e.type != EventType.Repaint) e.Use();
                return;
            }

            UITheme.Fill(new Rect(0f, 0f, w, h), new Color(0f, 0f, 0f, 0.55f));
            var rect = new Rect(w * 0.5f - 500f, h * 0.5f - 410f, 1000f, 820f);
            UITheme.Panel(rect);
            UITheme.Header(new Rect(rect.x + 30f, rect.y + 22f, 600f, 50f), "Settings");
            for (int i = 0; i < Tabs.Length; i++)
                if (UITheme.Button(new Rect(rect.x + 30f + i * 156f, rect.y + 86f, 150f, 40f), Tabs[i], true, tab == i, 17)) { tab = i; rebindIndex = -1; message = null; }

            var body = new Rect(rect.x + 30f, rect.y + 146f, rect.width - 60f, rect.height - 236f);
            bool changed = false;
            switch (tab)
            {
                case 0: changed = DrawGameplay(body, game); break;
                case 1: changed = DrawCamera(body, game); break;
                case 2: changed = DrawGraphics(body, game); break;
                case 3: changed = DrawAudio(body); break;
                case 4: changed = DrawAccessibility(body); break;
                default: DrawControls(body); break;
            }
            if (changed)
            {
                dirty = true; // written to disk when the window closes
                if (AudioManager.Instance != null) AudioManager.Instance.ApplyVolumes();
            }
            if (!string.IsNullOrEmpty(message)) UITheme.Text(new Rect(rect.x + 30f, rect.yMax - 72f, rect.width - 260f, 44f), message, 15, UITheme.Warn, TextAnchor.MiddleLeft);
            if (UITheme.Button(new Rect(rect.xMax - 210f, rect.yMax - 70f, 180f, 46f), "Done", true, true)) Close();
        }

        // ---- Row helpers (two columns per tab) ----

        const float Row = 40f;

        static void Section(ref float y, float x, float width, string title)
        {
            UITheme.Text(new Rect(x, y, width, 22f), title.ToUpperInvariant(), 14, UITheme.Accent, TextAnchor.UpperLeft, true);
            UITheme.Fill(new Rect(x, y + 22f, width, 1f), new Color(UITheme.Line.r, UITheme.Line.g, UITheme.Line.b, 0.6f));
            y += 30f;
        }

        static bool Choice(ref float y, float x, float width, string label, ref int value, string[] options)
        {
            int next = UITheme.Stepper(new Rect(x, y, width, Row - 6f), label, Mathf.Clamp(value, 0, options.Length - 1), options, 0.44f);
            y += Row;
            if (next == value) return false;
            value = next;
            return true;
        }

        static bool Check(ref float y, float x, float width, string label, ref bool value)
        {
            bool next = UITheme.Toggle(new Rect(x, y, width, Row - 8f), label, value);
            y += Row;
            if (next == value) return false;
            value = next;
            return true;
        }

        static bool Range(ref float y, float x, float width, string label, ref float value, float min, float max, string text)
        {
            float next = UITheme.Slider(new Rect(x, y, width, Row - 6f), label, value, min, max, text, 0.44f);
            y += Row;
            if (Mathf.Approximately(next, value)) return false;
            value = next;
            return true;
        }

        // Picks the nearest entry of a fixed list of values (UI scale, text size...).
        static bool Preset(ref float y, float x, float width, string label, ref float value, float[] values, string[] names)
        {
            int index = 0;
            for (int i = 1; i < values.Length; i++)
                if (Mathf.Abs(values[i] - value) < Mathf.Abs(values[index] - value)) index = i;
            int next = index;
            if (!Choice(ref y, x, width, label, ref next, names)) return false;
            value = values[next];
            return true;
        }

        static string Percent(float value) { return Mathf.RoundToInt(value * 100f) + "%"; }

        // ---- Gameplay: rules, assists, minimap and aiming ----

        bool DrawGameplay(Rect body, GameManager game)
        {
            var s = SaveManager.Settings;
            float colW = (body.width - 40f) * 0.5f;
            float lx = body.x, rx = body.x + colW + 40f;
            bool changed = false;

            float y = body.y;
            Section(ref y, lx, colW, "Gameplay");
            int difficulty = s.difficulty;
            if (Choice(ref y, lx, colW, "Difficulty", ref difficulty, OfficerSelectionManager.DifficultyNames))
            {
                s.difficulty = difficulty;
                changed = true;
                if (game.State == GameState.Briefing) game.RollPlan();
                if (game.State == GameState.Paused) message = "Difficulty changes apply from the next deployment.";
            }
            changed |= Check(ref y, lx, colW, "Planning mode pauses the game", ref s.planningPauses);
            changed |= Check(ref y, lx, colW, "Line of sight (hide what the team can't see)", ref s.lineOfSight);
            changed |= Check(ref y, lx, colW, "Automatic reload when empty", ref s.autoReload);
            changed |= Check(ref y, lx, colW, "Switch to sidearm when out of ammo", ref s.autoSwitchWhenEmpty);
            changed |= Check(ref y, lx, colW, "Automatic flashlight in the dark", ref s.autoFlashlight);
            changed |= Check(ref y, lx, colW, "Hit stop and last-takedown slow-mo", ref s.hitStop);
            changed |= Check(ref y, lx, colW, "Heavy weapon handling (guns turn by weight)", ref s.heavyHandling);
            changed |= Check(ref y, lx, colW, "Classic blocky characters (next mission)", ref s.classicCharacters);
            changed |= Check(ref y, lx, colW, "Realistic ammo (feel the magazine, no exact count)", ref s.realisticAmmo);

            y = body.y;
            Section(ref y, rx, colW, "Minimap");
            changed |= Check(ref y, rx, colW, "Show minimap", ref s.minimap);
            changed |= Range(ref y, rx, colW, "Size", ref s.minimapScale, 0.75f, 1.5f, Percent(s.minimapScale));
            changed |= Range(ref y, rx, colW, "Opacity", ref s.minimapOpacity, 0.3f, 1f, Percent(s.minimapOpacity));
            y += 6f;
            Section(ref y, rx, colW, "Aiming");
            changed |= Range(ref y, rx, colW, "Mouse sensitivity", ref s.mouseSensitivity, 0.4f, 2f, s.mouseSensitivity.ToString("0.00") + "x");
            changed |= Range(ref y, rx, colW, "Aim smoothing", ref s.aimSmoothing, 0f, 1f, s.aimSmoothing <= 0.01f ? "Off" : Percent(s.aimSmoothing));
            changed |= Range(ref y, rx, colW, "Controller sensitivity", ref s.controllerSensitivity, 0.3f, 2f, s.controllerSensitivity.ToString("0.0") + "x");
            changed |= Check(ref y, rx, colW, "Controller aim assist", ref s.controllerAimAssist);

            float by = body.yMax - 44f;
            UITheme.Text(new Rect(lx, by - 26f, body.width, 20f), "Mouse sensitivity 1.00x uses the system cursor; other values use a game cursor. Save file: " + SaveManager.FilePath, 12, UITheme.Faint);
            bool inMenu = game.State != GameState.Paused;
            if (UITheme.Button(new Rect(lx, by, 340f, 40f), confirmReset ? "Click again to erase all progress" : "Reset campaign progress", inMenu, confirmReset, 16))
            {
                if (confirmReset)
                {
                    SaveManager.ResetProgress();
                    message = "Campaign progress reset. Settings were kept.";
                    confirmReset = false;
                }
                else confirmReset = true;
            }
            if (!inMenu) UITheme.Text(new Rect(lx + 356f, by, 300f, 40f), "Available from the main menu.", 14, UITheme.Faint, TextAnchor.MiddleLeft);
            return changed;
        }

        // ---- Camera: top-down or first person, and how each behaves ----

        bool DrawCamera(Rect body, GameManager game)
        {
            var s = SaveManager.Settings;
            float colW = (body.width - 40f) * 0.5f;
            float lx = body.x, rx = body.x + colW + 40f;
            bool changed = false;

            float y = body.y;
            Section(ref y, lx, colW, "View");
            int view = s.cameraView;
            if (Choice(ref y, lx, colW, "Camera (" + UITheme.KeyFor(InputAction.SwitchView) + " switches any time)", ref view, ViewNames))
            {
                // Paused in a mission: straight away; otherwise for the next one.
                ViewMode.Set(view == 1, game.Level != null && game.State == GameState.Paused);
                changed = true;
            }
            changed |= Range(ref y, lx, colW, "Field of view", ref s.fieldOfView, 70f, 110f, Mathf.RoundToInt(s.fieldOfView) + " degrees");
            changed |= Check(ref y, lx, colW, "Body cam look (wide lens, grain, REC)", ref s.bodyCamLook);
            y += 8f;
            UITheme.Text(new Rect(lx, y, colW, 120f), "First person: the mouse looks around, right mouse aims down the sights, Ctrl + A / D peeks. Walls go full height with ceilings in first person and drop low again from above. Field of view and the body cam look apply in first person only; screen shake (Accessibility) also sets how much the body cam moves.", 13, UITheme.Faint);

            y = body.y;
            Section(ref y, rx, colW, "Top-down camera");
            changed |= Range(ref y, rx, colW, "Zoom speed", ref s.zoomSpeed, 0.3f, 2f, s.zoomSpeed.ToString("0.0") + "x");
            int preset = s.zoomPreset;
            if (Choice(ref y, rx, colW, "Default zoom", ref preset, CameraController.PresetNames))
            {
                s.zoomPreset = preset;
                if (game.CameraRig != null) game.CameraRig.SetPreset(preset, false);
                changed = true;
            }
            changed |= Check(ref y, rx, colW, "Zoom out automatically outdoors", ref s.autoIndoorZoom);
            changed |= Range(ref y, rx, colW, "Look-ahead", ref s.lookAhead, 0f, 0.5f, Percent(s.lookAhead * 2f));
            changed |= Range(ref y, rx, colW, "Camera smoothing", ref s.cameraSmoothing, 0f, 0.4f, s.cameraSmoothing <= 0.005f ? "Off" : Percent(s.cameraSmoothing / 0.4f));
            changed |= Check(ref y, rx, colW, "Edge scrolling", ref s.edgeScrolling);
            return changed;
        }

        // ---- Graphics: preset, display and per-option overrides ----

        bool DrawGraphics(Rect body, GameManager game)
        {
            var quality = QualityManager.Instance;
            var s = SaveManager.Settings;
            float colW = (body.width - 40f) * 0.5f;
            float lx = body.x, rx = body.x + colW + 40f;
            bool changed = false, apply = false;

            float y = body.y;
            Section(ref y, lx, colW, "Display");
            var names = new string[QualityManager.TierCount + 1];
            names[0] = "Auto (" + QualityManager.TierName((int)QualityManager.DetectTier()) + ")";
            for (int i = 0; i < QualityManager.TierCount; i++) names[i + 1] = QualityManager.TierName(i);
            int current = quality.IsAuto ? 0 : (int)quality.Tier + 1;
            int next = current;
            if (Choice(ref y, lx, colW, "Quality preset", ref next, names))
            {
                if (next == 0) quality.SetTier(QualityManager.DetectTier(), true);
                else quality.SetTier((QualityTier)(next - 1), false);
            }
            int style = s.artStyle;
            if (Choice(ref y, lx, colW, "Art style", ref style, QualityManager.ArtStyleNames))
            {
                s.artStyle = style;
                apply = true;
                message = style == 0 ? "Pixel art: low-resolution, crisp pixels and a flat top-down camera. Textures switch on the next mission." : "Smooth: full-resolution 3D with a perspective camera.";
            }
            if (s.artStyle == 0)
            {
                int pixelSize = s.pixelSize;
                if (Choice(ref y, lx, colW, "Pixel size", ref pixelSize, QualityManager.PixelSizeNames)) { s.pixelSize = pixelSize; apply = true; }
            }

            var resolutionNames = new string[resolutions.Count + 1];
            resolutionNames[0] = "Current (" + Screen.width + " x " + Screen.height + ")";
            int resolution = 0;
            for (int i = 0; i < resolutions.Count; i++)
            {
                resolutionNames[i + 1] = resolutions[i].x + " x " + resolutions[i].y;
                if (resolutions[i].x == s.resolutionWidth && resolutions[i].y == s.resolutionHeight) resolution = i + 1;
            }
            bool display = false;
            if (Choice(ref y, lx, colW, "Resolution", ref resolution, resolutionNames))
            {
                s.resolutionWidth = resolution == 0 ? 0 : resolutions[resolution - 1].x;
                s.resolutionHeight = resolution == 0 ? 0 : resolutions[resolution - 1].y;
                display = true;
            }
            int mode = System.Array.IndexOf(DisplayModes, s.fullscreenMode);
            if (mode < 0) mode = 0;
            if (Choice(ref y, lx, colW, "Display mode", ref mode, DisplayModeNames))
            {
                s.fullscreenMode = DisplayModes[mode];
                display = true;
            }
            if (display)
            {
                changed = true;
                quality.ApplyDisplay();
                if (Application.isEditor) message = "Resolution and display mode apply in a built game (the editor's Game view sets its own size).";
            }

            var preset = QualityManager.Current;
            var basis = PresetOf(quality);
            int vsync = s.vSync + 1;
            apply |= Choice(ref y, lx, colW, "VSync", ref vsync, new[] { "Preset (" + (PresetOf(quality).vSync ? "on" : "off") + ")", "Off", "On" });
            s.vSync = vsync - 1;
            apply |= Check(ref y, lx, colW, "Performance mode (same look, less work)", ref s.performanceMode);
            bool fps = quality.ShowFps;
            if (Check(ref y, lx, colW, "Show FPS counter (" + UITheme.KeyFor(InputAction.ToggleFps) + ")", ref fps))
            {
                quality.ShowFps = fps;
                quality.SaveFpsSetting();
            }
            y += 8f;
            string drawing = QualityManager.PixelArt && quality.ScaledView != null ? "Pixel art at " + quality.ScaledView.width + " x " + quality.ScaledView.height + " game pixels (each " + quality.PixelFactor + "x" + quality.PixelFactor + " on screen)"
                : "Drawing at " + Mathf.RoundToInt(preset.renderScale * 100f) + "% resolution";
            string summary = drawing + ", " + QualityManager.ShadowNames[Mathf.Clamp(preset.shadows, 0, 4)].ToLowerInvariant() + " shadows, "
                + (preset.fixtureLights ? "real fixture lights" : "light pools only") + ", AI every " + Mathf.RoundToInt(preset.aiThinkInterval * 1000f) + " ms.";
            UITheme.Text(new Rect(lx, y, colW, 44f), summary, 14, UITheme.Dim);
            y += 48f;
            UITheme.Text(new Rect(lx, y, colW, 80f), "Auto picks a preset from your hardware and steps down if the frame rate stays low. Hardware: " + quality.Hardware, 13, UITheme.Faint);

            y = body.y;
            Section(ref y, rx, colW, "Detail");
            int shadows = s.shadowQuality + 1;
            apply |= Choice(ref y, rx, colW, "Shadows", ref shadows, WithPreset(QualityManager.ShadowNames, QualityManager.ShadowNames[Mathf.Clamp(basis.shadows, 0, 4)]));
            s.shadowQuality = shadows - 1;
            int aa = Mathf.Max(0, System.Array.IndexOf(AaValues, s.antiAliasing));
            string aaPreset = QualityManager.PixelArt ? "off in pixel art" : basis.msaa > 0 ? basis.msaa + "x" : "off";
            if (Choice(ref y, rx, colW, "Anti-aliasing", ref aa, new[] { "Preset (" + aaPreset + ")", "Off", "2x MSAA", "4x MSAA", "8x MSAA" }))
            {
                s.antiAliasing = AaValues[aa];
                apply = true;
            }
            int effects = s.effectsQuality + 1;
            apply |= Choice(ref y, rx, colW, "Effects", ref effects, WithPreset(QualityManager.EffectsNames, basis.particleScale < 0.6f ? "low" : basis.particleScale < 1f ? "medium" : "high"));
            s.effectsQuality = effects - 1;
            int texture = s.textureQuality + 1;
            if (Choice(ref y, rx, colW, "Texture quality", ref texture, WithPreset(TextureNames, TextureNames[Mathf.Clamp(basis.textures, 0, 2)])))
            {
                s.textureQuality = texture - 1;
                apply = true;
                message = "Texture quality applies to the next mission you load.";
            }
            bool post = s.postProcessing;
            string postNote = !basis.post ? " (off on this preset)" : s.performanceMode ? " (off in performance mode)"
                : QualityManager.PostProcessingOn && UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ? " (vignette only)" : "";
            if (Check(ref y, rx, colW, "Post-processing" + postNote, ref post))
            {
                s.postProcessing = post;
                apply = true;
            }
            bool ao = s.ambientOcclusion;
            if (s.artStyle == 0)
            {
                bool outlines = s.pixelOutlines;
                if (Check(ref y, rx, colW, "Pixel outlines" + (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ? " (Built-in pipeline only)" : ""), ref outlines))
                {
                    s.pixelOutlines = outlines;
                    changed = true;
                }
            }
            string aoNote = !basis.contactShadows ? " (off on this preset)" : s.performanceMode ? " (off in performance mode)" : "";
            if (Check(ref y, rx, colW, "Ambient occlusion" + aoNote, ref ao))
            {
                s.ambientOcclusion = ao;
                apply = true;
            }
            if (Range(ref y, rx, colW, "View distance", ref s.viewDistance, 0.5f, 1.5f, Percent(s.viewDistance)))
            {
                changed = true;
                game.RefreshLighting();
            }
            changed |= Range(ref y, rx, colW, "Flashlight brightness", ref s.flashlightBrightness, 0.5f, 1.5f, Percent(s.flashlightBrightness));
            y += 8f;
            UITheme.Text(new Rect(rx, y, colW, 60f), "\"Preset\" follows the quality preset; any other choice overrides it. Ambient occlusion here is soft contact shading painted under walls and props.", 13, UITheme.Faint);

            if (apply)
            {
                quality.Apply();
                changed = true;
            }
            return changed;
        }

        static QualityManager.Profile PresetOf(QualityManager quality)
        {
            return QualityManager.PresetProfile(quality.Tier);
        }

        static string[] WithPreset(string[] options, string presetValue)
        {
            var list = new string[options.Length + 1];
            list[0] = "Preset (" + presetValue.ToLowerInvariant() + ")";
            for (int i = 0; i < options.Length; i++) list[i + 1] = options[i];
            return list;
        }

        void CollectResolutions()
        {
            resolutions.Clear();
            foreach (var r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (size.x >= 800 && size.y >= 600 && !resolutions.Contains(size)) resolutions.Add(size);
            }
        }

        // ---- Audio ----

        bool DrawAudio(Rect body)
        {
            var s = SaveManager.Settings;
            float y = body.y;
            bool changed = false;
            Section(ref y, body.x, body.width, "Mix");
            changed |= Volume(ref y, body, "Master volume", ref s.masterVolume);
            y += 8f;
            changed |= VolumeWithMute(ref y, body, "Music", ref s.musicVolume, ref s.muteMusic);
            changed |= VolumeWithMute(ref y, body, "Weapons", ref s.weaponsVolume, ref s.muteWeapons);
            changed |= VolumeWithMute(ref y, body, "Effects and environment", ref s.effectsVolume, ref s.muteEffects);
            changed |= VolumeWithMute(ref y, body, "Voice and radio", ref s.voiceVolume, ref s.muteVoice);
            changed |= VolumeWithMute(ref y, body, "Ambience", ref s.ambienceVolume, ref s.muteAmbience);
            changed |= VolumeWithMute(ref y, body, "Interface", ref s.interfaceVolume, ref s.muteInterface);
            y += 16f;
            UITheme.Text(new Rect(body.x, y, body.width, 40f), "Indoor rooms add reverb that matches their size and finish; footsteps change with the floor surface. All sounds are generated procedurally at startup as clearly identified placeholders.", 14, UITheme.Faint);
            return changed;
        }

        static bool Volume(ref float y, Rect body, string label, ref float value)
        {
            float next = UITheme.Slider(new Rect(body.x, y, body.width - 160f, 40f), label, value, 0f, 1f, Mathf.RoundToInt(value * 100f) + "%");
            y += 48f;
            if (Mathf.Approximately(next, value)) return false;
            value = next;
            return true;
        }

        static bool VolumeWithMute(ref float y, Rect body, string label, ref float value, ref bool mute)
        {
            bool newMute = UITheme.Toggle(new Rect(body.xMax - 130f, y + 4f, 130f, 32f), "Mute", mute);
            bool changed = newMute != mute;
            mute = newMute;
            return Volume(ref y, body, label, ref value) || changed;
        }

        // ---- Accessibility ----

        bool DrawAccessibility(Rect body)
        {
            var s = SaveManager.Settings;
            float colW = (body.width - 40f) * 0.5f;
            float lx = body.x, rx = body.x + colW + 40f;
            bool changed = false;

            float y = body.y;
            Section(ref y, lx, colW, "Interface");
            changed |= Preset(ref y, lx, colW, "UI scale", ref s.uiScale, UiScales, UiScaleNames);
            changed |= Preset(ref y, lx, colW, "Text size", ref s.textSize, TextSizes, TextSizeNames);
            changed |= Check(ref y, lx, colW, "Colorblind-friendly colors", ref s.colorblindMode);
            changed |= Check(ref y, lx, colW, "Subtitles for radio and voice", ref s.subtitles);
            changed |= Preset(ref y, lx, colW, "Subtitle size", ref s.subtitleSize, SubtitleSizes, SubtitleSizeNames);
            y += 6f;
            Section(ref y, lx, colW, "Comfort");
            changed |= Choice(ref y, lx, colW, "Screen shake", ref s.cameraShake, ShakeNames);
            changed |= Check(ref y, lx, colW, "Reduce flashes (flashbangs, alarms, bloom)", ref s.reduceFlashes);
            y += 8f;
            UITheme.Text(new Rect(lx, y, colW, 60f), "Colorblind mode swaps red/green status colors for orange/blue and brightens warnings. Mouse and controller sensitivity are under Gameplay; camera options have their own tab.", 13, UITheme.Faint);

            y = body.y;
            Section(ref y, rx, colW, "Crosshair");
            changed |= Range(ref y, rx, colW, "Size", ref s.crosshairSize, 0.5f, 2f, Percent(s.crosshairSize));
            changed |= Range(ref y, rx, colW, "Opacity", ref s.crosshairOpacity, 0.2f, 1f, Percent(s.crosshairOpacity));
            changed |= Choice(ref y, rx, colW, "Color", ref s.crosshairColor, UITheme.CrosshairColorNames);
            changed |= Check(ref y, rx, colW, "Hit confirmation marker and sound", ref s.hitMarker);
            // Live preview using the same drawing as the HUD.
            var preview = new Rect(rx, y + 6f, colW, 120f);
            UITheme.Fill(preview, new Color(0.12f, 0.14f, 0.16f, 1f));
            UITheme.Fill(new Rect(preview.x, preview.y, preview.width * 0.5f, preview.height), new Color(0.55f, 0.57f, 0.6f, 1f));
            float cycle = Mathf.Repeat(Time.unscaledTime, 1.2f);
            HUDController.DrawCrosshairShape(preview.center, 0f, cycle < 0.18f ? cycle : -1f, false);
            UITheme.Text(new Rect(preview.x + 8f, preview.yMax - 22f, preview.width - 16f, 20f), "Preview (hit marker flashes)", 12, UITheme.TextColor);
            return changed;
        }

        // ---- Controls ----

        void DrawControls(Rect body)
        {
            if (showGamepad) DrawGamepadLayout(body);
            else DrawKeyBindings(body);
            float by = body.yMax - 40f;
            if (!showGamepad && UITheme.Button(new Rect(body.x, by, 220f, 40f), "Reset to defaults", true, false, 16))
            {
                GameInput.ResetToDefaults();
                SaveManager.Save();
                message = "Controls reset to defaults.";
            }
            if (UITheme.Button(new Rect(body.x + 236f, by, 240f, 40f), showGamepad ? "Keyboard and mouse" : "Gamepad layout", true, false, 16))
            {
                showGamepad = !showGamepad;
                rebindIndex = -1;
            }
            UITheme.Text(new Rect(body.x + 492f, by, body.width - 492f, 40f), "Mouse wheel zooms. Number keys pick command wheel options. Hold " + GameInput.KeyName(GameInput.Binding(InputAction.SwitchWeapon)) + " for the weapon wheel.", 13, UITheme.Faint, TextAnchor.MiddleLeft);
        }

        void DrawKeyBindings(Rect body)
        {
            int count = GameInput.ActionCount;
            float row = 32f;
            int perColumn = Mathf.CeilToInt(count / 2f);
            float colW = (body.width - 20f) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                var action = (InputAction)i;
                float x = body.x + (i / perColumn) * (colW + 20f);
                float y = body.y + (i % perColumn) * row;
                UITheme.Text(new Rect(x, y, colW * 0.58f, row - 4f), GameInput.DisplayName(action), 15, UITheme.TextColor, TextAnchor.MiddleLeft);
                string key = rebindIndex == i ? "press a key..." : GameInput.KeyName(GameInput.Binding(action));
                if (UITheme.Button(new Rect(x + colW * 0.58f, y + 1f, colW * 0.42f, row - 4f), key, true, rebindIndex == i, 15))
                {
                    rebindIndex = i;
                    rebindArmedAt = Time.unscaledTime + 0.15f;
                    message = "Press a key or mouse button for \"" + GameInput.DisplayName(action) + "\". Esc cancels.";
                }
            }
        }

        static void DrawGamepadLayout(Rect body)
        {
            float y = body.y;
            string state = GameInput.GamepadConnected ? "Gamepad connected." : "No gamepad detected.";
#if !(ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM)
            state = "Gamepads need Unity's Input System package (Window > Package Manager). Without it the game uses keyboard and mouse.";
#endif
            UITheme.Text(new Rect(body.x, y, body.width, 24f), state, 15, GameInput.GamepadConnected ? UITheme.Good : UITheme.Warn);
            y += 34f;
            UITheme.Text(new Rect(body.x, y, body.width, 44f), "Left stick: move.   Right stick: aim (and pick in radial menus).   Prompts switch to gamepad buttons while you use one. In menus the left stick moves the cursor, A selects, B closes settings.", 14, UITheme.Dim);
            y += 52f;
            float colW = (body.width - 20f) * 0.5f;
            int shown = 0;
            for (int i = 0; i < GameInput.ActionCount; i++)
            {
                var action = (InputAction)i;
                var button = GameInput.PadBinding(action);
                if (button == PadButton.None) continue;
                float x = body.x + (shown % 2) * (colW + 20f);
                float ry = y + (shown / 2) * 34f;
                UITheme.Text(new Rect(x, ry, colW * 0.6f, 30f), GameInput.DisplayName(action), 15, UITheme.TextColor, TextAnchor.MiddleLeft);
                UITheme.KeyCap(new Vector2(x + colW * 0.6f, ry + 2f), GameInput.PadName(button), 26f);
                shown++;
            }
            y += Mathf.CeilToInt(shown / 2f) * 34f + 12f;
            UITheme.Text(new Rect(body.x, y, body.width, 40f), "Planning mode, fire mode, zoom presets and selecting individual officers stay on the keyboard; orders from the command wheel go to the whole squad unless officers are selected.", 13, UITheme.Faint);
        }

        void CaptureKey(Event e)
        {
            if (Time.unscaledTime < rebindArmedAt) return;
            KeyCode key = KeyCode.None;
            if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None) key = e.keyCode;
            else if (e.type == EventType.MouseDown) key = KeyCode.Mouse0 + Mathf.Clamp(e.button, 0, 6);
            else if (e.shift && e.type == EventType.Repaint && GameInput.KeyDown(KeyCode.LeftShift)) key = KeyCode.LeftShift;
            if (key == KeyCode.None) return;
            e.Use();
            var action = (InputAction)rebindIndex;
            rebindIndex = -1;
            if (key == KeyCode.Escape && action != InputAction.Pause)
            {
                message = "Rebinding cancelled.";
                return;
            }
            // Swap with whatever already used this key so nothing is left unbound.
            KeyCode old = GameInput.Binding(action);
            for (int i = 0; i < GameInput.ActionCount; i++)
            {
                var other = (InputAction)i;
                if (other != action && GameInput.Binding(other) == key)
                {
                    GameInput.SetBinding(other, old);
                    message = GameInput.DisplayName(other) + " moved to " + GameInput.KeyName(old) + ".";
                }
            }
            GameInput.SetBinding(action, key);
            if (message == null || message.StartsWith("Press")) message = GameInput.DisplayName(action) + " is now " + GameInput.KeyName(key) + ".";
            SaveManager.Save();
        }
    }
}
