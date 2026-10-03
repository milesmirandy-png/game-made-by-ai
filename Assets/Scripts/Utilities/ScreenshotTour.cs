using System.Collections;
using UnityEngine;

namespace Swat
{
    // A guided photo session: walks through the main screens and a few moments
    // of play on its own and saves a real screenshot of each (main menu, level
    // select, briefing, officers, loadout, a mission, the tactical map, the
    // pause menu, game modes, a Capture the Flag and a Zone Control match, and
    // the level creator). Start it with Shift+F12 in the game, or from the
    // editor menu SWAT -> Screenshot Tour (which also presses Play). Files go to
    // the same folder as F12 screenshots, named Tour_<time>_<number>_<what>.png.
    // Don't touch the controls while it runs (about a minute and a half);
    // press Esc twice quickly to stop it. Your match settings are put back afterwards.
    public class ScreenshotTour : MonoBehaviour
    {
        public static bool Running { get; private set; }
        static ScreenshotTour instance;

        int count;
        string prefix;
        bool stop;
        float lastEscape = -10f;

        public static void Begin()
        {
            var game = GameManager.Instance;
            if (Running || game == null) return;
            if (NetSession.Online)
            {
                UIManager.Notify("Leave the online game before taking the screenshot tour", true);
                return;
            }
            if (instance == null) instance = game.gameObject.AddComponent<ScreenshotTour>();
            instance.StartCoroutine(instance.Run());
        }

        void Update()
        {
            if (!Running) return;
            if (GameInput.KeyDown(KeyCode.Escape))
            {
                if (Time.unscaledTime - lastEscape < 0.6f) stop = true;
                lastEscape = Time.unscaledTime;
            }
        }

        IEnumerator Run()
        {
            Running = true;
            stop = false;
            count = 0;
            prefix = "Tour_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_";
            var game = GameManager.Instance;
            var options = SaveManager.Progress.versus;
            int mode = options.mode, teamSize = options.teamSize, time = options.timeOfDay, scoreIndex = options.scoreIndex, timeIndex = options.timeIndex;
            string map = options.mapId;
            UIManager.Notify("Screenshot tour: hands off for about a minute and a half (Esc twice to stop)");
            yield return Wait(2.5f);

            game.GoToMainMenu();
            yield return Shot(2f, "main_menu");
            game.GoToHeadquarters();
            yield return Shot(1.5f, "level_select");
            var mission = GameManager.NextMission();
            if (mission != null && !stop)
            {
                game.OpenBriefing(mission);
                yield return Shot(1.5f, "briefing");
                game.OpenOfficerSelection(false);
                yield return Shot(1.5f, "officers");
                game.OpenLoadout(false);
                yield return Shot(1.5f, "loadout");
                if (!stop)
                {
                    game.Deploy();
                    yield return Until(() => game.State == GameState.Deploying, 30f);
                    yield return Shot(1.2f, "deployment");
                    game.SkipDeployment();
                    yield return Until(() => game.State == GameState.Playing, 10f);
                    yield return Shot(2.5f, "mission");
                    if (game.State == GameState.Playing)
                    {
                        game.SetMapOpen(true);
                        yield return Shot(0.8f, "tactical_map");
                        game.SetMapOpen(false);
                        game.Pause();
                        yield return Shot(0.6f, "pause_menu");
                        game.Resume();
                    }
                }
            }

            game.OpenVersusSetup();
            yield return Shot(1.5f, "game_modes");
            yield return Match(game, 2, "warehouse", "capture_the_flag");
            yield return Match(game, 3, "office", "zone_control");

            game.OpenLevelEditor();
            yield return Shot(1.5f, "level_creator");

            options.mode = mode;
            options.mapId = map;
            options.teamSize = teamSize;
            options.timeOfDay = time;
            options.scoreIndex = scoreIndex;
            options.timeIndex = timeIndex;
            SaveManager.Save();
            game.GoToMainMenu();
            Running = false;
            yield return Wait(0.5f);
            UIManager.Notify((stop ? "Screenshot tour stopped: " : "Screenshot tour done: ") + count + " pictures in " + ScreenshotTool.Folder);
            Debug.Log("SWAT: screenshot tour saved " + count + " screenshots to " + ScreenshotTool.Folder);
        }

        IEnumerator Match(GameManager game, int mode, string map, string name)
        {
            if (stop) yield break;
            var options = SaveManager.Progress.versus;
            options.mode = mode;
            options.mapId = map;
            options.teamSize = 4;
            options.timeOfDay = 0;
            game.StartVersus();
            yield return Until(() => game.State == GameState.Deploying, 30f);
            game.SkipDeployment();
            yield return Until(() => game.State == GameState.Playing, 10f);
            yield return Shot(5f, name);
            yield return Shot(7f, name + "_later");
        }

        // Waits, then captures the screen (unless stopped).
        IEnumerator Shot(float wait, string name)
        {
            if (stop) yield break;
            yield return Wait(wait);
            if (stop) yield break;
            count++;
            ScreenshotTool.Capture(prefix + count.ToString("00") + "_" + name, true);
            // CaptureScreenshot writes at the end of the frame; give it a moment before changing screens.
            yield return Wait(0.3f);
        }

        IEnumerator Wait(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until && !stop) yield return null;
        }

        IEnumerator Until(System.Func<bool> done, float timeout)
        {
            float until = Time.unscaledTime + timeout;
            while (!done() && Time.unscaledTime < until && !stop) yield return null;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            Running = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Running = false;
            instance = null;
        }

#if UNITY_EDITOR
        // SWAT -> Screenshot Tour asks for a tour on the next Play.
        public const string RequestKey = "SWAT.ScreenshotTourRequested";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartRequested()
        {
            if (!UnityEditor.SessionState.GetBool(RequestKey, false)) return;
            UnityEditor.SessionState.SetBool(RequestKey, false);
            var runner = new GameObject("Screenshot Tour Starter").AddComponent<TourStarter>();
            runner.hideFlags = HideFlags.HideAndDontSave;
        }

        // Waits for the game to finish starting up, then begins the tour.
        class TourStarter : MonoBehaviour
        {
            float startAt;

            void Start() { startAt = Time.unscaledTime + 2f; }

            void Update()
            {
                if (Time.unscaledTime < startAt || GameManager.Instance == null) return;
                Begin();
                Destroy(gameObject);
            }
        }
#endif
    }
}
