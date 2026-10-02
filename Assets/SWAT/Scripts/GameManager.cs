using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Swat
{
    // Runs the whole game: sets up the scene, builds missions, keeps score and
    // draws the HUD and menus. It starts itself when you press Play, so the
    // scene does not need anything in it.
    public class GameManager : MonoBehaviour
    {
        public enum GameState { Briefing, Playing, Paused, Debrief }

        public static GameManager Instance { get; private set; }

        public GameState State { get; private set; }
        public bool IsPlaying { get { return State == GameState.Playing; } }
        // Ignores the click that started or resumed the mission so it doesn't fire a shot.
        public bool AcceptsInput { get { return IsPlaying && Time.unscaledTime - stateChangedAt > 0.25f; } }
        public PlayerController Player { get; private set; }
        public Transform LevelRoot { get { return level != null ? level.root : null; } }

        const string BestMissionKey = "SWAT.BestMission";
        static readonly Color NightSky = new Color(0.04f, 0.05f, 0.08f);
        static readonly Color Accent = new Color(0.35f, 0.6f, 1f);
        static readonly Color Good = new Color(0.4f, 0.95f, 0.5f);
        static readonly Color Warning = new Color(1f, 0.8f, 0.3f);
        static readonly Color Bad = new Color(1f, 0.35f, 0.3f);
        static readonly string[] Codenames =
        {
            "IRON DOOR", "RED LANTERN", "SILENT HARBOR", "BLACK CANYON", "NIGHT OWL",
            "STEEL RAIN", "COLD SNAP", "BROKEN ARROW", "GLASS HOUSE", "THUNDER ROAD",
        };
        static readonly string[] ShoutLines =
        {
            "POLICE! DROP YOUR WEAPON!", "SWAT! GET ON THE GROUND!", "POLICE! HANDS WHERE I CAN SEE THEM!", "DROP IT! DROP IT NOW!",
        };

        struct FeedItem
        {
            public string text;
            public Color color;
            public float time;
        }

        struct Result
        {
            public int arrests, kills, rescued, health, timeBonus, penalties, total;
            public string rating;
        }

        Level level;
        int mission = 1;
        int careerScore;
        int penalties;
        float missionTime, stateChangedAt, shoutTimer;
        float completeTimer = -1f;
        bool success;
        string failReason, codename, shoutText;
        Result result;
        readonly List<FeedItem> feed = new List<FeedItem>();
        GUIStyle style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<GameManager>() != null) return;
            new GameObject("SWAT Game").AddComponent<GameManager>();
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

            Sfx.Build();
            Camera cam = SetUpScene();
            var officer = new GameObject("SWAT Officer");
            officer.AddComponent<CharacterController>();
            Player = officer.AddComponent<PlayerController>();
            Player.Init(cam);
            StartMission();
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        Camera SetUpScene()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            if (FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = NightSky;
            cam.farClipPlane = 300f;

            Light moon = null;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) { moon = light; break; }
            if (moon == null)
            {
                moon = new GameObject("Moonlight").AddComponent<Light>();
                moon.type = LightType.Directional;
            }
            moon.color = new Color(0.6f, 0.68f, 0.9f);
            moon.intensity = 0.5f;
            moon.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.3f, 0.36f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.015f;
            RenderSettings.fogColor = NightSky;
            return cam;
        }

        void StartMission()
        {
            if (level != null && level.root != null) Destroy(level.root.gameObject);
            level = LevelGenerator.Build(transform, mission);
            Player.Respawn(level.playerSpawn, level.playerYaw);
            codename = Codenames[Random.Range(0, Codenames.Length)];
            missionTime = 0f;
            penalties = 0;
            completeTimer = -1f;
            shoutTimer = 0f;
            failReason = null;
            feed.Clear();
            SetState(GameState.Briefing);
        }

        void SetState(GameState next)
        {
            State = next;
            stateChangedAt = Time.unscaledTime;
            Time.timeScale = next == GameState.Paused ? 0f : 1f;
            bool locked = next == GameState.Playing;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            switch (State)
            {
                case GameState.Briefing:
                    if (GameInput.Confirm)
                    {
                        SetState(GameState.Playing);
                        Player.Play(Sfx.Radio, 0.6f);
                        AddFeed("All units: go, go, go!", Color.white);
                    }
                    break;

                case GameState.Playing:
                    if (GameInput.Pause)
                    {
                        SetState(GameState.Paused);
                        break;
                    }
                    // Clicking back into the game window captures the mouse again.
                    if (Cursor.lockState != CursorLockMode.Locked && GameInput.FirePressed)
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                    missionTime += Time.deltaTime;
                    if (completeTimer > 0f)
                    {
                        completeTimer -= Time.deltaTime;
                        if (completeTimer <= 0f) EndMission(true, null);
                    }
                    break;

                case GameState.Paused:
                    if (GameInput.Pause || GameInput.Confirm) SetState(GameState.Playing);
                    break;

                case GameState.Debrief:
                    if (Time.unscaledTime - stateChangedAt > 1f && GameInput.Confirm)
                    {
                        if (success) mission++;
                        StartMission();
                    }
                    break;
            }
            shoutTimer -= Time.deltaTime;
        }

        // ---- Events reported by the player, suspects and hostages ----

        public void ReportNoise(Vector3 position, float radius)
        {
            if (level == null) return;
            float sqrRadius = radius * radius;
            foreach (var suspect in level.suspects)
                if ((suspect.transform.position - position).sqrMagnitude < sqrRadius) suspect.HearNoise(position);
            foreach (var hostage in level.hostages)
                if ((hostage.transform.position - position).sqrMagnitude < sqrRadius) hostage.Scare();
        }

        public void OnPlayerShout(PlayerController player)
        {
            shoutText = ShoutLines[Random.Range(0, ShoutLines.Length)];
            shoutTimer = 1.4f;
            foreach (var suspect in level.suspects)
            {
                if (Vector3.Distance(suspect.HeadPosition, player.EyePosition) > 15f) continue;
                if (!ClearLine(player.EyePosition, suspect.HeadPosition, suspect.transform)) continue;
                suspect.HearShout(player);
            }
        }

        public void OnFlashbang(Vector3 position)
        {
            ReportNoise(position, 25f);
            foreach (var suspect in level.suspects)
            {
                float distance = Vector3.Distance(position, suspect.HeadPosition);
                if (distance < 9f && ClearLine(position, suspect.HeadPosition, suspect.transform))
                    suspect.Stun(Mathf.Lerp(7f, 3.5f, distance / 9f), position);
            }

            Vector3 eye = Player.EyePosition;
            float playerDistance = Vector3.Distance(position, eye);
            if (playerDistance < 14f && ClearLine(position, eye, Player.transform))
            {
                float facing = Vector3.Dot(Player.Cam.transform.forward, (position - eye).normalized);
                float amount = (1f - playerDistance / 14f) * (facing > 0.2f ? 1.3f : 0.45f);
                Player.Flashbanged(Mathf.Clamp01(amount));
            }
        }

        public void OnSuspectSurrendered(Suspect suspect)
        {
            AddFeed("Suspect is surrendering! Press E to restrain them", Warning);
        }

        public void OnSuspectArrested(Suspect suspect)
        {
            AddFeed("Suspect restrained", Good);
            CheckComplete();
        }

        public void OnSuspectKilled(Suspect suspect)
        {
            AddFeed("Suspect neutralized", new Color(1f, 0.6f, 0.3f));
            CheckComplete();
        }

        public void OnUnauthorizedForce()
        {
            penalties++;
            AddFeed("PENALTY: Unauthorized use of force", Bad);
        }

        public void OnHostageSecured(Hostage hostage)
        {
            AddFeed("Hostage rescued", Good);
            CheckComplete();
        }

        public void OnHostageKilled(Hostage hostage)
        {
            AddFeed("A hostage was shot!", Bad);
            EndMission(false, "You shot a hostage.");
        }

        public void OnPlayerKilled()
        {
            EndMission(false, "Officer down.");
        }

        public void AddFeed(string text, Color color)
        {
            feed.Add(new FeedItem { text = text, color = color, time = Time.time });
            if (feed.Count > 6) feed.RemoveAt(0);
        }

        void CheckComplete()
        {
            if (State != GameState.Playing || completeTimer > 0f) return;
            foreach (var suspect in level.suspects) if (!suspect.IsNeutralized) return;
            foreach (var hostage in level.hostages) if (!hostage.IsSecured) return;
            completeTimer = 2f;
            shoutText = "ALL CLEAR. BUILDING SECURE.";
            shoutTimer = 2f;
            Player.Play(Sfx.Radio, 0.6f);
        }

        void EndMission(bool won, string reason)
        {
            if (State == GameState.Debrief) return;
            success = won;
            failReason = reason;
            result = Score(won);
            if (won)
            {
                careerScore += result.total;
                if (mission > PlayerPrefs.GetInt(BestMissionKey, 0))
                {
                    PlayerPrefs.SetInt(BestMissionKey, mission);
                    PlayerPrefs.Save();
                }
            }
            Player.Play(won ? Sfx.Success : Sfx.Fail, 0.6f);
            SetState(GameState.Debrief);
        }

        Result Score(bool won)
        {
            var r = new Result();
            foreach (var suspect in level.suspects)
            {
                if (suspect.Current == Suspect.State.Arrested) r.arrests++;
                else if (suspect.Current == Suspect.State.Dead) r.kills++;
            }
            foreach (var hostage in level.hostages) if (hostage.IsSecured) r.rescued++;
            r.health = won ? Mathf.CeilToInt(Player.Health) : 0;
            r.timeBonus = won ? Mathf.Max(0, 300 - Mathf.FloorToInt(missionTime)) : 0;
            r.penalties = penalties;
            r.total = Mathf.Max(0, r.arrests * 100 + r.kills * 40 + r.rescued * 100 + r.health + r.timeBonus - penalties * 150);

            int best = level.suspects.Count * 100 + level.hostages.Count * 100 + 100 + 300;
            float percent = (float)r.total / best;
            if (!won) r.rating = "F";
            else if (percent >= 0.85f) r.rating = "S";
            else if (percent >= 0.7f) r.rating = "A";
            else if (percent >= 0.55f) r.rating = "B";
            else if (percent >= 0.4f) r.rating = "C";
            else r.rating = "D";
            return r;
        }

        static bool ClearLine(Vector3 from, Vector3 to, Transform target)
        {
            RaycastHit hit;
            if (!Physics.Linecast(from, to, out hit, ~0, QueryTriggerInteraction.Ignore)) return true;
            return hit.collider.transform.IsChildOf(target);
        }

        // ---- HUD and menus ----

        void OnGUI()
        {
            if (Player == null || level == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
            float u = Mathf.Max(0.5f, Screen.height / 1080f);

            if (State == GameState.Playing || State == GameState.Paused) DrawHud(u);
            Overlay(new Color(1f, 1f, 1f, Mathf.Clamp01(Player.BlindFlash * 1.2f)));
            Overlay(new Color(0.7f, 0f, 0f, Player.DamageFlash * 0.35f));
            if (!Player.IsAlive) Overlay(new Color(0.4f, 0f, 0f, 0.35f));

            switch (State)
            {
                case GameState.Briefing: DrawBriefing(u); break;
                case GameState.Paused: DrawPause(u); break;
                case GameState.Debrief: DrawDebrief(u); break;
            }
        }

        void DrawHud(float u)
        {
            float w = Screen.width, h = Screen.height, pad = 30f * u;
            var center = new Vector2(w * 0.5f, h * 0.5f);
            var weapon = Player.Weapon;

            // Crosshair, hidden while aiming through the sight.
            if (weapon.AimAmount < 0.5f && Player.IsAlive)
            {
                float gap = (6f + weapon.Spread * 7f) * u, length = 10f * u, thick = Mathf.Max(1f, 2f * u);
                var color = new Color(1f, 1f, 1f, 0.85f * (1f - weapon.AimAmount * 2f));
                Fill(new Rect(center.x - gap - length, center.y - thick * 0.5f, length, thick), color);
                Fill(new Rect(center.x + gap, center.y - thick * 0.5f, length, thick), color);
                Fill(new Rect(center.x - thick * 0.5f, center.y - gap - length, thick, length), color);
                Fill(new Rect(center.x - thick * 0.5f, center.y + gap, thick, length), color);
                Fill(new Rect(center.x - thick * 0.5f, center.y - thick * 0.5f, thick, thick), color);
            }

            // Hit marker: a white X, red for head shots.
            if (Player.HitMarker > 0.75f)
            {
                var color = Player.HitMarkerHead ? Bad : Color.white;
                float gap = 8f * u, length = 9f * u, thick = Mathf.Max(1f, 2f * u);
                Matrix4x4 saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, center);
                Fill(new Rect(center.x - gap - length, center.y - thick * 0.5f, length, thick), color);
                Fill(new Rect(center.x + gap, center.y - thick * 0.5f, length, thick), color);
                Fill(new Rect(center.x - thick * 0.5f, center.y - gap - length, thick, length), color);
                Fill(new Rect(center.x - thick * 0.5f, center.y + gap, thick, length), color);
                GUI.matrix = saved;
            }

            // Red marker pointing towards whoever is shooting at you.
            if (Player.DamageIndicator > 0f)
            {
                Vector3 toAttacker = Player.LastDamageSource - Player.transform.position;
                toAttacker.y = 0f;
                float angle = Vector3.SignedAngle(Player.transform.forward, toAttacker, Vector3.up);
                Matrix4x4 saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(angle, center);
                Fill(new Rect(center.x - 45f * u, center.y - 170f * u, 90f * u, 10f * u), new Color(1f, 0.15f, 0.1f, Player.DamageIndicator));
                GUI.matrix = saved;
            }

            // Mission info, top left.
            int suspectsDone = 0, waiting = 0, hostagesDone = 0;
            foreach (var suspect in level.suspects)
            {
                if (suspect.IsNeutralized) suspectsDone++;
                if (suspect.Current == Suspect.State.Surrendered) waiting++;
            }
            foreach (var hostage in level.hostages) if (hostage.IsSecured) hostagesDone++;
            string info = "<b>OPERATION " + codename + "</b>\n" + level.location.ToUpper() + "   " + FormatTime(missionTime) + "\n\n"
                + "Suspects   " + suspectsDone + " / " + level.suspects.Count + "\n"
                + "Hostages   " + hostagesDone + " / " + level.hostages.Count;
            Text(new Rect(pad, pad, 520f * u, 200f * u), info, 22f * u, TextAnchor.UpperLeft, Color.white);
            if (waiting > 0)
                Text(new Rect(pad, pad + 150f * u, 600f * u, 40f * u), waiting + " suspect(s) waiting to be restrained", 20f * u, TextAnchor.UpperLeft, Warning);

            // Event feed, top right.
            float y = pad;
            foreach (var item in feed)
            {
                float age = Time.time - item.time;
                if (age > 5f) continue;
                var color = item.color;
                color.a = Mathf.Clamp01(5f - age);
                Text(new Rect(w - pad - 640f * u, y, 640f * u, 32f * u), item.text, 22f * u, TextAnchor.UpperRight, color);
                y += 32f * u;
            }

            // Shouts and announcements.
            if (shoutTimer > 0f && !string.IsNullOrEmpty(shoutText))
            {
                var color = Warning;
                color.a = Mathf.Clamp01(shoutTimer * 2f);
                Text(new Rect(0f, h * 0.2f, w, 60f * u), "<b>" + shoutText + "</b>", 40f * u, TextAnchor.UpperCenter, color);
            }

            if (!string.IsNullOrEmpty(Player.InteractPrompt))
                Text(new Rect(0f, h * 0.6f, w, 44f * u), "<b>" + Player.InteractPrompt + "</b>", 28f * u, TextAnchor.UpperCenter, Color.white);

            // Health, bottom left.
            float barWidth = 300f * u;
            float baseY = h - pad - 60f * u;
            Text(new Rect(pad, baseY, barWidth, 30f * u), "<b>HEALTH</b>", 20f * u, TextAnchor.UpperLeft, new Color(0.8f, 0.85f, 0.9f));
            Fill(new Rect(pad, baseY + 32f * u, barWidth, 18f * u), new Color(0f, 0f, 0f, 0.55f));
            float health = Player.Health / Player.maxHealth;
            var healthColor = health > 0.5f ? Good : health > 0.25f ? Warning : Bad;
            Fill(new Rect(pad + 2f * u, baseY + 34f * u, (barWidth - 4f * u) * health, 14f * u), healthColor);
            Text(new Rect(pad + barWidth + 12f * u, baseY + 22f * u, 100f * u, 40f * u), "<b>" + Mathf.CeilToInt(Player.Health) + "</b>", 28f * u, TextAnchor.UpperLeft, Color.white);

            // Ammo and flashbangs, bottom right.
            var ammoRect = new Rect(w - pad - 500f * u, h - pad - 120f * u, 500f * u, 60f * u);
            if (weapon.Reloading)
                Text(ammoRect, "<b>RELOADING...</b>", 40f * u, TextAnchor.LowerRight, Warning);
            else
                Text(ammoRect, "<b>" + weapon.Ammo + "</b>  /  " + weapon.Reserve, 44f * u, TextAnchor.LowerRight, weapon.Ammo <= 5 ? Warning : Color.white);
            string gear = "FLASHBANGS  " + Player.Flashbangs;
            if (!weapon.Reloading && weapon.Ammo <= 5 && weapon.Reserve > 0) gear = "[R] RELOAD     " + gear;
            if (weapon.Ammo == 0 && weapon.Reserve == 0) gear = "OUT OF AMMO     " + gear;
            Text(new Rect(w - pad - 500f * u, h - pad - 55f * u, 500f * u, 40f * u), gear, 22f * u, TextAnchor.UpperRight, new Color(0.8f, 0.85f, 0.9f));

            Text(new Rect(0f, h - pad - 26f * u, w, 30f * u), "E  interact     F  shout     G  flashbang     R  reload     Esc  pause", 18f * u, TextAnchor.UpperCenter, new Color(1f, 1f, 1f, 0.55f));
        }

        void DrawBriefing(float u)
        {
            Overlay(new Color(0f, 0f, 0f, 0.55f));
            Rect panel = Panel(u, 1000f, 800f);
            float x = panel.x + 44f * u, width = panel.width - 88f * u, y = panel.y + 34f * u;

            Text(new Rect(x, y, width, 30f * u), "<b>MISSION " + mission + "  ·  BRIEFING</b>", 20f * u, TextAnchor.UpperLeft, Accent);
            y += 34f * u;
            Text(new Rect(x, y, width, 70f * u), "<b>OPERATION " + codename + "</b>", 52f * u, TextAnchor.UpperLeft, Color.white);
            y += 80f * u;

            string situation = "Armed suspects have taken over " + Article(level.location) + " " + level.location.ToLower()
                + " and are holding " + Count(level.hostages.Count, "hostage") + ". Intel reports " + Count(level.suspects.Count, "armed suspect") + " inside.\n\n"
                + "Breach the front door, clear every room and rescue the hostages. Shout at suspects to make them surrender, then restrain them. Arrests are worth more than kills.\n\n"
                + "<b>Rules of engagement:</b> never shoot hostages or suspects who have surrendered.";
            Text(new Rect(x, y, width, 260f * u), situation, 22f * u, TextAnchor.UpperLeft, new Color(0.85f, 0.88f, 0.92f));
            y += 270f * u;

            string left = "<b>WASD</b>   Move\n<b>Mouse</b>   Look\n<b>Left click</b>   Shoot\n<b>Right click</b>   Aim down sights\n<b>Shift</b>   Sprint";
            string right = "<b>R</b>   Reload\n<b>E</b>   Breach door / restrain / rescue\n<b>F</b>   Shout \"Police! Drop your weapon!\"\n<b>G</b>   Throw flashbang\n<b>Esc</b>   Pause";
            Text(new Rect(x, y, width * 0.42f, 180f * u), left, 21f * u, TextAnchor.UpperLeft, Color.white);
            Text(new Rect(x + width * 0.42f, y, width * 0.58f, 180f * u), right, 21f * u, TextAnchor.UpperLeft, Color.white);

            Prompt(panel, u, "CLICK OR PRESS SPACE TO DEPLOY");
        }

        void DrawPause(float u)
        {
            Overlay(new Color(0f, 0f, 0f, 0.5f));
            Rect panel = Panel(u, 700f, 460f);
            float x = panel.x + 44f * u, width = panel.width - 88f * u;
            Text(new Rect(x, panel.y + 40f * u, width, 70f * u), "<b>PAUSED</b>", 52f * u, TextAnchor.UpperCenter, Color.white);
            string tips = "Shout (F) at suspects before they see you. Surprised, flashbanged or wounded suspects give up more often.\n\n"
                + "Throw a flashbang (G) into a room before you go in.";
            Text(new Rect(x, panel.y + 130f * u, width, 200f * u), tips, 22f * u, TextAnchor.UpperCenter, new Color(0.85f, 0.88f, 0.92f));
            Prompt(panel, u, "CLICK OR PRESS ESC TO RESUME");
        }

        void DrawDebrief(float u)
        {
            Overlay(new Color(0f, 0f, 0f, 0.55f));
            Rect panel = Panel(u, 820f, 820f);
            float x = panel.x + 54f * u, width = panel.width - 108f * u, y = panel.y + 36f * u;

            Text(new Rect(x, y, width, 30f * u), "<b>OPERATION " + codename + "  ·  DEBRIEF</b>", 20f * u, TextAnchor.UpperCenter, Accent);
            y += 36f * u;
            Text(new Rect(x, y, width, 70f * u), success ? "<b>MISSION COMPLETE</b>" : "<b>MISSION FAILED</b>", 52f * u, TextAnchor.UpperCenter, success ? Good : Bad);
            y += 76f * u;
            if (!string.IsNullOrEmpty(failReason))
            {
                Text(new Rect(x, y, width, 36f * u), failReason, 26f * u, TextAnchor.UpperCenter, Color.white);
                y += 44f * u;
            }
            y += 10f * u;

            Row(x, ref y, width, u, "Suspects arrested", result.arrests + "  x 100", result.arrests * 100);
            Row(x, ref y, width, u, "Suspects neutralized", result.kills + "  x 40", result.kills * 40);
            Row(x, ref y, width, u, "Hostages rescued", result.rescued + "  x 100", result.rescued * 100);
            Row(x, ref y, width, u, "Health remaining", "", result.health);
            Row(x, ref y, width, u, "Time bonus", FormatTime(missionTime), result.timeBonus);
            if (result.penalties > 0) Row(x, ref y, width, u, "Penalties", result.penalties + "  x 150", -result.penalties * 150);

            y += 10f * u;
            Fill(new Rect(x, y, width, Mathf.Max(1f, 2f * u)), new Color(1f, 1f, 1f, 0.3f));
            y += 16f * u;
            Text(new Rect(x, y, width, 50f * u), "<b>TOTAL</b>", 32f * u, TextAnchor.UpperLeft, Color.white);
            Text(new Rect(x, y, width, 50f * u), "<b>" + result.total + "</b>", 32f * u, TextAnchor.UpperRight, Color.white);
            y += 52f * u;
            Text(new Rect(x, y, width, 60f * u), "<b>RATING</b>", 32f * u, TextAnchor.UpperLeft, Color.white);
            Text(new Rect(x, y, width, 60f * u), "<b>" + result.rating + "</b>", 48f * u, TextAnchor.UpperRight, success ? Warning : Bad);
            y += 66f * u;
            Text(new Rect(x, y, width, 30f * u), "Career score " + careerScore + "     Best mission reached " + PlayerPrefs.GetInt(BestMissionKey, 0), 20f * u, TextAnchor.UpperCenter, new Color(0.75f, 0.8f, 0.85f));

            if (Time.unscaledTime - stateChangedAt > 1f)
                Prompt(panel, u, success ? "CLICK FOR THE NEXT MISSION" : "CLICK TO TRY AGAIN");
        }

        void Row(float x, ref float y, float width, float u, string label, string detail, int points)
        {
            var dim = new Color(0.85f, 0.88f, 0.92f);
            Text(new Rect(x, y, width, 34f * u), label, 24f * u, TextAnchor.UpperLeft, dim);
            Text(new Rect(x, y, width * 0.7f, 34f * u), detail, 24f * u, TextAnchor.UpperRight, new Color(0.6f, 0.65f, 0.7f));
            Text(new Rect(x, y, width, 34f * u), (points >= 0 ? "+" : "") + points, 24f * u, TextAnchor.UpperRight, points < 0 ? Bad : dim);
            y += 36f * u;
        }

        Rect Panel(float u, float width, float height)
        {
            float w = Mathf.Min(Screen.width - 40f, width * u);
            float h = Mathf.Min(Screen.height - 40f, height * u);
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            Fill(panel, new Color(0.05f, 0.07f, 0.1f, 0.94f));
            Fill(new Rect(panel.x, panel.y, panel.width, 6f * u), Accent);
            return panel;
        }

        void Prompt(Rect panel, float u, string text)
        {
            var color = Color.white;
            color.a = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 4f);
            Text(new Rect(panel.x, panel.yMax - 70f * u, panel.width, 40f * u), "<b>" + text + "</b>", 26f * u, TextAnchor.UpperCenter, color);
        }

        void Text(Rect rect, string text, float size, TextAnchor anchor, Color color)
        {
            style.fontSize = Mathf.Max(8, Mathf.RoundToInt(size));
            style.alignment = anchor;
            style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.8f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }

        static void Fill(Rect rect, Color color)
        {
            Color saved = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = saved;
        }

        static void Overlay(Color color)
        {
            if (color.a > 0.001f) Fill(new Rect(0f, 0f, Screen.width, Screen.height), color);
        }

        static string FormatTime(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return string.Format("{0}:{1:00}", total / 60, total % 60);
        }

        static string Count(int n, string noun)
        {
            return n + " " + noun + (n == 1 ? "" : "s");
        }

        static string Article(string word)
        {
            return "aeiouAEIOU".IndexOf(word[0]) >= 0 ? "an" : "a";
        }
    }
}
