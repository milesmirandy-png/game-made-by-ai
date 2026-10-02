using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // The rolled variation of a mission, decided from the seed before
    // deployment so the briefing can show it.
    public class MissionPlan
    {
        public MissionData mission;
        public int seed;
        public int difficulty;
        public readonly List<ObjectiveDefinition> optional = new List<ObjectiveDefinition>();
        public bool powerOutage, alarmArmed, camerasActive;
        public float accuracyMultiplier = 1f, reactionMultiplier = 1f;
        public int enemyCount, civilianCount;
        public int lockedDoors; // filled in when the level is populated
    }

    // Lightweight replayability. Map layouts are fixed; the seed decides who
    // is where (from the map's approved spawn points), patrol routes, which
    // doors are locked, which optional objectives are offered, whether the
    // alarm and cameras are active and whether the power is out. Randomly
    // locked doors are always breachable or pickable, and every spawn point
    // is on the NavMesh, so each variation stays completable.
    public static class MissionRandomizer
    {
        static readonly float[] AccuracyByDifficulty = { 0.7f, 1f, 1.25f };
        static readonly float[] ReactionByDifficulty = { 1.35f, 1f, 0.8f };

        public static MissionPlan Plan(MissionData mission, int seed, int difficulty)
        {
            var rng = new System.Random(seed);
            var plan = new MissionPlan { mission = mission, seed = seed, difficulty = Mathf.Clamp(difficulty, 0, 2) };
            plan.powerOutage = rng.NextDouble() < mission.powerOutageChance;
            plan.alarmArmed = mission.mapId != "training" && rng.NextDouble() < mission.alarmArmedChance;
            plan.camerasActive = rng.NextDouble() < mission.camerasActiveChance;
            plan.accuracyMultiplier = AccuracyByDifficulty[plan.difficulty];
            plan.reactionMultiplier = ReactionByDifficulty[plan.difficulty];
            foreach (var group in mission.enemies) plan.enemyCount += group.count;
            foreach (var group in mission.civilians) plan.civilianCount += group.count;

            var pool = new List<ObjectiveDefinition>();
            foreach (var candidate in mission.optionalPool)
                if (Valid(candidate, mission, plan)) pool.Add(candidate);
            Shuffle(pool, rng);
            for (int i = 0; i < pool.Count && plan.optional.Count < mission.optionalCount; i++) plan.optional.Add(pool[i]);
            return plan;
        }

        // Optional objectives that can't be done in this variation are never offered.
        static bool Valid(ObjectiveDefinition objective, MissionData mission, MissionPlan plan)
        {
            switch (objective.type)
            {
                case ObjectiveType.DisableCameras: return plan.camerasActive;
                case ObjectiveType.AlarmNotTriggered: return plan.alarmArmed;
                case ObjectiveType.ArrestSuspects: return objective.count <= plan.enemyCount;
                case ObjectiveType.NoCivilianCasualties: return plan.civilianCount > 0;
                case ObjectiveType.TreatInjured:
                    foreach (var group in mission.civilians) if (group.type == CivilianType.Injured && group.count > 0) return true;
                    return false;
                case ObjectiveType.ApprehendLeader:
                    foreach (var group in mission.enemies)
                    {
                        var data = GameData.Enemy(group.enemyId);
                        if (data != null && data.archetype == EnemyArchetype.Leader) return true;
                    }
                    return false;
                default: return true;
            }
        }

        // Builds the mission's dynamic contents into a freshly built map.
        public static void Populate(MissionPlan plan, LevelLayout level, Transform actors)
        {
            var mission = plan.mission;
            var rng = new System.Random(plan.seed * 7919 + 17);

            // Security devices.
            SecurityCamera.All.Clear();
            foreach (var mount in level.cameraMounts) SecurityCamera.Create(level.root, mount.position, mount.yaw, plan.camerasActive);
            level.consoles.Clear();
            foreach (var mount in level.consoleMounts)
                level.consoles.Add(SecurityConsole.Create(level.root, mount.id, mount.title, mount.position, mount.yaw, mount.unlockDoors, mount.reviewFootage));
            level.alarm = level.hasAlarm ? AlarmSystem.Create(level.root, level.alarmPanel.position, level.alarmPanel.yaw, level.alarmBeacons, plan.alarmArmed) : null;

            // Evidence: as many as the objectives ask for, plus one spare if there's room.
            int evidenceNeeded = 0;
            foreach (var objective in mission.objectives) if (objective.type == ObjectiveType.SecureEvidence) evidenceNeeded = Mathf.Max(evidenceNeeded, objective.count);
            foreach (var objective in plan.optional) if (objective.type == ObjectiveType.SecureEvidence) evidenceNeeded = Mathf.Max(evidenceNeeded, objective.count);
            level.evidence.Clear();
            if (evidenceNeeded > 0)
            {
                var spots = new List<Vector3>(level.evidenceSpots);
                Shuffle(spots, rng);
                int count = Mathf.Min(spots.Count, evidenceNeeded + 1);
                for (int i = 0; i < count; i++) level.evidence.Add(EvidenceItem.Create(level.root, spots[i]));
            }
            foreach (var position in level.trainingTargets) TrainingTarget.Create(level.root, position);

            // Randomly locked doors (always breachable or pickable).
            plan.lockedDoors = 0;
            foreach (var door in level.lockableDoors)
            {
                if (rng.NextDouble() >= mission.randomLockChance) continue;
                door.SetLocked(true);
                plan.lockedDoors++;
            }

            // Power outage: indoor rooms go dark.
            foreach (var room in level.rooms) room.IsDark = plan.powerOutage && room.Indoor;

            // Suspects.
            var usedEnemySpots = new HashSet<EnemySpawnPoint>();
            foreach (var group in mission.enemies)
            {
                var data = GameData.Enemy(group.enemyId);
                if (data == null)
                {
                    Debug.LogWarning("SWAT: unknown enemy id '" + group.enemyId + "' in mission " + mission.id);
                    continue;
                }
                for (int i = 0; i < group.count; i++)
                {
                    var spot = PickEnemySpot(level, group.spawnTag, usedEnemySpots, rng);
                    if (spot == null) break;
                    var spawn = new EnemySpawnPoint { position = spot.position, yaw = spot.yaw, tag = spot.tag, patrol = RandomPatrol(spot, level, rng) };
                    AIManager.Instance.Register(EnemyAI.Spawn(actors, data, spawn, plan.accuracyMultiplier, plan.reactionMultiplier));
                }
            }

            // Civilians.
            var usedCivilianSpots = new HashSet<CivilianSpawnPoint>();
            foreach (var group in mission.civilians)
            {
                for (int i = 0; i < group.count; i++)
                {
                    var spot = PickCivilianSpot(level, group.spawnTag, usedCivilianSpots, rng);
                    if (spot == null) break;
                    AIManager.Instance.Register(CivilianAI.Spawn(actors, group.type, spot));
                }
            }
        }

        static EnemySpawnPoint PickEnemySpot(LevelLayout level, string tag, HashSet<EnemySpawnPoint> used, System.Random rng)
        {
            var candidates = new List<EnemySpawnPoint>();
            foreach (var spot in level.enemySpawns)
                if (!used.Contains(spot) && (string.IsNullOrEmpty(tag) || spot.tag == tag)) candidates.Add(spot);
            if (candidates.Count == 0 && !string.IsNullOrEmpty(tag))
                foreach (var spot in level.enemySpawns) if (!used.Contains(spot)) candidates.Add(spot);
            if (candidates.Count == 0) return null;
            var pick = candidates[rng.Next(candidates.Count)];
            used.Add(pick);
            return pick;
        }

        static CivilianSpawnPoint PickCivilianSpot(LevelLayout level, string tag, HashSet<CivilianSpawnPoint> used, System.Random rng)
        {
            var candidates = new List<CivilianSpawnPoint>();
            foreach (var spot in level.civilianSpawns)
                if (!used.Contains(spot) && (string.IsNullOrEmpty(tag) || spot.tag == tag)) candidates.Add(spot);
            if (candidates.Count == 0 && !string.IsNullOrEmpty(tag))
                foreach (var spot in level.civilianSpawns) if (!used.Contains(spot)) candidates.Add(spot);
            if (candidates.Count == 0) return null;
            var pick = candidates[rng.Next(candidates.Count)];
            used.Add(pick);
            return pick;
        }

        // Authored patrols run in a random direction from a random start;
        // some static posts get a short patrol between nearby spawn points.
        static Vector3[] RandomPatrol(EnemySpawnPoint spot, LevelLayout level, System.Random rng)
        {
            if (spot.patrol != null && spot.patrol.Length > 1)
            {
                var route = new List<Vector3>(spot.patrol);
                if (rng.NextDouble() < 0.5) route.Reverse();
                int start = rng.Next(route.Count);
                var rotated = new Vector3[route.Count];
                for (int i = 0; i < route.Count; i++) rotated[i] = route[(start + i) % route.Count];
                return rotated;
            }
            if (spot.tag == "dummy" || rng.NextDouble() > 0.3) return null;
            int area = level.AreaAt(spot.position);
            var room = level.RoomAt(spot.position);
            var nearby = new List<Vector3>();
            foreach (var other in level.enemySpawns)
            {
                if (other == spot || level.AreaAt(other.position) != area) continue;
                float distance = Vector3.Distance(other.position, spot.position);
                if (distance > 3f && distance < 10f && (room == null || level.RoomAt(other.position) == room || distance < 6f)) nearby.Add(other.position);
            }
            if (nearby.Count == 0) return null;
            return new[] { spot.position, nearby[rng.Next(nearby.Count)] };
        }

        public static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }
    }
}
