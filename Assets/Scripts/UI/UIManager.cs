using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    public enum BannerKind { Info, Good, Bad }

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

        struct BannerItem
        {
            public string title, detail;
            public BannerKind kind;
        }

        static readonly Queue<BannerItem> banners = new Queue<BannerItem>();
        static BannerItem currentBanner;
        static float bannerStart = -10f;
        const float BannerTime = 2.6f;

        public static void Banner(string title, string detail, BannerKind kind)
        {
            if (banners.Count > 4) return;
            banners.Enqueue(new BannerItem { title = title, detail = detail, kind = kind });
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
        readonly WeaponWheelUI weaponWheel = new WeaponWheelUI();
        readonly TacticalMapUI map = new TacticalMapUI();
        readonly PauseMenuController pause = new PauseMenuController();
        readonly MissionDebriefUI debrief = new MissionDebriefUI();
        readonly LevelEditorUI levelEditor = new LevelEditorUI();
        readonly VersusSetupUI versusSetup = new VersusSetupUI();
        readonly VersusResultUI versusResult = new VersusResultUI();

        SecurityConsole console;
        GameState lastState = GameState.MainMenu;
        float fadeStart = -10f;
        List<string> reconLines;
        float reconTime = -10f;
        string shoutText;
        float shoutTime = -10f;

        void Awake()
        {
            Instance = this;
            notes.Clear();
            banners.Clear();
            bannerStart = -10f;
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
            bool aiming = game.State == GameState.Playing && !game.PlanningMode && !game.ConsoleOpen && game.Player != null && game.Player.IsAlive;
            GameInput.Tick(aiming, Time.unscaledDeltaTime);
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
                GUI.DrawTexture(quality.ViewRect, quality.ScaledView, ScaleMode.StretchToFill, false);
            // Vignette overlay when post-processing is on but the Built-in effect isn't running (URP projects).
            bool inMission = game.State == GameState.Playing || game.State == GameState.Paused || game.State == GameState.Deploying;
            if (inMission && QualityManager.PostProcessingOn && !PostEffects.Active && Event.current.type == EventType.Repaint)
            {
                var old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, game.Lighting.time == TimeOfDay.Night ? 0.55f : 0.35f);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), UITheme.Vignette, ScaleMode.StretchToFill, true);
                GUI.color = old;
            }
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
                case GameState.Loading: DrawLoading(game); break;
                case GameState.Deploying: DrawDeploying(game); break;
                case GameState.Playing:
                    hud.Draw(game);
                    if (game.PlanningMode) map.Draw(game, true);
                    else if (game.MapOpen) map.Draw(game, false);
                    if (SquadCommandManager.Instance.WheelOpen) wheel.Draw(game);
                    bool weaponWheelOpen = game.Player != null && game.Player.Weapons.WheelOpen;
                    if (weaponWheelOpen) weaponWheel.Draw(game);
                    if (console != null) DrawConsole(game);
                    DrawRecon();
                    DrawShout();
                    DrawBanner();
                    if (!game.PlanningMode && !SquadCommandManager.Instance.WheelOpen && !weaponWheelOpen && console == null) hud.DrawCrosshair(game);
                    break;
                case GameState.Paused:
                    hud.Draw(game);
                    pause.Draw(game, this);
                    break;
                case GameState.Debrief:
                    if (game.LastMatch != null) versusResult.Draw(game);
                    else debrief.Draw(game);
                    break;
                case GameState.LevelEditor: levelEditor.Draw(game); break;
                case GameState.VersusSetup: versusSetup.Draw(game); break;
            }

            GUI.enabled = true;
            if (Settings.Open) Settings.Draw(game);
            DrawNotes(game);
            DrawPerformance(quality);
            DrawTransition(game);
        }

        // A quick fade from black when moving between screens (not for pausing, which should feel instant).
        void DrawTransition(GameManager game)
        {
            if (game.State != lastState)
            {
                bool pauseToggle = (game.State == GameState.Paused && lastState == GameState.Playing) || (game.State == GameState.Playing && lastState == GameState.Paused);
                if (!pauseToggle) fadeStart = Time.unscaledTime;
                lastState = game.State;
            }
            float age = Time.unscaledTime - fadeStart;
            if (age >= 0.35f) return;
            UITheme.Fill(new Rect(0f, 0f, UITheme.Width, UITheme.Height), new Color(0f, 0f, 0f, 0.65f * (1f - age / 0.35f)));
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
                color.a = Mathf.Clamp01(4.5f - age) * Mathf.Clamp01(age / 0.15f);
                float slide = (1f - Mathf.Clamp01(age / 0.18f)) * -10f;
                var rect = new Rect(w * 0.3f, y + slide, w * 0.4f, 30f);
                UITheme.Fill(new Rect(rect.x + rect.width * 0.1f, rect.y, rect.width * 0.8f, rect.height), new Color(0f, 0f, 0f, 0.45f * color.a));
                UITheme.Text(rect, note.text, 19, color, TextAnchor.MiddleCenter);
                y += 32f;
            }
        }

        // Objective and danger banners: small, near the top, one at a time, fading in and out.
        void DrawBanner()
        {
            float age = Time.unscaledTime - bannerStart;
            if (age > BannerTime)
            {
                if (banners.Count == 0) return;
                currentBanner = banners.Dequeue();
                bannerStart = Time.unscaledTime;
                age = 0f;
            }
            float alpha = Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01((BannerTime - age) / 0.4f);
            float slide = (1f - Mathf.Clamp01(age / 0.2f)) * -12f;
            Color accent = currentBanner.kind == BannerKind.Good ? UITheme.Good : currentBanner.kind == BannerKind.Bad ? UITheme.Bad : UITheme.Accent;
            float w = UITheme.Width;
            var rect = new Rect(w * 0.5f - 300f, 196f + slide, 600f, 58f);
            UITheme.Fill(rect, new Color(0.02f, 0.03f, 0.05f, 0.82f * alpha));
            UITheme.Fill(new Rect(rect.x, rect.y, rect.width, 3f), new Color(accent.r, accent.g, accent.b, alpha));
            UITheme.Text(new Rect(rect.x, rect.y + 6f, rect.width, 24f), currentBanner.title, 18, new Color(accent.r, accent.g, accent.b, alpha), TextAnchor.UpperCenter, true);
            UITheme.Text(new Rect(rect.x + 10f, rect.y + 30f, rect.width - 20f, 22f), currentBanner.detail, 15, new Color(1f, 1f, 1f, 0.9f * alpha), TextAnchor.UpperCenter);
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
            float height = 40f + reconLines.Count * 26f;
            var rect = new Rect(18f, UITheme.Height - 170f - height, 420f, height);
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

        // Shown while the mission map builds. The spinner only moves between frames;
        // there's no progress bar because the build happens in one step.
        void DrawLoading(GameManager game)
        {
            float w = UITheme.Width, h = UITheme.Height;
            UITheme.Fill(new Rect(0f, 0f, w, h), UITheme.Background);
            var plan = game.Plan;
            if (plan == null) return;
            var mission = plan.mission;
            float x = w * 0.5f - 560f, y = h * 0.5f - 210f;
            MissionSelectionUI.Thumbnail(new Rect(x, y, 420f, 260f), mission, true);
            float tx = x + 460f, tw = 660f;
            UITheme.Text(new Rect(tx, y, tw, 26f), mission.LevelLabel + "  -  DEPLOYING TO", 16, UITheme.Accent, TextAnchor.UpperLeft, true);
            UITheme.Text(new Rect(tx, y + 28f, tw, 46f), mission.displayName.ToUpperInvariant(), 38, UITheme.TextColor, TextAnchor.UpperLeft, true);
            UITheme.Text(new Rect(tx, y + 80f, tw, 24f), mission.location + "   |   " + LightingProfile.Names[(int)plan.timeOfDay] + "   |   " + OfficerSelectionManager.DifficultyNames[plan.difficulty], 17, UITheme.Dim);
            float dh = UITheme.TextHeight(mission.description, 18, tw);
            UITheme.Text(new Rect(tx, y + 118f, tw, dh), mission.description, 18, UITheme.TextColor);
            UITheme.Fill(new Rect(x, y + 290f, 1120f, 1f), UITheme.Line);
            UITheme.Text(new Rect(x, y + 304f, 1120f, 50f), "TIP:  " + game.LoadingTip, 17, UITheme.Dim);

            Vector2 c = new Vector2(w - 90f, h - 80f);
            float t = Time.unscaledTime * 6f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                float fade = Mathf.Repeat(i / 8f - t / (Mathf.PI * 2f), 1f);
                UITheme.Dot(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 18f, 4f, new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.2f + 0.8f * fade));
            }
            UITheme.Text(new Rect(w - 360f, h - 92f, 230f, 24f), "Loading", 17, UITheme.Dim, TextAnchor.MiddleRight);
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
