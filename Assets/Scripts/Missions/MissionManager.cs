using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    public class MissionResult
    {
        public MissionData mission;
        public MissionPlan plan;
        public bool success;
        public string reason;
        public List<ScoreLine> lines;
        public int total;
        public string rating;
        public float time;
        public float playerHealth;
        public List<Objective> objectives;
        public MissionStats stats;
        public readonly List<string> squad = new List<string>();
        public readonly List<string> unlocks = new List<string>();
        public bool newBest;
        public int previousBest;
    }

    // Runs the current mission: the objective tracker, statistics, the mission
    // clock and the final result. Gameplay systems report events here.
    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }

        public MissionData Mission { get; private set; }
        public MissionPlan Plan { get; private set; }
        public MissionStats Stats { get; private set; }
        public ObjectiveTracker Tracker { get; private set; }
        public List<Objective> Objectives { get { return Tracker.Objectives; } }
        public float Elapsed { get; private set; }
        public bool Running { get; private set; }
        public MissionResult Result { get; private set; }

        float nextTick;
        readonly Dictionary<Objective, ObjectiveState> lastStates = new Dictionary<Objective, ObjectiveState>();
        string lastForceName;
        float lastForceTime = -10f;

        void Awake()
        {
            Instance = this;
            Stats = new MissionStats();
            Tracker = new ObjectiveTracker();
        }

        public void Begin(MissionPlan plan)
        {
            Plan = plan;
            Mission = plan.mission;
            Stats = new MissionStats();
            Tracker = new ObjectiveTracker();
            Tracker.Begin(Mission, plan);
            Elapsed = 0f;
            Result = null;
            Running = true;
            nextTick = 0f;

            foreach (var enemy in AIManager.Instance.Enemies)
                if (enemy.Data.archetype != EnemyArchetype.TrainingDummy)
                {
                    Stats.suspectsTotal++;
                    if (enemy.Data.armed) Stats.armedSuspects++;
                }
            TocReports.Begin();
            foreach (var civilian in AIManager.Instance.Civilians)
            {
                Stats.civilians.total++;
                if (civilian.State == CivilianState.Injured) Stats.civilians.initiallyInjured++;
            }
            Stats.evidenceTotal = GameManager.Instance.Level.evidence.Count;
            lastStates.Clear();
            foreach (var objective in Tracker.Objectives) lastStates[objective] = objective.State;
            int optional = 0;
            foreach (var objective in Tracker.Objectives) if (objective.Optional) optional++;
            if (optional > 0) UIManager.Banner("OPTIONAL OBJECTIVES", optional + " available this deployment (press " + UITheme.KeyFor(InputAction.Objectives) + ")", BannerKind.Info);
            // Deployed solo: squad-command steps can't be done, so they're waived.
            if (AIManager.Instance.Officers.Count == 0) Tracker.CompleteType(ObjectiveType.TrainingCommandSquad);
            // A shield carrier has no primary weapon to switch back to.
            var player = GameManager.Instance.Player;
            if (player != null && player.Weapons.Inventory.PrimaryBlocked) Tracker.CompleteType(ObjectiveType.TrainingSwitchWeapon);
        }

        void Update()
        {
            var game = GameManager.Instance;
            if (!Running || game == null || !game.IsPlaying || game.Player == null) return;
            Elapsed += Time.deltaTime;
            if (Time.time < nextTick) return;
            nextTick = Time.time + 0.25f;
            Tracker.Tick(this, game, Elapsed);
            AnnounceObjectiveChanges();
            if (Tracker.ExtractionReached) game.EndMission(!Tracker.AnyMandatoryFailed, Tracker.AnyMandatoryFailed ? "A primary objective failed." : null);
            else if (Mission.isTraining && Tracker.CurrentStep == null) game.EndMission(true, null);
        }

        // Short banners and a radio call when objectives change (no long center-screen messages).
        void AnnounceObjectiveChanges()
        {
            foreach (var objective in Tracker.Objectives)
            {
                ObjectiveState before;
                if (!lastStates.TryGetValue(objective, out before)) before = objective.State;
                if (before == objective.State) continue;
                lastStates[objective] = objective.State;
                string label = objective.Definition.description;
                switch (objective.State)
                {
                    case ObjectiveState.Completed:
                        UIManager.Banner(objective.Optional ? "OPTIONAL OBJECTIVE COMPLETE" : "OBJECTIVE COMPLETE", label, BannerKind.Good);
                        AudioManager.Play2D(Sound.ObjectiveTone, 0.6f, 1f, SoundCategory.Interface);
                        if (objective.Type != ObjectiveType.ReachExtraction) SquadCommandManager.Instance.Radio(null, Mission.isTraining ? "Good. Next step." : "Objective complete.");
                        break;
                    case ObjectiveState.Failed:
                        UIManager.Banner(objective.Optional ? "OPTIONAL OBJECTIVE FAILED" : "OBJECTIVE FAILED", label, BannerKind.Bad);
                        AudioManager.Play2D(Sound.Warning, 0.5f, 1f, SoundCategory.Interface);
                        break;
                    case ObjectiveState.Active:
                        if (before == ObjectiveState.Pending)
                        {
                            UIManager.Banner("OBJECTIVE UPDATED", label, BannerKind.Info);
                            AudioManager.Play2D(Sound.RadioOrder, 0.4f, 1.2f, SoundCategory.Interface);
                        }
                        break;
                }
            }
        }

        // ---- Reports from gameplay ----

        public void Report(ObjectiveType type, int amount)
        {
            if (Running) Tracker.Report(type, amount);
        }

        public void ReportTarget(ObjectiveType type, string id)
        {
            if (Running) Tracker.ReportTarget(type, id);
        }

        public void ReportEquipment(EquipmentKind kind)
        {
            if (Running) Stats.UsedEquipment(kind);
        }

        public void ReportDoor(DoorController door)
        {
            ReportTarget(ObjectiveType.TrainingOpenDoor, door.Id);
        }

        public void ReportBreach(DoorController door, bool byPlayer)
        {
            if (!Running) return;
            Stats.doorsBreached++;
            ReportTarget(ObjectiveType.TrainingBreach, door.Id);
        }

        public void ReportConsole(SecurityConsole console)
        {
            ReportTarget(ObjectiveType.UseConsole, console.Id);
        }

        public void ReportFootage()
        {
            Stats.footageReviewed = true;
        }

        public void ReportCameras()
        {
            foreach (var cam in SecurityCamera.All) if (cam != null && cam.Active) return;
            Stats.camerasDisabled = true;
        }

        public void OnWeaponDropped() { if (Running) Stats.weaponsDropped++; }
        public void OnWeaponTaken() { if (Running) Stats.weaponsDropped = System.Math.Max(0, Stats.weaponsDropped - 1); }
        public void OnWeaponSecured() { if (Running) Stats.weaponsSecured++; }
        public void OnReportedToToc() { if (Running) Stats.tocReports++; }

        public void ReportEvidence()
        {
            Stats.evidenceSecured++;
            UIManager.Notify("Evidence secured (" + Stats.evidenceSecured + ")");
        }

        public void ReportAlarm()
        {
            Stats.alarmTriggered = true;
            if (Running) Tracker.Fail(ObjectiveType.AlarmNotTriggered);
        }

        // ---- Suspects ----

        public void OnUnauthorizedForce(string targetName)
        {
            if (!Running) return;
            // One shotgun blast or burst counts once.
            if (targetName == lastForceName && Time.time - lastForceTime < 2f) return;
            lastForceName = targetName;
            lastForceTime = Time.time;
            Stats.unauthorizedForce++;
            UIManager.Notify("Unauthorized use of force against " + targetName + " (penalty)", true);
        }

        public void OnSuspectRestrained(EnemyAI enemy)
        {
            if (!Running) return;
            if (enemy.Data.archetype == EnemyArchetype.TrainingDummy) Tracker.CompleteType(ObjectiveType.TrainingRestrain);
            else if (enemy.WasIncapacitated) UIManager.Notify("Incapacitated suspect restrained");
            else
            {
                Stats.suspectsArrested++;
                UIManager.Notify("Suspect arrested");
            }
        }

        public void OnSuspectIncapacitated(EnemyAI enemy)
        {
            if (!Running || enemy.Data.archetype == EnemyArchetype.TrainingDummy) return;
            Stats.suspectsIncapacitated++;
        }

        public void OnSuspectDown(EnemyAI enemy, bool wasRestrained)
        {
            if (!Running) return;
            if (enemy.Data.archetype == EnemyArchetype.TrainingDummy)
            {
                Tracker.Fail(ObjectiveType.TrainingRestrain);
                return;
            }
            if (enemy.WasIncapacitated) Stats.suspectsIncapacitated = Mathf.Max(0, Stats.suspectsIncapacitated - 1);
            else if (wasRestrained) Stats.suspectsArrested = Mathf.Max(0, Stats.suspectsArrested - 1);
            Stats.suspectsKilled++;
            bool last = true;
            foreach (var other in AIManager.Instance.Enemies)
                if (other != null && other != enemy && !other.IsNeutralized && other.Data.archetype != EnemyArchetype.TrainingDummy) { last = false; break; }
            if (last)
            {
                // The last one: a beat of slow motion as they fall.
                GameManager.Instance.SlowMotion(1.1f);
                UIManager.Notify("Last suspect neutralized");
            }
            else UIManager.Notify("Suspect neutralized");
        }

        public void OnLeaderEscaped(EnemyAI enemy)
        {
            if (!Running) return;
            Stats.suspectsEscaped++;
            Stats.leaderEscaped = true;
            UIManager.Notify("The suspect leader escaped!", true);
            SquadCommandManager.Instance.Radio(null, "Suspect leader has left the perimeter.");
        }

        // ---- Police ----

        public void OnOfficerDown(SquadAI officer)
        {
            if (!Running) return;
            Stats.officersDowned++;
            Tracker.Fail(ObjectiveType.NoOfficerDown);
        }

        public void OnPlayerDown()
        {
            if (!Running) return;
            Stats.playerDowned = true;
            Stats.officersDowned++;
            Tracker.Fail(ObjectiveType.NoOfficerDown);
        }

        // ---- Civilians ----

        public void OnCivilianEncountered(CivilianAI civilian)
        {
            if (!Running) return;
            Stats.civilians.encountered++;
            SquadCommandManager.Instance.CivilianLocated(civilian);
        }

        public void OnCivilianSecured(CivilianAI civilian)
        {
            if (Running) Stats.civilians.rescued++;
        }

        public void OnCivilianEvacuated(CivilianAI civilian)
        {
            if (!Running) return;
            Stats.civilians.evacuated++;
            UIManager.Notify("Civilian evacuated (" + Stats.civilians.evacuated + "/" + Stats.civilians.total + ")");
            AudioManager.Play2D(Sound.Rescue, 0.6f);
        }

        public void OnCivilianTreated(CivilianAI civilian)
        {
            if (Running) Stats.civilians.treated++;
        }

        public void OnCivilianInjured(CivilianAI civilian)
        {
            if (!Running) return;
            Stats.civilians.injured++;
            UIManager.Banner("CIVILIAN IN DANGER", "A civilian has been hurt", BannerKind.Bad);
            AudioManager.Play2D(Sound.Warning, 0.5f, 1f, SoundCategory.Interface);
        }

        public void OnCivilianKilled(CivilianAI civilian)
        {
            if (!Running) return;
            Stats.civilians.killed++;
            Tracker.Fail(ObjectiveType.NoCivilianCasualties);
            Tracker.Fail(ObjectiveType.TrainingEscort);
            UIManager.Notify("Civilian casualty", true);
        }

        // ---- End ----

        public MissionResult Finish(bool success, string reason)
        {
            if (!Running && Result != null) return Result;
            Running = false;
            Tracker.Settle(success);
            var game = GameManager.Instance;
            var player = game.Player;
            if (player != null)
            {
                Stats.shotsFired = player.Weapons.ShotsFired;
                Stats.shotsHit = player.Weapons.ShotsHit;
            }
            float health = player != null ? player.Health.Fraction : 0f;

            var result = new MissionResult
            {
                mission = Mission,
                plan = Plan,
                success = success,
                reason = reason,
                time = Elapsed,
                playerHealth = health,
                objectives = new List<Objective>(Tracker.Objectives),
                stats = Stats,
            };
            int total;
            string rating;
            result.lines = MissionScoring.Calculate(this, success, health, out total, out rating);
            result.total = total;
            result.rating = rating;
            foreach (var officer in AIManager.Instance.Officers)
                result.squad.Add(officer.Data.callsign + " (" + officer.Data.role + "): " + SquadStatusTracker.Condition(officer.Health)
                    + (officer.IsAlive ? " " + Mathf.RoundToInt(officer.Health.Fraction * 100f) + "%" : ""));
            Result = result;
            return result;
        }

        public void Abort()
        {
            Running = false;
        }

        // Game modes have no mission: nothing to track or show.
        public void Clear()
        {
            Running = false;
            Mission = null;
            Plan = null;
            Result = null;
            Stats = new MissionStats();
            Tracker = new ObjectiveTracker();
        }
    }
}
