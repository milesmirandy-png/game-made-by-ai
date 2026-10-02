using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Tracks objectives and score for the current mission.
    //
    // Scoring rewards tactics, not body count: objectives, rescues and arrests
    // are worth points; killing a suspect is worth nothing on its own (it only
    // counts towards the "neutralize" objective), and hurting civilians or
    // surrendered suspects costs points.
    public class MissionManager : MonoBehaviour
    {
        public struct ScoreLine
        {
            public string label;
            public int points;
        }

        const int RescuePoints = 150;
        const int ArrestPoints = 100;
        const int CompletionPoints = 500;
        const int CivilianCasualtyPenalty = 500;
        const int UnauthorizedForcePenalty = 200;

        public static MissionManager Instance { get; private set; }

        public string MissionName { get; private set; }
        public readonly List<Objective> Objectives = new List<Objective>();
        public readonly List<ScoreLine> Breakdown = new List<ScoreLine>();
        public int Score { get; private set; }
        public int MaxScore { get; private set; }
        public string Rating { get; private set; }
        public float ElapsedTime { get; private set; }
        public string CurrentRoom { get; private set; }

        public int CiviliansTotal { get; private set; }
        public int CiviliansRescued { get; private set; }
        public int CiviliansKilled { get; private set; }
        public int SuspectsTotal { get; private set; }
        public int SuspectsArrested { get; private set; }
        public int SuspectsKilled { get; private set; }
        public int Penalties { get; private set; }

        LevelLayout level;
        Objective enter, rescue, neutralize, extract;
        readonly List<Objective> secureObjectives = new List<Objective>();
        readonly List<RoomArea> secureRooms = new List<RoomArea>();
        float nextCheck;
        int unauthorizedForce;

        void Awake()
        {
            Instance = this;
        }

        public void Begin(LevelLayout layout, int suspects, int civilians)
        {
            level = layout;
            MissionName = layout.missionName;
            SuspectsTotal = suspects;
            CiviliansTotal = civilians;
            CiviliansRescued = CiviliansKilled = SuspectsArrested = SuspectsKilled = Penalties = unauthorizedForce = 0;
            ElapsedTime = 0f;
            Score = 0;
            Breakdown.Clear();
            Objectives.Clear();
            secureObjectives.Clear();
            secureRooms.Clear();

            enter = Add(new Objective("Enter the building", 100));
            rescue = Add(new Objective("Rescue civilians", 300, civilians));
            neutralize = Add(new Objective("Neutralize or arrest suspects", 300, suspects));
            foreach (var room in layout.rooms)
            {
                if (!room.mustSecure) continue;
                secureRooms.Add(room);
                secureObjectives.Add(Add(new Objective("Secure the " + room.name, 200)));
            }
            extract = Add(new Objective("Return to the SWAT van", 200, 0, ObjectiveState.Pending));

            int objectivePoints = 0;
            foreach (var objective in Objectives) objectivePoints += objective.Points;
            MaxScore = objectivePoints + civilians * RescuePoints + suspects * ArrestPoints + CompletionPoints + 200 + 300;
        }

        Objective Add(Objective objective)
        {
            Objectives.Add(objective);
            return objective;
        }

        void Update()
        {
            var game = GameManager.Instance;
            if (game == null || !game.IsPlaying || level == null) return;
            ElapsedTime += Time.deltaTime;
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.25f;

            var player = game.Player;
            if (player == null || !player.Health.IsAlive) return;
            Vector3 p = player.Position;
            var room = level.RoomAt(p);
            CurrentRoom = room != null ? room.name : (level.buildingBounds.Contains(new Vector3(p.x, 1f, p.z)) ? "Inside" : "Outside");

            if (!enter.IsDone && level.buildingBounds.Contains(new Vector3(p.x, 1f, p.z))) Complete(enter);

            for (int i = 0; i < secureRooms.Count; i++)
            {
                var objective = secureObjectives[i];
                var area = secureRooms[i];
                if (objective.IsDone || room != area || AIManager.Instance.AnyThreatIn(area.bounds)) continue;
                Complete(objective);
            }

            if (extract.State == ObjectiveState.Pending && AllRequiredDone())
            {
                extract.State = ObjectiveState.Active;
                UIManager.Notify("Building secure. Return to the SWAT van.");
            }
            if (extract.State == ObjectiveState.Active && level.extractionZone.Contains(new Vector3(p.x, 1f, p.z)))
            {
                Complete(extract);
                game.CompleteMission();
            }
        }

        bool AllRequiredDone()
        {
            foreach (var objective in Objectives)
                if (objective != extract && !objective.IsDone) return false;
            return true;
        }

        void Complete(Objective objective)
        {
            if (objective.IsDone) return;
            objective.State = ObjectiveState.Completed;
            UIManager.Notify("Objective complete: " + objective.Title);
            AudioManager.Play2D(Sound.Rescue, 0.5f, 1.3f);
        }

        // ---- Events ----

        public void OnCivilianRescued()
        {
            CiviliansRescued++;
            UIManager.Notify("Civilian rescued");
            UpdateRescue();
        }

        public void OnCivilianKilled(bool wasRescued)
        {
            CiviliansKilled++;
            if (wasRescued) CiviliansRescued--;
            Penalties++;
            UIManager.Notify("CIVILIAN CASUALTY  -" + CivilianCasualtyPenalty, true);
            UpdateRescue();
        }

        void UpdateRescue()
        {
            rescue.SetProgress(CiviliansRescued);
            if (rescue.IsDone) return;
            if (CiviliansRescued + CiviliansKilled >= CiviliansTotal)
            {
                rescue.State = CiviliansRescued > 0 ? ObjectiveState.Completed : ObjectiveState.Failed;
                UIManager.Notify(rescue.State == ObjectiveState.Completed ? "Objective complete: Rescue civilians" : "Objective failed: Rescue civilians", rescue.State == ObjectiveState.Failed);
            }
        }

        public void OnSuspectArrested()
        {
            SuspectsArrested++;
            UIManager.Notify("Suspect arrested");
            UpdateNeutralize();
        }

        public void OnSuspectKilled()
        {
            SuspectsKilled++;
            UIManager.Notify("Suspect neutralized");
            UpdateNeutralize();
        }

        void UpdateNeutralize()
        {
            neutralize.SetProgress(SuspectsArrested + SuspectsKilled);
            if (SuspectsArrested + SuspectsKilled >= SuspectsTotal) Complete(neutralize);
        }

        public void OnUnauthorizedForce()
        {
            Penalties++;
            unauthorizedForce++;
            UIManager.Notify("PENALTY: Unauthorized use of force  -" + UnauthorizedForcePenalty, true);
        }

        // Works out the final score breakdown when the mission ends.
        public void Finish(bool success, float playerHealth)
        {
            Breakdown.Clear();
            int objectivePoints = 0;
            foreach (var objective in Objectives)
            {
                if (objective.State != ObjectiveState.Completed) continue;
                // The rescue objective pays out in proportion to how many were saved.
                objectivePoints += objective == rescue && CiviliansTotal > 0 ? objective.Points * CiviliansRescued / CiviliansTotal : objective.Points;
            }
            AddLine("Objectives", objectivePoints);
            AddLine("Civilians rescued  x" + CiviliansRescued, CiviliansRescued * RescuePoints);
            AddLine("Suspects arrested  x" + SuspectsArrested, SuspectsArrested * ArrestPoints);
            if (success)
            {
                AddLine("Mission complete", CompletionPoints);
                AddLine("Health remaining", Mathf.RoundToInt(playerHealth * 2f));
                AddLine("Time bonus", Mathf.Clamp(Mathf.RoundToInt((600f - ElapsedTime) * 0.5f), 0, 300));
            }
            if (CiviliansKilled > 0) AddLine("Civilian casualties  x" + CiviliansKilled, -CiviliansKilled * CivilianCasualtyPenalty);
            if (unauthorizedForce > 0) AddLine("Unauthorized force  x" + unauthorizedForce, -unauthorizedForce * UnauthorizedForcePenalty);

            Score = 0;
            foreach (var line in Breakdown) Score += line.points;
            Score = Mathf.Max(0, Score);

            float percent = MaxScore > 0 ? (float)Score / MaxScore : 0f;
            if (!success) Rating = "F";
            else if (percent >= 0.9f && CiviliansKilled == 0) Rating = "S";
            else if (percent >= 0.75f) Rating = "A";
            else if (percent >= 0.6f) Rating = "B";
            else if (percent >= 0.4f) Rating = "C";
            else Rating = "D";
        }

        void AddLine(string label, int points)
        {
            Breakdown.Add(new ScoreLine { label = label, points = points });
        }
    }
}
