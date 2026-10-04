using UnityEngine;

namespace Swat
{
    // Someone in an online match whose movement comes over the network:
    //   on the host, a stand-in for another player (a "proxy": bots see it,
    //   shoot it, and it carries flags; hits on it are sent to that player);
    //   on the other players' screens, everyone else (a "puppet", including
    //   the host's bots; hits on it are sent to the host).
    // Positions arrive 15-20 times a second and are drawn a tenth of a second
    // late, blending between the two updates around that moment, so movement
    // looks smooth even when packets arrive unevenly.
    public class NetActor : MonoBehaviour, IVersusMember, IDamageable
    {
        const float InterpolationDelay = 0.1f;
        const float MaxExtrapolation = 0.25f;

        public int NetId { get; private set; }
        public int Side { get; private set; }
        public string Callsign { get; private set; }
        public bool IsProxy { get; private set; }
        public bool IsHuman { get; private set; }
        public NetPlayer Owner { get; private set; }   // proxy: the player it stands for
        // Proxy: that player has loaded the map (until then bots ignore it). A moment of protection follows.
        public bool Ready
        {
            get { return ready; }
            set
            {
                if (value && !ready) ProtectedUntil = Time.time + 2f;
                ready = value;
            }
        }
        bool ready;
        public bool Down { get; private set; }
        public WeaponData Weapon { get; private set; }
        public CharacterParts Parts { get; private set; }
        public float Health { get; set; }
        public float MaxHealth { get { return 100f; } }
        public float RespawnAt { get; set; }
        public float ProtectedUntil { get; set; }
        public float LastShotTime { get; private set; }
        public bool Seen { get; private set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Captures { get; set; }

        // ICombatTarget / IDamageable
        public Transform Transform { get { return transform; } }
        public Vector3 Position { get { return transform.position; } }
        public Vector3 ChestPosition { get { return transform.position + transform.right * leanShown + Vector3.up * (IsCrouched ? 0.85f : 1.2f); } }
        public bool IsAlive { get { return !Down && (!IsProxy || Ready); } }
        public bool IsMoving { get; private set; }
        public bool IsCrouched { get; private set; }
        public bool FlashlightOn { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsSliding { get; private set; }
        public bool Peeking { get; private set; }
        public float Lean { get; private set; }         // -1 left .. +1 right, as last reported
        public IDamageable Damageable { get { return this; } }
        public Team Team { get { return Side == 0 ? Team.Police : Team.Suspect; } }

        struct Sample
        {
            public float time;      // sender's clock
            public Vector3 position;
            public float yaw;
        }

        readonly Sample[] samples = new Sample[24];
        int count;
        float clockOffset;           // local time minus sender time, for the fastest packets seen
        bool hasClock;
        CapsuleCollider body;
        ProceduralAnimator animator;
        Vector3 lastDrawn;
        float speed;
        bool visible = true;
        float leanShown;             // meters the upper body is out to the side right now (eases toward Lean)
        CapsuleCollider leanBox;

        public static NetActor CreateProxy(Transform parent, int id, NetPlayer owner, Vector3 position, float yaw)
        {
            var actor = Create(parent, id, owner.side, owner.name, true, LookFor(owner.side, owner.officerId, owner.skin), GameData.Weapon(owner.weaponId) ?? GameData.Weapon("rifle_compact"), position, yaw);
            actor.IsProxy = true;
            actor.Owner = owner;
            actor.ProtectedUntil = Time.time + 2f;
            // Bots steer around the player's stand-in as they do around you.
            var obstacle = actor.gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule;
            obstacle.radius = 0.4f;
            obstacle.height = 1.8f;
            obstacle.center = new Vector3(0f, 0.9f, 0f);
            obstacle.carving = false;
            return actor;
        }

        public static NetActor CreatePuppet(Transform parent, RosterEntry entry)
        {
            return Create(parent, entry.id, entry.side, entry.name, entry.human, LookFor(entry.side, entry.officerId, entry.skin), VersusMatch.WeaponAt(entry.weapon) ?? GameData.Weapon("rifle_compact"), entry.position, entry.yaw);
        }

        static NetActor Create(Transform parent, int id, int side, string callsign, bool human, Appearance look, WeaponData weapon, Vector3 position, float yaw)
        {
            var go = new GameObject((side == 0 ? "Blue " : "Red ") + callsign + " (online)");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var actor = go.AddComponent<NetActor>();
            actor.NetId = id;
            actor.Side = side;
            actor.Callsign = callsign;
            actor.IsHuman = human;
            actor.Health = 100f;
            actor.LastShotTime = -10f;
            actor.body = go.AddComponent<CapsuleCollider>();
            actor.body.center = new Vector3(0f, 0.9f, 0f);
            actor.body.height = 1.8f;
            actor.body.radius = 0.35f;
            actor.leanBox = LeanHitbox.Create(go.transform, actor.body);
            actor.Parts = CharacterFactory.Build(go.transform, look);
            actor.Weapon = weapon;
            CharacterFactory.SetWeapon(actor.Parts, weapon, null);
            actor.animator = new ProceduralAnimator(actor.Parts);
            actor.animator.SetPose(Pose.Aim);
            actor.lastDrawn = position;
            Shapes.SetLayer(go, Layers.Characters);
            return actor;
        }

        // Squadmates and players keep their officer's face and kit (in red for the Red Team); others get the team look.
        public static Appearance LookFor(int side, string officerId, Color skin)
        {
            var officer = string.IsNullOrEmpty(officerId) ? null : GameData.Officer(officerId);
            if (officer != null)
            {
                var look = PlayerController.OfficerAppearance(officer, GameData.LoadoutFor(officer), false);
                look.shield = false;
                return VersusMatch.TeamColours(look, side);
            }
            if (skin.a <= 0f) skin = CharacterFactory.Skin(side * 3 + 1);
            return side == 0 ? VersusMatch.BlueLook(skin) : VersusMatch.RedLook(skin);
        }

        // ---- Movement updates ----

        // A position report stamped with the sender's clock.
        public void Push(float senderTime, Vector3 position, float yaw, bool moving, bool running, bool crouched, bool flashlight)
        {
            float offset = Time.time - senderTime;
            // The smallest offset is the quickest delivery; let it drift up slowly so clock drift can't stick.
            clockOffset = hasClock ? Mathf.Min(offset, clockOffset + Time.deltaTime * 0.02f) : offset;
            hasClock = true;
            if (count > 0 && senderTime <= samples[count - 1].time) return; // late or repeated
            if (count == samples.Length)
            {
                System.Array.Copy(samples, 1, samples, 0, count - 1);
                count--;
            }
            samples[count++] = new Sample { time = senderTime, position = position, yaw = yaw };
            IsMoving = moving;
            IsRunning = running;
            if (crouched != IsCrouched)
            {
                IsCrouched = crouched;
                animator.SetCrouch(crouched);
            }
            FlashlightOn = flashlight;
        }

        // Sliding and peeking, from the same updates.
        public void SetMoves(bool sliding, bool peeking, float lean)
        {
            if (Down) sliding = peeking = false;
            if (sliding != IsSliding)
            {
                IsSliding = sliding;
                animator.SetSlide(sliding);
                if (sliding && visible) AudioManager.Play(Sound.SlideScrape, transform.position, 0.45f, Random.Range(0.92f, 1.08f));
            }
            Peeking = peeking;
            Lean = peeking ? Mathf.Clamp(lean, -1f, 1f) : 0f;
        }

        // Jumps straight to a place (respawns): no blending from where it was.
        public void Place(Vector3 position, float yaw)
        {
            count = 0;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            lastDrawn = position;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (count > 0)
            {
                float renderTime = Time.time - clockOffset - InterpolationDelay;
                Vector3 position;
                float yaw;
                SampleAt(renderTime, out position, out yaw);
                transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            }
            float moved = (transform.position - lastDrawn).magnitude / dt;
            lastDrawn = transform.position;
            speed = Mathf.Lerp(speed, moved > 12f ? 0f : moved, 1f - Mathf.Exp(-12f * dt));
            leanShown = Mathf.MoveTowards(leanShown, Down ? 0f : Lean * PlayerController.LeanReach, 3.6f * dt);
            animator.SetLean(leanShown / PlayerController.LeanReach);
            LeanHitbox.Place(leanBox, Down ? 0f : leanShown, IsCrouched);
            animator.Tick(dt, Down ? 0f : speed, IsRunning);
        }

        void SampleAt(float time, out Vector3 position, out float yaw)
        {
            if (time <= samples[0].time || count == 1)
            {
                position = samples[0].position;
                yaw = samples[0].yaw;
                return;
            }
            for (int i = 1; i < count; i++)
            {
                if (samples[i].time < time) continue;
                var a = samples[i - 1];
                var b = samples[i];
                // A long jump between updates is a respawn: no sliding across the map.
                if ((b.position - a.position).sqrMagnitude > 16f)
                {
                    position = b.position;
                    yaw = b.yaw;
                    return;
                }
                float t = Mathf.InverseLerp(a.time, b.time, time);
                position = Vector3.Lerp(a.position, b.position, t);
                yaw = Mathf.LerpAngle(a.yaw, b.yaw, t);
                return;
            }
            // Past the newest update: keep going the same way for a moment, then hold.
            var last = samples[count - 1];
            var previous = samples[count - 2];
            float span = Mathf.Max(0.01f, last.time - previous.time);
            float ahead = Mathf.Min(time - last.time, MaxExtrapolation);
            Vector3 velocity = (last.position - previous.position) / span;
            position = velocity.sqrMagnitude < 100f ? last.position + velocity * ahead : last.position;
            yaw = last.yaw;
        }

        public void SetWeapon(WeaponData weapon)
        {
            if (weapon == null || weapon == Weapon) return;
            Weapon = weapon;
            CharacterFactory.SetWeapon(Parts, weapon, null);
            if (!visible) Parts.SetVisible(false);
        }

        // ---- Tagged out and back ----

        public void SetDown(bool down)
        {
            if (down == Down) return;
            Down = down;
            body.enabled = !down;
            if (down) SetMoves(false, false, 0f);
            animator.SetDown(down);
            if (down) Health = 0f;
            else Health = MaxHealth;
        }

        // Host: back in at a spawn point.
        public void Revive(Vector3 position, float yaw)
        {
            SetDown(false);
            RespawnAt = 0f;
            ProtectedUntil = Time.time + 1.5f;
            Place(position, yaw);
        }

        // ---- Shots ----

        // Another player's (or a bot's) shot: flash, sound and tracers only; hits are settled by whoever fired.
        public void ShowShot(WeaponData weapon, Vector3[] ends, int count)
        {
            if (weapon == null) weapon = Weapon;
            LastShotTime = Time.time;
            if (weapon != Weapon) SetWeapon(weapon);
            Vector3 muzzle = Parts.muzzle != null ? Parts.muzzle.position : ChestPosition;
            float width = weapon != null ? weapon.tracerWidth : 0.04f;
            Color tracer = weapon != null ? weapon.tracerColor : new Color(1f, 0.85f, 0.5f);
            for (int i = 0; i < count; i++) EffectsManager.Instance.SpawnTracer(muzzle, ends[i], tracer, width, weapon != null && weapon.blastRadius > 0f ? 0.09f : 0.06f);
            if (weapon != null && weapon.blastRadius > 0f && count > 0) WeaponEffects.BlastVisual(ends[0], weapon.blastRadius);
            animator.Fire(weapon != null ? Mathf.Clamp(0.45f + weapon.kick * 0.45f, 0.4f, 1.5f) : 0.6f);
            if (visible) WeaponEffects.Fired(Parts.muzzle, weapon, 0.75f, 20f, Side == 0 ? NoiseKind.Gunshot : NoiseKind.EnemyGunshot, Side == 0 ? 1f : 0.95f);
            if (weapon != null && weapon.ejectsShells && visible) WeaponEffects.EjectShell(Parts.gunRoot.position, transform.right, weapon.category == WeaponCategory.Shotgun || weapon.category == WeaponCategory.AutoShotgun);
        }

        // ---- Being hit ----

        public void TakeDamage(DamageInfo info)
        {
            if (!IsAlive || Time.time < ProtectedUntil) return;
            var match = VersusMatch.Instance;
            if (match == null || NetSession.Instance == null) return;
            int attackerSide, attackerId;
            Attacker(match, info, out attackerSide, out attackerId);
            if (attackerSide == Side) return; // no friendly fire
            animator.Hit(info.direction);
            if (IsProxy) NetSession.Instance.SendHitToOwner(this, attackerId, info);   // host: tell the player they were hit
            else NetSession.Instance.SendHitToHost(this, info);                          // client: tell the host you hit them
        }

        static void Attacker(VersusMatch match, DamageInfo info, out int side, out int id)
        {
            var member = info.shooter as IVersusMember;
            if (member != null)
            {
                side = member.Side;
                id = member.NetId;
                return;
            }
            if (info.byPlayer || info.shooter is PlayerController)
            {
                side = match.MySide;
                id = match.MyId;
                return;
            }
            side = info.attacker == Team.Police ? 0 : 1;
            id = -1;
        }

        // ---- Fog of war ----

        public void SetSeen(bool seen)
        {
            Seen = seen;
            ApplyVisible(seen || Down);
        }

        public void SetVisible(bool show)
        {
            ApplyVisible(show);
        }

        void ApplyVisible(bool show)
        {
            if (show == visible) return;
            visible = show;
            Parts.SetVisible(show);
        }
    }
}
