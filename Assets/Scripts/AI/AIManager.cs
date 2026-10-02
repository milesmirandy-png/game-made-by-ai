using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    // Runs every suspect and civilian from a single Update. Each one "thinks"
    // (perception and decisions) only a few times a second, staggered so they
    // don't all think on the same frame. Also delivers noises, flashbang
    // stuns and shouts, finds cover, and opens doors for the AI.
    public class AIManager : MonoBehaviour
    {
        public static AIManager Instance { get; private set; }

        public readonly List<EnemyAI> Enemies = new List<EnemyAI>();
        public readonly List<CivilianAI> Civilians = new List<CivilianAI>();

        readonly List<Vector3> coverPoints = new List<Vector3>();
        readonly List<DoorController> doors = new List<DoorController>();
        float nextDoorCheck;

        void Awake()
        {
            Instance = this;
        }

        public void Begin(LevelLayout level)
        {
            Enemies.Clear();
            Civilians.Clear();
            doors.Clear();
            doors.AddRange(level.doors);
            foreach (var door in doors) door.RefreshNavMeshCarving();
            coverPoints.Clear();
            // Keep only cover spots that are actually on the NavMesh.
            foreach (var point in level.coverPoints)
            {
                NavMeshHit hit;
                if (NavMesh.SamplePosition(point, out hit, 0.6f, NavMesh.AllAreas)) coverPoints.Add(hit.position);
            }
        }

        public void Register(EnemyAI enemy)
        {
            enemy.NextThink = Time.time + Random.value * 0.3f;
            Enemies.Add(enemy);
        }

        public void Register(CivilianAI civilian)
        {
            civilian.NextThink = Time.time + Random.value * 0.3f;
            Civilians.Add(civilian);
        }

        void Update()
        {
            var game = GameManager.Instance;
            if (game == null || !game.IsPlaying) return;
            float dt = Time.deltaTime;
            float now = Time.time;
            float interval = QualityManager.Current.aiThinkInterval;
            var player = game.Player;

            for (int i = 0; i < Enemies.Count; i++)
            {
                var enemy = Enemies[i];
                if (enemy.State == EnemyState.Dead) continue;
                enemy.FrameUpdate(dt, player);
                if (now >= enemy.NextThink)
                {
                    enemy.NextThink = now + interval;
                    enemy.Think(player);
                }
            }

            for (int i = 0; i < Civilians.Count; i++)
            {
                var civilian = Civilians[i];
                if (civilian.State == CivilianState.Dead || civilian.State == CivilianState.Rescued) continue;
                civilian.FrameUpdate(dt);
                if (now >= civilian.NextThink)
                {
                    civilian.NextThink = now + interval * 1.5f;
                    civilian.Think();
                }
            }

            if (now >= nextDoorCheck)
            {
                nextDoorCheck = now + 0.25f;
                OpenDoorsForAI();
            }
        }

        // Unlocked doors open when a suspect or civilian walks up to them.
        void OpenDoorsForAI()
        {
            foreach (var door in doors)
            {
                if (door.State != DoorState.Closed) continue;
                Vector3 doorPosition = door.transform.position;
                foreach (var enemy in Enemies)
                {
                    if (enemy.IsThreat && enemy.State != EnemyState.Stunned && FlatDistance(enemy.Position, doorPosition) < 1.3f)
                    {
                        door.Open(enemy.Position);
                        break;
                    }
                }
                if (door.State != DoorState.Closed) continue;
                foreach (var civilian in Civilians)
                {
                    if (civilian.IsAlive && civilian.State != CivilianState.Rescued && FlatDistance(civilian.transform.position, doorPosition) < 1.3f)
                    {
                        door.Open(civilian.transform.position);
                        break;
                    }
                }
            }
        }

        public void HearNoise(Vector3 position, float radius, NoiseKind kind)
        {
            float sqrRadius = radius * radius;
            foreach (var enemy in Enemies)
                if ((enemy.Position - position).sqrMagnitude < sqrRadius) enemy.HearNoise(position, kind);

            if (kind != NoiseKind.Gunshot && kind != NoiseKind.EnemyGunshot && kind != NoiseKind.Explosion) return;
            foreach (var civilian in Civilians)
                if ((civilian.transform.position - position).sqrMagnitude < sqrRadius * 0.7f) civilian.Panic(position);
        }

        // A suspect who spots the officer shouts to friends nearby.
        public void Callout(EnemyAI caller, Vector3 target)
        {
            foreach (var enemy in Enemies)
                if (enemy != caller && (enemy.Position - caller.Position).sqrMagnitude < 12f * 12f) enemy.HearNoise(target, NoiseKind.Callout);
        }

        public void Shout(PlayerController player)
        {
            UIManager.ShowShout();
            Vector3 from = player.ChestPosition;
            foreach (var enemy in Enemies)
            {
                if (!enemy.IsThreat || (enemy.Position - player.Position).sqrMagnitude > 10f * 10f) continue;
                if (!Physics.Linecast(from, enemy.Position + Vector3.up * 1.5f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) enemy.HearShout(player);
            }
            foreach (var civilian in Civilians)
                if ((civilian.transform.position - player.Position).sqrMagnitude < 10f * 10f) civilian.HearShout();
        }

        public void Stun(Vector3 center, float radius, float duration)
        {
            float sqrRadius = radius * radius;
            foreach (var enemy in Enemies)
            {
                Vector3 head = enemy.Position + Vector3.up * 1.5f;
                float sqr = (head - center).sqrMagnitude;
                if (sqr < sqrRadius && !Physics.Linecast(center, head, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                    enemy.Stun(duration * (1f - 0.5f * Mathf.Sqrt(sqr) / radius));
            }
            foreach (var civilian in Civilians)
            {
                Vector3 head = civilian.transform.position + Vector3.up * 1.5f;
                if ((head - center).sqrMagnitude < sqrRadius && !Physics.Linecast(center, head, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                    civilian.Stun(duration * 0.6f);
            }
        }

        // Nearest cover spot (within reach) that the threat can't see.
        public bool FindCover(Vector3 from, Vector3 threat, float maxDistance, out Vector3 cover)
        {
            cover = from;
            float best = maxDistance * maxDistance;
            bool found = false;
            Vector3 threatEye = threat + Vector3.up * 1.4f;
            int checks = 0;
            foreach (var point in coverPoints)
            {
                float sqr = (point - from).sqrMagnitude;
                if (sqr >= best || sqr < 1f) continue;
                if (++checks > 12) break; // keep raycasts bounded
                if (!Physics.Linecast(point + Vector3.up * 1f, threatEye, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                best = sqr;
                cover = point;
                found = true;
            }
            return found;
        }

        public bool AnyThreatNear(Vector3 position, float radius)
        {
            foreach (var enemy in Enemies)
                if (enemy.IsThreat && (enemy.Position - position).sqrMagnitude < radius * radius) return true;
            return false;
        }

        public bool AnyThreatIn(Bounds area)
        {
            foreach (var enemy in Enemies)
            {
                if (!enemy.IsThreat) continue;
                Vector3 p = enemy.Position;
                if (area.Contains(new Vector3(p.x, area.center.y, p.z))) return true;
            }
            return false;
        }

        public DoorController FindBreachableDoor(Vector3 position, float range)
        {
            DoorController best = null;
            float bestDistance = range;
            foreach (var door in doors)
            {
                if (door.State != DoorState.Locked || !door.Breachable || door.ChargePlaced) continue;
                float distance = FlatDistance(door.transform.position, position);
                if (distance < bestDistance)
                {
                    best = door;
                    bestDistance = distance;
                }
            }
            return best;
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
