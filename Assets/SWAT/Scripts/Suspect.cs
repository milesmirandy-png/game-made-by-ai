using UnityEngine;

namespace Swat
{
    // An armed suspect. Spots and shoots at the officer, turns towards noises,
    // can be stunned by flashbangs and may surrender when shouted at.
    public class Suspect : MonoBehaviour, IShootable, IInteractable
    {
        public enum State { Idle, Alert, Engaging, Stunned, Surrendered, Arrested, Dead }

        public State Current { get; private set; }
        public bool IsNeutralized { get { return Current == State.Dead || Current == State.Arrested; } }
        public Vector3 HeadPosition { get { return parts.head.position; } }

        public bool CanInteract { get { return Current == State.Surrendered; } }
        public string Prompt { get { return "[E] Restrain suspect"; } }

        static readonly Color[] Shirts =
        {
            new Color(0.45f, 0.1f, 0.1f), new Color(0.22f, 0.22f, 0.24f),
            new Color(0.35f, 0.27f, 0.12f), new Color(0.15f, 0.25f, 0.15f),
        };

        HumanParts parts;
        Transform gun;
        float health = 100f;
        float accuracy;
        float reactionTime;
        float compliance;

        float stateTimer, reactionTimer, shotTimer, staggerTimer, idleYaw, idlePhase, nextComplianceCheck;
        int burstLeft;
        bool hasSeenPlayer;
        float surprisedUntil;
        float lastSeenTime = -100f;
        Vector3 lastKnownPosition;
        float deathProgress = -1f;
        Quaternion deathStartRotation;
        Vector3 deathStartPosition;

        public static Suspect Spawn(Transform parent, Vector3 position, float yaw, float accuracy, float reactionTime)
        {
            var go = new GameObject("Suspect");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var suspect = go.AddComponent<Suspect>();
            suspect.accuracy = accuracy;
            suspect.reactionTime = reactionTime;
            suspect.compliance = Random.Range(-0.1f, 0.15f);
            suspect.idleYaw = yaw;
            suspect.idlePhase = Random.Range(0f, 10f);
            suspect.lastKnownPosition = position + go.transform.forward;
            suspect.BuildModel();
            return suspect;
        }

        void BuildModel()
        {
            var mask = new Color(0.08f, 0.08f, 0.09f);
            parts = Humanoid.Build(transform, this, Shirts[Random.Range(0, Shirts.Length)], new Color(0.12f, 0.12f, 0.14f), mask, mask, false);
            // A strip of skin showing through the balaclava shows which way they are facing.
            Shapes.Box("Eyes", parts.head, new Vector3(0f, 0.1f, 0.42f), new Vector3(0.75f, 0.2f, 0.25f), Humanoid.RandomSkin(), false);
            gun = Shapes.Box("Gun", transform, new Vector3(0.1f, 1.22f, 0.45f), new Vector3(0.07f, 0.13f, 0.5f), new Color(0.05f, 0.05f, 0.05f), false).transform;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            AnimateArms(dt);
            if (Current == State.Dead)
            {
                AnimateDeath(dt);
                return;
            }

            var game = GameManager.Instance;
            if (game == null || !game.IsPlaying || game.Player == null) return;
            if (Current == State.Surrendered || Current == State.Arrested) return;

            if (Current == State.Stunned)
            {
                stateTimer -= dt;
                transform.Rotate(0f, Mathf.Sin(Time.time * 6f + idlePhase) * 120f * dt, 0f);
                if (stateTimer <= 0f)
                {
                    Current = State.Alert;
                    stateTimer = 6f;
                }
                return;
            }

            var player = game.Player;
            bool sees = player.IsAlive && CanSee(player);
            if (sees)
            {
                lastSeenTime = Time.time;
                lastKnownPosition = player.transform.position;
                if (Current != State.Engaging)
                {
                    if (!hasSeenPlayer) surprisedUntil = Time.time + 2f;
                    reactionTimer = reactionTime * Random.Range(0.8f, 1.3f) * (hasSeenPlayer ? 0.6f : 1f);
                    hasSeenPlayer = true;
                    Current = State.Engaging;
                }
            }
            else if (Current == State.Engaging && Time.time - lastSeenTime > 4f)
            {
                Current = State.Alert;
                stateTimer = 8f;
            }

            staggerTimer -= dt;
            switch (Current)
            {
                case State.Idle:
                    idlePhase += dt * 0.6f;
                    TurnTowardsYaw(idleYaw + Mathf.Sin(idlePhase) * 55f, 50f, dt);
                    break;
                case State.Alert:
                    TurnTowards(lastKnownPosition, 150f, dt);
                    stateTimer -= dt;
                    if (stateTimer <= 0f)
                    {
                        Current = State.Idle;
                        idleYaw = transform.eulerAngles.y;
                    }
                    break;
                case State.Engaging:
                    TurnTowards(lastKnownPosition, 240f, dt);
                    if (sees && staggerTimer <= 0f) UpdateShooting(player, dt);
                    break;
            }
        }

