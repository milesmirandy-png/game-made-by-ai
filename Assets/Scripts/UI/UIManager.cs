using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Draws every screen from a single OnGUI call (Unity's immediate-mode GUI,
    // so the project needs no UI packages, canvases or prefabs) and hosts the
    // shared overlays: notifications, the shout banner, the security console
    // and recon camera panels, and the settings window.
    public class UIManager : MonoBehaviour
    {
        struct Note
        {
            public string text;
            public float time;
            public bool bad;
        }

        static readonly string[] ShoutLines = { "POLICE! DROP THE WEAPON!", "POLICE! GET ON THE GROUND!", "SHOW ME YOUR HANDS!" };

        public static UIManager Instance { get; private set; }
        static readonly List<Note> notes = new List<Note>();

        public bool ConsoleOpen { get { return console != null; } }
        public bool SubPanelOpen { get { return Settings.Open || Settings.JustClosed; } }

        public readonly SettingsUI Settings = new SettingsUI();
        readonly MainMenuController mainMenu = new MainMenuController();
        readonly MissionSelectionUI missionSelection = new MissionSelectionUI();
        readonly BriefingUI briefing = new BriefingUI();
        readonly OfficerSelectionUI officerSelection = new OfficerSelectionUI();
        readonly LoadoutUI loadout = new LoadoutUI();
        readonly HUDController hud = new HUDController();
        readonly CommandWheelUI wheel = new CommandWheelUI();
        readonly TacticalMapUI map = new TacticalMapUI();
        readonly PauseMenuController pause = new PauseMenuController();
        readonly MissionDebriefUI debrief = new MissionDebriefUI();

        SecurityConsole console;
        List<string> reconLines;
        float reconTime = -10f;
        string shoutText;
        float shoutTime = -10f;

        void Awake()
        {
            Instance = this;
            notes.Clear();
            useGUILayout = false; // only GUI.* calls, which skips the layout pass
        }

        public static void Notify(string text, bool bad = false)
        {
            Notify(text, bad, 0f);
        }

        // A delay keeps a message out of the frame being captured (screenshots).
        public static void Notify(string text, bool bad, float delay)
        {
            notes.Add(new Note { text = text, time = Time.unscaledTime + delay, bad = bad });
            if (notes.Count > 5) notes.RemoveAt(0);
        }

        public static void ShowShout()
        {
            if (Instance == null) return;
            Instance.shoutText = ShoutLines[Random.Range(0, ShoutLines.Length)];
            Instance.shoutTime = Time.unscaledTime;
        }

        public static void OpenConsole(SecurityConsole target)
        {
            if (Instance == null) return;
            Instance.console = target;
            AudioManager.Play2D(Sound.Console, 0.6f, 1f, SoundCategory.Interface);
        }

        public void CloseConsole()
        {
            console = null;
        }

        public static void ShowRecon(List<string> lines)
        {
            if (Instance == null) return;
            Instance.reconLines = lines;
            Instance.reconTime = Time.unscaledTime;
        }

        void Update()
        {
            var game = GameManager.Instance;
            if (game == null) return;
            bool preview = game.State == GameState.OfficerSelection || game.State == GameState.Loadout;
            if (!preview) CharacterPreview.Hide();
            else CharacterPreview.Tick(Time.unscaledDeltaTime);
            if (console != null && (game.State != GameState.Playing || game.Player == null || !game.Player.IsAlive)) console = null;
        }

        void OnGUI()
        {
            var game = GameManager.Instance;
            if (game == null) return;

            // On low graphics tiers the world is drawn at reduced resolution; stretch it to the screen first.
            var quality = QualityManager.Instance;
            GUI.matrix = Matrix4x4.identity;
            if (quality != null && quality.ScaledView != null && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), quality.ScaledView, ScaleMode.StretchToFill, false);
            UITheme.Begin();

            // While the settings window is open, the screen underneath is drawn but can't be clicked.
            GUI.enabled = !Settings.Open;
            switch (game.State)
            {
                case GameState.MainMenu: mainMenu.Draw(game); break;
                case GameState.Headquarters: missionSelection.Draw(game); break;
                case GameState.Briefing: briefing.Draw(game); break;
                case GameState.OfficerSelection: officerSelection.Draw(game); break;
                case GameState.Loadout: loadout.Draw(game); break;
                case GameState.Deploying: DrawDeploying(game); break;
                case GameState.Playing:
                    hud.Draw(game);
                    if (game.PlanningMode) map.Draw(game, true);
                    else if (game.MapOpen) map.Draw(game, false);
                    if (SquadCommandManager.Instance.WheelOpen) wheel.Draw(game);
                    if (console != null) DrawConsole(game);
                    DrawRecon();
                    DrawShout();
                    if (!game.PlanningMode && !SquadCommandManager.Instance.WheelOpen && console == null) hud.DrawCrosshair(game);
                    break;
                case GameState.Paused:
                    hud.Draw(game);
                    pause.Draw(game, this);
                    break;
                case GameState.Debrief: debrief.Draw(game); break;
            }

            GUI.enabled = true;
            if (Settings.Open) Settings.Draw(game);
            DrawNotes(game);
            DrawPerformance(quality);
        }

        // ---- Shared overlays ----

        void DrawNotes(GameManager game)
        {
            float w = UITheme.Width;
            float y = game.State == GameState.Playing ? 18f : UITheme.Height - 200f;
            foreach (var note in notes)
            {
                float age = Time.unscaledTime - note.time;
                if (age > 4.5f || age < 0f) continue;
                var color = note.bad ? UITheme.Bad : UITheme.TextColor;
                color.a = Mathf.Clamp01(4.5f - age);
                var rect = new Rect(w * 0.3f, y, w * 0.4f, 30f);
                UITheme.Fill(new Rect(rect.x + rect.width * 0.1f, rect.y, rect.width * 0.8f, rect.height), new Color(0f, 0f, 0f, 0.45f * color.a));
                UITheme.Text(rect, note.text, 19, color, TextAnchor.MiddleCenter);
                y += 32f;
            }
        }

        void DrawShout()
        {
            float age = Time.unscaledTime - shoutTime;
            if (age > 1.3f || string.IsNullOrEmpty(shoutText)) return;
            var color = UITheme.Warn;
            color.a = Mathf.Clamp01(1.3f - age);
            UITheme.ShadowText(new Rect(0f, UITheme.Height * 0.3f, UITheme.Width, 50f), shoutText, 34, color, TextAnchor.MiddleCenter, true);
        }

        void DrawRecon()
        {
            float age = Time.unscaledTime - reconTime;
            if (reconLines == null || age > 7f) return;
            var rect = new Rect(UITheme.Width - 420f, 330f, 390f, 40f + reconLines.Count * 26f);
            UITheme.Panel(rect);
            UITheme.Text(new Rect(rect.x + 14f, rect.y + 8f, rect.width - 28f, 24f), "RECON CAMERA - " + reconLines[0].ToUpperInvariant(), 16, UITheme.Accent, TextAnchor.UpperLeft, true);
            for (int i = 1; i < reconLines.Count; i++)
                UITheme.Text(new Rect(rect.x + 14f, rect.y + 10f + i * 26f, rect.width - 28f, 24f), reconLines[i], 16, i == reconLines.Count - 1 ? UITheme.Faint : UITheme.TextColor);
        }

        void DrawConsole(GameManager game)
        {
            float w = UITheme.Width, h = UITheme.Height;
            UITheme.Fill(new Rect(0f, 0f, w, h), new Color(0f, 0f, 0f, 0.5f));
            var rect = new Rect(w * 0.5f - 300f, h * 0.5f - 250f, 600f, 500f);
            UITheme.Panel(rect);
            UITheme.Header(new Rect(rect.x + 24f, rect.y + 18f, rect.width - 48f, 50f), console.Title, "Security console");

            int active = 0;
            foreach (var cam in SecurityCamera.All) if (cam != null && cam.Active) active++;
            var alarm = game.Level.alarm;
            float y = rect.y + 90f;
            UITheme.Text(new Rect(rect.x + 24f, y, rect.width - 48f, 26f), "Cameras online: " + active + " / " + SecurityCamera.All.Count, 18, active > 0 ? UITheme.Warn : UITheme.Good);
            y += 28f;
            string alarmText = alarm == null ? "No alarm system" : "Alarm: " + alarm.State;
            UITheme.Text(new Rect(rect.x + 24f, y, rect.width - 48f, 26f), alarmText, 18, alarm != null && alarm.State == AlarmState.Triggered ? UITheme.Bad : UITheme.TextColor);
            y += 28f;
            int locked = 0;
            foreach (var door in game.Level.doors) if (door.Electronic && door.State == DoorState.Locked) locked++;
            UITheme.Text(new Rect(rect.x + 24f, y, rect.width - 48f, 26f), "Electronic locks engaged: " + locked, 18, UITheme.TextColor);
            y += 44f;

            if (UITheme.Button(new Rect(rect.x + 24f, y, rect.width - 48f, 46f), "Disable all security cameras", active > 0)) console.DisableCameras();
            y += 54f;
            bool alarmActive = alarm != null && (alarm.State == AlarmState.Armed || alarm.State == AlarmState.Triggered);
            if (UITheme.Button(new Rect(rect.x + 24f, y, rect.width - 48f, 46f), alarm != null && alarm.State == AlarmState.Triggered ? "Silence the alarm" : "Disable the alarm system", alarmActive)) console.SilenceAlarm();
            y += 54f;
            if (console.CanUnlockDoors && UITheme.Button(new Rect(rect.x + 24f, y, rect.width - 48f, 46f), "Unlock electronic doors", locked > 0)) console.UnlockDoors();
            if (console.CanUnlockDoors) y += 54f;
            if (console.CanReviewFootage && UITheme.Button(new Rect(rect.x + 24f, y, rect.width - 48f, 46f), console.FootageReviewed ? "Footage reviewed" : "Review camera footage", !console.FootageReviewed)) console.ReviewFootage();
            if (UITheme.Button(new Rect(rect.x + rect.width - 184f, rect.yMax - 60f, 160f, 42f), "Close (" + UITheme.KeyFor(InputAction.Pause) + ")")) CloseConsole();
        }

        void DrawDeploying(GameManager game)
        {
            float w = UITheme.Width, h = UITheme.Height;
            UITheme.Fill(new Rect(0f, 0f, w, 90f), Color.black);
            UITheme.Fill(new Rect(0f, h - 120f, w, 120f), Color.black);
            var plan = game.Plan;
            if (plan == null) return;
            UITheme.Text(new Rect(60f, h - 104f, w - 120f, 40f), plan.mission.displayName.ToUpperInvariant(), 32, UITheme.TextColor, TextAnchor.UpperLeft, true);
            UITheme.Text(new Rect(60f, h - 60f, w - 120f, 30f), plan.mission.location + "   |   " + OfficerSelectionManager.DifficultyNames[plan.difficulty] + "   |   Seed " + plan.seed, 18, UITheme.Dim);
            UITheme.Text(new Rect(0f, h - 60f, w - 60f, 30f), "Press Space to skip", 18, UITheme.Faint, TextAnchor.UpperRight);
            UITheme.Text(new Rect(60f, 30f, w - 120f, 40f), "DEPLOYING", 22, UITheme.Accent, TextAnchor.MiddleLeft, true);
        }

        void DrawPerformance(QualityManager quality)
        {
            if (quality == null) return;
            if (quality.ShowFps)
                UITheme.ShadowText(new Rect(UITheme.Width - 260f, 4f, 250f, 24f), Mathf.RoundToInt(quality.Fps) + " FPS  (" + QualityManager.TierName((int)quality.Tier) + ")", 16, UITheme.Good, TextAnchor.UpperRight);
            if (!string.IsNullOrEmpty(quality.Notice) && Time.unscaledTime - quality.NoticeTime < 5f)
                UITheme.ShadowText(new Rect(0f, UITheme.Height - 170f, UITheme.Width, 30f), quality.Notice, 18, UITheme.Warn, TextAnchor.UpperCenter);
        }
    }
}
