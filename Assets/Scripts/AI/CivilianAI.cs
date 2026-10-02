using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    public enum CivilianState { Idle, Wander, Panic, Hiding, Stunned, Evacuating, Rescued, Dead }

    // A civilian caught up in the raid. Calm civilians idle and wander. Gunfire
    // makes them panic and run, and they hide (crouch) when suspects are close.
    // Press E next to one to rescue them; they then run to the SWAT van.
    public class CivilianAI : MonoBehaviour, IDamageable, IInteractable
    {
        static readonly Color[] Shirts =
        {
            new Color(0.92f, 0.92f, 0.9f), new Color(0.55f, 0.75f, 0.95f), new Color(0.95f, 0.85f, 0.4f),
            new Color(0.95f, 0.6f, 0.7f), new Color(0.6f, 0.85f, 0.6f),
        };
        static readonly Color CivilianRing = new Color(1f, 0.85f, 0.2f);
        static readonly Color RescuedRing = new Color(0.3f, 1f, 0.5f);

        public CivilianState State { get; private set; }
        public bool IsAlive { get { return State != CivilianState.Dead; } }
        public bool NeedsRescue { get { return State != CivilianState.Dead && State != CivilianState.Evacuating && State != CivilianState.Rescued; } }
        public Team Team { get { return Team.Civilian; } }
        public float NextThink { get; set; }

        public string Prompt { get { return "[E] Rescue civilian"; } }
        public Vector3 InteractPosition { get { return transform.position + Vector3.up; } }

        AgentMover mover;
        CharacterParts parts;
        Collider bodyCollider;
        Vector3 home;
        Vector3 extraction;
        float stateStart, nextWander, calmAt, stunUntil;

        float StateTime { get { return Time.time - stateStart; } }

        public static CivilianAI Spawn(Transform parent, CivilianSpawn spawn, Vector3 extractionPoint)
        {
            var go = new GameObject("Civilian");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(spawn.position, Quaternion.Euler(0f, spawn.yaw, 0f));

            var collider = go.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.9f, 0f);
            collider.height = 1.8f;
            collider.radius = 0.32f;
            go.AddComponent<NavMeshAgent>();

            var civilian = go.AddComponent<CivilianAI>();
            civilian.bodyCollider = collider;
            civilian.mover = go.AddComponent<AgentMover>();
            civilian.mover.Init(1.3f, 4.2f);
            civilian.home = spawn.position;
            civilian.extraction = extractionPoint;
            var hair = new[] { new Color(0.1f, 0.07f, 0.05f), new Color(0.4f, 0.25f, 0.12f), new Color(0.85f, 0.7f, 0.4f), new Color(0.6f, 0.6f, 0.6f) };
            civilian.parts = CharacterFactory.Build(go.transform, Shirts[Random.Range(0, Shirts.Length)], new Color(0.25f, 0.3f, 0.45f),
                CharacterFactory.RandomSkin(), hair[Random.Range(0, hair.Length)], CivilianRing, false, false);
            civilian.SetState(CivilianState.Idle);
            civilian.nextWander = Time.time + Random.Range(2f, 6f);
            Shapes.SetLayer(go, Layers.Characters);
            return civilian;
        }

        void SetState(CivilianState next)
        {
            State = next;
            stateStart = Time.time;
            bool crouched = next == CivilianState.Hiding || next == CivilianState.Stunned;
            parts.model.localScale = new Vector3(1f, crouched ? 0.6f : 1f, 1f);
            if (next == CivilianState.Hiding) parts.SetArms(new Vector3(-160f, 0f, 30f), new Vector3(-160f, 0f, -30f)); // hands over head
            else if (next == CivilianState.Panic) parts.SetArms(new Vector3(-120f, 0f, -10f), new Vector3(-120f, 0f, 10f));
            else parts.PoseRelaxed();
        }

        public void FrameUpdate(float dt)
        {
            if (State == CivilianState.Dead || State == CivilianState.Rescued) return;
            float bob = mover.IsMoving ? Mathf.Abs(Mathf.Sin(Time.time * 11f + home.x)) * 0.05f : 0f;
            parts.model.localPosition = new Vector3(0f, bob, 0f);
        }

        public void Think()
        {
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
                    if (mover.HasArrived) SetState(CivilianState.Idle);
                    break;

                case CivilianState.Panic:
                    if (mover.HasArrived || StateTime > 6f) TryHide();
                    break;

                case CivilianState.Hiding:
                    if (Time.time > calmAt && !AIManager.Instance.AnyThreatNear(transform.position, 8f)) SetState(CivilianState.Idle);
                    break;

                case CivilianState.Stunned:
                    if (Time.time > stunUntil) TryHide();
                    break;

                case CivilianState.Evacuating:
                    if (mover.HasArrived)
                    {
                        SetState(CivilianState.Rescued);
                        mover.Disable();
                        gameObject.SetActive(false);
                    }
                    break;
            }
        }

        void TryHide()
        {
            mover.Stop();
            SetState(CivilianState.Hiding);
            calmAt = Time.time + 12f;
        }

        // Gunfire or explosions nearby: run away from the noise.
        public void Panic(Vector3 source)
        {
            if (State == CivilianState.Dead || State == CivilianState.Evacuating || State == CivilianState.Rescued || State == CivilianState.Stunned) return;
            calmAt = Time.time + 12f;
            if (State == CivilianState.Panic || State == CivilianState.Hiding) return;

            Vector3 away = transform.position - source;
            away.y = 0f;
            Vector3 target = transform.position + (away.sqrMagnitude > 0.01f ? away.normalized : transform.forward) * 6f;
            Vector3 point;
            if (mover.RandomPointNear(target, 2.5f, out point))
            {
                SetState(CivilianState.Panic);
                mover.MoveTo(point, true);
            }
            else
            {
                TryHide();
            }
        }

        // "Get down!" when the officer shouts: panicking civilians drop where they are.
        public void HearShout()
        {
            if (State == CivilianState.Panic || State == CivilianState.Wander) TryHide();
        }

        public void Stun(float duration)
        {
            if (!NeedsRescue) return;
            mover.Stop();
            stunUntil = Time.time + duration;
            SetState(CivilianState.Stunned);
        }

        public bool CanInteract(PlayerController player)
        {
            return NeedsRescue;
        }

        public void Interact(PlayerController player)
        {
            if (!NeedsRescue) return;
            SetState(CivilianState.Evacuating);
            parts.SetRingColor(RescuedRing);
            mover.MoveTo(extraction + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f)), true);
            AudioManager.Play(Sound.Rescue, transform.position, 0.8f);
            MissionManager.Instance.OnCivilianRescued();
        }

        // Only the police can hurt civilians (suspects aren't aiming at them).
        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive || info.attacker == Team.Suspect || State == CivilianState.Rescued) return;
            bool wasRescued = State == CivilianState.Evacuating;
            SetState(CivilianState.Dead);
            mover.Disable();
            bodyCollider.enabled = false;
            parts.Fall();
            MissionManager.Instance.OnCivilianKilled(wasRescued);
        }
    }
}
