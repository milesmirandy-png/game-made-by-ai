using UnityEngine;

namespace Swat
{
    // The officer's rifle: firing, recoil, reloading and aiming down the sight,
    // plus a gun model built from boxes that sits in front of the camera.
    public class Weapon : MonoBehaviour
    {
        public int magazineSize = 30;
        public int startingReserve = 120;
        public float fireRate = 10f;
        public float damage = 34f;
        public float reloadTime = 2.2f;
        public float hipSpread = 1.8f;
        public float aimSpread = 0.2f;

        const float Range = 150f;
        const float HipFov = 70f;
        const float AimFov = 48f;
        static readonly Vector3 HipPosition = new Vector3(0.2f, -0.21f, 0.42f);
        static readonly Vector3 AimPosition = new Vector3(0f, -0.105f, 0.3f);

        public int Ammo { get; private set; }
        public int Reserve { get; private set; }
        public bool Reloading { get { return reloadTimer > 0f; } }
        public float AimAmount { get; private set; }
        public float Spread { get; private set; }

        PlayerController player;
        Camera cam;
        Transform model, muzzle;
        GameObject muzzleFlash;
        float nextShotTime, reloadTimer, kick, bloom, flashTimer, sprintAmount;

        public void Init(PlayerController owner, Camera camera)
        {
            player = owner;
            cam = camera;
            BuildModel();
            ResetAmmo();
        }

        public void ResetAmmo()
        {
            Ammo = magazineSize;
            Reserve = startingReserve;
            reloadTimer = 0f;
            bloom = 0f;
        }

        void BuildModel()
        {
            model = new GameObject("Rifle").transform;
            model.SetParent(cam.transform, false);
            model.localPosition = HipPosition;

            var black = new Color(0.07f, 0.07f, 0.08f);
            var grey = new Color(0.18f, 0.18f, 0.19f);
            var glove = new Color(0.1f, 0.1f, 0.1f);
            var sleeve = new Color(0.12f, 0.13f, 0.16f);

            Shapes.Box("Receiver", model, new Vector3(0f, 0f, 0.02f), new Vector3(0.065f, 0.09f, 0.4f), grey, false);
            Shapes.Box("Handguard", model, new Vector3(0f, 0.005f, 0.33f), new Vector3(0.06f, 0.07f, 0.24f), black, false);
            Shapes.Box("Barrel", model, new Vector3(0f, 0.015f, 0.52f), new Vector3(0.025f, 0.025f, 0.16f), black, false);
            Shapes.Box("Stock", model, new Vector3(0f, -0.015f, -0.27f), new Vector3(0.055f, 0.085f, 0.18f), black, false);
            Shapes.Box("Magazine", model, new Vector3(0f, -0.1f, 0.08f), new Vector3(0.045f, 0.14f, 0.075f), black, false)
                .transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            Shapes.Box("Grip", model, new Vector3(0f, -0.08f, -0.08f), new Vector3(0.04f, 0.1f, 0.05f), black, false)
                .transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);

            // Holographic sight. The red dot lines up with the centre of the screen when aiming.
            Shapes.Box("Sight Base", model, new Vector3(0f, 0.052f, 0.03f), new Vector3(0.04f, 0.015f, 0.07f), black, false);
            Shapes.Box("Sight Post L", model, new Vector3(-0.02f, 0.095f, 0.03f), new Vector3(0.006f, 0.07f, 0.012f), black, false);
            Shapes.Box("Sight Post R", model, new Vector3(0.02f, 0.095f, 0.03f), new Vector3(0.006f, 0.07f, 0.012f), black, false);
            Shapes.Box("Sight Top", model, new Vector3(0f, 0.13f, 0.03f), new Vector3(0.046f, 0.008f, 0.012f), black, false);
            Shapes.Box("Red Dot", model, new Vector3(0f, 0.105f, 0.03f), new Vector3(0.005f, 0.005f, 0.002f), new Color(1f, 0.1f, 0.1f), false, 6f);

            Shapes.Box("Left Glove", model, new Vector3(-0.005f, -0.04f, 0.33f), new Vector3(0.075f, 0.07f, 0.11f), glove, false);
            Shapes.Box("Right Glove", model, new Vector3(0.01f, -0.075f, -0.07f), new Vector3(0.065f, 0.08f, 0.08f), glove, false);
            Shapes.Box("Left Sleeve", model, new Vector3(-0.07f, -0.1f, 0.17f), new Vector3(0.085f, 0.085f, 0.3f), sleeve, false)
                .transform.localRotation = Quaternion.Euler(-12f, 25f, 0f);
            Shapes.Box("Right Sleeve", model, new Vector3(0.05f, -0.12f, -0.22f), new Vector3(0.09f, 0.09f, 0.25f), sleeve, false)
                .transform.localRotation = Quaternion.Euler(-20f, -15f, 0f);

            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(model, false);
            muzzle.localPosition = new Vector3(0f, 0.015f, 0.61f);
            muzzleFlash = Shapes.Box("Muzzle Flash", muzzle, new Vector3(0f, 0f, 0.05f), new Vector3(0.07f, 0.07f, 0.12f), new Color(1f, 0.8f, 0.35f), false, 5f);
            muzzleFlash.SetActive(false);

