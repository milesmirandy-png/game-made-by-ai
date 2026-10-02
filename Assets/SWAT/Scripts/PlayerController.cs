using UnityEngine;

namespace Swat
{
    // The SWAT officer: first-person movement and mouse look, health, and the
    // E / F / G actions (interact, shout, flashbang).
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float walkSpeed = 4.2f;
        public float sprintSpeed = 6.8f;
        public float aimSpeed = 2.4f;
        public float lookSensitivity = 2f;
        public float maxHealth = 100f;
        public int startingFlashbangs = 3;

        const float EyeHeight = 1.65f;

        public float Health { get; private set; }
        public bool IsAlive { get { return Health > 0f; } }
        public bool IsSprinting { get; private set; }
        public int Flashbangs { get; private set; }
        public float HorizontalSpeed { get; private set; }
        public Vector3 BobOffset { get; private set; }
        public Weapon Weapon { get; private set; }
        public Camera Cam { get; private set; }
        public string InteractPrompt { get; private set; }
        public Vector3 EyePosition { get { return head.position; } }
        public Vector3 ChestPosition { get { return transform.position + Vector3.up * 1.3f; } }

        // Screen effects the HUD reads.
        public float DamageFlash { get; private set; }
        public float BlindFlash { get; private set; }
        public float HitMarker { get; private set; }
        public bool HitMarkerHead { get; private set; }
        public float DamageIndicator { get; private set; }
        public Vector3 LastDamageSource { get; private set; }

        CharacterController controller;
        Transform head;
        AudioSource audioSource;
        float yaw, pitch, verticalVelocity, bobPhase, recoilPitch, recoilYaw, shoutCooldown;

