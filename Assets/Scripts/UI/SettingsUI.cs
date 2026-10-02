using UnityEngine;

namespace Swat
{
    // Settings window (from the main menu or pause menu): graphics quality,
    // audio volumes and per-category mute, gameplay/camera options and key
    // remapping. Everything is saved to the JSON settings file.
    public class SettingsUI
    {
        static readonly string[] Tabs = { "Graphics", "Audio", "Gameplay", "Controls" };

        public bool Open { get; private set; }
        public bool Rebinding { get { return rebindIndex >= 0; } }
        int tab, rebindIndex = -1, closedFrame = -1;
        float rebindArmedAt;
        bool confirmReset, dirty;
        string message;

        public bool JustClosed { get { return closedFrame == Time.frameCount; } }

        public void Show(int startTab)
        {
            Open = true;
            tab = startTab;
            rebindIndex = -1;
            confirmReset = false;
            message = null;
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
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                Close();
                e.Use();
                return;
            }

            UITheme.Fill(new Rect(0f, 0f, w, h), new Color(0f, 0f, 0f, 0.55f));
            var rect = new Rect(w * 0.5f - 480f, h * 0.5f - 400f, 960f, 800f);
            UITheme.Panel(rect);
            UITheme.Header(new Rect(rect.x + 30f, rect.y + 22f, 600f, 50f), "Settings");
            for (int i = 0; i < Tabs.Length; i++)
                if (UITheme.Button(new Rect(rect.x + 30f + i * 160f, rect.y + 86f, 150f, 40f), Tabs[i], true, tab == i, 17)) { tab = i; rebindIndex = -1; }

            var body = new Rect(rect.x + 30f, rect.y + 146f, rect.width - 60f, rect.height - 230f);
            bool changed = false;
            switch (tab)
            {
                case 0: changed = DrawGraphics(body); break;
                case 1: changed = DrawAudio(body); break;
                case 2: changed = DrawGameplay(body, game); break;
                default: DrawControls(body); break;
            }
            if (changed)
            {
                dirty = true; // written to disk when the window closes
                if (AudioManager.Instance != null) AudioManager.Instance.ApplyVolumes();
            }
            if (!string.IsNullOrEmpty(message)) UITheme.Text(new Rect(rect.x + 30f, rect.yMax - 70f, rect.width - 260f, 40f), message, 15, UITheme.Warn, TextAnchor.MiddleLeft);
            if (UITheme.Button(new Rect(rect.xMax - 210f, rect.yMax - 70f, 180f, 46f), "Done", true, true)) Close();
        }

        bool DrawGraphics(Rect body)
        {
            var quality = QualityManager.Instance;
            float y = body.y;
            var names = new string[QualityManager.TierCount + 1];
            names[0] = "Auto (" + QualityManager.TierName((int)QualityManager.DetectTier()) + " detected)";
            for (int i = 0; i < QualityManager.TierCount; i++) names[i + 1] = QualityManager.TierName(i);
            int current = quality.IsAuto ? 0 : (int)quality.Tier + 1;
            int next = UITheme.Stepper(new Rect(body.x, y, body.width, 40f), "Quality preset", current, names);
            if (next != current)
            {
                if (next == 0) quality.SetTier(QualityManager.DetectTier(), true);
                else quality.SetTier((QualityTier)(next - 1), false);
            }
            y += 50f;
            var profile = QualityManager.Current;
            string details = "Render scale " + Mathf.RoundToInt(profile.renderScale * 100f) + "%   |   Shadows " + (profile.shadows == 0 ? "off" : profile.shadows == 1 ? "hard" : "soft")
                + "   |   Dynamic lights " + (profile.dynamicLights ? "on" : "off") + "   |   Effects " + Mathf.RoundToInt(profile.particleScale * 100f) + "%   |   AI updates every " + Mathf.RoundToInt(profile.aiThinkInterval * 1000f) + " ms";
            UITheme.Text(new Rect(body.x, y, body.width, 44f), details, 15, UITheme.Dim);
            y += 50f;
            bool fps = UITheme.Toggle(new Rect(body.x, y, body.width, 32f), "Show FPS counter (" + UITheme.KeyFor(InputAction.ToggleFps) + ")", quality.ShowFps);
            if (fps != quality.ShowFps)
            {
                quality.ShowFps = fps;
                quality.SaveFpsSetting();
            }
            y += 44f;
            UITheme.Text(new Rect(body.x, y, body.width, 60f), "Auto mode picks a preset from your hardware and steps down if the frame rate stays low. Lower presets draw the 3D view at reduced resolution (the interface stays sharp), turn off shadows and dynamic lights, and update AI less often.", 15, UITheme.Faint);
            y += 70f;
            UITheme.Text(new Rect(body.x, y, body.width, 24f), "Hardware: " + quality.Hardware, 14, UITheme.Faint);
            return false;
        }