            Shapes.NoShadows(model.gameObject);
        }

        // Called by the player every frame. When not active the gun only animates.
        public void Tick(bool active)
        {
            float dt = Time.deltaTime;
            bool aiming = active && GameInput.AimHeld && !Reloading && !player.IsSprinting;
            AimAmount = Mathf.MoveTowards(AimAmount, aiming ? 1f : 0f, dt * 7f);
            cam.fieldOfView = Mathf.Lerp(HipFov, AimFov, Mathf.SmoothStep(0f, 1f, AimAmount));

            if (Reloading)
            {
                reloadTimer -= dt;
                if (reloadTimer <= 0f) FinishReload();
            }
            else if (active && GameInput.Reload && Ammo < magazineSize && Reserve > 0)
            {
                StartReload();
            }

            if (active && !Reloading && !player.IsSprinting && GameInput.FireHeld && Time.time >= nextShotTime)
            {
                if (Ammo > 0)
                {
                    Fire();
                }
                else if (GameInput.FirePressed)
                {
                    player.Play(Sfx.Click, 0.7f);
                    nextShotTime = Time.time + 0.25f;
                    if (Reserve > 0) StartReload();
                }
            }

            bloom = Mathf.MoveTowards(bloom, 0f, dt * 3.5f);
            Spread = Mathf.Lerp(hipSpread, aimSpread, AimAmount) + bloom + player.HorizontalSpeed * 0.3f * (1f - 0.6f * AimAmount);

            kick = Mathf.MoveTowards(kick, 0f, dt * 8f);
            flashTimer -= dt;
            if (muzzleFlash.activeSelf && flashTimer <= 0f) muzzleFlash.SetActive(false);
            AnimateModel(dt);
        }

        void Fire()
        {
            nextShotTime = Time.time + 1f / fireRate;
            Ammo--;
            kick = 1f;
            bloom = Mathf.Min(bloom + 0.35f, 2.5f);
            player.AddRecoil(Mathf.Lerp(1.4f, 0.7f, AimAmount), Random.Range(-0.35f, 0.35f));
            player.Play(Sfx.Rifle, 0.5f);

            muzzleFlash.SetActive(true);
            muzzleFlash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
            flashTimer = 0.04f;
            Effects.Flash(muzzle.position, new Color(1f, 0.8f, 0.45f), 2.5f, 7f, 0.06f);

            var game = GameManager.Instance;
            if (game != null) game.ReportNoise(transform.position, 22f);

            Vector3 origin = cam.transform.position;
            Vector3 direction = Scatter(cam.transform.forward, Spread);
            Vector3 end = origin + direction * Range;
            RaycastHit hit;
            if (Physics.Raycast(origin, direction, out hit, Range, ~0, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var hitbox = hit.collider.GetComponent<Hitbox>();
                if (hitbox != null && hitbox.Owner != null)
                {
                    hitbox.Owner.TakeHit(damage * hitbox.multiplier, hit.point, direction, true);
                    Effects.Burst(hit.point, hit.normal, new Color(0.55f, 0.05f, 0.05f), 6, 2f);
                    player.ShowHitMarker(hitbox.isHead);
                }
                else
                {
                    Effects.Burst(hit.point, hit.normal, new Color(0.7f, 0.68f, 0.62f), 5, 2.5f, 0.035f);
                    if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
                        hit.rigidbody.AddForceAtPosition(direction * 4f, hit.point, ForceMode.Impulse);
                }
            }
            Effects.Tracer(muzzle.position, end, new Color(1f, 0.85f, 0.5f));
        }

        void StartReload()
        {
            reloadTimer = reloadTime;
            player.Play(Sfx.Click, 0.6f);
        }

        void FinishReload()
        {
            reloadTimer = 0f;
            int taken = Mathf.Min(magazineSize - Ammo, Reserve);
            Ammo += taken;
            Reserve -= taken;
            player.Play(Sfx.Click, 0.8f);
        }

        void AnimateModel(float dt)
        {
            float aim = Mathf.SmoothStep(0f, 1f, AimAmount);
            sprintAmount = Mathf.MoveTowards(sprintAmount, player.IsSprinting ? 1f : 0f, dt * 5f);

            Vector3 position = Vector3.Lerp(HipPosition, AimPosition, aim);
            position += player.BobOffset * (1f - 0.85f * aim);
            position.z -= kick * 0.05f;
            float reload = Reloading ? Mathf.Sin((1f - reloadTimer / reloadTime) * Mathf.PI) : 0f;
            position.y -= reload * 0.12f;
            position += new Vector3(-0.05f, -0.04f, 0f) * sprintAmount;

            model.localPosition = position;
            model.localRotation = Quaternion.Euler(-kick * 5f + reload * 30f + sprintAmount * 10f, sprintAmount * -35f, reload * -25f);
        }

        static Vector3 Scatter(Vector3 forward, float degrees)
        {
            Vector2 offset = Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
            return (Quaternion.LookRotation(forward) * new Vector3(offset.x, offset.y, 1f)).normalized;
        }
    }
}
