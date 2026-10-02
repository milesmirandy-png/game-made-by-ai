using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Keeps every objective's state up to date. Most objectives are checked
    // from the game state a few times a second (cheap counts over short
    // lists); events such as "door opened" or "console used" arrive as
    // reports. Extraction only becomes active once every other mandatory
    // objective is resolved.
    public class ObjectiveTracker
    {
        public readonly List<Objective> Objectives = new List<Objective>();
        public bool Sequential { get; private set; }
        public bool ExtractionReached { get; private set; }

        public void Begin(MissionData mission, MissionPlan plan)
        {
            Objectives.Clear();
            ExtractionReached = false;
            Sequential = mission.sequentialObjectives;
            foreach (var definition in mission.objectives) Objectives.Add(new Objective(definition, false));
            foreach (var definition in plan.optional) Objectives.Add(new Objective(definition, true));
            foreach (var objective in Objectives)
            {
                if (objective.Type == ObjectiveType.TimeLimit) objective.Target = 0; // the count is seconds, not a tally
                if (objective.Type == ObjectiveType.ReachExtraction) objective.State = ObjectiveState.Pending;
            }
            UpdateSequence();
        }

        public IEnumerable<Objective> Mandatory
        {
            get { foreach (var o in Objectives) if (!o.Optional) yield return o; }
        }

        public bool AnyMandatoryFailed
        {
            get
            {
                foreach (var o in Objectives) if (!o.Optional && o.State == ObjectiveState.Failed) return true;
                return false;
            }
        }

        public Objective CurrentStep
        {
            get
            {
                foreach (var o in Objectives) if (!o.Optional && o.State == ObjectiveState.Active) return o;
                return null;
            }
        }

        // ---- Reports ----

        public void Report(ObjectiveType type, int amount)
        {
            // Pending steps count too, so doing a training step early never soft-locks it.
            foreach (var objective in Objectives)
            {
                if (objective.Type != type || objective.IsDone) continue;
                switch (type)
                {
                    case ObjectiveType.TrainingMove:
                        if (string.IsNullOrEmpty(objective.TargetId)) objective.Complete();
                        break;
                    case ObjectiveType.TrainingSwitchWeapon:
                        // Sidearm first (index 1), then back to the primary (index 0).
                        if (amount == 1) objective.Progress |= 1;
                        else if (amount == 0 && (objective.Progress & 1) != 0) objective.Complete();
                        break;
                    case ObjectiveType.TrainingRestrain:
                        break; // completed when a suspect is restrained
                    default:
                        objective.Progress += amount;
                        if (objective.Target <= 1 ? objective.Progress > 0 : objective.Progress >= objective.Target) objective.Complete();
                        break;
                }
            }
            UpdateSequence();
        }

        public void ReportTarget(ObjectiveType type, string targetId)
        {
            foreach (var objective in Objectives)
                if (objective.Type == type && !objective.IsDone && (string.IsNullOrEmpty(objective.TargetId) || objective.TargetId == targetId))
                    objective.Complete();
            UpdateSequence();
        }

        public void Fail(ObjectiveType type)
        {
            foreach (var objective in Objectives)
                if (objective.Type == type && !objective.IsDone) objective.Fail();
            UpdateSequence();
        }

        public void CompleteType(ObjectiveType type)
        {
            foreach (var objective in Objectives)
                if (objective.Type == type && !objective.IsDone) objective.Complete();
            UpdateSequence();
        }

        // ---- Polling ----

        public void Tick(MissionManager mission, GameManager game, float elapsed)
        {
            var level = game.Level;
            var player = game.Player;
            var ai = AIManager.Instance;
            var stats = mission.Stats;

            foreach (var objective in Objectives)
            {
                if (objective.State != ObjectiveState.Active) continue;
                switch (objective.Type)
                {
                    case ObjectiveType.EnterBuilding:
                    {
                        var room = level.RoomAt(player.Position);
                        if (room != null && room.Indoor) objective.Complete();
                        break;
                    }
                    case ObjectiveType.TrainingMove:
                        if (!string.IsNullOrEmpty(objective.TargetId) && level.InZone(objective.TargetId, player.Position)) objective.Complete();
                        break;
                    case ObjectiveType.SecureSuspects:
                    {
                        int total = 0, secured = 0;
                        foreach (var enemy in ai.Enemies)
                        {
                            if (enemy.Data.archetype == EnemyArchetype.TrainingDummy) continue;
                            total++;
                            if (enemy.State == EnemyState.Dead || enemy.State == EnemyState.Restrained) secured++;
                        }
                        objective.Target = total;
                        objective.Progress = secured;
                        if (stats.suspectsEscaped > 0) objective.Fail();
                        else if (secured >= total) objective.Complete();
                        break;
                    }
                    case ObjectiveType.ArrestSuspects:
                    {
                        objective.Progress = stats.suspectsArrested;
                        int stillPossible = stats.suspectsArrested;
                        foreach (var enemy in ai.Enemies) if (!enemy.IsNeutralized) stillPossible++;
                        if (objective.Progress >= objective.Target) objective.Complete();
                        else if (stillPossible < objective.Target) objective.Fail();
                        break;
                    }
                    case ObjectiveType.RescueCivilians:
                    {
                        var civilians = stats.civilians;
                        objective.Target = civilians.total;
                        objective.Progress = civilians.evacuated;
                        if (civilians.total > 0 && civilians.evacuated == 0 && civilians.killed >= civilians.total) objective.Fail();
                        else if (civilians.Remaining <= 0) objective.Complete();
                        break;
                    }
                    case ObjectiveType.TrainingEscort:
                        if (stats.civilians.evacuated > 0) objective.Complete();
                        break;
                    case ObjectiveType.SecureRoom:
                    {
                        var room = level.Room(objective.TargetId);
                        if (room == null || room.State >= RoomState.Secured) objective.Complete();
                        break;
                    }
                    case ObjectiveType.InvestigateRoom:
                    {
                        var room = level.Room(objective.TargetId);
                        if (room == null || room.State >= RoomState.Investigated) objective.Complete();
                        break;
                    }
                    case ObjectiveType.SecureEvidence:
                        objective.Progress = stats.evidenceSecured;
                        if (objective.Progress >= Mathf.Max(1, objective.Target)) objective.Complete();
                        break;
                    case ObjectiveType.ApprehendLeader:
                        foreach (var enemy in ai.Enemies)
                        {
                            if (!enemy.IsLeader) continue;
                            if (enemy.State == EnemyState.Restrained) objective.Complete();
                            else if (enemy.State == EnemyState.Dead || enemy.Escaped) objective.Fail();
                        }
                        break;
                    case ObjectiveType.DisableCameras:
                    {
                        bool anyActive = false;
                        foreach (var cam in SecurityCamera.All) if (cam != null && cam.Active) { anyActive = true; break; }
                        if (!anyActive) objective.Complete();
                        break;
                    }
                    case ObjectiveType.TreatInjured:
                    {
                        var civilians = stats.civilians;
                        objective.Target = civilians.initiallyInjured;
                        objective.Progress = civilians.treated;
                        if (civilians.treated >= civilians.initiallyInjured) objective.Complete();
                        break;
                    }
                    case ObjectiveType.TimeLimit:
                        if (elapsed > objective.Definition.count) objective.Fail();
                        break;
                    case ObjectiveType.ReachExtraction:
                        if (level.extraction != null && level.extraction.Contains(player.Position))
                        {
                            objective.Complete();
                            ExtractionReached = true;
                        }
                        break;
                }
            }
            UpdateSequence();
        }

        // Activates extraction once everything else mandatory is resolved and,
        // for training, unlocks one step at a time.
        void UpdateSequence()
        {
            if (Sequential)
            {
                bool earlierOpen = false;
                foreach (var objective in Objectives)
                {
                    if (objective.Optional || objective.IsDone) continue;
                    objective.State = earlierOpen ? ObjectiveState.Pending : ObjectiveState.Active;
                    earlierOpen = true;
                }
                return;
            }
            bool othersResolved = true;
            foreach (var objective in Objectives)
                if (!objective.Optional && objective.Type != ObjectiveType.ReachExtraction && !objective.IsDone && !objective.IsCondition) othersResolved = false;
            foreach (var objective in Objectives)
            {
                if (objective.Type != ObjectiveType.ReachExtraction || objective.IsDone) continue;
                bool wasPending = objective.State == ObjectiveState.Pending;
                objective.State = othersResolved ? ObjectiveState.Active : ObjectiveState.Pending;
                if (wasPending && othersResolved) UIManager.Notify("All objectives resolved. Return to the extraction point.");
            }
        }

        // Settles conditions when the mission ends.
        public void Settle(bool success)
        {
            foreach (var objective in Objectives)
            {
                if (objective.IsDone) continue;
                if (objective.IsCondition && success) objective.Complete();
                else objective.Fail();
            }
        }
    }
}