        bool DrawAudio(Rect body)
        {
            var s = SaveManager.Settings;
            float y = body.y;
            bool changed = false;
            changed |= Volume(ref y, body, "Master volume", ref s.masterVolume);
            y += 10f;
            changed |= VolumeWithMute(ref y, body, "Effects", ref s.effectsVolume, ref s.muteEffects);
            changed |= VolumeWithMute(ref y, body, "Voice and radio", ref s.voiceVolume, ref s.muteVoice);
            changed |= VolumeWithMute(ref y, body, "Ambience", ref s.ambienceVolume, ref s.muteAmbience);
            changed |= VolumeWithMute(ref y, body, "Interface", ref s.interfaceVolume, ref s.muteInterface);
            y += 16f;
            UITheme.Text(new Rect(body.x, y, body.width, 40f), "All sounds are generated procedurally at startup as clearly identified placeholders.", 14, UITheme.Faint);
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

        bool DrawGameplay(Rect body, GameManager game)
        {
            var s = SaveManager.Settings;
            float y = body.y;
            bool changed = false;
            int difficulty = UITheme.Stepper(new Rect(body.x, y, body.width, 40f), "Difficulty", s.difficulty, OfficerSelectionManager.DifficultyNames);
            if (difficulty != s.difficulty)
            {
                s.difficulty = difficulty;
                changed = true;
                if (game.State == GameState.Briefing) game.RollPlan();
            }
            y += 46f;
            UITheme.Text(new Rect(body.x, y, body.width, 22f), game.State == GameState.Paused ? "Difficulty changes apply from the next deployment." : "Recruit: less accurate, slower suspects. Veteran: sharper suspects, higher score multiplier.", 14, UITheme.Faint);
            y += 34f;
            float zoom = UITheme.Slider(new Rect(body.x, y, body.width, 40f), "Zoom sensitivity", s.zoomSpeed, 0.3f, 2f, s.zoomSpeed.ToString("0.0") + "x");
            if (!Mathf.Approximately(zoom, s.zoomSpeed)) { s.zoomSpeed = zoom; changed = true; }
            y += 46f;
            float look = UITheme.Slider(new Rect(body.x, y, body.width, 40f), "Mouse look-ahead", s.lookAhead, 0f, 0.5f, Mathf.RoundToInt(s.lookAhead * 200f) + "%");
            if (!Mathf.Approximately(look, s.lookAhead)) { s.lookAhead = look; changed = true; }
            y += 46f;
            int preset = UITheme.Stepper(new Rect(body.x, y, body.width, 40f), "Default zoom preset", s.zoomPreset, CameraController.PresetNames);
            if (preset != s.zoomPreset)
            {
                s.zoomPreset = preset;
                game.CameraRig.SetPreset(preset, false);
                changed = true;
            }
            y += 50f;
            bool pauses = UITheme.Toggle(new Rect(body.x, y, body.width, 32f), "Planning mode pauses the game (off: slow motion)", s.planningPauses);
            if (pauses != s.planningPauses) { s.planningPauses = pauses; changed = true; }
            y += 40f;
            bool los = UITheme.Toggle(new Rect(body.x, y, body.width, 32f), "Line of sight: hide suspects and civilians your team can't see", s.lineOfSight);
            if (los != s.lineOfSight) { s.lineOfSight = los; changed = true; }
            y += 56f;

            UITheme.Text(new Rect(body.x, y, body.width, 22f), "Save file: " + SaveManager.FilePath, 13, UITheme.Faint);
            y += 30f;
            bool inMenu = game.State != GameState.Paused;
            if (UITheme.Button(new Rect(body.x, y, 340f, 42f), confirmReset ? "Click again to erase all progress" : "Reset campaign progress", inMenu, confirmReset, 16))
            {
                if (confirmReset)
                {
                    SaveManager.ResetProgress();
                    message = "Campaign progress reset. Settings were kept.";
                    confirmReset = false;
                }
                else confirmReset = true;
            }
            if (!inMenu) UITheme.Text(new Rect(body.x + 356f, y, body.width - 356f, 42f), "Available from the main menu.", 14, UITheme.Faint, TextAnchor.MiddleLeft);
            return changed;
        }

        void DrawControls(Rect body)
        {
            int count = GameInput.ActionCount;
            float row = 34f;
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
            float by = body.y + perColumn * row + 12f;
            if (UITheme.Button(new Rect(body.x, by, 240f, 40f), "Reset to defaults", true, false, 16))
            {
                GameInput.ResetToDefaults();
                SaveManager.Save();
                message = "Controls reset to defaults.";
            }
            UITheme.Text(new Rect(body.x + 260f, by, body.width - 260f, 40f), "Mouse wheel zooms the camera. Number keys pick command wheel options while it is open.", 14, UITheme.Faint, TextAnchor.MiddleLeft);
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
