using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    public enum BotRole { Roam, Attack, Defend }

    // A player in the game modes, on either team. Sees opponents with the same
    // line-of-sight rules as everyone else, waits a short reaction time, then
    // strafes while it shoots, reloads when empty, and otherwise plays the
    // objective VersusMatch gives it (hunt, take or defend a flag, hold the
    // zone). Taken down means "tagged out" until it respawns at its base.
    public class ArenaBot : MonoBehaviour, ICombatTarget, IDamageable
    {
        static readonly float[] AimError = { 7f, 4.5f, 2.8f };   // degrees, by skill
        static readonly float[] Reaction = { 0.6f, 0.4f, 0.25f };  // seconds before the first shot at a new target
        const float SightRange = 26f;

        public int Side { get; private set; }                // 0 your team (blue), 1 red
        public string Callsign { get; private set; }
        public BotRole Role { get; set; }
        public Weapon Gun { get; private set; }
        public CharacterParts Parts { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public float RespawnAt { get; set; }
        public float ProtectedUntil { get; set; }
        public float LastShotTime { get; private set; }
        public DamageInfo LastHit { get; private set; }
        public float NextThink { get; set; }
        public bool Seen { get; private set; }
        public int Kills, Deaths, Captures;

        // Where VersusMatch currently wants this bot to wander (roaming, defending, holding the zone).
        public Vector3 RoamPoint;
        public float RoamUntil;

        // ICombatTarget / IDamageable
        public Transform Transform { get { return transform; } }
        public Vector3 Position { get { return transform.position; } }
        public Vector3 ChestPosition { get { return transform.position + Vector3.up * 1.2f; } }
        public bool IsAlive { get { return Health > 0f; } }
        public bool IsMoving { get { return mover != null && mover.IsMoving; } }
        public bool IsCrouched { get { return false; } }
        public bool FlashlightOn { get { return false; } }
        public IDamageable Damageable { get { return this; } }
        public Team Team { get { return Side == 0 ? Team.Police : Team.Suspect; } }

        AgentMover mover;
        CapsuleCollider body;
        ProceduralAnimator animator;
        ICombatTarget target;
        Vector3 lastKnown;
        float lastKnownTime = -100f, targetReadyAt, nextShot, reloadEnd, strafeUntil, stunUntil;
        int skill, autoLeft, burstLeft;

        public static ArenaBot Spawn(Transform parent, int side, string callsign, Appearance look, WeaponData weapon, OfficerLoadout attachments, Vector3 position, float yaw, int skill)
        {
            var go = new GameObject((side == 0 ? "Blue " : "Red ") + callsign);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var collider = go.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 0.9f, 0f);
            collider.height = 1.8f;
            collider.radius = 0.35f;
            go.AddComponent<NavMeshAgent>();

            var bot = go.AddComponent<ArenaBot>();
            bot.Side = side;
            bot.Callsign = callsign;
            bot.skill = Mathf.Clamp(skill, 0, 2);
            bot.body = collider;
            bot.mover = go.AddComponent<AgentMover>();
            bot.mover.Init(3.4f, 5.8f);
            bot.mover.Warp(position);
            bot.Parts = CharacterFactory.Build(go.transform, look);
            CharacterFactory.SetWeapon(bot.Parts, weapon, attachments);
            bot.animator = new ProceduralAnimator(bot.Parts);
            bot.animator.SetPose(Pose.Aim);
            bot.Gun = new Weapon(weapon, attachments);
            bot.MaxHealth = bot.Health = 100f;
            bot.LastShotTime = -10f;
            bot.autoLeft = Random.Range(3, 7);
            bot.burstLeft = weapon.burstCount;
            Shapes.SetLayer(go, Layers.Characters);
            return bot;
        }

        // ---- Every frame: animation, turning toward the target, shooting ----

        public void FrameUpdate(float dt)
        {
            animator.Tick(dt, IsAlive ? mover.Speed : 0f, mover.IsRunning);
            if (!IsAlive) return;
            if (reloadEnd > 0f && Time.time >= reloadEnd)
            {
                reloadEnd = 0f;
                Gun.Magazine = Gun.Data.magazineSize;
            }
            if (Time.time < stunUntil) return;
            if (target != null && target.IsAlive)
            {
                mover.Face(target.Position, 540f, dt);
                if (Time.time >= targetReadyAt) TryFire();
            }
        }

        // ---- A few times a second: what to look at and where to go ----

        public void Think(VersusMatch match)
        {
            if (!IsAlive) return;
            mover.TrackProgress();
            if (mover.IsStuck)
            {
                mover.Stop();
                RoamUntil = 0f;
            }
            mover.SetSpeedMultiplier(Time.time < stunUntil ? 0.35f : 1f);

            var seen = FindTarget(match);
            if (seen != null)
            {
                if (seen != target) targetReadyAt = Time.time + Reaction[skill] * Random.Range(0.8f, 1.3f);
                target = seen;
                lastKnown = seen.Position;
                lastKnownTime = Time.time;
                match.ShareSighting(this, seen.Position);
            }
            else if (target != null && (!target.IsAlive || Time.time - lastKnownTime > 1.2f)) target = null;

            // Carrying the enemy flag: always head home, shooting on the way.
            if (match.IsCarrying(this))
            {
                MoveTo(match.FlagHome(Side), true);
                return;
            }
            if (target != null)
            {
                Engage(match);
                return;
            }
            // Go and check where an opponent was last seen (defenders hold their post instead).
            if (Role != BotRole.Defend && Time.time - lastKnownTime < 5f && AIManager.FlatDistance(lastKnown, Position) > 2f && !match.HoldsZone(this))
            {
                MoveTo(lastKnown, true);
                return;
            }
            Vector3 point;
            bool run;
            if (match.ObjectiveFor(this, out point, out run)) MoveTo(point, run);
        }

        ICombatTarget FindTarget(VersusMatch match)
        {
            Vector3 eye = Position + Vector3.up * 1.5f;
            ICombatTarget best = null;
            float bestScore = float.MaxValue;
            foreach (var candidate in match.Opponents(Side))
            {
                if (candidate == null || !candidate.IsAlive) continue;
                float distance = Vector3.Distance(candidate.Position, Position);
                if (distance > SightRange * 1.3f) continue;
                float visibility = AIVisibility.VisibilityOf(candidate.Position, candidate.IsCrouched, candidate.FlashlightOn);
                if (!AIVisibility.CanSee(eye, transform.forward, 260f, SightRange, candidate.ChestPosition, visibility)) continue;
                // Stick with the current target unless someone is much closer.
                float score = distance - (candidate == target ? 4f : 0f);
                if (score < bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            return best;
        }

        // Fighting: close in if out of range, otherwise sidestep every second or so.
        void Engage(VersusMatch match)
        {
            float distance = Vector3.Distance(target.Position, Position);
            float range = Gun.Data.range;
            if (distance > range * 0.85f)
            {
                MoveTo(target.Position, true);
                return;
            }
            if (Time.time < strafeUntil) return;
            strafeUntil = Time.time + Random.Range(0.8f, 1.8f);
            Vector3 to = target.Position - Position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            to.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, to) * (Random.value < 0.5f ? -1f : 1f);
            float advance = distance > range * 0.5f ? 1.5f : distance < 4f ? -1.5f : 0f;
            Vector3 wanted = Position + side * Random.Range(1.5f, 3.5f) + to * advance;
            if (match.HoldsZone(this)) wanted = match.ClampToZone(wanted);
            Vector3 point;
            if (mover.RandomPointNear(wanted, 1f, out point)) mover.MoveTo(point, false);
        }

        void MoveTo(Vector3 point, bool run)
        {
            // Only re-path when the destination actually changes.
            if (!mover.HasArrived && AIManager.FlatDistance(mover.Destination, point) < 1.5f) return;
            if (AIManager.FlatDistance(Position, point) < 0.6f) return;
            mover.MoveTo(point, run);
        }

        void TryFire()
        {
            if (reloadEnd > 0f || Time.time < nextShot) return;
            if (Gun.Magazine <= 0)
            {
                reloadEnd = Time.time + Gun.Data.reloadTime;
                AudioManager.Play(Sound.MagOut, Position, 0.45f, 1f, SoundCategory.Weapons);
                return;
            }
            Vector3 to = target.Position - Position;
            to.y = 0f;
            if (Vector3.Angle(transform.forward, to) > 14f) return;
            Vector3 origin = ChestPosition;
            Vector3 aim = target.ChestPosition;
            if (!WeaponEffects.ClearShot(origin, aim, transform, Team))
            {
                nextShot = Time.time + 0.15f;
                return;
            }

            var data = Gun.Data;
            float distance = Vector3.Distance(origin, aim);
            float error = AimError[skill] * (1f + distance / 18f) * (target.IsMoving ? 1.3f : 1f) * (IsMoving ? 1.2f : 1f) + data.spread * 0.6f;
            Vector3 direction = (aim - origin).normalized;
            var damage = new DamageInfo { amount = data.damage, attacker = Team, lessLethal = data.lessLethal, stun = data.stunDuration, weapon = data, shooter = this };
            Vector3 muzzle = Parts.muzzle.position;
            for (int i = 0; i < Mathf.Max(1, data.pellets); i++)
                WeaponEffects.Shoot(origin, WeaponEffects.Scatter(direction, error), data.range, damage, muzzle, data.tracerColor);
            Gun.Magazine--;
            LastShotTime = Time.time;
            animator.Fire(Mathf.Clamp(0.45f + data.kick * 0.45f, 0.4f, 1.5f));
            WeaponEffects.Fired(Parts.muzzle, data, 0.75f, Gun.NoiseRadius, Side == 0 ? NoiseKind.Gunshot : NoiseKind.EnemyGunshot, Side == 0 ? 1f : 0.95f);
            if (data.ejectsShells) WeaponEffects.EjectShell(Parts.gunRoot.position, transform.right, data.category == WeaponCategory.Shotgun || data.category == WeaponCategory.AutoShotgun);

            // Firing rhythm: automatic weapons fire short bursts, burst weapons their burst, the rest single shots.
            float interval = 1f / Mathf.Max(0.5f, data.fireRate);
            switch (Gun.Mode)
            {
                case FireMode.FullAuto:
                    if (--autoLeft <= 0)
                    {
                        autoLeft = Random.Range(3, 7);
                        nextShot = Time.time + Random.Range(0.25f, 0.55f);
                    }
                    else nextShot = Time.time + interval;
                    break;
                case FireMode.Burst:
                    if (--burstLeft <= 0)
                    {
                        burstLeft = Mathf.Max(1, data.burstCount);
                        nextShot = Time.time + Random.Range(0.35f, 0.55f);
                    }
                    else nextShot = Time.time + interval;
                    break;
                default:
                    nextShot = Time.time + Mathf.Max(interval, 0.18f) * Random.Range(1.2f, 1.7f);
                    break;
            }
        }

        // Flashbangs: dazed for a while (can't shoot, moves slowly, loses the target).
        public void Stun(float seconds)
        {
            if (!IsAlive || seconds <= 0f) return;
            stunUntil = Mathf.Max(stunUntil, Time.time + seconds);
            target = null;
        }

        // An opponent was spotted by a teammate nearby.
        public void HearOf(Vector3 position)
        {
            if (target != null) return;
            lastKnown = position;
            lastKnownTime = Time.time;
        }

        // ---- Damage, takedown and respawn ----

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive || info.attacker == Team || info.attacker == Team.Civilian || info.attacker == Team.Environment) return;
            if (Time.time < ProtectedUntil) return;
            float amount = info.amount * 0.85f; // everyone wears the same vest in the exercises
            if (info.lessLethal && info.stun > 0f)
            {
                // Less-lethal rounds slow and stop a bot from shooting for a moment.
                stunUntil = Mathf.Max(stunUntil, Time.time + info.stun * 0.5f);
                amount = Mathf.Max(amount, 6f);
            }
            Health = Mathf.Max(0f, Health - amount);
            LastHit = info;
            if (info.shooter != null)
            {
                lastKnown = info.shooter.transform.position;
                lastKnownTime = Time.time;
            }
            if (IsAlive)
            {
                animator.Hit(info.direction);
                return;
            }
            Deaths++;
            target = null;
            mover.Disable();
            body.enabled = false;
            animator.SetDown(true);
            if (VersusMatch.Instance != null) VersusMatch.Instance.OnBotDown(this, info);
        }

        public void Respawn(Vector3 position, float yaw)
        {
            Health = MaxHealth;
            body.enabled = true;
            mover.Enable();
            mover.Warp(position);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            animator.SetDown(false);
            Gun.Refill();
            reloadEnd = 0f;
            stunUntil = 0f;
            target = null;
            lastKnownTime = -100f;
            RoamUntil = 0f;
            ProtectedUntil = Time.time + 1.5f;
            mover.SetSpeedMultiplier(1f);
        }

        // Fog of war: opponents are only drawn while your team can see them.
        public void SetSeen(bool seen)
        {
            Seen = seen;
            Parts.SetVisible(seen || !IsAlive);
        }
    }
}
