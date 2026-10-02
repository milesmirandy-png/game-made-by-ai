using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    public enum GameState { MainMenu, Headquarters, Briefing, OfficerSelection, Loadout, Loading, Deploying, Playing, Paused, Debrief, LevelEditor }

    // Owns the game's lifecycle. Everything lives in one scene that is built
    // at runtime: the headquarters diorama behind the menus and, while
    // deployed, the mission map. Flow:
    // Main Menu -> Headquarters (mission board) -> Briefing -> Officer
    // Selection -> Loadout -> Deployment (van arrival) -> Gameplay -> Debriefing.
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; }
        public bool IsPlaying { get { return State == GameState.Playing; } }
        public bool MapOpen { get; private set; }       // Tab: map overlay, game keeps running
        public bool PlanningMode { get; private set; }  // Space: interactive map, time paused or slowed
        public bool ConsoleOpen { get { return UIManager.Instance != null && UIManager.Instance.ConsoleOpen; } }
        public bool ShowObjectives { get; set; }
        // Ignores the click that started or resumed the game so it doesn't fire a shot.
        public bool AcceptsGameplayInput
        {
            get
            {
                return State == GameState.Playing && !PlanningMode && !ConsoleOpen && Time.unscaledTime - stateChangedAt > 0.2f
                    && (SquadCommandManager.Instance == null || !SquadCommandManager.Instance.WheelOpen);
            }
        }

        public PlayerController Player { get; private set; }
        public LevelLayout Level { get; private set; }
        public HeadquartersMap Headquarters { get; private set; }
        public CameraController CameraRig { get; private set; }
        public Transform PoolRoot { get; private set; }
        public MissionPlan Plan { get; private set; }
        public MissionResult LastResult { get; private set; }
        public bool BrowseMode { get; private set; } // roster/equipment opened from the main menu
        public string FailReason { get; private set; }

        Transform missionRoot;
        NavMeshDataInstance navMesh;
        Light sun;
        PostEffects postEffects;
        MapDresser dresser, hqDresser;
        public LightingProfile Lighting { get; private set; }
        public string LoadingTip { get; private set; }
        AmbientDust dust;
        RoomController lastRoom;
        bool lastIndoor, lastDark;
        float nextAutoLight;
        VehicleArrival arrival;
        Transform van;
        float stateChangedAt, failAt = -1f, deployStarted;
        bool indoorAmbience;

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
            SaveManager.Load();
            PoolRoot = new GameObject("Pools").transform;
            PoolRoot.SetParent(transform, false);

            CameraRig = CameraController.Create();
            postEffects = PostEffects.Attach(CameraRig.Cam);
            sun = SetUpSun();
            var quality = gameObject.AddComponent<QualityManager>();
            quality.Init(CameraRig.Cam, sun);
            quality.Changed += OnQualityChanged;
            gameObject.AddComponent<AudioManager>();
            gameObject.AddComponent<EffectsManager>();
            gameObject.AddComponent<AIManager>();
            gameObject.AddComponent<MissionManager>();
            gameObject.AddComponent<TacticalIntel>();
            gameObject.AddComponent<SquadCommandManager>();
            gameObject.AddComponent<UIManager>();

            Headquarters = HeadquartersMap.Build(transform);
            hqDresser = MapDresser.Dress(Headquarters.layout, LightingProfile.For(TimeOfDay.Day), 1, true);
            GoToMainMenu();
        }

        static Light SetUpSun()
        {
            Light found = null;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) { found = light; break; }
            if (found == null)
            {
                found = new GameObject("Sun").AddComponent<Light>();
                found.type = LightType.Directional;
            }
            found.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            found.shadowBias = 0.04f;
            found.shadowNormalBias = 0.3f;
            return found;
        }

        void OnQualityChanged()
        {
            if (dresser != null) dresser.ApplyQuality();
            if (hqDresser != null) hqDresser.ApplyQuality();
            if (Level != null && Plan != null) ApplyLighting();
        }

        // The mission's lighting profile (day, evening or night): sun, ambient
        // light, haze and color grading. Room fixtures, light pools and
        // emergency lights are set up by MapDresser.
        // Re-applies view distance and the lighting profile after a settings change.
        public void RefreshLighting()
        {
            if (Level != null && Plan != null) ApplyLighting();
        }

        void ApplyLighting()
        {
            Lighting = LightingProfile.For(Plan.timeOfDay);
            Lighting.ApplyEnvironment(sun, CameraRig.Cam, QualityManager.Current.dynamicLights, SaveManager.Settings.viewDistance);
            if (postEffects != null) postEffects.SetProfile(Plan.timeOfDay);
        }

        void SetState(GameState next)
        {
            State = next;
            stateChangedAt = Time.unscaledTime;
            if (next != GameState.Playing && next != GameState.Paused)
            {
                MapOpen = false;
                PlanningMode = false;
            }
            bool playing = next == GameState.Playing;
            // During play the HUD draws its own crosshair, so the system cursor is hidden.
            Cursor.visible = !playing;
            Cursor.lockState = playing ? CursorLockMode.Confined : CursorLockMode.None;
            UpdateTimeScale();
        }

        void UpdateTimeScale()
        {
            float scale = 1f;
            if (State == GameState.Paused) scale = 0f;
            else if (State == GameState.Playing)
            {
                if (PlanningMode) scale = SaveManager.Settings.planningPauses ? 0f : 0.2f;
                else if (SquadCommandManager.Instance != null && SquadCommandManager.Instance.WheelOpen) scale = 0.3f;
            }
            if (!Mathf.Approximately(Time.timeScale, scale)) Time.timeScale = scale;
            // World audio stops with the game (menus and music keep playing) and resumes where it left off.
            AudioManager.SetPaused(scale <= 0f);
            // While playing, the HUD draws a crosshair; the system cursor is only shown for menus on top.
            if (State == GameState.Playing)
                Cursor.visible = PlanningMode || ConsoleOpen || (SquadCommandManager.Instance != null && SquadCommandManager.Instance.WheelOpen);
        }

        // ---- Menu flow ----

        public void GoToMainMenu()
        {
            ClearMission();
            ShowHeadquarters(Headquarters.menuCamera, Headquarters.menuTarget, true);
            BrowseMode = false;
            SetState(GameState.MainMenu);
        }

        public void GoToHeadquarters()
        {
            if (Level != null) ClearMission();
            ShowHeadquarters(Headquarters.missionsCamera, Headquarters.missionsTarget, false);
            BrowseMode = false;
            if (OfficerSelectionManager.Mission == null) OfficerSelectionManager.Mission = NextMission();
            SetState(GameState.Headquarters);
        }

        public void SelectMission(MissionData mission)
        {
            if (OfficerSelectionManager.Mission != mission) OfficerSelectionManager.TimeOverride = -1;
            OfficerSelectionManager.Mission = mission;
            if (mission.seed != 0) OfficerSelectionManager.Seed = mission.seed;
            else if (!OfficerSelectionManager.KeepSeed || OfficerSelectionManager.Seed == 0) OfficerSelectionManager.NewSeed();
            RollPlan();
        }

        public void RollPlan()
        {
            var mission = OfficerSelectionManager.Mission;
            if (mission == null) return;
            if (OfficerSelectionManager.Seed == 0) OfficerSelectionManager.NewSeed();
            Plan = MissionRandomizer.Plan(mission, OfficerSelectionManager.Seed, OfficerSelectionManager.Difficulty);
        }

        public void OpenBriefing(MissionData mission)
        {
            if (Level != null) ClearMission();
            SelectMission(mission);
            ShowHeadquarters(Headquarters.missionsCamera, Headquarters.missionsTarget, false);
            if (!mission.isCustom) SaveManager.Progress.lastMissionId = mission.id;
            AudioManager.RadioChirp();
            SetState(GameState.Briefing);
        }

        // ---- Level creator ----

        public void OpenLevelEditor()
        {
            if (Level != null) ClearMission();
            ShowHeadquarters(Headquarters.menuCamera, Headquarters.menuTarget, false);
            BrowseMode = false;
            SetState(GameState.LevelEditor);
        }

        // Plays a level from the level creator through the normal briefing, squad and loadout screens.
        public void PlayCustomLevel(CustomLevel level)
        {
            CustomLevelStore.Remember(level);
            OpenBriefing(CustomLevelBuilder.ToMission(level));
        }

        // The "back" target for screens shown before or after a mission.
        public void LeaveMissionScreens()
        {
            var mission = OfficerSelectionManager.Mission;
            if (mission != null && mission.isCustom) OpenLevelEditor();
            else GoToHeadquarters();
        }

        // Back to the briefing from officer selection, keeping the rolled plan.
        public void OpenBriefingKeepPlan()
        {
            if (Plan == null || OfficerSelectionManager.Mission == null)
            {
                GoToHeadquarters();
                return;
            }
            ShowHeadquarters(Headquarters.missionsCamera, Headquarters.missionsTarget, false);
            SetState(GameState.Briefing);
        }

        public void OpenOfficerSelection(bool browse)
        {
            BrowseMode = browse;
            ShowHeadquarters(Headquarters.rosterCamera, Headquarters.rosterTarget, false);
            SetState(GameState.OfficerSelection);
        }

        public void OpenLoadout(bool browse)
        {
            BrowseMode = browse;
            ShowHeadquarters(Headquarters.armoryCamera, Headquarters.armoryTarget, false);
            SetState(GameState.Loadout);
        }

        void ShowHeadquarters(Vector3 cameraPosition, Vector3 target, bool instant)
        {
            Headquarters.root.gameObject.SetActive(true);
            LightingProfile.ApplyHeadquarters(sun, CameraRig.Cam);
            if (postEffects != null) postEffects.SetProfile(null);
            CameraRig.ShowcaseShot(cameraPosition, target, instant);
            AudioManager.Instance.ListenFrom(target);
            AudioManager.Instance.SetAmbience(Sound.RoomTone);
            AudioManager.Instance.SetAlarm(false);
            AudioManager.Instance.SetReverb(AudioReverbPreset.Room);
            AudioManager.Instance.SetHum(false);
            AudioManager.Instance.SetMusic(Sound.MusicMenu);
        }

        // The next mission to play: the first available one not yet completed.
        public static MissionData NextMission()
        {
            var missions = new List<MissionData>(GameData.AllMissions);
            missions.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
            foreach (var mission in missions)
                if (!mission.isTraining && Progression.IsAvailable(mission) && !SaveManager.Record(mission.id).completed) return mission;
            foreach (var mission in missions) if (!mission.isTraining) return mission;
            return missions.Count > 0 ? missions[0] : null;
        }

        // ---- Deployment ----

        public void Deploy()
        {
            if (OfficerSelectionManager.Mission == null) return;
            if (Plan == null || Plan.mission != OfficerSelectionManager.Mission || Plan.difficulty != OfficerSelectionManager.Difficulty) RollPlan();
            SaveManager.Save();
            StartCoroutine(LoadMission(Plan));
        }

        static readonly string[] Tips =
        {
            "Check your equipment before deployment: charges for locked doors, lights for dark rooms.",
            "Use the tactical map (Tab) to track discovered areas and last known threats.",
            "Protect civilians and complete your objectives. Arrests score higher than force.",
            "Shout (X) before you shoot. Suspects who surrender must be restrained, not shot.",
            "Hold Z and point at a door to stack your squad for a coordinated entry.",
            "Planning mode (Space) pauses the game so you can send squadmates to waypoints.",
            "A recon camera under a door shows part of the room behind it.",
            "Flashbangs work through open doors. Stand clear of the blast yourself.",
            "Wedge doors you don't want suspects coming through.",
        };

        // Shows the loading screen for at least one frame, then builds the mission.
        // There is no progress bar: the map builds in one step and nothing is faked.
        System.Collections.IEnumerator LoadMission(MissionPlan plan)
        {
            LoadingTip = Tips[Random.Range(0, Tips.Length)];
            SetState(GameState.Loading);
            yield return null;
            yield return null;
            BuildMission(plan);
            SetState(GameState.Deploying);
            deployStarted = Time.unscaledTime;
            AudioManager.Instance.SetMusic(Sound.MusicMission);
        }

        public void RestartMission()
        {
            if (Plan == null) return;
            // Same seed, same variation.
            var time = Plan.timeOfDay;
            Plan = MissionRandomizer.Plan(Plan.mission, Plan.seed, Plan.difficulty);
            Plan.timeOfDay = time;
            StartCoroutine(LoadMission(Plan));
        }

        void ClearMission()
        {
            Time.timeScale = 1f;
            failAt = -1f;
            FailReason = null;
            MissionManager.Instance.Abort();
            if (missionRoot != null)
            {
                missionRoot.gameObject.SetActive(false);
                Destroy(missionRoot.gameObject);
                missionRoot = null;
            }
            if (navMesh.valid) navMesh.Remove();
            EffectsManager.Instance.ClearAll();
            SmokeCloud.ClearAll();
            ThrownGrenade.ClearAll();
            AIManager.Instance.Clear();
            SecurityCamera.All.Clear();
            Player = null;
            Level = null;
            dresser = null;
            dust = null;
            lastRoom = null;
            arrival = null;
            van = null;
            if (AudioManager.Instance != null) AudioManager.Instance.SetAlarm(false);
        }

        void BuildMission(MissionPlan plan)
        {
            ClearMission();
            var mission = plan.mission;
            Headquarters.root.gameObject.SetActive(false);
            missionRoot = new GameObject("Mission: " + mission.displayName).transform;
            Level = BuildMap(mission.mapId, missionRoot);
            navMesh = NavMeshBaker.Bake(Level.root, Level.navBounds);
            AIManager.Instance.Begin(Level);

            var actors = new GameObject("Actors").transform;
            actors.SetParent(missionRoot, false);
            MissionRandomizer.Populate(plan, Level, actors);
            ApplyLighting();
            dresser = MapDresser.Dress(Level, Lighting, plan.seed, true);
            dust = AmbientDust.Create(missionRoot);

            // The team.
            var leader = OfficerSelectionManager.Leader;
            var leaderLoadout = GameData.LoadoutFor(leader);
            Player = PlayerController.Spawn(missionRoot, Level.playerSpawn, Level.playerYaw, leader, leaderLoadout, mission.bonusEquipment);
            dust.Follow(Player.transform);
            AIManager.Instance.RegisterPlayer(Player);
            var squad = OfficerSelectionManager.Squad;
            for (int i = 0; i < squad.Count && i < Level.squadSpawns.Count; i++)
            {
                var officer = SquadAI.Spawn(actors, squad[i], GameData.LoadoutFor(squad[i]), i, Level.squadSpawns[i], Level.playerYaw);
                officer.Area = Level.AreaAt(officer.Position);
                AIManager.Instance.Register(officer);
            }

            van = VanBuilder.Build(missionRoot, Level.vanArrivalStart, Level.vanYaw);
            arrival = VehicleArrival.Begin(van, Level.vanArrivalStart, Level.vanParking);
            VanSupply.Attach(van);
            SetTeamVisible(false);

            MissionManager.Instance.Begin(plan);
            TacticalIntel.Instance.Begin();
            SquadCommandManager.Instance.Begin();

            var limits = Level.navBounds;
            limits.Expand(new Vector3(-12f, 0f, -12f));
            CameraRig.Follow(Player, limits);
            Vector3 vanView = Level.vanParking + new Vector3(0f, 10f, -11f);
            CameraRig.ShowcaseShot(vanView, Level.vanParking, true);
            AudioManager.Instance.FollowWithListener(Player.transform);
            AudioManager.Instance.SetAmbience(Sound.Wind);
            AudioManager.Instance.SetReverb(AudioReverbPreset.Off);
            indoorAmbience = false;
        }

        static LevelLayout BuildMap(string mapId, Transform parent)
        {
            if (mapId != null && mapId.StartsWith(CustomLevelStore.MapPrefix))
            {
                var custom = CustomLevelStore.Get(mapId.Substring(CustomLevelStore.MapPrefix.Length));
                if (custom != null) return CustomLevelBuilder.Build(parent, custom);
                Debug.LogWarning("SWAT: custom level '" + mapId + "' not found; loading the office map instead.");
            }
            switch (mapId)
            {
                case "warehouse": return WarehouseMap.Build(parent);
                case "apartment": return ApartmentMap.Build(parent);
                case "training": return TrainingMap.Build(parent);
                case "store": return StoreMap.Build(parent);
                case "motel": return MotelMap.Build(parent);
                case "bank": return BankMap.Build(parent);
                case "clinic": return ClinicMap.Build(parent);
                case "nightclub": return NightclubMap.Build(parent);
                case "factory": return FactoryMap.Build(parent);
                default: return OfficeMap.Build(parent);
            }
        }

        void SetTeamVisible(bool visible)
        {
            if (Player != null) Player.Parts.SetVisible(visible);
            foreach (var officer in AIManager.Instance.Officers) officer.SetVisible(visible);
        }

        void FinishDeployment()
        {
            if (arrival != null && !arrival.Done) arrival.Skip();
            if (van != null)
            {
                // Agents walk around the parked van without rebuilding the NavMesh.
                var obstacle = van.gameObject.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = new Vector3(0f, 1.3f, 0.3f);
                obstacle.size = new Vector3(2.4f, 2.6f, 6.2f);
                obstacle.carving = true;
            }
            SetTeamVisible(true);
            AudioManager.Play(Sound.DoorOpen, Level.vanParking, 0.6f, 0.7f);
            CameraRig.Follow(Player, CameraFocusLimits());
            SetState(GameState.Playing);
            SquadCommandManager.Instance.Radio(null, Plan.mission.isTraining ? "Instructors are standing by. Follow the steps on the left." : "TOC to TRU, you are clear to enter. Good luck.");
        }

        Bounds CameraFocusLimits()
        {
            var limits = Level.navBounds;
            limits.Expand(new Vector3(-12f, 0f, -12f));
            return limits;
        }

        // ---- Gameplay events ----

        public void OnPlayerDown()
        {
            if (State != GameState.Playing) return;
            MissionManager.Instance.OnPlayerDown();
            FailReason = "The team leader is down.";
            failAt = Time.time + 2f; // let the fall play out first
            UIManager.Notify("You are down!", true);
        }

        // The player takes the stairs; squadmates and civilians following them come along.
        public void UseStairs(Stairwell stairs)
        {
            if (Player == null) return;
            Vector3 from = Player.Position;
            int area = stairs.DestinationArea;
            Player.TeleportTo(stairs.Destination, stairs.DestinationYaw);
            int slot = 0;
            foreach (var officer in AIManager.Instance.Officers)
            {
                if (!officer.IsAlive) continue;
                bool following = officer.Order == SquadOrder.Follow || officer.Order == SquadOrder.Regroup || officer.Order == SquadOrder.ReturnToPlayer;
                if (!following || Vector3.Distance(officer.Position, from) > 12f) continue;
                officer.TeleportTo(SquadFormation.Spread(stairs.Destination, ++slot), area);
            }
            foreach (var civilian in AIManager.Instance.Civilians)
            {
                if (civilian.State != CivilianState.Following || civilian.Leader != Player.transform) continue;
                civilian.TeleportTo(SquadFormation.Spread(stairs.Destination, ++slot), area);
            }
            CameraRig.Snap();
            AudioManager.Play2D(Sound.StepConcrete, 0.4f);
            UIManager.Notify(stairs.Label);
        }

        // A squadmate escorting a civilian downstairs.
        public void MoveThroughStairs(Stairwell stairs, SquadAI officer, CivilianAI escort)
        {
            int area = stairs.DestinationArea;
            officer.TeleportTo(stairs.Destination, area);
            if (escort != null && escort.IsAlive) escort.TeleportTo(SquadFormation.Spread(stairs.Destination, 1), area);
        }

        public void EndMission(bool success, string reason)
        {
            if (State != GameState.Playing && State != GameState.Paused) return;
            failAt = -1f;
            LastResult = MissionManager.Instance.Finish(success, reason ?? FailReason);
            RecordProgress(LastResult);
            AudioManager.Play2D(success ? Sound.Complete : Sound.Fail, 0.7f, 1f, SoundCategory.Interface);
            AudioManager.Instance.SetAlarm(false);
            AudioManager.Instance.SetMusic(Sound.MusicMenu);
            SetState(GameState.Debrief);
        }

        void RecordProgress(MissionResult result)
        {
            var progress = SaveManager.Progress;
            var record = SaveManager.Record(result.mission.id);
            record.attempts++;
            if (result.mission.isCustom)
            {
                // Custom levels keep a best score but don't count toward campaign unlocks.
                result.previousBest = record.bestScore;
                if (result.success)
                {
                    record.completed = true;
                    result.newBest = result.total > record.bestScore;
                    if (result.total >= record.bestScore) { record.bestScore = result.total; record.bestRating = result.rating; record.bestTime = result.time; }
                }
                SaveManager.Save();
                return;
            }
            result.previousBest = record.bestScore;
            int before = progress.missionsCompleted;
            bool trainingBefore = progress.trainingComplete;
            if (result.success)
            {
                if (!record.completed)
                {
                    record.completed = true;
                    if (!result.mission.isTraining) progress.missionsCompleted++;
                }
                if (result.mission.isTraining) progress.trainingComplete = true;
                if (result.total > record.bestScore || string.IsNullOrEmpty(record.bestRating) || record.bestRating == "-")
                {
                    result.newBest = result.total > record.bestScore;
                    record.bestScore = Mathf.Max(record.bestScore, result.total);
                    record.bestRating = result.rating;
                    record.bestTime = result.time;
                }
            }
            progress.totalShots += result.stats.shotsFired;
            progress.totalHits += result.stats.shotsHit;
            progress.totalArrests += result.stats.suspectsArrested;
            progress.totalRescues += result.stats.civilians.evacuated;
            result.unlocks.AddRange(Progression.NewUnlocks(before, progress.missionsCompleted, progress.trainingComplete && !trainingBefore));
            SaveManager.Save();
        }

        public void Pause()
        {
            if (State == GameState.Playing) SetState(GameState.Paused);
        }

        public void Resume()
        {
            if (State == GameState.Paused) SetState(GameState.Playing);
        }

        public void SetPlanning(bool on)
        {
            if (State != GameState.Playing) return;
            PlanningMode = on;
            if (on) MapOpen = false;
            stateChangedAt = Time.unscaledTime;
            UpdateTimeScale();
        }

        public void Quit()
        {
            SaveManager.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---- Per frame ----

        void Update()
        {
            if (GameInput.Down(InputAction.Screenshot)) ScreenshotTool.Capture(State.ToString());

            switch (State)
            {
                case GameState.Deploying:
                    bool skip = Time.unscaledTime - deployStarted > 0.4f && (GameInput.Confirm || GameInput.KeyDown(KeyCode.Space) || GameInput.KeyDown(KeyCode.Mouse0));
                    if (skip || (arrival != null && arrival.Done && Time.unscaledTime - deployStarted > 3.4f)) FinishDeployment();
                    break;

                case GameState.Playing:
                    if (UIManager.Instance.ConsoleOpen)
                    {
                        if (GameInput.Down(InputAction.Pause)) UIManager.Instance.CloseConsole();
                        break;
                    }
                    if (GameInput.Down(InputAction.Pause))
                    {
                        if (PlanningMode) SetPlanning(false);
                        else if (SquadCommandManager.Instance.WheelOpen) { }
                        else Pause();
                    }
                    else if (GameInput.Down(InputAction.PlanningMode)) SetPlanning(!PlanningMode);
                    else if (GameInput.Down(InputAction.TacticalMap) && !PlanningMode) MapOpen = !MapOpen;
                    if (GameInput.Down(InputAction.Objectives)) ShowObjectives = !ShowObjectives;
                    if (failAt > 0f && Time.time >= failAt) EndMission(false, FailReason);
                    UpdateAmbience();
                    break;

                case GameState.Paused:
                    if (GameInput.Down(InputAction.Pause) && !UIManager.Instance.SubPanelOpen) Resume();
                    break;
            }
            UpdateTimeScale();
        }

        // Indoor/outdoor ambience, reverb, fluorescent hum, automatic camera zoom
        // and the optional automatic flashlight, updated when the player changes room.
        void UpdateAmbience()
        {
            if (Player == null || Level == null) return;
            var room = Level.RoomAt(Player.Position);
            bool indoor = room != null && room.Indoor;
            bool dark = room != null && room.IsDark;
            if (room != lastRoom || indoor != lastIndoor || dark != lastDark)
            {
                lastRoom = room;
                var audio = AudioManager.Instance;
                if (indoor != indoorAmbience || room == null)
                {
                    indoorAmbience = indoor;
                    audio.SetAmbience(indoor ? Sound.RoomTone : Sound.Wind);
                }
                audio.SetReverb(room != null ? room.Style.reverb : AudioReverbPreset.Off);
                audio.SetHum(indoor && !dark && room.Style.hum);
                if (dust != null) dust.SetActive(indoor && room.Style.kind != RoomKind.Restroom);
                if (indoor != lastIndoor) CameraRig.SetEnvironment(indoor);
                lastIndoor = indoor;
                lastDark = dark;
            }

            if (SaveManager.Settings.autoFlashlight && Player.IsAlive && Time.time >= nextAutoLight)
            {
                nextAutoLight = Time.time + 0.5f;
                bool needLight = dark || (Lighting.time == TimeOfDay.Night && !indoor);
                if (needLight != Player.Flashlight.On) Player.Flashlight.Set(needLight);
            }
        }

        void OnApplicationQuit()
        {
            SaveManager.Save();
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            if (navMesh.valid) navMesh.Remove();
            Instance = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
