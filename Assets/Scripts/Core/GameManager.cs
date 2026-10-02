using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    // Owns the game's lifecycle: sets up the persistent systems once, builds
    // a mission (level, NavMesh, player, suspects, civilians) and switches
    // between briefing, playing, paused, complete and failed.
    public class GameManager : MonoBehaviour
    {
        public enum GameState { Briefing, Playing, Paused, MissionComplete, MissionFailed }

        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; }
        public bool IsPlaying { get { return State == GameState.Playing; } }
        // Ignores the click that started or resumed the game so it doesn't fire a shot.
        public bool AcceptsInput { get { return IsPlaying && Time.unscaledTime - stateChangedAt > 0.2f; } }
        public PlayerController Player { get; private set; }
        public LevelLayout Level { get; private set; }
        public CameraController CameraRig { get; private set; }
        public Transform PoolRoot { get; private set; }
        public string FailReason { get; private set; }

        Transform missionRoot;
        NavMeshDataInstance navMesh;
        float stateChangedAt;
        float failAt = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            PoolRoot = new GameObject("Pools").transform;
            PoolRoot.SetParent(transform, false);

            CameraRig = CameraController.Create();
            Light sun = SetUpLighting();
            gameObject.AddComponent<QualityManager>().Init(CameraRig.Cam, sun);
            gameObject.AddComponent<AudioManager>();
            gameObject.AddComponent<EffectsManager>();
            gameObject.AddComponent<AIManager>();
            gameObject.AddComponent<MissionManager>();
            gameObject.AddComponent<UIManager>();

            StartMission();
        }

        static Light SetUpLighting()
        {
            Light sun = null;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) { sun = light; break; }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.56f);
            RenderSettings.fog = false;
            return sun;
        }

        // Builds (or rebuilds) the mission from scratch.
        public void StartMission()
        {
            Time.timeScale = 1f;
            failAt = -1f;
            FailReason = null;

            if (missionRoot != null)
            {
                missionRoot.gameObject.SetActive(false);
                Destroy(missionRoot.gameObject);
            }
            if (navMesh.valid) navMesh.Remove();
            EffectsManager.Instance.ClearAll();
            SmokeCloud.ClearAll();
            ThrownGrenade.ClearAll();

            missionRoot = new GameObject("Mission").transform;
            Level = LevelBuilder.BuildClearTheBuilding(missionRoot);
            navMesh = NavMeshBaker.Bake(Level.root, Level.navBounds);
            AIManager.Instance.Begin(Level);

            Player = PlayerController.Spawn(missionRoot, Level.playerSpawn, Level.playerYaw);
            var actors = new GameObject("Actors").transform;
            actors.SetParent(missionRoot, false);
            foreach (var spawn in Level.enemies) AIManager.Instance.Register(EnemyAI.Spawn(actors, spawn));
            foreach (var spawn in Level.civilians) AIManager.Instance.Register(CivilianAI.Spawn(actors, spawn, Level.extractionPoint));

            MissionManager.Instance.Begin(Level, Level.enemies.Count, Level.civilians.Count);
            CameraRig.Follow(Player);
            AudioManager.Instance.FollowWithListener(Player.transform);
            SetState(GameState.Briefing);
        }

        void SetState(GameState next)
        {
            State = next;
            stateChangedAt = Time.unscaledTime;
            Time.timeScale = next == GameState.Paused ? 0f : 1f;
            bool playing = next == GameState.Playing;
            // During play the HUD draws its own crosshair, so the system cursor is hidden.
            Cursor.visible = !playing;
            Cursor.lockState = playing ? CursorLockMode.Confined : CursorLockMode.None;
        }

        void Update()
        {
            switch (State)
            {
                case GameState.Briefing:
                    if (GameInput.Confirm) BeginPlay();
                    break;
                case GameState.Playing:
                    if (GameInput.Pause) SetState(GameState.Paused);
                    else if (failAt > 0f && Time.time >= failAt) Fail();
                    break;
                case GameState.Paused:
                    if (GameInput.Pause) Resume();
                    break;
                case GameState.MissionComplete:
                case GameState.MissionFailed:
                    if (GameInput.Confirm && Time.unscaledTime - stateChangedAt > 1f) StartMission();
                    break;
            }
        }

        public void BeginPlay()
        {
            if (State != GameState.Briefing) return;
            SetState(GameState.Playing);
            AudioManager.Play2D(Sound.Shout, 0.6f);
            UIManager.Notify("Go, go, go!");
        }

        public void Pause()
        {
            if (State == GameState.Playing) SetState(GameState.Paused);
        }

        public void Resume()
        {
            if (State == GameState.Paused) SetState(GameState.Playing);
        }

        public void OnPlayerDied()
        {
            FailReason = "Officer down.";
            failAt = Time.time + 1.5f; // let the fall play out first
        }

        void Fail()
        {
            failAt = -1f;
            MissionManager.Instance.Finish(false, 0f);
            AudioManager.Play2D(Sound.Fail, 0.7f);
            SetState(GameState.MissionFailed);
        }

        public void CompleteMission()
        {
            if (State != GameState.Playing) return;
            MissionManager.Instance.Finish(true, Player.Health.Current);
            AudioManager.Play2D(Sound.Complete, 0.7f);
            SetState(GameState.MissionComplete);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            if (navMesh.valid) navMesh.Remove();
            Instance = null;
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
