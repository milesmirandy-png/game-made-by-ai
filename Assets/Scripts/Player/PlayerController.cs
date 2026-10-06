using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    // The team leader you control: WASD movement relative to the screen,
    // facing the mouse, sprint with stamina, crouch, slide (crouch while
    // sprinting), peek / lean around corners and door frames (hold Ctrl),
    // steady aim, flashlight and the selected officer's role ability. Health,
    // weapons and interaction are separate components.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour, ICombatTarget
    {
        // A loaded-up officer: deliberate walking pace, a short sprint, and momentum both ways.
        [SerializeField] float walkSpeed = 3.3f;
        [SerializeField] float sprintSpeed = 5.3f;
        [SerializeField] float crouchMultiplier = 0.5f;
        [SerializeField] float steadyAimMultiplier = 0.55f;
        [SerializeField] float acceleration = 9f;      // m/s per second speeding up
        [SerializeField] float deceleration = 13f;     // and slowing down
        [SerializeField] float aimHeight = 1.1f;
        [SerializeField] float maxStamina = 100f;
        [SerializeField] float staminaDrain = 22f;
        [SerializeField] float staminaRegen = 10f;
        public const float LeanReach = 0.55f;           // how far your upper body moves out when you peek
        [SerializeField] float leanDistance = LeanReach;
        [SerializeField] float slideSpeed = 8f;
        [SerializeField] float slideTime = 0.55f;
        const float SlideStamina = 22f;

        public OfficerData Officer { get; private set; }
        public OfficerLoadout Loadout { get; private set; }
        public PlayerHealth Health { get; private set; }
        public WeaponController Weapons { get; private set; }
        public PlayerInteraction Interaction { get; private set; }
        public FlashlightController Flashlight { get; private set; }
        public CharacterParts Parts { get; private set; }
        public ProceduralAnimator Animator { get; private set; }

        public Vector3 Position { get { return transform.position; } }
        // Peeking moves your chest and eyes out to the side: you see, shoot and are seen from there.
        public Vector3 ChestPosition { get { return transform.position + LeanOffset + Vector3.up * (IsCrouched ? 0.85f : 1.2f); } }
        public Vector3 EyePosition { get { return transform.position + LeanOffset + Vector3.up * (IsCrouched ? 1.05f : 1.6f); } }
        public Vector3 LeanOffset { get { return transform.right * leanMeters; } }
        public float Lean { get { return leanDistance > 0f ? leanMeters / leanDistance : 0f; } } // -1 left .. +1 right
        public bool Peeking { get; private set; }
        public bool IsSliding { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public Vector3 AimDirection { get; private set; }       // flat: where you face
        // First person: where you look (with pitch), and where shots start and go.
        public Vector3 LookDirection { get; private set; }
        public float LookPitch { get { return lookPitch; } }
        public bool FirstPerson { get { return ViewMode.FirstPerson && FirstPersonRig.Active; } }
        public Vector3 ShotOrigin { get { return FirstPerson ? FirstPersonRig.CameraPosition : ChestPosition; } }
        public Vector3 ShotDirection { get { return FirstPerson ? LookDirection : AimDirection; } }
        // The gun the shot, flash and shells come from: the view model's in first person.
        public Transform Muzzle { get { return FirstPerson && FirstPersonRig.Muzzle != null ? FirstPersonRig.Muzzle : Parts.muzzle; } }
        public Transform GunRoot { get { return FirstPerson && FirstPersonRig.Gun != null ? FirstPersonRig.Gun : Parts.gunRoot; } }
        public bool IsMoving { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsCrouched { get; private set; }
        public bool IsSteadyAiming { get; private set; }
        public float Stamina { get; private set; }
        public float StaminaFraction { get { return Stamina / maxStamina; } }
        public float AbilityReadyIn { get { return Mathf.Max(0f, abilityReadyAt - Time.time); } }
        public float LastMeleeTime { get; private set; } = -10f;
        public int VersusSide { get; private set; }       // the game modes: 0 blue, 1 red

        // ICombatTarget
        public Transform Transform { get { return transform; } }
        public bool IsAlive { get { return Health.IsAlive; } }
        public bool FlashlightOn { get { return Flashlight.On; } }
        public IDamageable Damageable { get { return Health; } }

        CharacterController controller;
        float verticalVelocity, stepTimer, abilityReadyAt, treatUntil;
        bool exhausted, sprintLatch;
        Vector3 aimTarget;
        float leanMeters;
        int leanSide;               // the side chosen while the peek key is held (-1 left, +1 right)
        float slideStart, slideReadyAt, nextSlideDust;
        Vector3 slideVelocity, moveVelocity;
        float lookYaw, lookPitch, recoilDebt, lastKickTime;
        CapsuleCollider leanBox;
        float meleeReadyAt;
        static readonly Collider[] meleeHits = new Collider[16];

        public static PlayerController Spawn(Transform parent, Vector3 position, float yaw, OfficerData officer, OfficerLoadout loadout, System.Collections.Generic.List<EquipmentCount> bonus, int versusSide = 0)
        {
            var go = new GameObject("Team Leader (" + officer.displayName + ")");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;

            // Lets squadmates' NavMesh avoidance steer around the player.
            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = 0.4f;
            obstacle.height = 1.8f;
            obstacle.center = new Vector3(0f, 0.9f, 0f);
            obstacle.carving = false;

            var player = go.AddComponent<PlayerController>();
            player.controller = controller;
            player.Officer = officer;
            player.Loadout = loadout;
            player.VersusSide = versusSide;
            player.Stamina = player.maxStamina;
            player.AimDirection = player.LookDirection = go.transform.forward;
            player.lookYaw = yaw;
            player.AimPoint = player.aimTarget = position + go.transform.forward * 3f;

            var armor = GameData.Armor(loadout.armorId);
            // Online on the Red Team, your officer wears red.
            player.Parts = CharacterFactory.Build(go.transform, VersusMatch.TeamColours(OfficerAppearance(officer, loadout, true), versusSide));
            player.Animator = new ProceduralAnimator(player.Parts);
            player.Animator.SetPose(Pose.Aim);

            player.Health = go.AddComponent<PlayerHealth>();
            player.Health.Init(officer.maxHealth, armor, officer.armorRating, loadout.useShield);
            player.Interaction = go.AddComponent<PlayerInteraction>();
            player.Weapons = go.AddComponent<WeaponController>();
            var inventory = new WeaponInventory(loadout, bonus);
            player.Weapons.Init(player, inventory);

            player.Flashlight = go.AddComponent<FlashlightController>();
            float lightRange = 13f * inventory.Primary.LightRangeMultiplierOrOne();
            player.Flashlight.Init(CharacterFactory.AddFlashlight(player.Parts, lightRange), lightRange, true, true);
            player.leanBox = LeanHitbox.Create(go.transform, controller);

            Shapes.SetLayer(go, Layers.Characters);
            return player;
        }

        public static Appearance OfficerAppearance(OfficerData officer, OfficerLoadout loadout, bool isPlayer)
        {
            var armor = GameData.Armor(loadout.armorId);
            // The uniform is a whole kit (black, greens, tans...): the soldier model wears all of it.
            var kit = Progression.Kit(loadout.uniformIndex);
            // Headgear, face and patches come from the Look tab; the vest's look from the armor.
            var style = GearCatalog.StyleFor(armor);
            bool helmet = GearCatalog.HasHelmet(loadout.headgearIndex);
            var headgear = (GearCatalog.Headgear)loadout.headgearIndex;
            return new Appearance
            {
                shirt = kit.shirt,
                pants = kit.pants,
                skin = CharacterFactory.Skin(officer.skinTone + officer.id.Length),
                headwear = kit.helmet,
                // The classic blocky figures get the nearest of their head styles.
                head = helmet ? HeadStyle.Helmet : headgear == GearCatalog.Headgear.BareHead ? HeadStyle.Hair : HeadStyle.Cap,
                hair = HairStyle.Buzz,
                headgear = loadout.headgearIndex,
                face = loadout.faceIndex,
                armorStyle = style,
                patch = loadout.patchIndex,
                patchColor = loadout.patchColorIndex,
                vestOn = style != ArmorStyle.None,
                vest = armor != null ? kit.vest : Color.black,
                pouches = kit.pouches,
                gear = kit.gear,
                shield = loadout.useShield,
                ring = isPlayer ? new Color(0.3f, 0.8f, 1f) : new Color(0.2f, 0.45f, 1f),
                armed = true,
                outfit = Outfit.Tactical,
                idMarker = true,
                idColor = UITheme.RoleColor(officer.role),
                holster = true,
                soldier = true,
            };
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var game = GameManager.Instance;
            bool active = game != null && game.AcceptsGameplayInput && Health.IsAlive;
            if (active)
            {
                // Aim holds still while the weapon wheel is open (the mouse is picking an entry).
                if (!Weapons.WheelOpen) Aim(dt);
                // Crouch while sprinting slides; otherwise it toggles crouching.
                if (GameInput.Down(InputAction.Crouch) && !Weapons.WheelOpen && !IsSliding)
                {
                    if (IsSprinting && IsMoving && Time.time >= slideReadyAt && Stamina >= SlideStamina) StartSlide();
                    else SetCrouch(!IsCrouched);
                }
                Move(dt);
                if (GameInput.Down(InputAction.Melee) && !Weapons.WheelOpen && !IsSliding) Melee();
                if (GameInput.Down(InputAction.Flashlight)) Flashlight.Toggle();
                if (GameInput.Down(InputAction.Ability)) UseAbility();
            }
            else
            {
                IsMoving = IsSprinting = IsSteadyAiming = false;
                if (IsSliding) EndSlide();
            }
            UpdateLean(dt, active);
            Weapons.Tick(dt, active);
            Interaction.Tick(active);

            if (Health.Bracing) Animator.SetPose(Pose.Shielding);
            else if (Time.time < treatUntil) Animator.SetPose(Pose.Treating);
            else if (Interaction.IsInteracting) Animator.SetPose(Pose.Interact);
            else Animator.SetPose(Pose.Aim);
            Animator.Tick(dt, IsMoving ? controller.velocity.magnitude : 0f, IsSprinting);
        }

        // Stairs between floors move the player instantly.
        public void TeleportTo(Vector3 position, float yaw)
        {
            if (IsSliding) EndSlide();
            leanMeters = 0f;
            moveVelocity = Vector3.zero;
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            controller.enabled = true;
            verticalVelocity = 0f;
            AimDirection = LookDirection = transform.forward;
            lookYaw = yaw;
            lookPitch = 0f;
            recoilDebt = 0f;
            AimPoint = aimTarget = position + transform.forward * 3f;
        }

        // Game modes: stand up again after a respawn.
        public void ResetStance()
        {
            if (IsSliding) EndSlide();
            if (IsCrouched) SetCrouch(false);
            leanMeters = 0f;
            leanSide = 0;
            Animator.SetLean(0f);
            LeanHitbox.Place(leanBox, 0f, false);
            treatUntil = 0f;
        }

        // Short "applying a bandage" pose used by the medical kit.
        public void PlayTreatAnimation(float seconds)
        {
            treatUntil = Time.time + seconds;
        }

        void Aim(float dt)
        {
            if (ViewMode.FirstPerson)
            {
                AimFirstPerson();
                return;
            }
            var cam = GameManager.Instance.CameraRig.Cam;
            var settings = SaveManager.Settings;
            if (GameInput.UsingGamepad) AimWithStick(dt, settings);
            else
            {
                Vector2 mouse = GameInput.MousePosition;
                // Viewport coordinates keep aiming correct when the world is drawn at reduced resolution.
                Vector2 viewport = QualityManager.ScreenToViewport(mouse);
                Ray ray = cam.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
                var plane = new Plane(Vector3.up, new Vector3(0f, aimHeight, 0f));
                float enter;
                if (plane.Raycast(ray, out enter)) aimTarget = ray.GetPoint(enter);
            }

            // Optional aim smoothing (0 = the aim follows the pointer exactly).
            float smoothing = Mathf.Clamp01(settings.aimSmoothing);
            AimPoint = smoothing <= 0.01f ? aimTarget : Vector3.Lerp(AimPoint, aimTarget, 1f - Mathf.Exp(-Mathf.Lerp(40f, 7f, smoothing) * dt));

            Vector3 flat = AimPoint - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.04f)
            {
                // Heavy handling: the gun swings toward the cursor at a speed set by its weight
                // (the aim laser shows where it really points). Off: it snaps to the cursor.
                Vector3 wanted = flat.normalized;
                AimDirection = settings.heavyHandling ? Vector3.RotateTowards(AimDirection, wanted, TurnRate * Mathf.Deg2Rad * dt, 0f) : wanted;
                transform.rotation = Quaternion.LookRotation(AimDirection);
            }
            if (GameInput.UsingGamepad)
            {
                // Lets the crosshair, radial menus and camera follow the stick aim.
                Vector3 viewportAim = cam.WorldToViewportPoint(AimPoint);
                GameInput.SetGamepadPointer(QualityManager.ViewportToScreen(new Vector2(viewportAim.x, viewportAim.y)));
            }
        }

        // First person: the mouse turns you and tilts the view; the weight of the gun shows as
        // view-model sway instead of a turn limit. The aim point is whatever the view centre is on.
        void AimFirstPerson()
        {
            Vector2 look = GameInput.LookDelta;
            lookYaw = Mathf.Repeat(lookYaw + look.x, 360f);
            lookPitch = Mathf.Clamp(lookPitch - look.y, -80f, 80f);
            // Recoil settles: once you stop firing, about half the climb comes back down by itself.
            if (recoilDebt > 0f && Time.time - lastKickTime > 0.12f)
            {
                float back = Mathf.Min(recoilDebt, Mathf.Max(4f, recoilDebt * 6f) * Time.deltaTime);
                recoilDebt -= back;
                lookPitch = Mathf.Clamp(lookPitch + back, -80f, 80f);
            }
            transform.rotation = Quaternion.Euler(0f, lookYaw, 0f);
            AimDirection = transform.forward;
            LookDirection = Quaternion.Euler(lookPitch, lookYaw, 0f) * Vector3.forward;
            Vector3 eye = FirstPersonRig.Active ? FirstPersonRig.CameraPosition : EyePosition;
            RaycastHit hit;
            AimPoint = aimTarget = Physics.Raycast(eye, LookDirection, out hit, 40f, Layers.ShootableMask, QueryTriggerInteraction.Ignore) ? hit.point : eye + LookDirection * 20f;
        }

        // First person recoil: the view climbs by up degrees (and wanders sideways a little).
        public void AddLookKick(float up, float side)
        {
            lookPitch = Mathf.Clamp(lookPitch - up, -80f, 80f);
            lookYaw = Mathf.Repeat(lookYaw + side, 360f);
            recoilDebt = Mathf.Min(recoilDebt + up * 0.5f, 12f);
            lastKickTime = Time.time;
        }

        // Movement keys: screen directions from above, or forward / back / strafe in first person.
        Vector3 MoveDirection(Vector2 input)
        {
            if (ViewMode.FirstPerson) return transform.right * input.x + transform.forward * input.y;
            return new Vector3(input.x, 0f, input.y);
        }

        // How fast you can swing the gun around (degrees a second): light guns quick, heavy ones slow,
        // slower still while aiming down the sights, sprinting or carrying a shield.
        public float TurnRate
        {
            get
            {
                var data = Weapons != null && Weapons.Current != null ? Weapons.Current.Data : null;
                float weight = data != null ? Mathf.InverseLerp(0.75f, 1f, data.moveSpeedMultiplier) : 1f;
                float rate = Mathf.Lerp(260f, 560f, weight);
                if (IsSteadyAiming) rate *= 0.7f;
                if (IsSprinting) rate *= 0.6f;
                if (Health.HasShield) rate *= 0.65f;
                return rate;
            }
        }

        // Twin-stick aiming: the right stick points the weapon (screen up is world +z),
        // turning at a speed set by controller sensitivity, with optional light aim assist.
        void AimWithStick(float dt, SettingsData settings)
        {
            Vector2 stick = GameInput.RightStick;
            Vector3 current = aimTarget - transform.position;
            current.y = 0f;
            if (current.sqrMagnitude < 0.01f) current = transform.forward;
            Vector3 wanted = current.normalized;
            float reach = Mathf.Max(3f, current.magnitude);
            if (stick.sqrMagnitude > 0.04f)
            {
                wanted = new Vector3(stick.x, 0f, stick.y).normalized;
                reach = Mathf.Lerp(3f, 9f, stick.magnitude);
                if (settings.controllerAimAssist) wanted = AimAssist(wanted);
            }
            else if (IsMoving)
            {
                Vector2 move = GameInput.Move;
                wanted = new Vector3(move.x, 0f, move.y).normalized;
            }
            float turn = 720f * Mathf.Clamp(settings.controllerSensitivity, 0.3f, 2f) * Mathf.Deg2Rad * dt;
            Vector3 direction = Vector3.RotateTowards(current.normalized, wanted, turn, 0f);
            aimTarget = transform.position + direction * reach;
            aimTarget.y = aimHeight;
        }

        // Nudges the stick direction onto a visible, armed suspect within a narrow cone.
        Vector3 AimAssist(Vector3 direction)
        {
            var intel = TacticalIntel.Instance;
            Vector3 best = direction;
            float bestAngle = 9f;
            foreach (var enemy in AIManager.Instance.Enemies)
            {
                if (enemy == null || !enemy.IsArmedThreat || (intel != null && !intel.IsRevealed(enemy))) continue;
                Vector3 to = enemy.Position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 16f * 16f || to.sqrMagnitude < 0.25f) continue;
                float angle = Vector3.Angle(direction, to);
                if (angle >= bestAngle) continue;
                bestAngle = angle;
                best = to.normalized;
            }
            // Full snap only when very close to the target; otherwise a partial pull.
            return bestAngle < 3f ? best : Vector3.Slerp(direction, best, 0.5f).normalized;
        }

        void Move(float dt)
        {
            if (IsSliding)
            {
                Slide(dt);
                return;
            }
            // While peeking you stay planted; the movement keys pick the side to lean to instead.
            Vector2 input = Peeking ? Vector2.zero : GameInput.Move;
            IsMoving = input.sqrMagnitude > 0.01f;
            IsSteadyAiming = GameInput.Held(InputAction.AltAim) && !Health.Bracing;

            // On a gamepad, clicking the left stick toggles sprint until you stop moving.
            if (GameInput.UsingGamepad && GameInput.PadDown(GameInput.PadBinding(InputAction.Sprint))) sprintLatch = !sprintLatch;
            if (!IsMoving || !GameInput.UsingGamepad) sprintLatch = false;
            bool sprintInput = sprintLatch || GameInput.KeyHeld(GameInput.Binding(InputAction.Sprint));
            // Sprinting from a crouch (or after a slide) stands you up.
            if (sprintInput && IsMoving && IsCrouched && !IsSteadyAiming && !Health.Bracing && Stamina > maxStamina * 0.25f) SetCrouch(false);
            bool wantsSprint = sprintInput && IsMoving && !IsCrouched && !IsSteadyAiming && !Health.Bracing;
            if (Stamina <= 0f) exhausted = true;
            if (exhausted && Stamina > maxStamina * 0.25f) exhausted = false;
            IsSprinting = wantsSprint && !exhausted;
            Stamina = Mathf.Clamp(Stamina + (IsSprinting ? -staminaDrain : staminaRegen) * dt, 0f, maxStamina);

            var armor = Health.Armor;
            float speed = (IsSprinting ? sprintSpeed : walkSpeed) * Officer.moveSpeed * Weapons.MoveSpeedMultiplier * (armor != null ? armor.speedMultiplier : 1f);
            if (IsCrouched) speed *= crouchMultiplier;
            if (IsSteadyAiming) speed *= steadyAimMultiplier;
            if (Health.HasShield) speed *= Health.Bracing ? 0.35f : 0.9f;

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += Physics.gravity.y * dt;
            // Momentum: it takes a moment to get going and to stop (instant with heavy handling off).
            Vector3 desired = MoveDirection(input) * speed;
            float rate = !SaveManager.Settings.heavyHandling ? 60f : desired.sqrMagnitude > moveVelocity.sqrMagnitude ? acceleration : deceleration;
            moveVelocity = Vector3.MoveTowards(moveVelocity, desired, rate * dt);
            controller.Move((moveVelocity + Vector3.up * verticalVelocity) * dt);
            Vector3 actual = controller.velocity;
            actual.y = 0f;
            if (actual.sqrMagnitude < moveVelocity.sqrMagnitude * 0.25f) moveVelocity = actual; // ran into something
            IsMoving = IsMoving || moveVelocity.sqrMagnitude > 0.36f;

            if (!IsMoving) return;
            MissionManager.Instance.Report(ObjectiveType.TrainingMove, 1);
            stepTimer -= dt;
            if (stepTimer > 0f) return;
            stepTimer = IsSprinting ? 0.3f : IsCrouched ? 0.6f : 0.45f;
            // The step sound matches what's underfoot (carpet, tile, concrete, grass, asphalt, metal).
            AudioManager.Play2D(AudioManager.StepFor(GroundSurface()), IsSprinting ? 0.34f : IsCrouched ? 0.1f : 0.2f, Random.Range(0.9f, 1.1f));
            // Sprinting is loud; walking and crouching let you get close quietly.
            Noise.Emit(transform.position, IsSprinting ? 7f : IsCrouched ? 1f : 2.5f, NoiseKind.Footstep);
        }

        Surface GroundSurface()
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out hit, 1f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                return SurfaceTag.Of(hit.collider);
            return Surface.Concrete;
        }

        // ---- Slide ----

        // A burst along the way you're running, low and fast, ending in a crouch.
        void StartSlide()
        {
            Vector2 input = GameInput.Move;
            Vector3 direction = MoveDirection(input);
            if (direction.sqrMagnitude < 0.01f) direction = AimDirection;
            direction.y = 0f;
            var armor = Health.Armor;
            float speed = slideSpeed * Officer.moveSpeed * (armor != null ? Mathf.Lerp(1f, armor.speedMultiplier, 0.5f) : 1f);
            slideVelocity = direction.normalized * speed;
            slideStart = Time.time;
            nextSlideDust = 0f;
            IsSliding = true;
            IsSprinting = false;
            Stamina = Mathf.Max(0f, Stamina - SlideStamina);
            SetCrouch(true);
            Animator.SetSlide(true);
            AudioManager.Play(Sound.SlideScrape, transform.position, 0.55f, Random.Range(0.92f, 1.08f));
            Noise.Emit(transform.position, 5f, NoiseKind.Footstep);
            GameManager.Instance.CameraRig.Kick(direction, 0.06f);
        }

        void Slide(float dt)
        {
            float t = (Time.time - slideStart) / slideTime;
            // Bump into something solid (or run out of slide) and it's over.
            Vector3 flat = controller.velocity;
            flat.y = 0f;
            if (t >= 1f || (t > 0.2f && flat.sqrMagnitude < 2f))
            {
                EndSlide();
                return;
            }
            // A little steering toward the keys you hold.
            Vector2 input = GameInput.Move;
            if (input.sqrMagnitude > 0.01f)
            {
                Vector3 wanted = MoveDirection(input).normalized * slideVelocity.magnitude;
                slideVelocity = Vector3.RotateTowards(slideVelocity, wanted, 1.4f * dt, 0f);
            }
            float speedScale = Mathf.Lerp(1f, 0.3f, t * t);
            IsMoving = true;
            IsSteadyAiming = false;
            Stamina = Mathf.Min(maxStamina, Stamina + staminaRegen * 0.3f * dt);
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += Physics.gravity.y * dt;
            controller.Move((slideVelocity * speedScale + Vector3.up * verticalVelocity) * dt);
            if (Time.time >= nextSlideDust)
            {
                nextSlideDust = Time.time + 0.07f;
                EffectsManager.Instance.Burst(transform.position + Vector3.up * 0.05f - slideVelocity.normalized * 0.2f, Vector3.up, new Color(0.62f, 0.6f, 0.56f), 2, 1.2f, 0.06f);
            }
        }

        void EndSlide()
        {
            IsSliding = false;
            slideReadyAt = Time.time + 1.2f;
            moveVelocity = slideVelocity * 0.3f;
            Animator.SetSlide(false);
        }

        // ---- Melee ----

        // A shove with the gun, or a bash with the shield (longer reach, longer daze): staggers whoever
        // is right in front. Suspects and civilians are dazed, not hurt; in the game modes it's a light hit.
        void Melee()
        {
            if (Time.time < meleeReadyAt) return;
            LastMeleeTime = Time.time;
            bool shield = Health.HasShield;
            meleeReadyAt = Time.time + (shield ? 0.8f : 1.1f);
            float reach = shield ? 1.4f : 1.05f;
            float daze = shield ? 3f : 2f;
            Vector3 center = Position + Vector3.up + AimDirection * (reach * 0.55f);
            Animator.Fire(1.4f);
            moveVelocity += AimDirection * 1.5f;
            GameManager.Instance.CameraRig.Kick(-AimDirection, 0.08f);
            bool landed = false;
            int count = Physics.OverlapSphereNonAlloc(center, reach * 0.65f, meleeHits, 1 << Layers.Characters, QueryTriggerInteraction.Ignore);
            var struck = new System.Collections.Generic.HashSet<Object>();
            for (int i = 0; i < count; i++)
            {
                var collider = meleeHits[i];
                if (collider == null || collider.transform.IsChildOf(transform)) continue;
                Vector3 to = collider.transform.position - Position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f && Vector3.Angle(AimDirection, to) > 75f) continue;
                var enemy = collider.GetComponentInParent<EnemyAI>();
                if (enemy != null)
                {
                    if (struck.Add(enemy) && !enemy.IsNeutralized && enemy.State != EnemyState.Surrendering)
                    {
                        enemy.Shoved(AimDirection, daze);
                        landed = true;
                    }
                    continue;
                }
                var civilian = collider.GetComponentInParent<CivilianAI>();
                if (civilian != null)
                {
                    if (struck.Add(civilian))
                    {
                        civilian.Stun(1.2f);
                        civilian.HearShout();
                        landed = true;
                    }
                    continue;
                }
                if (!VersusMatch.Active) continue;
                var target = collider.GetComponentInParent<IDamageable>();
                var member = target as IVersusMember;
                if (target == null || member == null || !target.IsAlive || member.Side == VersusMatch.Instance.MySide || !struck.Add((Object)target)) continue;
                target.TakeDamage(new DamageInfo { amount = shield ? 25f : 15f, attacker = Team.Police, byPlayer = true, shooter = this, direction = AimDirection, point = center });
                var bot = target as ArenaBot;
                if (bot != null) bot.Stun(shield ? 1.5f : 0.8f);
                landed = true;
            }
            if (landed)
            {
                AudioManager.Play(Sound.Kick, center, 0.6f, shield ? 0.75f : 1.15f);
                GameManager.Instance.CameraRig.Shake(shield ? 0.18f : 0.1f);
                Noise.Emit(Position, 4f, NoiseKind.Footstep);
            }
            else AudioManager.Play(Sound.Equip, Position, 0.4f, 1.3f);
        }

        // ---- Peek / lean ----

        // Hold Peek to lean your upper body out past a corner or door frame. On its own it leans
        // toward the open side of whatever you're aiming past; with a movement key it leans that
        // way on screen. Never into a wall: the lean stops short of anything solid.
        void UpdateLean(float dt, bool active)
        {
            bool hold = active && !IsSliding && !Health.Bracing && !Weapons.WheelOpen && GameInput.Held(InputAction.Peek);
            Peeking = hold;
            float target = 0f;
            if (hold)
            {
                Vector2 input = GameInput.Move;
                Vector3 right = transform.right;
                // First person: A and D lean left and right; from above, the key pointing that way on screen.
                float push = ViewMode.FirstPerson ? input.x : input.x * right.x + input.y * right.z;
                if (Mathf.Abs(push) > 0.3f) leanSide = push > 0f ? 1 : -1;
                else if (leanSide == 0) leanSide = AutoLeanSide();
                if (leanSide != 0) target = leanSide * Clearance(leanSide);
            }
            else leanSide = 0;
            leanMeters = Mathf.MoveTowards(leanMeters, target, dt * 3.6f);
            if (Mathf.Abs(leanMeters) > 0.001f)
            {
                // Turning while leaned can bring a wall closer: stay clear of it.
                float room = Clearance(leanMeters > 0f ? 1 : -1);
                if (Mathf.Abs(leanMeters) > room) leanMeters = Mathf.Sign(leanMeters) * room;
            }
            Animator.SetLean(Lean);
            LeanHitbox.Place(leanBox, leanMeters, IsCrouched);
        }

        // How far the upper body can move to one side before touching a wall.
        float Clearance(int side)
        {
            Vector3 origin = transform.position + Vector3.up * (IsCrouched ? 0.85f : 1.2f);
            RaycastHit hit;
            if (Physics.SphereCast(origin, 0.16f, transform.right * side, out hit, leanDistance + 0.2f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                return Mathf.Clamp(hit.distance - 0.2f, 0f, leanDistance);
            return leanDistance;
        }

        // The side that shows more of what's ahead: compare how far you could see along your aim
        // (and a little toward that side) from each leaned position.
        int AutoLeanSide()
        {
            Vector3 chest = transform.position + Vector3.up * (IsCrouched ? 1.05f : 1.6f);
            Vector3 forward = AimDirection;
            int best = 0;
            float bestScore = 0.25f;
            for (int side = -1; side <= 1; side += 2)
            {
                float reach = Clearance(side);
                if (reach < 0.2f) continue;
                Vector3 sideways = transform.right * side;
                Vector3 from = chest + sideways * reach;
                Vector3 diagonal = (forward + sideways * 0.5f).normalized;
                float gain = (ViewDistance(from, forward) - ViewDistance(chest, forward)) + 0.5f * (ViewDistance(from, diagonal) - ViewDistance(chest, diagonal));
                float score = gain + reach;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = side;
                }
            }
            return best;
        }

        static float ViewDistance(Vector3 from, Vector3 direction)
        {
            RaycastHit hit;
            return Physics.Raycast(from, direction, out hit, 14f, Layers.WorldMask, QueryTriggerInteraction.Ignore) ? hit.distance : 14f;
        }

        void SetCrouch(bool crouch)
        {
            IsCrouched = crouch;
            controller.height = crouch ? 1.2f : 1.8f;
            controller.center = new Vector3(0f, crouch ? 0.6f : 0.9f, 0f);
            Animator.SetCrouch(crouch);
        }

        void UseAbility()
        {
            if (Officer.role == OfficerRole.Shield)
            {
                Health.SetBracing(!Health.Bracing);
                UIManager.Notify(Health.Bracing ? "Shield braced" : "Shield lowered");
                AudioManager.Play(Sound.Ability, Position, 0.5f);
                return;
            }
            if (Time.time < abilityReadyAt)
            {
                UIManager.Notify(Officer.abilityName + " ready in " + Mathf.CeilToInt(AbilityReadyIn) + "s");
                return;
            }
            if (!RoleAbilities.Use(this)) return;
            abilityReadyAt = Time.time + Officer.abilityCooldown;
            AudioManager.Play(Sound.Ability, Position, 0.6f);
        }
    }

    public static class WeaponExtensions
    {
        public static float LightRangeMultiplierOrOne(this Weapon weapon)
        {
            return weapon != null ? weapon.LightRangeMultiplier : 1f;
        }
    }
}
