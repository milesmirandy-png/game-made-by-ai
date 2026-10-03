using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    // The team leader you control: WASD movement relative to the screen,
    // facing the mouse, sprint with stamina, crouch, steady aim, flashlight and
    // the selected officer's role ability. Health, weapons and interaction are
    // separate components.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour, ICombatTarget
    {
        [SerializeField] float walkSpeed = 4.3f;
        [SerializeField] float sprintSpeed = 6.8f;
        [SerializeField] float crouchMultiplier = 0.55f;
        [SerializeField] float steadyAimMultiplier = 0.6f;
        [SerializeField] float aimHeight = 1.1f;
        [SerializeField] float maxStamina = 100f;
        [SerializeField] float staminaDrain = 16f;
        [SerializeField] float staminaRegen = 14f;

        public OfficerData Officer { get; private set; }
        public OfficerLoadout Loadout { get; private set; }
        public PlayerHealth Health { get; private set; }
        public WeaponController Weapons { get; private set; }
        public PlayerInteraction Interaction { get; private set; }
        public FlashlightController Flashlight { get; private set; }
        public CharacterParts Parts { get; private set; }
        public ProceduralAnimator Animator { get; private set; }

        public Vector3 Position { get { return transform.position; } }
        public Vector3 ChestPosition { get { return transform.position + Vector3.up * (IsCrouched ? 0.85f : 1.2f); } }
        public Vector3 AimPoint { get; private set; }
        public Vector3 AimDirection { get; private set; }
        public bool IsMoving { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsCrouched { get; private set; }
        public bool IsSteadyAiming { get; private set; }
        public float Stamina { get; private set; }
        public float StaminaFraction { get { return Stamina / maxStamina; } }
        public float AbilityReadyIn { get { return Mathf.Max(0f, abilityReadyAt - Time.time); } }

        // ICombatTarget
        public Transform Transform { get { return transform; } }
        public bool IsAlive { get { return Health.IsAlive; } }
        public bool FlashlightOn { get { return Flashlight.On; } }
        public IDamageable Damageable { get { return Health; } }

        CharacterController controller;
        float verticalVelocity, stepTimer, abilityReadyAt, treatUntil;
        bool exhausted, sprintLatch;
        Vector3 aimTarget;

        public static PlayerController Spawn(Transform parent, Vector3 position, float yaw, OfficerData officer, OfficerLoadout loadout, System.Collections.Generic.List<EquipmentCount> bonus)
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
            player.Stamina = player.maxStamina;
            player.AimDirection = go.transform.forward;
            player.AimPoint = player.aimTarget = position + go.transform.forward * 3f;

            var armor = GameData.Armor(loadout.armorId);
            player.Parts = CharacterFactory.Build(go.transform, OfficerAppearance(officer, loadout, true));
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

            Shapes.SetLayer(go, Layers.Characters);
            return player;
        }

        public static Appearance OfficerAppearance(OfficerData officer, OfficerLoadout loadout, bool isPlayer)
        {
            var armor = GameData.Armor(loadout.armorId);
            var uniform = Progression.Uniform(loadout.uniformIndex);
            return new Appearance
            {
                shirt = uniform,
                pants = Shapes.Shade(uniform, 0.8f),
                skin = CharacterFactory.Skin(officer.skinTone + officer.id.Length),
                headwear = armor != null && armor.helmet ? new Color(0.08f, 0.09f, 0.12f) : new Color(0.1f, 0.1f, 0.12f),
                head = armor != null && armor.helmet ? HeadStyle.Helmet : HeadStyle.Cap,
                vestOn = armor != null,
                vest = armor != null ? armor.color : Color.black,
                shield = loadout.useShield,
                ring = isPlayer ? new Color(0.3f, 0.8f, 1f) : new Color(0.2f, 0.45f, 1f),
                armed = true,
                outfit = Outfit.Tactical,
                idMarker = true,
                idColor = UITheme.RoleColor(officer.role),
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
                Move(dt);
                if (GameInput.Down(InputAction.Crouch) && !Weapons.WheelOpen) SetCrouch(!IsCrouched);
                if (GameInput.Down(InputAction.Flashlight)) Flashlight.Toggle();
                if (GameInput.Down(InputAction.Ability)) UseAbility();
            }
            else
            {
                IsMoving = IsSprinting = IsSteadyAiming = false;
            }
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
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            controller.enabled = true;
            verticalVelocity = 0f;
            AimDirection = transform.forward;
            AimPoint = aimTarget = position + transform.forward * 3f;
        }

        // Game modes: stand up again after a respawn.
        public void ResetStance()
        {
            if (IsCrouched) SetCrouch(false);
            treatUntil = 0f;
        }

        // Short "applying a bandage" pose used by the medical kit.
        public void PlayTreatAnimation(float seconds)
        {
            treatUntil = Time.time + seconds;
        }

        void Aim(float dt)
        {
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
                AimDirection = flat.normalized;
                transform.rotation = Quaternion.LookRotation(AimDirection);
            }
            if (GameInput.UsingGamepad)
            {
                // Lets the crosshair, radial menus and camera follow the stick aim.
                Vector3 viewportAim = cam.WorldToViewportPoint(AimPoint);
                GameInput.SetGamepadPointer(QualityManager.ViewportToScreen(new Vector2(viewportAim.x, viewportAim.y)));
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
            Vector2 input = GameInput.Move;
            IsMoving = input.sqrMagnitude > 0.01f;
            IsSteadyAiming = GameInput.Held(InputAction.AltAim) && !Health.Bracing;

            // On a gamepad, clicking the left stick toggles sprint until you stop moving.
            if (GameInput.UsingGamepad && GameInput.PadDown(GameInput.PadBinding(InputAction.Sprint))) sprintLatch = !sprintLatch;
            if (!IsMoving || !GameInput.UsingGamepad) sprintLatch = false;
            bool sprintInput = sprintLatch || GameInput.KeyHeld(GameInput.Binding(InputAction.Sprint));
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
            controller.Move(new Vector3(input.x * speed, verticalVelocity, input.y * speed) * dt);

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
