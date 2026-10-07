using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    public enum EnemyState
    {
        Idle, Patrol, Suspicious, Investigating, Alert, Chasing, Attacking, TakingCover, Searching,
        Fleeing, Hiding, Stunned, Surrendering, Restrained, Dead, Holding,
        Incapacitated,   // down but alive: out of the fight until restrained
    }

    // A suspect's brain: a small finite state machine shared by every archetype.
    //
    //   Idle/Patrol --hears something--> Suspicious --> Investigating --> Searching --> back to post
    //        |                                                                ^
    //        +--sees police--> Alert --> Attacking <--> Chasing --------------+
    //                            |          |
    //                            |          +--> TakingCover
    //                            +--> Fleeing --> Hiding          (unarmed, nervous, leaders)
    //
    // Flashbangs and less-lethal rounds cause Stunned. Shouting can cause
    // Surrendering; restraining a surrendered suspect makes them Restrained.
    // A body or limb hit that would put them down often leaves them Incapacitated
    // (down, alive, unarmed) instead; they still have to be restrained and reported.
    // Think() runs a few times a second; FrameUpdate() only turns and shoots.
    //
    // Smarter behaviour on top: alerted suspects often Hold their room instead of
    // charging (kneeling a few metres back with a clear view of the door the police
    // will come through, and firing the moment someone appears); several chasing
    // suspects spread out instead of filing in; rounds cracking past them keep their
    // heads down (Suppress); and a few armed ones only pretend to surrender, then pull
    // a weapon once nobody is covering them.
    public class EnemyAI : MonoBehaviour, IInteractable
    {
        public EnemyData Data { get; private set; }
        public EnemyState State { get; private set; }
        public bool IsNeutralized { get { return State == EnemyState.Dead || State == EnemyState.Restrained || Escaped; } }
        public bool IsArmedThreat { get { return Data.armed && !Down && State != EnemyState.Surrendering && State != EnemyState.Hiding; } }
        // Out of the fight: neutralized, or lying incapacitated.
        public bool Down { get { return IsNeutralized || State == EnemyState.Incapacitated; } }
        public bool WasIncapacitated { get; private set; }
        public bool NeedsSecuring { get { return !IsNeutralized; } }
        public bool IsLeader { get { return Data.archetype == EnemyArchetype.Leader; } }
        public bool Escaped { get; private set; }
        public bool HasSpottedPolice { get { return everAlerted; } }
        public Vector3 Position { get { return transform.position; } }
        public Vector3 Head { get { return transform.position + Vector3.up * 1.5f; } }
        public float NextThink { get; set; }
        public int Area { get; set; }
        public EnemyController Body { get { return body; } }

        public string Prompt { get { return State == EnemyState.Incapacitated ? "[E] Restrain Incapacitated Suspect (hold)" : Data.armed ? "[E] Secure Suspect (hold)" : "[E] Secure and Question Suspect (hold)"; } }
        public Vector3 InteractPosition { get { return transform.position + Vector3.up; } }

        AgentMover mover;
        EnemyHealth health;
        EnemyWeapon weapon;
        EnemyController body;
        Collider bodyCollider;
        WeaponData gunData;          // the gun they carry (dropped when they give up or go down)
        string gunVariant;
        bool gunDropped;

        Vector3 post;
        float postYaw;
        Vector3[] patrol;
        int patrolIndex;
        float stateStart, idleUntil, reactionDone, lastSeenTime = -100f, stunUntil, coverWaitUntil, nextCoverCheck, searchDuration, nextShoutCheck, nextErratic, alarmCallAt = -1f;
        float reactionMultiplier = 1f;
        float holdUntil, suppressedUntil, fakeAt = -1f, gassedUntil, nextCough;
        int fakeTries;
        Vector3 holdFacing;
        bool everAlerted;
        float lastShoutHeard = -100f;   // when an officer last ordered them to comply
        ICombatTarget target;
        Vector3 lastKnown, noisePosition, searchCenter, coverPoint, fleeTarget;

        float StateTime { get { return Time.time - stateStart; } }
        bool IsCalm
        {
            get
            {
                return State == EnemyState.Idle || State == EnemyState.Patrol || State == EnemyState.Suspicious
                    || State == EnemyState.Investigating || State == EnemyState.Searching;
            }
        }

        public static EnemyAI Spawn(Transform parent, EnemyData data, EnemySpawnPoint spawn, float accuracyMultiplier, float reactionMultiplier)
        {
            var go = new GameObject(data.displayName);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(spawn.position, Quaternion.Euler(0f, spawn.yaw, 0f));

            var collider = go.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.9f, 0f);
            collider.height = 1.8f;
            collider.radius = 0.35f;
            go.AddComponent<NavMeshAgent>();

            var ai = go.AddComponent<EnemyAI>();
            ai.Data = data;
            ai.bodyCollider = collider;
            ai.reactionMultiplier = reactionMultiplier;
            ai.mover = go.AddComponent<AgentMover>();
            ai.mover.Init(data.walkSpeed, data.runSpeed);
            ai.health = go.AddComponent<EnemyHealth>();
            ai.health.Init(data, ai);

            var look = CharacterFactory.SuspectLook(data);
            var parts = CharacterFactory.Build(go.transform, look);
            WeaponData gun = null;
            if (data.armed)
            {
                gun = GameData.Weapon(SuspectGun(data.archetype)) ?? GameData.Weapon("smg_compact");
                // Some carry the pack's double-barrel shotgun or snub-nosed revolver instead (looks only).
                string variant = gun.id == "shotgun_ts8" && Random.value < 0.5f ? "gun_double" : gun.id == "revolver_r6" && Random.value < 0.5f ? "gun_snub" : null;
                CharacterFactory.SetWeapon(parts, gun, null, variant);
                ai.gunData = gun;
                ai.gunVariant = variant;
            }
            ai.body = go.AddComponent<EnemyController>();
            ai.body.Init(parts, ai.mover, data.armed);
            ai.weapon = go.AddComponent<EnemyWeapon>();
            ai.weapon.Init(data, parts.muzzle, accuracyMultiplier, gun);

            ai.post = spawn.position;
            ai.postYaw = spawn.yaw;
            ai.patrol = spawn.patrol ?? new Vector3[0];
            ai.lastKnown = spawn.position;
            ai.SetState(EnemyState.Idle);
            ai.idleUntil = Time.time + Random.Range(0.5f, 3f);
            Shapes.SetLayer(go, Layers.Characters);
            return ai;
        }

        // Which model a suspect carries (looks and sound only; damage comes from EnemyData).
        static string SuspectGun(EnemyArchetype archetype)
        {
            switch (archetype)
            {
                case EnemyArchetype.Nervous: return Random.value < 0.5f ? "pistol_bk6" : "revolver_r6";
                case EnemyArchetype.Armored: return Random.value < 0.5f ? "rifle_service" : "shotgun_as12";
                case EnemyArchetype.Leader: return Random.value < 0.5f ? "pistol_h50" : "rifle_compact";
                case EnemyArchetype.Guard: return Random.value < 0.6f ? "pistol_p17" : "smg_v10";
                default:
                    float roll = Random.value;
                    return roll < 0.4f ? "smg_compact" : roll < 0.7f ? "mp_m9" : "shotgun_ts8";
            }
        }

        void SetState(EnemyState next)
        {
            if (State == EnemyState.Hiding && next != EnemyState.Hiding) body.SetHiding(false);
            // Kneeling is for holding an angle, cover and shooting from it; on the move they stand.
            bool low = next == EnemyState.Holding || next == EnemyState.TakingCover || next == EnemyState.Attacking || next == EnemyState.Hiding || next == EnemyState.Restrained;
            if (!low && body != null && body.IsCrouched) body.SetCrouched(false);
            State = next;
            stateStart = Time.time;
        }

        // ---- Every frame: turning and shooting ----

        public void FrameUpdate(float dt)
        {
            body.Animate(dt);
            switch (State)
            {
                case EnemyState.Attacking:
                    if (target == null || !target.IsAlive) break;
                    mover.Face(target.Position, 360f, dt);
                    if (Data.armed && FacingWithin(target.Position, 15f)) weapon.TryFire(target);
                    break;
                case EnemyState.Alert:
                    mover.Face(lastKnown, 300f, dt);
                    break;
                case EnemyState.Holding:
                    if (mover.HasArrived) mover.Face(holdFacing, 240f, dt);
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

        public void Think()
        {
            if (Down) return;
            if (State == EnemyState.Surrendering)
            {
                if (fakeAt > 0f && Time.time >= fakeAt) TryFakeOut();
                return;
            }
            mover.TrackProgress();
            if (mover.IsStuck) mover.Stop();

            if (State == EnemyState.Stunned)
            {
                if (Time.time >= stunUntil)
                {
                    body.SetStunned(false, Data.armed);
                    if (Data.armed) BeginSearch(lastKnown, 8f);
                    else BeginFlee(lastKnown);
                }
                return;
            }

            var seen = FindVisibleTarget();
            if (seen != null)
            {
                target = seen;
                lastKnown = seen.Position;
                lastSeenTime = Time.time;
                ReactToSighting();
            }

            if (alarmCallAt > 0f && Time.time >= alarmCallAt)
            {
                alarmCallAt = -1f;
                var alarm = AlarmSystem.Instance;
                if (alarm != null && alarm.State == AlarmState.Armed) alarm.Trigger(Position, "a guard radioed for help");
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
                        if (ShouldFlee()) BeginFlee(lastKnown);
                        else if (seen != null) StartAttack();
                        else StartChase();
                    }
                    break;

                case EnemyState.Chasing:
                    if (mover.HasArrived || StateTime > 20f) BeginSearch(lastKnown, 10f);
                    break;

                case EnemyState.Attacking:
                    if (seen == null && Time.time - lastSeenTime > 1.2f)
                    {
                        StartChase();
                        break;
                    }
                    if (Data.erratic && Time.time > nextErratic)
                    {
                        // Nervous suspects shuffle around unpredictably while shooting.
                        nextErratic = Time.time + Random.Range(1f, 2.5f);
                        Vector3 point;
                        if (mover.RandomPointNear(Position, 2.5f, out point)) mover.MoveTo(point, true);
                    }
                    if (Time.time >= nextCoverCheck)
                    {
                        nextCoverCheck = Time.time + Random.Range(2f, 4f);
                        if (health.Fraction < 0.35f && Random.value < Data.fleeChance)
                        {
                            BeginFlee(lastKnown);
                            break;
                        }
                        bool wantsCover = health.Fraction < 0.6f || Random.value < 0.25f;
                        if (wantsCover && CoverPoint.Find(Position, lastKnown, 9f, out coverPoint))
                        {
                            SetState(EnemyState.TakingCover);
                            coverWaitUntil = 0f;
                            mover.MoveTo(coverPoint, true);
                        }
                    }
                    break;

                case EnemyState.Holding:
                    // Kneel once in position; give up the angle after a while and go looking.
                    if (mover.HasArrived && !body.IsCrouched) body.SetCrouched(true);
                    if (Time.time > holdUntil)
                    {
                        if (Random.value < 0.5f) StartChase();
                        else BeginSearch(lastKnown, 8f);
                    }
                    break;

                case EnemyState.TakingCover:
                    if (mover.HasArrived)
                    {
                        if (!body.IsCrouched) body.SetCrouched(true);
                        if (coverWaitUntil <= 0f) coverWaitUntil = Time.time + Random.Range(1.5f, 3f);
                        if (seen != null) StartAttack();
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

                case EnemyState.Fleeing:
                    if (IsLeader && Vector3.Distance(Position, fleeTarget) < 1.5f && GameManager.Instance.Level.escapePoints.Count > 0)
                    {
                        Escape();
                        break;
                    }
                    if (mover.HasArrived || StateTime > 12f)
                    {
                        SetState(EnemyState.Hiding);
                        mover.Stop();
                        body.SetHiding(true);
                    }
                    break;

                case EnemyState.Hiding:
                    // Cornered: an armed suspect who is found up close may fight back.
                    if (seen != null && Data.armed && Vector3.Distance(seen.Position, Position) < 4f && Random.value < 0.3f)
                    {
                        body.SetHiding(false);
                        StartAttack();
                    }
                    break;
            }
        }

        ICombatTarget FindVisibleTarget()
        {
            Vector3 eye = Head;
            float fov = IsCalm ? Data.fieldOfView : 300f;
            ICombatTarget best = null;
            float bestDistance = float.MaxValue;
            foreach (var candidate in AIManager.Instance.PoliceTargets)
            {
                if (!candidate.IsAlive) continue;
                float distance = Vector3.Distance(candidate.Position, Position);
                if (distance > Data.detectionRange * 1.3f || distance >= bestDistance) continue;
                float visibility = AIVisibility.VisibilityOf(candidate);
                if (!AIVisibility.CanSee(eye, transform.forward, fov, Data.detectionRange, candidate.ChestPosition, visibility)) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        bool ShouldFlee()
        {
            if (!Data.armed) return true;
            if (IsLeader) return Random.value < Data.fleeChance;
            return Data.archetype == EnemyArchetype.Nervous && Random.value < Data.fleeChance * 0.5f;
        }

        void ReactToSighting()
        {
            if (IsCalm)
            {
                SetState(EnemyState.Alert);
                mover.Stop();
                reactionDone = Time.time + Data.reactionTime * reactionMultiplier * (everAlerted ? 0.5f : 1f) * Random.Range(0.85f, 1.2f);
                body.ShowAlert(1.5f);
                if (!everAlerted)
                {
                    AudioManager.Play(Sound.Detect, Position + Vector3.up * 2f, 0.7f);
                    AIManager.Instance.Callout(this, lastKnown, Data.callForHelpRadius);
                    if (Data.archetype == EnemyArchetype.Guard && Random.value < 0.4f) alarmCallAt = Time.time + 2.5f;
                }
                everAlerted = true;
            }
            else if ((State == EnemyState.Chasing || State == EnemyState.Holding) && Data.armed)
            {
                // Someone holding an angle is already aimed in: no reaction time.
                StartAttack();
            }
        }

        void StartAttack()
        {
            if (!Data.armed)
            {
                BeginFlee(lastKnown);
                return;
            }
            SetState(EnemyState.Attacking);
            mover.Stop();
            weapon.ResetBurst();
            nextCoverCheck = Time.time + Random.Range(1f, 3f);
        }

        void StartChase()
        {
            if (!Data.armed)
            {
                BeginSearch(lastKnown, 6f);
                return;
            }
            SetState(EnemyState.Chasing);
            // Spread out a little so several suspects don't come through in single file.
            Vector2 offset = Random.insideUnitCircle * 2.5f;
            Vector3 goal;
            if (!mover.RandomPointNear(lastKnown + new Vector3(offset.x, 0f, offset.y), 1.5f, out goal)) goal = lastKnown;
            mover.MoveTo(goal, true);
        }

        // How likely each kind of suspect is to dig in and hold the room rather than charge.
        bool WantsToHold()
        {
            switch (Data.archetype)
            {
                case EnemyArchetype.Leader: return Random.value < 0.7f;
                case EnemyArchetype.Guard: return Random.value < 0.6f;
                case EnemyArchetype.Armored: return Random.value < 0.5f;
                case EnemyArchetype.Hostile: return Random.value < 0.45f;
                case EnemyArchetype.Nervous: return Random.value < 0.15f;
                default: return false;
            }
        }

        // Kneel a few metres back from the door the threat will most likely come through,
        // somewhere with a clear view of it, and wait.
        bool BeginHold(Vector3 threat)
        {
            var level = GameManager.Instance.Level;
            var room = level != null ? level.RoomAt(Position) : null;
            if (room == null || !room.Indoor) return false;
            DoorController entry = null;
            float best = float.MaxValue;
            foreach (var door in level.doors)
            {
                if (door == null || (door.RoomFront != room && door.RoomBack != room)) continue;
                float d = (door.transform.position - threat).sqrMagnitude;
                if (d < best) { best = d; entry = door; }
            }
            Vector3 watch = entry != null ? entry.transform.position : threat;
            Vector3 inward = room.Bounds.center - watch;
            inward.y = 0f;
            if (inward.sqrMagnitude < 0.01f) inward = Position - watch;
            inward.y = 0f;
            if (inward.sqrMagnitude < 0.01f) inward = transform.forward;
            inward.Normalize();
            Vector3 aimAt = watch + inward * 0.4f + Vector3.up * 1.1f;
            Vector3 spot = Position;
            for (int i = 0; i < 6; i++)
            {
                Vector3 candidate = watch + inward * Random.Range(3f, 5.5f) + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
                Vector3 point;
                if (!mover.RandomPointNear(candidate, 1f, out point) || !room.Contains(point)) continue;
                if (Physics.Linecast(point + Vector3.up * 1.1f, aimAt, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                spot = point;
                break;
            }
            holdFacing = watch;
            holdUntil = Time.time + Random.Range(25f, 45f);
            SetState(EnemyState.Holding);
            mover.MoveTo(spot, true);
            return true;
        }

        void BeginSearch(Vector3 center, float duration)
        {
            SetState(EnemyState.Searching);
            searchCenter = center;
            searchDuration = duration;
            mover.MoveTo(center, false);
        }

        // Run away from the threat. Leaders head for an escape route.
        void BeginFlee(Vector3 threat)
        {
            SetState(EnemyState.Fleeing);
            body.ShowAlert(1f);
            if (IsLeader)
            {
                float best = float.MaxValue;
                bool found = false;
                foreach (var point in GameManager.Instance.Level.escapePoints)
                {
                    if (!mover.CanReach(point)) continue;
                    float distance = Vector3.Distance(point, Position);
                    if (distance < best)
                    {
                        best = distance;
                        fleeTarget = point;
                        found = true;
                    }
                }
                if (found)
                {
                    mover.MoveTo(fleeTarget, true);
                    UIManager.Notify("The leader is trying to escape!", true);
                    return;
                }
            }
            Vector3 away = Position - threat;
            away.y = 0f;
            Vector3 goal = Position + (away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward) * 8f;
            if (mover.RandomPointNear(goal, 3f, out fleeTarget)) mover.MoveTo(fleeTarget, true);
            else
            {
                SetState(EnemyState.Hiding);
                body.SetHiding(true);
            }
        }

        void Escape()
        {
            Escaped = true;
            mover.Disable();
            bodyCollider.enabled = false;
            body.Parts.SetVisible(false);
            gameObject.SetActive(false);
            MissionManager.Instance.OnLeaderEscaped(this);
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
            // Holding an angle: a door or footsteps close by draw their aim, nothing else moves them.
            if (State == EnemyState.Holding)
            {
                if ((kind == NoiseKind.Door || kind == NoiseKind.Footstep) && (position - Position).sqrMagnitude < 100f) holdFacing = position;
                return;
            }
            if (Down || State == EnemyState.Surrendering || State == EnemyState.Stunned || State == EnemyState.Attacking
                || State == EnemyState.Alert || State == EnemyState.Fleeing || State == EnemyState.Hiding) return;

            bool fromAlly = kind == NoiseKind.EnemyGunshot || kind == NoiseKind.Callout;
            if (fromAlly && !IsCalm) return;
            bool loud = fromAlly || kind == NoiseKind.Gunshot || kind == NoiseKind.Explosion || kind == NoiseKind.Alarm;
            if (loud)
            {
                lastKnown = position;
                if (State == EnemyState.TakingCover) return;
                if (!everAlerted) body.ShowAlert(1f);
                everAlerted = true;
                if (!Data.armed && kind != NoiseKind.Alarm) BeginFlee(position);
                else if (!(IsCalm && WantsToHold() && BeginHold(position))) StartChase();
            }
            else if (IsCalm && State != EnemyState.Investigating)
            {
                noisePosition = position;
                SetState(EnemyState.Suspicious);
                mover.Stop();
            }
        }

        public void OnAlarm(Vector3 source)
        {
            if (!Data.respondsToAlarms || !IsCalm) return;
            noisePosition = source;
            HearNoise(source, NoiseKind.Alarm);
        }

        public void Stun(float duration)
        {
            if (Down || State == EnemyState.Surrendering) return;
            SetState(EnemyState.Stunned);
            mover.Stop();
            stunUntil = Time.time + duration;
            body.SetStunned(true, Data.armed);
        }

        // In a cloud of CS gas (twice a second): coughing and dazed, aim all over the place, and
        // some give up on the spot (the leader holds out longer).
        public void Gassed()
        {
            if (Down || State == EnemyState.Surrendering || Data.archetype == EnemyArchetype.TrainingDummy) return;
            gassedUntil = Time.time + 4f;
            everAlerted = true;
            if (Data.armed) weapon.Stagger(0.8f);
            if (State != EnemyState.Stunned || stunUntil < Time.time + 0.6f) Stun(1.6f);
            if (Time.time >= nextCough)
            {
                nextCough = Time.time + Random.Range(1.2f, 2.2f);
                AudioManager.Play(Sound.Gasp, Position, 0.5f, Random.Range(0.8f, 1f));
            }
            if (Random.value < (IsLeader ? 0.04f : 0.12f)) Surrender();
        }

        // A shove or a shield bash: knocked back and dazed for a moment, no harm done.
        // A dazed suspect is much more likely to give up when shouted at.
        public void Shoved(Vector3 direction, float seconds)
        {
            if (Down || State == EnemyState.Surrendering) return;
            body.Animator.Hit(direction);
            mover.Nudge(direction * 0.6f);
            if (Data.armed) weapon.Stagger(seconds);
            var player = GameManager.Instance.Player;
            if (player != null) lastKnown = player.Position;
            everAlerted = true;
            Stun(seconds);
        }

        // An officer shouted "Police! Show me your hands!"
        public void HearShout(Vector3 from, bool byPlayer)
        {
            if (!Down) lastShoutHeard = Time.time;
            if (Down || State == EnemyState.Surrendering || Time.time < nextShoutCheck) return;
            nextShoutCheck = Time.time + 1f;

            float chance = Data.surrenderChance;
            if (State == EnemyState.Stunned) chance += 0.5f;
            if (!everAlerted) chance += 0.25f;
            if (State == EnemyState.Hiding) chance += 0.3f;
            if (health.Fraction < 0.5f) chance += 0.25f;
            if (State == EnemyState.Attacking && health.Fraction > 0.7f) chance -= 0.15f;
            if (AIManager.Instance.PoliceNear(Position, 6f) >= 2) chance += 0.1f; // outnumbered
            if (Time.time < suppressedUntil) chance += 0.2f; // pinned down
            if (Time.time < gassedUntil) chance += 0.25f;   // choking on CS gas

            if (Random.value < chance)
            {
                Surrender();
                return;
            }
            lastKnown = from;
            if (IsCalm) ReactToSighting();
        }

        void Surrender()
        {
            // A few armed suspects only pretend: they wait for a moment when nobody has them covered.
            fakeAt = -1f;
            fakeTries = 0;
            float fake = Data.archetype == EnemyArchetype.Leader ? 0.25f : Data.archetype == EnemyArchetype.Hostile ? 0.15f : 0f;
            if (Data.armed && Random.value < fake) fakeAt = Time.time + Random.Range(4f, 9f);
            SetState(EnemyState.Surrendering);
            mover.Stop();
            body.SetSurrendered();
            DropGun();
            UIManager.Notify(Data.displayName + " surrendered. Restrain them (E).");
            MissionManager.Instance.Report(ObjectiveType.TrainingRestrain, 0);
        }

        // A fake surrender ends: if an officer close by is watching them they keep waiting
        // (and after a few tries give up for real); otherwise the gun comes out.
        void TryFakeOut()
        {
            if (CoveredByPolice())
            {
                if (++fakeTries > 3) fakeAt = -1f;
                else fakeAt = Time.time + Random.Range(3f, 6f);
                return;
            }
            fakeAt = -1f;
            // The gun they dropped, if nobody picked it up; otherwise most give up for real, a few have a backup.
            var floorGun = DroppedWeapon.ForOwner(this);
            bool grab = floorGun != null && (floorGun.transform.position - Position).sqrMagnitude < 6.25f;
            if (!grab && Random.value < 0.6f) return;
            if (grab) floorGun.Take();
            gunDropped = false;
            body.SetArmedAgain();
            ICombatTarget nearest = null;
            float best = float.MaxValue;
            foreach (var police in AIManager.Instance.PoliceTargets)
            {
                if (!police.IsAlive) continue;
                float d = (police.Position - Position).sqrMagnitude;
                if (d < best) { best = d; nearest = police; }
            }
            if (nearest != null) lastKnown = nearest.Position;
            SetState(EnemyState.Alert);
            mover.Stop();
            reactionDone = Time.time + 0.35f;
            UIManager.Notify(Data.displayName + (grab ? " grabbed their gun off the floor!" : " pulled a hidden backup gun!"), true);
        }

        bool CoveredByPolice()
        {
            foreach (var police in AIManager.Instance.PoliceTargets)
            {
                if (!police.IsAlive) continue;
                Vector3 to = Position - police.Position;
                to.y = 0f;
                if (to.sqrMagnitude > 64f) continue;
                if (to.sqrMagnitude < 2.25f) return true; // someone right on top of them
                if (Vector3.Angle(police.Transform.forward, to) > 30f) continue;
                if (!Physics.Linecast(police.ChestPosition, Head, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return true;
            }
            return false;
        }

        // Rounds cracking past: heads down. Worse aim for a moment, maybe a dash for cover,
        // and more likely to give up when shouted at.
        public void Suppress(Vector3 from)
        {
            if (Down || State == EnemyState.Surrendering || State == EnemyState.Stunned || Data.archetype == EnemyArchetype.TrainingDummy) return;
            suppressedUntil = Time.time + 2f;
            if (Data.armed) weapon.Stagger(1f);
            if (State == EnemyState.Attacking && Random.value < 0.35f && CoverPoint.Find(Position, from, 8f, out coverPoint))
            {
                SetState(EnemyState.TakingCover);
                coverWaitUntil = 0f;
                mover.MoveTo(coverPoint, true);
            }
            else if (IsCalm) HearNoise(from, NoiseKind.Gunshot);
        }

        public float InteractDuration(PlayerController player) { return 1f; }
        public bool CanInteract(PlayerController player) { return State == EnemyState.Surrendering || State == EnemyState.Incapacitated; }
        public void Interact(PlayerController player) { Restrain(true); }

        public void Restrain(bool byPlayer)
        {
            if (State != EnemyState.Surrendering && State != EnemyState.Incapacitated) return;
            bool lying = State == EnemyState.Incapacitated;
            SetState(EnemyState.Restrained);
            mover.Disable();
            // Someone incapacitated is cuffed where they lie.
            if (!lying) body.SetRestrained();
            AudioManager.Play(Sound.Click, Position, 0.8f);
            MissionManager.Instance.OnSuspectRestrained(this);
            if (byPlayer && Data.archetype != EnemyArchetype.TrainingDummy && !VersusMatch.Active)
                UIManager.Notify("Suspect restrained. Report it to TOC (" + GameInput.PromptKey(InputAction.Report) + ")" + (DroppedWeapon.ForOwner(this) != null ? " and secure their weapon" : ""));
            if (!Data.armed && Data.archetype != EnemyArchetype.TrainingDummy)
            {
                // Questioning an unarmed suspect reveals who else is nearby.
                TacticalIntel.Instance.RevealAround(Position, 15f, 20f);
                UIManager.Notify("The suspect tells you where the others are (marked on the map)");
            }
        }

        // Rules of engagement (SWAT 4): force is unauthorized against someone who has given up, is
        // restrained or already down, against an unarmed suspect (less-lethal aside), and deadly force
        // against an armed suspect who hasn't seen the police and wasn't ordered to comply first.
        bool Unjustified(DamageInfo info)
        {
            if (State == EnemyState.Surrendering || State == EnemyState.Restrained || State == EnemyState.Incapacitated) return true;
            if (Data.archetype == EnemyArchetype.TrainingDummy) return true;
            if (info.lessLethal) return false;
            if (!Data.armed) return true;
            return !everAlerted && IsCalm && Time.time - lastShoutHeard > 10f && !VersusMatch.Active;
        }

        void ReportForce(DamageInfo info)
        {
            if (info.attacker != Team.Police || !Unjustified(info)) return;
            if (Data.armed && !everAlerted && IsCalm && info.byPlayer)
                UIManager.Notify("The suspect hadn't seen you and wasn't ordered to comply: shout (" + GameInput.PromptKey(InputAction.Shout) + ") first", true);
            MissionManager.Instance.OnUnauthorizedForce(Data.displayName);
        }

        // Whether a hit that would put them down leaves them incapacitated instead (a head hit never does).
        public bool Incapacitates(DamageInfo info)
        {
            if (Data.archetype == EnemyArchetype.TrainingDummy || State == EnemyState.Restrained || VersusMatch.Active) return false;
            if (!info.zoned) return Random.value < 0.6f;
            switch (info.zone)
            {
                case HitZone.Head: return false;
                case HitZone.Arm: return Random.value < 0.9f;
                case HitZone.Leg: return Random.value < 0.85f;
                default: return Random.value < 0.45f;
            }
        }

        public void Incapacitate(DamageInfo info)
        {
            ReportForce(info);
            body.Animator.Hit(info.direction);
            DropGun();
            WasIncapacitated = true;
            SetState(EnemyState.Incapacitated);
            mover.Disable();
            body.SetDead();
            MissionManager.Instance.OnSuspectIncapacitated(this);
            if (info.byPlayer) UIManager.Notify(Data.displayName + " is down but alive. Restrain them (E) and report it.");
        }

        public void OnHit(DamageInfo info, bool lethal)
        {
            // A takedown too: the flash shows the hit, and they fall away from the shot.
            body.Animator.Hit(info.direction);
            ReportForce(info);

            if (lethal)
            {
                Die();
                return;
            }
            if (Down || State == EnemyState.Surrendering) return;
            // Getting hit throws their aim off for a moment.
            if (Data.armed && !info.lessLethal) weapon.Stagger(0.35f);
            if (info.zoned && !info.lessLethal)
            {
                // A round in the gun arm: often they drop it and give up. In a leg: they can't run.
                if (info.zone == HitZone.Arm && Data.armed && Random.value < 0.35f)
                {
                    UIManager.Notify(Data.displayName + " dropped their weapon");
                    Surrender();
                    return;
                }
                if (info.zone == HitZone.Leg) mover.SetSpeedMultiplier(0.55f);
            }

            if (info.lessLethal && info.stun > 0f && Data.archetype != EnemyArchetype.Armored)
            {
                Stun(info.stun);
                float bonus = info.weapon != null ? info.weapon.surrenderBonus : 0.35f;
                if (Random.value < Data.surrenderChance + bonus) Surrender();
                return;
            }

            var player = GameManager.Instance.Player;
            if (player != null) lastKnown = player.Position;
            everAlerted = true;
            if (IsCalm)
            {
                SetState(EnemyState.Alert);
                mover.Stop();
                reactionDone = Time.time + 0.25f;
                body.ShowAlert(1.5f);
            }
            else if (!Data.armed && State != EnemyState.Fleeing)
            {
                BeginFlee(lastKnown);
            }
        }

        // Their gun hits the floor (once): an officer has to secure it.
        void DropGun()
        {
            if (gunDropped || !Data.armed || gunData == null || Data.archetype == EnemyArchetype.TrainingDummy) return;
            gunDropped = true;
            DroppedWeapon.Drop(this, gunData, gunVariant);
        }

        void Die()
        {
            bool wasRestrained = State == EnemyState.Restrained;
            if (!wasRestrained) DropGun();
            SetState(EnemyState.Dead);
            mover.Disable();
            bodyCollider.enabled = false;
            body.SetDead();
            MissionManager.Instance.OnSuspectDown(this, wasRestrained);
        }

        public void SetSeen(bool visible)
        {
            body.Parts.SetVisible(visible || State == EnemyState.Dead || State == EnemyState.Restrained || State == EnemyState.Incapacitated);
        }
    }
}