        bool CanSee(PlayerController player)
        {
            Vector3 eye = HeadPosition;
            Vector3 toPlayer = player.EyePosition - eye;
            float distance = toPlayer.magnitude;
            if (distance > 35f) return false;
            if (distance > 2.5f && Vector3.Angle(transform.forward, toPlayer) > 70f) return false;

            Vector3 direction = toPlayer / distance;
            RaycastHit hit;
            if (Physics.Raycast(eye + direction * 0.25f, direction, out hit, distance, ~0, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponent<PlayerController>() != null;
            return true;
        }

        void UpdateShooting(PlayerController player, float dt)
        {
            reactionTimer -= dt;
            if (reactionTimer > 0f) return;

            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (Vector3.Angle(transform.forward, toPlayer) > 20f) return;

            shotTimer -= dt;
            if (shotTimer > 0f) return;
            if (burstLeft <= 0) burstLeft = Random.Range(2, 5);
            Shoot(player);
            burstLeft--;
            shotTimer = burstLeft > 0 ? 0.14f : Random.Range(0.7f, 1.3f);
        }

        void Shoot(PlayerController player)
        {
            if (gun == null) return;
            Vector3 muzzle = gun.position + gun.forward * 0.28f;
            Vector3 target = player.ChestPosition;
            float distance = Vector3.Distance(muzzle, target);
            float chance = accuracy - distance * 0.012f - Mathf.Clamp01(player.HorizontalSpeed / 6f) * 0.15f;
            bool onTarget = Random.value < Mathf.Clamp(chance, 0.06f, 0.85f);
            Vector3 aimPoint = onTarget ? target : target + Random.onUnitSphere * Random.Range(0.6f, 1.4f);
            Vector3 direction = (aimPoint - muzzle).normalized;
            Vector3 end = muzzle + direction * 80f;

            RaycastHit hit;
            if (Physics.Raycast(muzzle, direction, out hit, 80f, ~0, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var victim = hit.collider.GetComponent<PlayerController>();
                if (victim != null) victim.TakeDamage(Random.Range(7f, 12f), transform.position);
                else if (hit.collider.GetComponent<Hitbox>() == null)
                    Effects.Burst(hit.point, hit.normal, new Color(0.7f, 0.68f, 0.62f), 4, 2.5f, 0.035f);
            }

            Effects.Tracer(muzzle, end, new Color(1f, 0.55f, 0.25f));
            Effects.Flash(muzzle, new Color(1f, 0.7f, 0.4f), 2f, 6f, 0.06f);
            Sfx.PlayAt(Sfx.Pistol, muzzle, 0.8f, 5f, Random.Range(0.9f, 1.05f));
        }

        public void HearNoise(Vector3 position)
        {
            if (Current != State.Idle && Current != State.Alert) return;
            Current = State.Alert;
            stateTimer = 8f;
            lastKnownPosition = position;
        }

        // The officer yelled "Police! Drop your weapon!". Surprised, stunned or
        // wounded suspects are much more likely to give up.
        public void HearShout(PlayerController player)
        {
            if (Current == State.Dead || Current == State.Surrendered || Current == State.Arrested) return;
            if (Time.time < nextComplianceCheck) return;
            nextComplianceCheck = Time.time + 1f;

            float chance = 0.12f + compliance;
            if (Current == State.Stunned) chance += 0.65f;
            if (!hasSeenPlayer || Time.time < surprisedUntil) chance += 0.3f;
            if (health < 100f) chance += 0.25f + 0.3f * (1f - health / 100f);

            if (Random.value < chance)
            {
                Surrender();
            }
            else if (Current != State.Stunned)
            {
                lastKnownPosition = player.transform.position;
                if (Current != State.Engaging)
                {
                    Current = State.Alert;
                    stateTimer = 8f;
                }
            }
        }

        public void Stun(float duration, Vector3 source)
        {
            if (Current == State.Dead || Current == State.Surrendered || Current == State.Arrested) return;
            Current = State.Stunned;
            stateTimer = duration;
            burstLeft = 0;
            lastKnownPosition = source;
        }

        void Surrender()
        {
            Current = State.Surrendered;
            DropGun();
            GameManager.Instance.OnSuspectSurrendered(this);
        }

        public void Interact(PlayerController player)
        {
            if (Current != State.Surrendered) return;
            Current = State.Arrested;
            player.Play(Sfx.Click, 0.8f);
            Shapes.Box("Zip Cuffs", transform, new Vector3(0f, 0.86f, -0.26f), new Vector3(0.22f, 0.05f, 0.05f), Color.white, false);
            GameManager.Instance.OnSuspectArrested(this);
        }

        public void TakeHit(float damage, Vector3 point, Vector3 direction, bool fromPlayer)
        {
            if (!fromPlayer || Current == State.Dead) return;
            var game = GameManager.Instance;
            if (Current == State.Surrendered || Current == State.Arrested) game.OnUnauthorizedForce();

            health -= damage;
            if (health <= 0f)
            {
                Die();
                return;
            }

            staggerTimer = 0.35f;
            if (Current == State.Idle || Current == State.Alert)
            {
                hasSeenPlayer = true;
                lastKnownPosition = game.Player.transform.position;
                reactionTimer = 0.3f;
                Current = State.Engaging;
            }
        }

        void Die()
        {
            Current = State.Dead;
            DropGun();
            foreach (var hitbox in parts.hitboxes)
            {
                var col = hitbox.GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
            deathProgress = 0f;
            deathStartRotation = transform.rotation;
            deathStartPosition = transform.position;
            GameManager.Instance.OnSuspectKilled(this);
        }

        void DropGun()
        {
            if (gun == null) return;
            gun.SetParent(transform.parent, true);
            gun.gameObject.AddComponent<BoxCollider>();
            var body = gun.gameObject.AddComponent<Rigidbody>();
            body.mass = 2f;
            body.AddForce(transform.forward * 1.5f + Vector3.up, ForceMode.VelocityChange);
            body.AddTorque(Random.insideUnitSphere * 3f, ForceMode.VelocityChange);
            gun = null;
        }

        void AnimateArms(float dt)
        {
            Quaternion left, right;
            switch (Current)
            {
                case State.Dead:
                    return;
                case State.Surrendered:
                    left = Quaternion.Euler(180f, 0f, -20f);
                    right = Quaternion.Euler(180f, 0f, 20f);
                    break;
                case State.Arrested:
                    left = Quaternion.Euler(25f, 0f, 15f);
                    right = Quaternion.Euler(25f, 0f, -15f);
                    break;
                case State.Stunned:
                    left = Quaternion.Euler(-150f, 0f, 25f);
                    right = Quaternion.Euler(-150f, 0f, -25f);
                    break;
                default:
                    left = Quaternion.Euler(-80f, 0f, 25f);
                    right = Quaternion.Euler(-85f, 0f, -10f);
                    break;
            }
            float blend = 1f - Mathf.Exp(-10f * dt);
            parts.leftArm.localRotation = Quaternion.Slerp(parts.leftArm.localRotation, left, blend);
            parts.rightArm.localRotation = Quaternion.Slerp(parts.rightArm.localRotation, right, blend);
        }

        void AnimateDeath(float dt)
        {
            if (deathProgress >= 1f) return;
            deathProgress = Mathf.Min(1f, deathProgress + dt * 2.5f);
            float t = deathProgress * deathProgress;
            transform.rotation = Quaternion.Slerp(deathStartRotation, deathStartRotation * Quaternion.Euler(-90f, 0f, 0f), t);
            transform.position = deathStartPosition + Vector3.up * 0.15f * t;
        }

        void TurnTowards(Vector3 point, float degreesPerSecond, float dt)
        {
            Vector3 to = point - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            TurnTowardsYaw(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, degreesPerSecond, dt);
        }

        void TurnTowardsYaw(float yaw, float degreesPerSecond, float dt)
        {
            float current = Mathf.MoveTowardsAngle(transform.eulerAngles.y, yaw, degreesPerSecond * dt);
            transform.rotation = Quaternion.Euler(0f, current, 0f);
        }
    }
}
