using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Lightweight HUD and menus drawn with Unity's immediate-mode GUI, so the
    // project needs no UI packages, canvases or prefabs.
    public class UIManager : MonoBehaviour
    {
        struct Note
        {
            public string text;
            public float time;
            public bool bad;
        }

        static readonly Color Panel = new Color(0.04f, 0.06f, 0.09f, 0.8f);
        static readonly Color Accent = new Color(0.35f, 0.65f, 1f);
        static readonly Color Good = new Color(0.45f, 0.95f, 0.55f);
        static readonly Color Warn = new Color(1f, 0.8f, 0.3f);
        static readonly Color Bad = new Color(1f, 0.35f, 0.3f);
        static readonly Color Dim = new Color(0.7f, 0.75f, 0.8f);
        static readonly string[] ShoutLines = { "POLICE! DROP YOUR WEAPON!", "SWAT! GET ON THE GROUND!", "HANDS WHERE I CAN SEE THEM!" };
        const string Controls = "WASD move   Mouse aim   Left click fire/use   R reload   Q or 1-7 switch   E interact   F shout   Shift sprint   Wheel zoom   Esc pause";

        static UIManager instance;
        static readonly List<Note> notes = new List<Note>();

        GUIStyle label, button;
        float u;
        string shoutText;
        float shoutTime = -10f;

        void Awake()
        {
            instance = this;
            notes.Clear();
            useGUILayout = false; // we only use GUI.*, which skips the layout pass
        }

        public static void Notify(string text, bool bad = false)
        {
            notes.Add(new Note { text = text, time = Time.unscaledTime, bad = bad });
            if (notes.Count > 5) notes.RemoveAt(0);
        }

        public static void ShowShout()
        {
            if (instance == null) return;
            instance.shoutText = ShoutLines[Random.Range(0, ShoutLines.Length)];
            instance.shoutTime = Time.unscaledTime;
        }

        void OnGUI()
        {
            var game = GameManager.Instance;
            if (game == null) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { richText = true };
            }
            u = Mathf.Max(0.6f, Screen.height / 1080f);

            // On low graphics tiers the world is drawn at reduced resolution; stretch it to the screen first.
            var quality = QualityManager.Instance;
            if (quality != null && quality.ScaledView != null && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), quality.ScaledView, ScaleMode.StretchToFill, false);

            var player = game.Player;
            if (player != null)
            {
                Overlay(new Color(1f, 1f, 1f, player.Health.Blind));
                Overlay(new Color(0.8f, 0f, 0f, player.Health.DamageFlash * 0.3f));
                if (!player.Health.IsAlive) Overlay(new Color(0.35f, 0f, 0f, 0.35f));
            }

            switch (game.State)
            {
                case GameManager.GameState.Briefing:
                    DrawBriefing(game);
                    break;
                case GameManager.GameState.Playing:
                    DrawHud(game);
                    DrawCrosshair(game);
                    break;
                case GameManager.GameState.Paused:
                    DrawHud(game);
                    DrawPause(game);
                    break;
                case GameManager.GameState.MissionComplete:
                case GameManager.GameState.MissionFailed:
                    DrawResults(game);
                    break;
            }

            if (quality != null)
            {
                if (quality.ShowFps)
                    Text(new Rect(Screen.width - 220f * u, 8f * u, 210f * u, 30f * u), Mathf.RoundToInt(quality.Fps) + " FPS  (" + QualityManager.TierName((int)quality.Tier) + ")", 18f, TextAnchor.UpperRight, Good);
                if (!string.IsNullOrEmpty(quality.Notice) && Time.unscaledTime - quality.NoticeTime < 5f)
                    Text(new Rect(0f, Screen.height - 140f * u, Screen.width, 30f * u), quality.Notice, 20f, TextAnchor.UpperCenter, Warn);
            }
        }

        // ---- In-game HUD ----

        void DrawHud(GameManager game)
        {
            var mission = MissionManager.Instance;
            var player = game.Player;
            float w = Screen.width, h = Screen.height, pad = 20f * u;

            // Objectives, top left.
            float rowHeight = 28f * u;
            var panel = new Rect(pad, pad, 430f * u, 90f * u + mission.Objectives.Count * rowHeight);
            Fill(panel, Panel);
            Text(new Rect(panel.x + 14f * u, panel.y + 8f * u, panel.width, 30f * u), "<b>" + mission.MissionName.ToUpper() + "</b>", 20f, TextAnchor.UpperLeft, Accent);
            Text(new Rect(panel.x + 14f * u, panel.y + 36f * u, panel.width - 28f * u, 30f * u), (mission.CurrentRoom ?? "Outside") + "      " + FormatTime(mission.ElapsedTime), 17f, TextAnchor.UpperLeft, Dim);
            float y = panel.y + 70f * u;
            foreach (var objective in mission.Objectives)
            {
                Color color = objective.State == ObjectiveState.Completed ? Good
                    : objective.State == ObjectiveState.Failed ? Bad
                    : objective.State == ObjectiveState.Pending ? new Color(0.55f, 0.58f, 0.62f) : Color.white;
                string box = objective.State == ObjectiveState.Completed || objective.State == ObjectiveState.Failed ? "■" : "□";
                Text(new Rect(panel.x + 14f * u, y, panel.width - 28f * u, rowHeight), box + "  " + objective.Label, 18f, TextAnchor.UpperLeft, color);
                y += rowHeight;
            }

            // Notifications, top centre.
            y = pad;
            foreach (var note in notes)
            {
                float age = Time.unscaledTime - note.time;
                if (age > 4f) continue;
                var color = note.bad ? Bad : Color.white;
                color.a = Mathf.Clamp01(4f - age);
                Text(new Rect(w * 0.25f, y, w * 0.5f, 30f * u), note.text, 20f, TextAnchor.UpperCenter, color);
                y += 28f * u;
            }
            float shoutAge = Time.unscaledTime - shoutTime;
            if (shoutAge < 1.3f)
            {
                var color = Warn;
                color.a = Mathf.Clamp01((1.3f - shoutAge) * 2f);
                Text(new Rect(0f, h * 0.22f, w, 50f * u), "<b>" + shoutText + "</b>", 34f, TextAnchor.UpperCenter, color);
            }

            if (player == null) return;

            // Interaction prompt, floating above whatever you can use.
            var target = player.Interaction.Current;
            if (target != null)
            {
                Vector2 screen = ToScreen(game.CameraRig.Cam, target.InteractPosition + Vector3.up * 1.2f);
                Text(new Rect(screen.x - 250f * u, screen.y - 20f * u, 500f * u, 34f * u), "<b>" + target.Prompt + "</b>", 22f, TextAnchor.MiddleCenter, Color.white);
            }

            // Health, bottom left.
            float barWidth = 300f * u;
            float baseY = h - pad - 64f * u;
            Text(new Rect(pad, baseY, barWidth, 26f * u), "<b>HEALTH</b>", 17f, TextAnchor.UpperLeft, Dim);
            Fill(new Rect(pad, baseY + 28f * u, barWidth, 20f * u), new Color(0f, 0f, 0f, 0.6f));
            float health = player.Health.Fraction;
            Fill(new Rect(pad + 2f * u, baseY + 30f * u, (barWidth - 4f * u) * health, 16f * u), health > 0.5f ? Good : health > 0.25f ? Warn : Bad);
            Text(new Rect(pad + barWidth + 10f * u, baseY + 18f * u, 90f * u, 40f * u), "<b>" + Mathf.CeilToInt(player.Health.Current) + "</b>", 26f, TextAnchor.UpperLeft, Color.white);

            // Current weapon and ammo, bottom right.
            var weapons = player.Weapons;
            var info = new Rect(w - pad - 420f * u, h - pad - 110f * u, 420f * u, 110f * u);
            if (weapons.IsWeaponSelected)
            {
                var weapon = weapons.CurrentWeapon;
                Text(new Rect(info.x, info.y, info.width, 30f * u), "<b>" + weapon.Data.displayName.ToUpper() + "</b>  " + (weapon.Data.fireMode == FireMode.FullAuto ? "AUTO" : "SEMI"), 19f, TextAnchor.UpperRight, Dim);
                var ammoColor = weapon.Magazine == 0 ? Bad : weapon.Magazine <= weapon.Data.magazineSize / 4 ? Warn : Color.white;
                Text(new Rect(info.x, info.y + 26f * u, info.width, 56f * u), "<b>" + weapon.Magazine + "</b> / " + weapon.Reserve, 42f, TextAnchor.UpperRight, ammoColor);
                if (weapons.IsReloading)
                {
                    var bar = new Rect(info.xMax - 200f * u, info.y + 88f * u, 200f * u, 8f * u);
                    Fill(bar, new Color(0f, 0f, 0f, 0.6f));
                    Fill(new Rect(bar.x, bar.y, bar.width * weapons.ReloadProgress, bar.height), Warn);
                }
                else if (weapon.Magazine <= weapon.Data.magazineSize / 4 && weapon.Reserve > 0)
                {
                    Text(new Rect(info.x, info.y + 80f * u, info.width, 26f * u), "[R] RELOAD", 17f, TextAnchor.UpperRight, Warn);
                }
            }
            else
            {
                var slot = weapons.CurrentEquipment;
                Text(new Rect(info.x, info.y, info.width, 30f * u), "<b>" + slot.Data.displayName.ToUpper() + "</b>", 19f, TextAnchor.UpperRight, Dim);
                Text(new Rect(info.x, info.y + 26f * u, info.width, 56f * u), "<b>x" + slot.Count + "</b>", 42f, TextAnchor.UpperRight, slot.Count > 0 ? Color.white : Bad);
                string hint = slot.Data.kind == EquipmentKind.BreachingCharge ? "Click next to a locked door" : "Click to throw";
                Text(new Rect(info.x, info.y + 80f * u, info.width, 26f * u), hint, 17f, TextAnchor.UpperRight, Warn);
            }

            DrawSlots(weapons, w, h, pad);
            Text(new Rect(0f, h - 26f * u, w, 24f * u), Controls, 14f, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, 0.45f));
        }

        void DrawSlots(WeaponController weapons, float w, float h, float pad)
        {
            int count = weapons.SlotCount;
            float slotWidth = 92f * u, slotHeight = 46f * u, gap = 6f * u;
            float total = count * slotWidth + (count - 1) * gap;
            float x = (w - total) * 0.5f;
            float y = h - pad - slotHeight - 22f * u;
            for (int i = 0; i < count; i++)
            {
                bool selected = i == weapons.CurrentSlot;
                var rect = new Rect(x + i * (slotWidth + gap), y, slotWidth, slotHeight);
                Fill(rect, selected ? new Color(0.2f, 0.4f, 0.7f, 0.9f) : Panel);
                string name, detail;
                if (i < weapons.Weapons.Count)
                {
                    var weapon = weapons.Weapons[i];
                    name = Short(weapon.Data.displayName);
                    detail = weapon.Magazine + "/" + weapon.Reserve;
                }
                else
                {
                    var slot = weapons.Equipment[i - weapons.Weapons.Count];
                    name = Short(slot.Data.displayName);
                    detail = "x" + slot.Count;
                }
                Text(new Rect(rect.x + 6f * u, rect.y + 3f * u, rect.width, 20f * u), (i + 1) + "  <b>" + name + "</b>", 14f, TextAnchor.UpperLeft, Color.white);
                Text(new Rect(rect.x + 6f * u, rect.y + 23f * u, rect.width - 12f * u, 20f * u), detail, 14f, TextAnchor.UpperRight, Dim);
            }
        }

        void DrawCrosshair(GameManager game)
        {
            var player = game.Player;
            if (player == null || !player.Health.IsAlive) return;
            Vector2 mouse = GameInput.MousePosition;
            var center = new Vector2(mouse.x, Screen.height - mouse.y);
            float thick = Mathf.Max(2f, 2f * u), length = 9f * u;
            var weapons = player.Weapons;

            if (!weapons.IsWeaponSelected)
            {
                // A simple square marks where equipment will land.
                float s = 10f * u;
                Fill(new Rect(center.x - s, center.y - s, s * 2f, thick), Warn);
                Fill(new Rect(center.x - s, center.y + s - thick, s * 2f, thick), Warn);
                Fill(new Rect(center.x - s, center.y - s, thick, s * 2f), Warn);
                Fill(new Rect(center.x + s - thick, center.y - s, thick, s * 2f), Warn);
                return;
            }

            float gap = (5f + weapons.Spread * 2.2f) * u;
            var color = Time.time - weapons.LastHitTime < 0.15f ? Bad : Color.white;
            Fill(new Rect(center.x - gap - length, center.y - thick * 0.5f, length, thick), color);
            Fill(new Rect(center.x + gap, center.y - thick * 0.5f, length, thick), color);
            Fill(new Rect(center.x - thick * 0.5f, center.y - gap - length, thick, length), color);
            Fill(new Rect(center.x - thick * 0.5f, center.y + gap, thick, length), color);
            Fill(new Rect(center.x - thick * 0.5f, center.y - thick * 0.5f, thick, thick), color);
        }

        // ---- Menus ----

        void DrawBriefing(GameManager game)
        {
            var level = game.Level;
            Overlay(new Color(0f, 0f, 0f, 0.5f));
            var panel = PanelRect(1000f, 860f);
            float x = panel.x + 40f * u, width = panel.width - 80f * u, y = panel.y + 30f * u;

            Text(new Rect(x, y, width, 30f * u), "<b>MISSION BRIEFING</b>", 18f, TextAnchor.UpperLeft, Accent);
            y += 30f * u;
            Text(new Rect(x, y, width, 60f * u), "<b>" + level.missionName.ToUpper() + "</b>", 44f, TextAnchor.UpperLeft, Color.white);
            y += 64f * u;
            Text(new Rect(x, y, width, 190f * u), level.briefing, 20f, TextAnchor.UpperLeft, Dim);
            y += 190f * u;

            string left = "<b>WASD</b>  Move\n<b>Mouse</b>  Aim\n<b>Left click</b>  Fire / use equipment\n<b>R</b>  Reload\n<b>Shift</b>  Sprint (loud)\n<b>Mouse wheel</b>  Zoom";
            string right = "<b>Q</b> or <b>1-7</b>  Switch weapon / equipment\n<b>E</b>  Open doors, rescue, arrest\n<b>F</b>  Shout \"Police!\" (suspects may surrender)\n<b>Esc</b>  Pause\n<b>F3</b>  Show FPS";
            Text(new Rect(x, y, width * 0.45f, 170f * u), left, 19f, TextAnchor.UpperLeft, Color.white);
            Text(new Rect(x + width * 0.45f, y, width * 0.55f, 170f * u), right, 19f, TextAnchor.UpperLeft, Color.white);
            y += 175f * u;

            DrawGraphicsOptions(x, ref y, width);

            if (Button(new Rect(panel.center.x - 160f * u, panel.yMax - 80f * u, 320f * u, 56f * u), "<b>START MISSION</b>", 24f))
                game.BeginPlay();
            Text(new Rect(panel.x, panel.yMax - 22f * u, panel.width, 20f * u), "or press Space", 14f, TextAnchor.UpperCenter, Dim);
        }

        void DrawPause(GameManager game)
        {
            Overlay(new Color(0f, 0f, 0f, 0.55f));
            var panel = PanelRect(760f, 560f);
            float x = panel.x + 40f * u, width = panel.width - 80f * u, y = panel.y + 30f * u;
            Text(new Rect(x, y, width, 60f * u), "<b>PAUSED</b>", 44f, TextAnchor.UpperCenter, Color.white);
            y += 80f * u;

            float bw = 260f * u, bh = 50f * u;
            if (Button(new Rect(panel.center.x - bw * 0.5f, y, bw, bh), "<b>RESUME</b>", 22f)) game.Resume();
            y += bh + 12f * u;
            if (Button(new Rect(panel.center.x - bw * 0.5f, y, bw, bh), "RESTART MISSION", 20f)) game.StartMission();
            y += bh + 30f * u;

            DrawGraphicsOptions(x, ref y, width);

            if (Button(new Rect(panel.center.x - bw * 0.5f, panel.yMax - 74f * u, bw, bh), "QUIT", 20f)) game.Quit();
        }

        // Graphics tier picker: Auto plus the five tiers from potato to ultra.
        void DrawGraphicsOptions(float x, ref float y, float width)
        {
            var quality = QualityManager.Instance;
            if (quality == null) return;
            string current = quality.IsAuto ? "Auto (" + QualityManager.TierName((int)quality.Tier) + ")" : QualityManager.TierName((int)quality.Tier);
            Text(new Rect(x, y, width, 26f * u), "<b>GRAPHICS</b>   " + current, 18f, TextAnchor.UpperLeft, Accent);
            y += 30f * u;

            int buttons = QualityManager.TierCount + 1;
            float gap = 8f * u;
            float bw = (width - gap * (buttons - 1)) / buttons, bh = 40f * u;
            for (int i = 0; i < buttons; i++)
            {
                bool isAuto = i == 0;
                bool selected = isAuto ? quality.IsAuto : !quality.IsAuto && (int)quality.Tier == i - 1;
                string text = isAuto ? "Auto" : QualityManager.TierName(i - 1);
                if (Button(new Rect(x + i * (bw + gap), y, bw, bh), selected ? "<b>[" + text + "]</b>" : text, 17f))
                {
                    if (isAuto) quality.SetTier(QualityManager.DetectTier(), true);
                    else quality.SetTier((QualityTier)(i - 1), false);
                    AudioManager.Play2D(Sound.Click, 0.5f);
                }
            }
            y += bh + 8f * u;

            bool showFps = GUI.Toggle(new Rect(x, y, 200f * u, 26f * u), quality.ShowFps, " Show FPS");
            if (showFps != quality.ShowFps)
            {
                quality.ShowFps = showFps;
                quality.SaveFpsSetting();
            }
            Text(new Rect(x + 200f * u, y, width - 200f * u, 26f * u), quality.Hardware, 13f, TextAnchor.UpperRight, new Color(0.6f, 0.65f, 0.7f));
            y += 34f * u;
        }

        void DrawResults(GameManager game)
        {
            var mission = MissionManager.Instance;
            bool success = game.State == GameManager.GameState.MissionComplete;
            Overlay(new Color(0f, 0f, 0f, 0.55f));
            var panel = PanelRect(760f, 820f);
            float x = panel.x + 50f * u, width = panel.width - 100f * u, y = panel.y + 30f * u;

            Text(new Rect(x, y, width, 60f * u), success ? "<b>MISSION COMPLETE</b>" : "<b>MISSION FAILED</b>", 44f, TextAnchor.UpperCenter, success ? Good : Bad);
            y += 62f * u;
            if (!success && !string.IsNullOrEmpty(game.FailReason))
            {
                Text(new Rect(x, y, width, 30f * u), game.FailReason, 22f, TextAnchor.UpperCenter, Color.white);
                y += 34f * u;
            }
            Text(new Rect(x, y, width, 26f * u), "Time " + FormatTime(mission.ElapsedTime) + "     Suspects arrested " + mission.SuspectsArrested + ", neutralized " + mission.SuspectsKilled
                + "     Civilians rescued " + mission.CiviliansRescued + "/" + mission.CiviliansTotal, 16f, TextAnchor.UpperCenter, Dim);
            y += 40f * u;

            foreach (var line in mission.Breakdown)
            {
                Text(new Rect(x, y, width, 30f * u), line.label, 21f, TextAnchor.UpperLeft, Dim);
                Text(new Rect(x, y, width, 30f * u), (line.points >= 0 ? "+" : "") + line.points, 21f, TextAnchor.UpperRight, line.points < 0 ? Bad : Color.white);
                y += 32f * u;
            }
            y += 8f * u;
            Fill(new Rect(x, y, width, Mathf.Max(1f, 2f * u)), new Color(1f, 1f, 1f, 0.3f));
            y += 14f * u;
            Text(new Rect(x, y, width, 44f * u), "<b>SCORE</b>", 30f, TextAnchor.UpperLeft, Color.white);
            Text(new Rect(x, y, width, 44f * u), "<b>" + mission.Score + "</b>", 30f, TextAnchor.UpperRight, Color.white);
            y += 48f * u;
            Text(new Rect(x, y, width, 60f * u), "<b>RATING</b>", 30f, TextAnchor.UpperLeft, Color.white);
            Text(new Rect(x, y, width, 60f * u), "<b>" + mission.Rating + "</b>", 48f, TextAnchor.UpperRight, success ? Warn : Bad);

            float bw = 240f * u, bh = 52f * u;
            if (Button(new Rect(panel.center.x - bw - 10f * u, panel.yMax - 80f * u, bw, bh), success ? "<b>PLAY AGAIN</b>" : "<b>TRY AGAIN</b>", 22f)) game.StartMission();
            if (Button(new Rect(panel.center.x + 10f * u, panel.yMax - 80f * u, bw, bh), "QUIT", 22f)) game.Quit();
        }

        // ---- Drawing helpers ----

        Rect PanelRect(float width, float height)
        {
            float w = Mathf.Min(Screen.width - 30f, width * u);
            float h = Mathf.Min(Screen.height - 30f, height * u);
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            Fill(rect, new Color(0.05f, 0.07f, 0.1f, 0.95f));
            Fill(new Rect(rect.x, rect.y, rect.width, 5f * u), Accent);
            return rect;
        }

        bool Button(Rect rect, string text, float size)
        {
            button.fontSize = Mathf.RoundToInt(size * u);
            return GUI.Button(rect, text, button);
        }

        void Text(Rect rect, string text, float size, TextAnchor anchor, Color color)
        {
            label.fontSize = Mathf.Max(9, Mathf.RoundToInt(size * u));
            label.alignment = anchor;
            label.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.75f);
            GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), text, label);
            label.normal.textColor = color;
            GUI.Label(rect, text, label);
        }

        static void Fill(Rect rect, Color color)
        {
            var saved = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = saved;
        }

        static void Overlay(Color color)
        {
            if (color.a > 0.005f) Fill(new Rect(0f, 0f, Screen.width, Screen.height), color);
        }

        static Vector2 ToScreen(Camera cam, Vector3 world)
        {
            Vector3 viewport = cam.WorldToViewportPoint(world);
            if (viewport.z < 0f) return new Vector2(Screen.width * 0.5f, Screen.height * 0.6f);
            return new Vector2(viewport.x * Screen.width, (1f - viewport.y) * Screen.height);
        }

        static string Short(string name)
        {
            switch (name)
            {
                case "Assault Rifle": return "Rifle";
                case "Smoke Grenade": return "Smoke";
                case "Breaching Charge": return "Breach";
                case "Flashbang": return "Flash";
                default: return name;
            }
        }

        static string FormatTime(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return (total / 60) + ":" + (total % 60).ToString("00");
        }
    }
}