        public void Init(Camera cam)
        {
            controller = GetComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.4f;

            head = new GameObject("Head").transform;
            head.SetParent(transform, false);
            head.localPosition = new Vector3(0f, EyeHeight, 0f);

            Cam = cam;
            cam.transform.SetParent(head, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            cam.nearClipPlane = 0.02f;
            cam.fieldOfView = 70f;

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            Weapon = gameObject.AddComponent<Weapon>();
            Weapon.Init(this, cam);
        }

        public void Respawn(Vector3 position, float facing)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, facing, 0f));
            controller.enabled = true;

            yaw = facing;
            pitch = recoilPitch = recoilYaw = verticalVelocity = 0f;
            Health = maxHealth;
            Flashbangs = startingFlashbangs;
            DamageFlash = BlindFlash = DamageIndicator = HitMarker = 0f;
            head.localPosition = new Vector3(0f, EyeHeight, 0f);
            head.localRotation = Quaternion.identity;
            Weapon.ResetAmmo();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            DamageFlash = Mathf.MoveTowards(DamageFlash, 0f, dt * 1.5f);
            BlindFlash = Mathf.MoveTowards(BlindFlash, 0f, dt * 0.3f);
            DamageIndicator = Mathf.MoveTowards(DamageIndicator, 0f, dt);
            HitMarker = Mathf.MoveTowards(HitMarker, 0f, dt);
            shoutCooldown -= dt;

            if (!IsAlive)
            {
                // Slump to the floor.
                head.localPosition = Vector3.Lerp(head.localPosition, new Vector3(0f, 0.4f, 0f), dt * 3f);
                head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(-10f, 0f, 35f), dt * 3f);
            }

            var game = GameManager.Instance;
            bool active = game != null && game.AcceptsInput && IsAlive;
            if (!active)
            {
                InteractPrompt = null;
                IsSprinting = false;
                Weapon.Tick(false);
                return;
            }

            Look(dt);
            Move(dt);
            UpdateInteraction();
            if (GameInput.Shout && shoutCooldown <= 0f)
            {
                shoutCooldown = 1.2f;
                Play(Sfx.Radio, 0.5f);
                game.OnPlayerShout(this);
            }
            if (GameInput.Flashbang && Flashbangs > 0) ThrowFlashbang();
            Weapon.Tick(true);
        }

        void Look(float dt)
        {
            Vector2 look = GameInput.Look * lookSensitivity * Mathf.Lerp(1f, 0.55f, Weapon.AimAmount);
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);

            float settle = 1f - Mathf.Exp(-10f * dt);
            recoilPitch = Mathf.Lerp(recoilPitch, 0f, settle);
            recoilYaw = Mathf.Lerp(recoilYaw, 0f, settle);

            transform.rotation = Quaternion.Euler(0f, yaw + recoilYaw, 0f);
            Vector3 shake = Random.insideUnitSphere * DamageFlash * 0.03f;
            head.localPosition = new Vector3(0f, EyeHeight + BobOffset.y, 0f) + shake;
            head.localRotation = Quaternion.Euler(Mathf.Clamp(pitch - recoilPitch, -89f, 89f), 0f, 0f);
        }

        void Move(float dt)
        {
            Vector2 input = GameInput.Move;
            IsSprinting = GameInput.Sprint && input.y > 0.1f && Weapon.AimAmount < 0.1f && !GameInput.FireHeld;
            float speed = IsSprinting ? sprintSpeed : Mathf.Lerp(walkSpeed, aimSpeed, Weapon.AimAmount);

            Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;
            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += Physics.gravity.y * dt;
            move.y = verticalVelocity;
            controller.Move(move * dt);

            Vector3 velocity = controller.velocity;
            velocity.y = 0f;
            HorizontalSpeed = velocity.magnitude;
            if (HorizontalSpeed > 0.1f) bobPhase += dt * (4f + HorizontalSpeed * 1.4f);
            float amount = Mathf.Clamp01(HorizontalSpeed / sprintSpeed);
            BobOffset = new Vector3(Mathf.Cos(bobPhase) * 0.015f, Mathf.Abs(Mathf.Sin(bobPhase)) * 0.03f - 0.015f, 0f) * amount;
        }

        void UpdateInteraction()
        {
            IInteractable target = FindInteractable();
            InteractPrompt = target != null ? target.Prompt : null;
            if (target != null && GameInput.Interact) target.Interact(this);
        }

        IInteractable FindInteractable()
        {
            var hits = Physics.SphereCastAll(EyePosition, 0.3f, Cam.transform.forward, 2.3f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform == transform) continue;
                var target = hit.collider.GetComponentInParent<IInteractable>();
                if (target != null)
                {
                    if (target.CanInteract) return target;
                    continue;
                }
                if (hit.distance > 0f) return null; // a wall or prop is in the way
            }
            return null;
        }

        void ThrowFlashbang()
        {
            Flashbangs--;
            Vector3 forward = Cam.transform.forward;
            float clearance = 0.6f;
            RaycastHit block;
            if (Physics.Raycast(EyePosition, forward, out block, 0.7f, ~0, QueryTriggerInteraction.Ignore))
                clearance = Mathf.Max(0f, block.distance - 0.15f);
            Vector3 start = EyePosition + forward * clearance + Vector3.down * 0.1f;
            Flashbang.Throw(start, forward * 12f + Vector3.up * 2f + controller.velocity, controller);
            Play(Sfx.Click, 0.5f);
            GameManager.Instance.AddFeed("Flashbang out!", new Color(0.8f, 0.9f, 1f));
        }

        public void TakeDamage(float amount, Vector3 source)
        {
            var game = GameManager.Instance;
            if (!IsAlive || game == null || !game.IsPlaying) return;
            Health = Mathf.Max(0f, Health - amount);
            DamageFlash = Mathf.Min(1f, DamageFlash + 0.45f);
            DamageIndicator = 1f;
            LastDamageSource = source;
            Play(Sfx.Hurt, 0.7f);
            if (!IsAlive) game.OnPlayerKilled();
        }

        public void Flashbanged(float amount)
        {
            BlindFlash = Mathf.Max(BlindFlash, amount);
        }

        public void AddRecoil(float kickUp, float kickSide)
        {
            recoilPitch += kickUp;
            recoilYaw += kickSide;
            pitch = Mathf.Clamp(pitch - kickUp * 0.25f, -85f, 85f);
        }

        public void ShowHitMarker(bool headshot)
        {
            HitMarker = 1f;
            HitMarkerHead = headshot;
            Play(Sfx.HitMarker, 0.35f);
        }

        public void Play(AudioClip clip, float volume)
        {
            if (clip != null) audioSource.PlayOneShot(clip, volume);
        }
    }
}
