using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Runs every suspect, civilian and squad officer from a single Update.
    // Each one "thinks" (perception and decisions) only a few times a second,
    // staggered so they don't all think on the same frame. Also delivers
    // noises, alarms, flashbang stuns and shouts, and opens doors for the AI.
    public class AIManager : MonoBehaviour
    {
        public static AIManager Instance { get; private set; }

        public readonly List<EnemyAI> Enemies = new List<EnemyAI>();
        public readonly List<CivilianAI> Civilians = new List<CivilianAI>();
        public readonly List<SquadAI> Officers = new List<SquadAI>();
        public readonly List<ICombatTarget> PoliceTargets = new List<ICombatTarget>();

        readonly List<DoorController> doors = new List<DoorController>();
        LevelLayout level;
        float nextDoorCheck;

        void Awake()
        {
            Instance = this;
        }

        public void Begin(LevelLayout layout)
        {
            level = layout;
            Enemies.Clear();
            Civilians.Clear();
            Officers.Clear();
            PoliceTargets.Clear();
            doors.Clear();
            doors.AddRange(layout.doors);
            foreach (var door in doors) door.RefreshNavMeshCarving();
            CoverPoint.Build(layout.coverPoints);
        }

        public void Clear()
        {
            level = null;
            Enemies.Clear();
            Civilians.Clear();
            Officers.Clear();
            PoliceTargets.Clear();
            doors.Clear();
        }

        public void RegisterPlayer(PlayerController player)
        {
            PoliceTargets.Insert(0, player);
        }

        public void Register(EnemyAI enemy)
        {
            enemy.NextThink = Time.time + Random.value * 0.3f;
            enemy.Area = level.AreaAt(enemy.Position);
            Enemies.Add(enemy);
        }

        public void Register(CivilianAI civilian)
        {
            civilian.NextThink = Time.time + Random.value * 0.3f;
            civilian.Area = level.AreaAt(civilian.Position);
            Civilians.Add(civilian);
        }

        public void Register(SquadAI officer)
        {
            officer.NextThink = Time.time + Random.value * 0.2f;
            Officers.Add(officer);
            PoliceTargets.Add(officer);
        }

        void Update()
        {
            var game = GameManager.Instance;
            if (game == null || !game.IsPlaying || level == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float now = Time.time;
            float interval = QualityManager.Current.aiThinkInterval;

            for (int i = 0; i < Enemies.Count; i++)
            {
                var enemy = Enemies[i];
                if (enemy.State == EnemyState.Dead || enemy.Escaped) continue;
                enemy.FrameUpdate(dt);
                if (now < enemy.NextThink) continue;
                enemy.NextThink = now + interval;
                enemy.Think();
            }

            for (int i = 0; i < Officers.Count; i++)
            {
                var officer = Officers[i];
                officer.FrameUpdate(dt);
                if (now < officer.NextThink) continue;
                officer.NextThink = now + interval * officer.ThinkScale;
                officer.Think();
            }

            for (int i = 0; i < Civilians.Count; i++)
            {
                var civilian = Civilians[i];
                if (!civilian.IsAlive || civilian.IsEvacuated) continue;
                civilian.FrameUpdate(dt);
                if (now < civilian.NextThink) continue;
                civilian.NextThink = now + interval * 1.5f;
                civilian.Think();
            }

            if (now >= nextDoorCheck)
            {
                nextDoorCheck = now + 0.25f;
                OpenDoorsForAI();
            }
        }

        // Unlocked doors open when anyone controlled by the AI walks up to them.
        void OpenDoorsForAI()
        {
            foreach (var door in doors)
            {
                if (door.State != DoorState.Closed) continue;
                Vector3 doorPosition = door.transform.position;
                foreach (var enemy in Enemies)
                {
                    if (enemy.Down || enemy.State == EnemyState.Surrendering || enemy.State == EnemyState.Stunned || enemy.State == EnemyState.Hiding) continue;
                    if (FlatDistance(enemy.Position, doorPosition) < 1.3f) { door.Open(enemy.Position); break; }
                }
                if (door.State != DoorState.Closed) continue;
                foreach (var officer in Officers)
                {
                    if (!officer.IsAlive || !officer.IsMoving || officer.HoldsDoorsClosed) continue;
                    // Officers stacking on a door wait for the entry order instead of walking it open,
                    // unless they are on the far side and have to come through it to reach the stack.
                    if (officer.StackDoor == door && Vector3.Dot(officer.Position - doorPosition, door.transform.forward) * officer.StackSide > 0f) continue;
                    if (FlatDistance(officer.Position, doorPosition) < 1.3f)
                    {
                        // Walking through to reach the stack isn't an entry signal.
                        if (officer.StackDoor == door) SquadCommandManager.Instance.NoteDoorAlreadyOpen(door);
                        door.Open(officer.Position);
                        break;
                    }
                }
                if (door.State != DoorState.Closed) continue;
                foreach (var civilian in Civilians)
                {
                    if (!civilian.IsAlive || civilian.IsEvacuated || civilian.State == CivilianState.Hiding) continue;
                    if (FlatDistance(civilian.Position, doorPosition) < 1.3f) { door.Open(civilian.Position); break; }
                }
            }
        }

        public void HearNoise(Vector3 position, float radius, NoiseKind kind)
        {
            if (level == null) return;
            int area = level.AreaAt(position);
            float sqrRadius = radius * radius;
            foreach (var enemy in Enemies)
                if (enemy.Area == area && (enemy.Position - position).sqrMagnitude < sqrRadius) enemy.HearNoise(position, kind);

            if (kind != NoiseKind.Gunshot && kind != NoiseKind.EnemyGunshot && kind != NoiseKind.Explosion) return;
            foreach (var civilian in Civilians)
                if (civilian.Area == area && (civilian.Position - position).sqrMagnitude < sqrRadius * 0.7f) civilian.Panic(position);
        }

        // A suspect who spots police calls friends nearby.
        public void Callout(EnemyAI caller, Vector3 target, float radius)
        {
            foreach (var enemy in Enemies)
                if (enemy != caller && enemy.Area == caller.Area && (enemy.Position - caller.Position).sqrMagnitude < radius * radius)
                    enemy.HearNoise(target, NoiseKind.Callout);
        }

        public void OnAlarm(Vector3 source)
        {
            foreach (var enemy in Enemies) enemy.OnAlarm(source);
            foreach (var civilian in Civilians) civilian.HearShout();
        }

        // "Police! Show me your hands!" Suspects in sight may surrender; panicking civilians get down.
        public void Shout(Vector3 chest, Vector3 position, bool byPlayer)
        {
            if (byPlayer) UIManager.ShowShout();
            foreach (var enemy in Enemies)
            {
                if (enemy.IsNeutralized || (enemy.Position - position).sqrMagnitude > 10f * 10f) continue;
                if (!Physics.Linecast(chest, enemy.Head, Layers.WorldMask, QueryTriggerInteraction.Ignore)) enemy.HearShout(position, byPlayer);
            }
            foreach (var civilian in Civilians)
                if ((civilian.Position - position).sqrMagnitude < 10f * 10f) civilian.HearShout();
        }

        public void Stun(Vector3 center, float radius, float duration, bool hurtsPolice)
        {
            float sqrRadius = radius * radius;
            foreach (var enemy in Enemies)
            {
                float sqr = (enemy.Head - center).sqrMagnitude;
                if (sqr < sqrRadius && !Physics.Linecast(center, enemy.Head, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                    enemy.Stun(duration * (1f - 0.5f * Mathf.Sqrt(sqr) / radius));
            }
            foreach (var civilian in Civilians)
            {
                Vector3 head = civilian.Position + Vector3.up * 1.5f;
                if ((head - center).sqrMagnitude < sqrRadius && !Physics.Linecast(center, head, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                    civilian.Stun(duration * 0.6f);
            }
            foreach (var officer in Officers)
            {
                if ((officer.Position - center).sqrMagnitude < sqrRadius * 0.5f && !Physics.Linecast(center, officer.Position + Vector3.up * 1.5f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                    officer.Dazzle(duration * 0.4f);
            }
            // Game-mode bots on either team.
            if (VersusMatch.Active)
                foreach (var bot in VersusMatch.Instance.Bots)
                {
                    Vector3 head = bot.Position + Vector3.up * 1.5f;
                    float sqr = (head - center).sqrMagnitude;
                    if (bot.IsAlive && sqr < sqrRadius && !Physics.Linecast(center, head, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                        bot.Stun(duration * 0.6f * (1f - 0.5f * Mathf.Sqrt(sqr) / radius));
                }
        }

        public bool AnyThreatNear(Vector3 position, float radius)
        {
            foreach (var enemy in Enemies)
                if (enemy.IsArmedThreat && (enemy.Position - position).sqrMagnitude < radius * radius) return true;
            return false;
        }

        public bool AnyThreatIn(RoomController room)
        {
            foreach (var enemy in Enemies)
                if (enemy.IsArmedThreat && room.Contains(enemy.Position)) return true;
            return false;
        }

        public bool AnySuspectIn(RoomController room)
        {
            foreach (var enemy in Enemies)
                if (enemy.NeedsSecuring && room.Contains(enemy.Position)) return true;
            return false;
        }

        // Police rounds passing within a metre and a bit of a suspect (who wasn't hit) suppress them.
        public void Suppress(Vector3 from, Vector3 to, Object victim)
        {
            Vector3 line = to - from;
            float length = line.magnitude;
            if (length < 0.5f) return;
            line /= length;
            foreach (var enemy in Enemies)
            {
                if (enemy == null || (victim != null && enemy.gameObject == ((Component)victim).gameObject)) continue;
                Vector3 offset = enemy.Head - from;
                float along = Vector3.Dot(offset, line);
                if (along < 1f || along > length + 1f) continue;
                if ((offset - line * along).sqrMagnitude < 1.7f) enemy.Suppress(from);
            }
        }

        public int PoliceNear(Vector3 position, float radius)
        {
            int count = 0;
            foreach (var target in PoliceTargets)
                if (target.IsAlive && (target.Position - position).sqrMagnitude < radius * radius) count++;
            return count;
        }

        public DoorController FindDoor(Vector3 position, float range, System.Predicate<DoorController> match)
        {
            DoorController best = null;
            float bestDistance = range;
            foreach (var door in doors)
            {
                if (!match(door)) continue;
                float distance = FlatDistance(door.transform.position, position);
                if (distance < bestDistance)
                {
                    best = door;
                    bestDistance = distance;
                }
            }
            return best;
        }

        public CivilianAI NearestCivilianNeedingHelp(Vector3 position, float radius, int area)
        {
            CivilianAI best = null;
            float bestDistance = radius;
            foreach (var civilian in Civilians)
            {
                if (!civilian.NeedsHelp || civilian.Area != area || !TacticalIntel.Instance.IsDiscovered(civilian)) continue;
                float distance = Vector3.Distance(civilian.Position, position);
                if (distance < bestDistance)
                {
                    best = civilian;
                    bestDistance = distance;
                }
            }
            return best;
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
