using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    public enum CivilianState { Idle, Wander, Panic, Flee, Hiding, Following, Waiting, Injured, Captive, Stunned, Evacuated, Dead }

    // A civilian caught up in the incident. Calm civilians idle and wander;
    // gunfire makes them panic, flee and hide. Officers can tell them to
    // follow or wait, treat the injured and free hostages, then escort them
    // to the safe zone. Suspects never target civilians; police fire can hurt them.
    public class CivilianAI : MonoBehaviour, IDamageable, IInteractable
    {
        static readonly Color CivilianRing = new Color(1f, 0.85f, 0.2f);
        static readonly Color RescuedRing = new Color(0.3f, 1f, 0.5f);

        public CivilianType Type { get; private set; }
        public CivilianState State { get; private set; }
        public bool IsAlive { get { return State != CivilianState.Dead; } }
        public bool IsEvacuated { get { return State == CivilianState.Evacuated; } }
        public bool NeedsHelp { get { return IsAlive && !IsEvacuated && State != CivilianState.Following; } }
        public bool WasInjured { get; private set; }
        public bool WasTreated { get; private set; }
        public bool UnderPoliceControl { get; private set; }
        public Team Team { get { return Team.Civilian; } }
        public float NextThink { get; set; }
        public int Area { get; set; }
        public Transform Leader { get; private set; }
        public Vector3 Position { get { return transform.position; } }

        AgentMover mover;
        CharacterParts parts;
        ProceduralAnimator animator;
        Collider bodyCollider;
        GameObject helpMarker;
        Vector3 home;
        float health = 100f, stateStart, nextWander, calmAt, stunUntil;
        CivilianState stateBeforeStun;

        public string Prompt
        {
            get
            {
                switch (State)
                {
                    case CivilianState.Injured: return "[E] Treat Injuries (hold)";
                    case CivilianState.Captive: return "[E] Rescue Hostage (hold)";
                    case CivilianState.Following: return "[E] Tell Them to Wait Here";
                    default: return "[E] Rescue Civilian (\"Follow me!\")";
                }
            }
        }

        public Vector3 InteractPosition { get { return transform.position + Vector3.up; } }

        public static CivilianAI Spawn(Transform parent, CivilianType type, CivilianSpawnPoint spawn)
        {
            var go = new GameObject("Civilian (" + type + ")");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(spawn.position, Quaternion.Euler(0f, spawn.yaw, 0f));

            var collider = go.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.9f, 0f);
            collider.height = 1.8f;
            collider.radius = 0.32f;
            go.AddComponent<NavMeshAgent>();

            var civilian = go.AddComponent<CivilianAI>();
            civilian.Type = type;
            civilian.bodyCollider = collider;
            civilian.mover = go.AddComponent<AgentMover>();
            civilian.mover.Init(1.4f, 4.2f, 0.28f);
            civilian.home = spawn.position;

            var look = CharacterFactory.CivilianLook(type, CivilianRing);
            civilian.parts = CharacterFactory.Build(go.transform, look);
            civilian.animator = new ProceduralAnimator(civilian.parts);
            civilian.helpMarker = new GameObject("Help Marker");
            civilian.helpMarker.transform.SetParent(civilian.parts.visuals.transform, false);
            Shapes.Box("Cross H", civilian.helpMarker.transform, new Vector3(0f, 2.3f, 0f), new Vector3(0.4f, 0.12f, 0.12f), Color.white, false, 2f);
            Shapes.Box("Cross V", civilian.helpMarker.transform, new Vector3(0f, 2.3f, 0f), new Vector3(0.12f, 0.4f, 0.12f), Color.white, false, 2f);
            civilian.helpMarker.SetActive(false);

            switch (type)
            {
                case CivilianType.Injured:
                    civilian.WasInjured = true;
                    civilian.health = 40f;
                    civilian.SetState(CivilianState.Injured);
                    break;
                case CivilianType.Hostage: civilian.SetState(CivilianState.Captive); break;
                case CivilianType.Hiding: civilian.SetState(CivilianState.Hiding); break;
                default: civilian.SetState(CivilianState.Idle); break;
            }
            civilian.nextWander = Time.time + Random.Range(2f, 6f);
            Shapes.SetLayer(go, Layers.Characters);
            return civilian;
        }

        void SetState(CivilianState next)
        {
            State = next;
            stateStart = Time.time;
            bool crouched = next == CivilianState.Hiding || next == CivilianState.Stunned || next == CivilianState.Injured || next == CivilianState.Captive;
            animator.SetCrouch(crouched);
            switch (next)
            {
                case CivilianState.Hiding:
                case CivilianState.Stunned: animator.SetPose(Pose.Cower); break;
                case CivilianState.Panic:
                case CivilianState.Flee: animator.SetPose(Pose.HandsUp); break;
                case CivilianState.Captive: animator.SetPose(Pose.Cuffed); break;
                case CivilianState.Injured: animator.SetPose(Pose.Treating); break;
                default: animator.SetPose(Pose.Relaxed); break;
            }
            helpMarker.SetActive(next == CivilianState.Injured || next == CivilianState.Captive);
        }

        public void FrameUpdate(float dt)
        {
            if (State == CivilianState.Dead || State == CivilianState.Evacuated) return;
            animator.Tick(dt, mover.Speed, mover.IsRunning);
        }

        public void Think()
        {
            mover.TrackProgress();
            switch (State)
            {
                case CivilianState.Idle:
                    if (Time.time > nextWander)
                    {
                        Vector3 point;
                        if (mover.RandomPointNear(home, 2.5f, out point))
                        {
                            mover.MoveTo(point, false);
                            SetState(CivilianState.Wander);
                        }
                        nextWander = Time.time + Random.Range(4f, 9f);
                    }
                    break;
                case CivilianState.Wander:
                    if (mover.HasArrived || mover.IsStuck) SetState(CivilianState.Idle);
                    break;
                case CivilianState.Panic:
                    if (StateTime > 0.8f) SetState(CivilianState.Flee);
                    break;
                case CivilianState.Flee:
                    if (mover.HasArrived || StateTime > 6f || mover.IsStuck) Hide();
                    break;
                case CivilianState.Hiding:
                    if (Type != CivilianType.Hiding && Time.time > calmAt && !AIManager.Instance.AnyThreatNear(transform.position, 8f)) SetState(CivilianState.Idle);
                    break;
                case CivilianState.Stunned:
                    if (Time.time > stunUntil) SetState(stateBeforeStun == CivilianState.Following ? CivilianState.Following : CivilianState.Hiding);
                    break;
                case CivilianState.Following:
                    UpdateFollow();
                    break;
            }
            if (UnderPoliceControl && IsAlive && !IsEvacuated && GameManager.Instance.Level.InSafeZone(transform.position)) Evacuate();
        }

        float StateTime { get { return Time.time - stateStart; } }

        void UpdateFollow()
        {
            if (Leader == null || !Leader.gameObject.activeInHierarchy)
            {
                Wait();
                return;
            }
            float distance = Vector3.Distance(Leader.position, transform.position);
            if (distance > 30f) return; // left behind on another floor; wait for the leader to return
            if (distance > 2.4f) mover.MoveTo(Leader.position - Leader.forward * 1.6f, distance > 5f);
            else mover.Stop();
        }

        void Hide()
        {
            mover.Stop();
            SetState(CivilianState.Hiding);
            calmAt = Time.time + 12f;
        }

        void Evacuate()
        {
            SetState(CivilianState.Evacuated);
            mover.Disable();
            gameObject.SetActive(false);
            AudioManager.Play(Sound.Rescue, transform.position, 0.8f);
            MissionManager.Instance.OnCivilianEvacuated(this);
        }

        // Gunfire or explosions nearby: panic and run away from the noise.
        public void Panic(Vector3 source)
        {
            if (State != CivilianState.Idle && State != CivilianState.Wander && State != CivilianState.Waiting) { calmAt = Time.time + 12f; return; }
            calmAt = Time.time + 12f;
            Vector3 away = transform.position - source;
            away.y = 0f;
            Vector3 target = transform.position + (away.sqrMagnitude > 0.01f ? away.normalized : transform.forward) * 6f;
            Vector3 point;
            if (Random.value < 0.4f) AudioManager.Play(Sound.Gasp, transform.position, 0.5f, Random.Range(0.9f, 1.2f));
            if (mover.RandomPointNear(target, 2.5f, out point))
            {
                SetState(CivilianState.Panic);
                mover.MoveTo(point, true);
            }
            else Hide();
        }

        // "Get down!" when an officer shouts: panicking civilians drop where they are.
        public void HearShout()
        {
            if (State == CivilianState.Panic || State == CivilianState.Flee || State == CivilianState.Wander) Hide();
        }

        public void Stun(float duration)
        {
            if (!IsAlive || IsEvacuated || State == CivilianState.Stunned) return;
            stateBeforeStun = State;
            mover.Stop();
            stunUntil = Time.time + duration;
            SetState(CivilianState.Stunned);
        }

        // ---- Orders from officers ----

        public void FollowLeader(Transform leader)
        {
            if (State == CivilianState.Injured || State == CivilianState.Captive || !IsAlive || IsEvacuated) return;
            bool first = !UnderPoliceControl;
            UnderPoliceControl = true;
            Leader = leader;
            SetState(CivilianState.Following);
            parts.SetRingColor(RescuedRing);
            if (first) MissionManager.Instance.OnCivilianSecured(this);
        }

        public void Wait()
        {
            Leader = null;
            mover.Stop();
            SetState(CivilianState.Waiting);
        }

        public void Treat()
        {
            if (State != CivilianState.Injured) return;
            WasTreated = true;
            health = Mathf.Max(health, 70f);
            AudioManager.Play(Sound.Medkit, transform.position, 0.6f);
            MissionManager.Instance.OnCivilianTreated(this);
            SetState(CivilianState.Waiting);
        }

        public void Free()
        {
            if (State != CivilianState.Captive) return;
            AudioManager.Play(Sound.Click, transform.position, 0.6f);
            SetState(CivilianState.Waiting);
        }

        public float InteractDuration(PlayerController player)
        {
            if (State == CivilianState.Injured) return player.Officer.role == OfficerRole.Medic ? 1.5f : 3f;
            if (State == CivilianState.Captive) return 1.5f;
            return 0f;
        }

        public bool CanInteract(PlayerController player)
        {
            return IsAlive && !IsEvacuated && State != CivilianState.Stunned;
        }

        public void Interact(PlayerController player)
        {
            switch (State)
            {
                case CivilianState.Injured:
                    Treat();
                    UIManager.Notify("Injuries treated. They can walk now.");
                    break;
                case CivilianState.Captive:
                    Free();
                    UIManager.Notify("Hostage freed. Tell them to follow you.");
                    break;
                case CivilianState.Following:
                    Wait();
                    UIManager.Notify("\"Stay here, we'll come back for you.\"");
                    break;
                default:
                    FollowLeader(player.transform);
                    UIManager.Notify("\"Follow me, stay close!\"");
                    break;
            }
            AudioManager.RadioChirp(1.2f);
        }

        // Only police fire can hurt civilians (suspects aren't aiming at them).
        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive || IsEvacuated || info.attacker != Team.Police && info.attacker != Team.Environment) return;
            if (info.lessLethal)
            {
                if (!WasInjured) MissionManager.Instance.OnCivilianInjured(this);
                WasInjured = true;
                Stun(info.stun > 0f ? info.stun : 2f);
                return;
            }
            health -= info.amount;
            if (!WasInjured) MissionManager.Instance.OnCivilianInjured(this);
            WasInjured = true;
            if (health > 0f)
            {
                mover.Stop();
                Leader = null;
                SetState(CivilianState.Injured);
                return;
            }
            SetState(CivilianState.Dead);
            mover.Disable();
            bodyCollider.enabled = false;
            animator.SetDown(true, info.direction);
            helpMarker.SetActive(false);
            MissionManager.Instance.OnCivilianKilled(this);
        }

        public void SetSeen(bool visible)
        {
            parts.SetVisible(visible || UnderPoliceControl || State == CivilianState.Dead);
        }

        public void TeleportTo(Vector3 position, int area)
        {
            mover.Warp(position);
            Area = area;
        }
    }
}
