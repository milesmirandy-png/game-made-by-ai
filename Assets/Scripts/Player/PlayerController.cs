using UnityEngine;

namespace Swat
{
    // The SWAT operator: WASD movement, aiming at the mouse cursor and footsteps.
    // Health, weapons and interaction live in their own components.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] float walkSpeed = 4.2f;
        [SerializeField] float sprintSpeed = 6.6f;
        [SerializeField, Tooltip("Height of the plane the mouse aims on (chest height)")] float aimHeight = 1.1f;

        public PlayerHealth Health { get; private set; }
        public WeaponController Weapons { get; private set; }
        public PlayerInteraction Interaction { get; private set; }
        public CharacterParts Parts { get; private set; }
        public Vector3 Position { get { return transform.position; } }
        public Vector3 ChestPosition { get { return transform.position + Vector3.up * 1.2f; } }
        public Vector3 AimPoint { get; private set; }
        public Vector3 AimDirection { get; private set; }
        public bool IsMoving { get; private set; }
        public bool IsSprinting { get; private set; }

        CharacterController controller;
        float verticalVelocity;
        float stepTimer;

        public static PlayerController Spawn(Transform parent, Vector3 position, float yaw)
        {
            var go = new GameObject("SWAT Operator");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;

            var player = go.AddComponent<PlayerController>();
            player.controller = controller;
            player.AimDirection = go.transform.forward;
            player.AimPoint = position + go.transform.forward * 3f;
            player.Health = go.AddComponent<PlayerHealth>();
            player.Interaction = go.AddComponent<PlayerInteraction>();
            player.Weapons = go.AddComponent<WeaponController>();

            var navy = new Color(0.12f, 0.16f, 0.26f);
            player.Parts = CharacterFactory.Build(go.transform, navy, new Color(0.1f, 0.12f, 0.18f), CharacterFactory.RandomSkin(),
                new Color(0.08f, 0.09f, 0.12f), new Color(0.2f, 0.55f, 1f), true, true);
            Shapes.Box("Vest", player.Parts.model, new Vector3(0f, 1.12f, 0f), new Vector3(0.58f, 0.45f, 0.36f), new Color(0.07f, 0.08f, 0.1f), false);

            Shapes.SetLayer(go, Layers.Characters);
            player.Weapons.Init(player, player.Parts);
            return player;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var game = GameManager.Instance;
            bool active = game != null && game.AcceptsInput && Health.IsAlive;
            if (active)
            {
                Aim();
                Move(dt);
            }
            else
            {
                IsMoving = false;
                IsSprinting = false;
            }
            Weapons.Tick(dt, active);
            Interaction.Tick(active);

            // Small bob while walking.
            float bob = IsMoving ? Mathf.Abs(Mathf.Sin(Time.time * (IsSprinting ? 14f : 10f))) * 0.05f : 0f;
            if (Health.IsAlive) Parts.model.localPosition = new Vector3(0f, bob, 0f);
        }

        void Aim()
        {
            var cam = GameManager.Instance.CameraRig.Cam;
            Vector2 mouse = GameInput.MousePosition;
            // Viewport coordinates keep aiming correct when the world is drawn at reduced resolution.
            Ray ray = cam.ViewportPointToRay(new Vector3(mouse.x / Screen.width, mouse.y / Screen.height, 0f));
            var plane = new Plane(Vector3.up, new Vector3(0f, aimHeight, 0f));
            float enter;
            if (plane.Raycast(ray, out enter)) AimPoint = ray.GetPoint(enter);

            Vector3 flat = AimPoint - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.04f)
            {
                AimDirection = flat.normalized;
                transform.rotation = Quaternion.LookRotation(AimDirection);
            }
        }

        void Move(float dt)
        {
            Vector2 input = GameInput.Move;
            IsMoving = input.sqrMagnitude > 0.01f;
            IsSprinting = IsMoving && GameInput.Sprint;
            float speed = (IsSprinting ? sprintSpeed : walkSpeed) * Weapons.MoveSpeedMultiplier;

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += Physics.gravity.y * dt;
            var velocity = new Vector3(input.x * speed, verticalVelocity, input.y * speed);
            controller.Move(velocity * dt);

            if (!IsMoving) return;
            stepTimer -= dt;
            if (stepTimer <= 0f)
            {
                stepTimer = IsSprinting ? 0.3f : 0.45f;
                AudioManager.Play2D(Sound.Footstep, IsSprinting ? 0.3f : 0.18f, Random.Range(0.9f, 1.1f));
                // Sprinting is loud; walking lets you sneak up on people.
                Noise.Emit(transform.position, IsSprinting ? 7f : 2.5f, NoiseKind.Footstep);
            }
        }
    }
}
