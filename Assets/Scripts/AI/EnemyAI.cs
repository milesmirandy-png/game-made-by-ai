using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    public enum EnemyState
    {
        Idle, Patrol, Suspicious, Investigating, Alert, Chasing, Attacking, TakingCover, Searching,
        Stunned, Surrendered, Arrested, Dead,
    }

    // A suspect's brain: a small finite state machine.
    //
    //   Idle/Patrol --hears something--> Suspicious --> Investigating --> Searching --> back to post
    //        |                                                                ^
    //        +--sees officer--> Alert --> Attacking <--> Chasing -------------+
    //                                        |
    //                                        +--> TakingCover --> Chasing
    //
    // Flashbangs put suspects in Stunned. Shouting (F) can make them Surrendered,
    // and pressing E on a surrendered suspect arrests them.
    // Think() runs a few times a second; FrameUpdate() only does cheap turning and shooting.
    public class EnemyAI : MonoBehaviour, IInteractable
    {
        public EnemyState State { get; private set; }
        public bool IsNeutralized { get { return State == EnemyState.Dead || State == EnemyState.Arrested; } }
        public bool IsThreat { get { return !IsNeutralized && State != EnemyState.Surrendered; } }
        public Vector3 Position { get { return transform.position; } }
        public float NextThink { get; set; }

        public string Prompt { get { return "[E] Arrest suspect"; } }
        public Vector3 InteractPosition { get { return transform.position + Vector3.up; } }

        EnemyData data;
        AgentMover mover;
        EnemyHealth health;
        EnemyWeapon weapon;
        EnemyController body;
        Collider bodyCollider;

        Vector3 post;
        float postYaw;
        Vector3[] patrol;
        int patrolIndex;

        float stateStart, idleUntil, reactionDone, lastSeenTime = -100f, stunUntil, coverWaitUntil, nextCoverCheck, searchDuration, nextShoutCheck;
        bool seesPlayer, everAlerted;
        Vector3 lastKnownPlayer, noisePosition, searchCenter, coverPoint;

        float StateTime { get { return Time.time - stateStart; } }
        bool IsCalm { get { return State == EnemyState.Idle || State == EnemyState.Patrol || State == EnemyState.Suspicious || State == EnemyState.Investigating || State == EnemyState.Searching; } }

        public static EnemyAI Spawn(Transform parent, EnemySpawn spawn)
        {
            var data = GameData.Enemy(spawn.profile);
            var go = new GameObject("Suspect (" + spawn.profile + ")");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(spawn.position, Quaternion.Euler(0f, spawn.yaw, 0f));

            var collider = go.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.9f, 0f);
            collider.height = 1.8f;
            collider.radius = 0.35f;
            go.AddComponent<NavMeshAgent>();

            var ai = go.AddComponent<EnemyAI>();
            ai.data = data;
            ai.bodyCollider = collider;
            ai.mover = go.AddComponent<AgentMover>();
            ai.mover.Init(data.walkSpeed, data.runSpeed);
            ai.health = go.AddComponent<EnemyHealth>();
            ai.health.Init(data.maxHealth, ai);

            var parts = CharacterFactory.Build(go.transform, data.shirtColor, new Color(0.15f, 0.15f, 0.17f), CharacterFactory.RandomSkin(),
                new Color(0.08f, 0.08f, 0.09f), Color.red, true, false);
            ai.body = go.AddComponent<EnemyController>();
            ai.body.Init(parts, ai.mover);
            ai.weapon = go.AddComponent<EnemyWeapon>();
            ai.weapon.Init(data, parts.muzzle);

            ai.post = spawn.position;
            ai.postYaw = spawn.yaw;
            ai.patrol = spawn.patrol ?? new Vector3[0];
            ai.lastKnownPlayer = spawn.position;
            ai.SetState(EnemyState.Idle);
            ai.idleUntil = Time.time + Random.Range(0.5f, 3f);
            Shapes.SetLayer(go, Layers.Characters);
            return ai;
        }

        void SetState(EnemyState next)
        {
            State = next;
            stateStart = Time.time;
        }

        // ---- Every frame: turning and shooting ----

        public void FrameUpdate(float dt, PlayerController player)
        {
            body.Animate();
            switch (State)
            {
                case EnemyState.Attacking:
                    if (player == null) break;
                    mover.Face(player.Position, 360f, dt);
                    if (seesPlayer && FacingWithin(player.Position, 15f)) weapon.TryFire(player);
                    break;
                case EnemyState.Alert:
                    mover.Face(lastKnownPlayer, 300f, dt);
                    break;
                case EnemyState.Suspicious:
                    mover.Face(noisePosition, 200f, dt);
                    break;
                case EnemyState.Stunned:
                    transform.Rotate(0f, Mathf.Sin(Time.time * 5f + post.x) * 90f * dt, 0f);
                    break;
                case EnemyState.Idle:
                    if (mover.HasArrived) mover.FaceYaw(postYaw + Mathf.Sin(Time.time * 0.5f + post.z) * 40f, 60f, dt);
                    break;
            }
        }

        bool FacingWithin(Vector3 point, float degrees)
        {
            Vector3 to = point - transform.position;
            to.y = 0f;
            return Vector3.Angle(transform.forward, to) < degrees;
        }

        // ---- A few times a second: perception and decisions ----

        public void Think(PlayerController player)
        {
            if (State == EnemyState.Dead || State == EnemyState.Arrested || State == EnemyState.Surrendered) return;
            if (State == EnemyState.Stunned)
            {
                if (Time.time >= stunUntil)
                {
                    body.SetStunned(false);
                    BeginSearch(lastKnownPlayer, 8f);
                }
                return;
            }

            seesPlayer = player != null && player.Health.IsAlive && CanSee(player);
            if (seesPlayer)
            {
                lastKnownPlayer = player.Position;
                lastSeenTime = Time.time;
                ReactToSighting();
            }

            switch (State)
            {
                case EnemyState.Idle:
                    if (patrol.Length > 1 && Time.time > idleUntil)
                    {
                        SetState(EnemyState.Patrol);
                        mover.MoveTo(patrol[patrolIndex], false);
                    }
                    break;

                case EnemyState.Patrol:
                    if (mover.HasArrived)
                    {
                        patrolIndex = (patrolIndex + 1) % patrol.Length;
                        SetState(EnemyState.Idle);
                        idleUntil = Time.time + Random.Range(1.5f, 4f);
                    }
                    break;

                case EnemyState.Suspicious:
                    if (StateTime > 1.2f)
                    {
                        SetState(EnemyState.Investigating);
                        mover.MoveTo(noisePosition, false);
                    }
                    break;

                case EnemyState.Investigating:
                    if (mover.HasArrived || StateTime > 15f) BeginSearch(noisePosition, 6f);
                    break;

                case EnemyState.Alert:
                    if (Time.time >= reactionDone)
                    {
                        if (seesPlayer) StartAttack();
                        else StartChase();
                    }
                    break;

                case EnemyState.Chasing:
                    if (mover.HasArrived || StateTime > 20f) BeginSearch(lastKnownPlayer, 10f);
                    break;

                case EnemyState.Attacking:
                    if (!seesPlayer && Time.time - lastSeenTime > 1.2f)
                    {
                        StartChase();
                        break;
                    }
                    if (Time.time >= nextCoverCheck)
                    {
                        nextCoverCheck = Time.time + Random.Range(2f, 4f);
                        bool wantsCover = health.Fraction < 0.6f || Random.value < 0.25f;
                        if (wantsCover && AIManager.Instance.FindCover(Position, lastKnownPlayer, 9f, out coverPoint))
                        {
                            SetState(EnemyState.TakingCover);
                            coverWaitUntil = 0f;
                            mover.MoveTo(coverPoint, true);
                        }
                    }
                    break;

                case EnemyState.TakingCover:
                    if (mover.HasArrived)
                    {
                        if (coverWaitUntil <= 0f) coverWaitUntil = Time.time + Random.Range(1.5f, 3f);
                        if (seesPlayer) StartAttack();
                        else if (Time.time > coverWaitUntil) StartChase();
                    }
                    else if (StateTime > 6f)
                    {
                        StartChase();
                    }
                    break;

                case EnemyState.Searching:
                    if (StateTime > searchDuration)
                    {
                        ReturnToPost();
                        break;
                    }
                    if (mover.HasArrived)
                    {
                        Vector3 point;
                        if (mover.RandomPointNear(searchCenter, 5f, out point)) mover.MoveTo(point, false);
                    }
                    break;
            }
        }

        bool CanSee(PlayerController player)
        {
            Vector3 eye = transform.position + Vector3.up * 1.6f;
            Vector3 target = player.ChestPosition;
            Vector3 to = target - eye;
            float distance = to.magnitude;

            float range = data.detectionRange;
            if (SmokeCloud.Contains(player.Position)) range *= 0.3f;
            if (distance > range) return false;

            // Calm suspects only see what's in front of them; alerted ones look all around.
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            float fov = IsCalm ? data.fieldOfView : 300f;
            if (distance > 2.5f && Vector3.Angle(transform.forward, flat) > fov * 0.5f) return false;

            if (Physics.Linecast(eye, target, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return false;
            return !SmokeCloud.Blocks(eye, target);
        }

        void ReactToSighting()
        {
            if (IsCalm)
            {
                SetState(EnemyState.Alert);
                mover.Stop();
                reactionDone = Time.time + data.reactionTime * (everAlerted ? 0.5f : 1f) * Random.Range(0.85f, 1.2f);
                body.ShowAlert(1.5f);
                if (!everAlerted)
                {
                    AudioManager.Play(Sound.Detect, Position + Vector3.up * 2f, 0.7f);
                    AIManager.Instance.Callout(this, lastKnownPlayer);
                }
                everAlerted = true;
            }
            else if (State == EnemyState.Chasing)
            {
                StartAttack();
            }
        }

        void StartAttack()
        {
            SetState(EnemyState.Attacking);
            mover.Stop();
            weapon.ResetBurst();
            nextCoverCheck = Time.time + Random.Range(1f, 3f);
        }

        void StartChase()
        {
            SetState(EnemyState.Chasing);
            mover.MoveTo(lastKnownPlayer, true);
        }

        void BeginSearch(Vector3 center, float duration)
        {
            SetState(EnemyState.Searching);
            searchCenter = center;
            searchDuration = duration;
            mover.MoveTo(center, false);
        }

        void ReturnToPost()
        {
            if (patrol.Length > 1)
            {
                SetState(EnemyState.Patrol);
                mover.MoveTo(patrol[patrolIndex], false);
            }
            else
            {
                SetState(EnemyState.Idle);
                idleUntil = float.MaxValue;
                mover.MoveTo(post, false);
            }
        }

        // ---- Events from the outside world ----

        public void HearNoise(Vector3 position, NoiseKind kind)
        {
            if (!IsThreat || State == EnemyState.Stunned || State == EnemyState.Attacking || State == EnemyState.Alert) return;

            bool fromAlly = kind == NoiseKind.EnemyGunshot || kind == NoiseKind.Callout;
            // Friends' gunfire brings calm suspects running, but doesn't distract ones already tracking the officer.
            if (fromAlly && !IsCalm) return;
            bool loud = fromAlly || kind == NoiseKind.Gunshot || kind == NoiseKind.Explosion;
            if (loud)
            {
                lastKnownPlayer = position;
                if (State == EnemyState.TakingCover) return;
                if (!everAlerted) body.ShowAlert(1f);
                everAlerted = true;
                StartChase();
            }
            else if (IsCalm && State != EnemyState.Investigating)
            {
                noisePosition = position;
                SetState(EnemyState.Suspicious);
                mover.Stop();
            }
        }

        public void Stun(float duration)
        {
            if (!IsThreat) return;
            SetState(EnemyState.Stunned);
            mover.Stop();
            stunUntil = Time.time + duration;
            body.SetStunned(true);
        }

        // The officer shouted "Police! Drop your weapon!"
        public void HearShout(PlayerController player)
        {
            if (!IsThreat || Time.time < nextShoutCheck) return;
            nextShoutCheck = Time.time + 1f;

            float chance = data.surrenderChance;
            if (State == EnemyState.Stunned) chance += 0.5f;
            if (!everAlerted) chance += 0.25f;
            if (health.Fraction < 0.5f) chance += 0.25f;
            if (State == EnemyState.Attacking && health.Fraction > 0.7f) chance -= 0.15f;

            if (Random.value < chance)
            {
                Surrender();
                return;
            }
            lastKnownPlayer = player.Position;
            if (IsCalm) ReactToSighting();
        }

        void Surrender()
        {
            SetState(EnemyState.Surrendered);
            mover.Stop();
            body.SetSurrendered();
            UIManager.Notify("Suspect surrendered. Press E to arrest them.");
        }

        public bool CanInteract(PlayerController player)
        {
            return State == EnemyState.Surrendered;
        }

        public void Interact(PlayerController player)
        {
            if (State != EnemyState.Surrendered) return;
            SetState(EnemyState.Arrested);
            mover.Disable();
            body.SetArrested();
            AudioManager.Play(Sound.Click, Position, 0.8f);
            MissionManager.Instance.OnSuspectArrested();
        }

        public void OnHit(DamageInfo info, bool lethal)
        {
            if (info.attacker == Team.Police && (State == EnemyState.Surrendered || State == EnemyState.Arrested))
                MissionManager.Instance.OnUnauthorizedForce();

            if (lethal)
            {
                Die();
                return;
            }
            if (!IsThreat) return;

            var player = GameManager.Instance.Player;
            if (player != null) lastKnownPlayer = player.Position;
            if (IsCalm)
            {
                SetState(EnemyState.Alert);
                mover.Stop();
                reactionDone = Time.time + 0.25f;
                body.ShowAlert(1.5f);
                everAlerted = true;
            }
        }

        void Die()
        {
            bool wasArrested = State == EnemyState.Arrested;
            SetState(EnemyState.Dead);
            mover.Disable();
            bodyCollider.enabled = false;
            body.SetDead();
            if (!wasArrested) MissionManager.Instance.OnSuspectKilled();
        }
    }
}
