using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Swat
{
    // First person, body cam style (Settings -> Camera view). The camera sits at
    // the officer's eyes (lower when crouched or sliding, out to the side and
    // rolled when peeking) with the drift and bob of a camera worn on a moving
    // body. The gun is a separate view model held by the soldier model's gloved
    // forearms: it trails the view when you turn (heavy guns more), bobs with
    // your steps, comes up to the eye when you aim down the sights, drops to a
    // low ready when you sprint or come up against a wall, dips for reloads
    // and draws, and kicks with every shot. Recoil climbs the view itself.
    // Scoped rifles zoom and show the scope instead of the gun. A shield is held
    // up on the left. Your own body still casts its shadow but isn't drawn.
    // CameraController drives this from LateUpdate while it's in use.
    public static class FirstPersonRig
    {
        public static bool Active { get; private set; }
        // Where the camera is this frame: shots start here, so they go where the screen centre is.
        public static Vector3 CameraPosition { get; private set; }
        // The view model's muzzle and gun (flash, tracers and shells come from them).
        public static Transform Muzzle { get; private set; }
        public static Transform Gun { get; private set; }
        // 0 at the hip .. 1 aiming down the sights.
        public static float AimBlend { get; private set; }
        // Aiming a scoped rifle: the HUD draws the scope and the gun is hidden.
        public static bool Scoped { get; private set; }

        const float NearClip = 0.03f;

        static PlayerController player;
        static CharacterController body;
        static Camera cam;
        static Transform viewRoot, gunPivot, leftFore, rightFore, shieldPivot;
        static Vector3 leftRest = new Vector3(0f, -0.26f, 0.09f), rightRest = new Vector3(0f, -0.26f, 0.09f);
        static WeaponData builtWeapon;
        static bool builtShield;
        static float front, sightHeight, scopeZoom = 1f;
        static bool pistol, scoped, throughOptic;
        static Transform laserDot;
        static Vector3 support;

        static float eyeHeight, bobPhase, bobAmount, lastYaw, lastPitch, sprintBlend, wallPull, braceBlend;
        static Vector2 sway;
        static float kickBack, kickPitch, kickRoll, viewPunch, drawStart = -10f, drawTime = 0.4f;
        static int lastShots;
        static float refreshAt;

        static readonly Dictionary<Renderer, ShadowCastingMode> hidden = new Dictionary<Renderer, ShadowCastingMode>();
        static Light flashlight;
        static Vector3 flashlightPosition;
        static Quaternion flashlightRotation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active = false;
            player = null;
            cam = null;
            viewRoot = gunPivot = leftFore = rightFore = shieldPivot = null;
            laserDot = null;
            Muzzle = Gun = null;
            builtWeapon = null;
            hidden.Clear();
            flashlight = null;
        }

        // Called every LateUpdate while first person is in use. shake and punch are the camera's
        // current shake and impact amounts (explosions, takedowns).
        public static void Tick(Camera camera, PlayerController who, float shake, float punch)
        {
            if (!Active || who != player || camera != cam)
            {
                Stop();
                Begin(camera, who);
            }
            float dt = Time.deltaTime;
            var weapons = player.Weapons;
            var data = weapons != null && weapons.Current != null ? weapons.Current.Data : null;
            if (data != builtWeapon || player.Health.HasShield != builtShield) Build(data);
            if (Time.unscaledTime >= refreshAt) HideBody();

            var settings = SaveManager.Settings;
            // How much the camera moves on its own: follows the Camera Shake setting, never fully still.
            float motion = settings.cameraShake <= 0 ? 0.3f : settings.cameraShake == 1 ? 0.7f : 1f;
            // 0 heavy (machine guns, shotguns) .. 1 light (pistols).
            float light = data != null ? Mathf.InverseLerp(0.75f, 1f, data.moveSpeedMultiplier) : 1f;

            // ---- Aim, sprint, wall and shield blends ----
            bool switching = Time.time - drawStart < drawTime;
            bool reloading = weapons != null && weapons.IsReloading;
            bool aimingNow = player.IsSteadyAiming && !player.IsSprinting && !reloading && !switching && wallPull < 0.5f;
            float aimSpeed = weapons != null && weapons.Current != null ? weapons.Current.AimSpeed : 1f;
            AimBlend = Mathf.MoveTowards(AimBlend, aimingNow ? 1f : 0f, dt * aimSpeed / Mathf.Lerp(0.32f, 0.16f, light));
            Scoped = scoped && AimBlend > 0.85f;
            sprintBlend = Mathf.MoveTowards(sprintBlend, player.IsSprinting ? 1f : 0f, dt * 5f);
            braceBlend = Mathf.MoveTowards(braceBlend, player.Health.Bracing ? 1f : 0f, dt * 4f);

            // ---- Camera ----
            float speed = 0f;
            if (body != null)
            {
                Vector3 v = body.velocity;
                v.y = 0f;
                speed = v.magnitude;
            }
            bool grounded = body == null || body.isGrounded;
            float stride = player.IsSprinting ? 1.25f : player.IsCrouched ? 1.6f : 1.9f;
            bobPhase += dt * speed * stride;
            bobAmount = Mathf.Lerp(bobAmount, player.IsMoving && grounded && !player.IsSliding ? Mathf.Clamp01(speed / 3f) : 0f, 1f - Mathf.Exp(-8f * dt));
            float bobScale = bobAmount * motion * (player.IsSprinting ? 1.7f : player.IsCrouched ? 0.6f : 1f) * (1f - 0.65f * AimBlend);

            float targetHeight = player.IsSliding ? 0.8f : player.IsCrouched ? 1.05f : 1.6f;
            eyeHeight = Mathf.Lerp(eyeHeight, targetHeight, 1f - Mathf.Exp(-10f * dt));
            float yaw = player.transform.eulerAngles.y;
            float pitch = player.LookPitch;

            // A worn camera never sits still: a slow drift, stronger on the move.
            float t = Time.time;
            float drift = (0.2f + bobAmount * (player.IsSprinting ? 1.5f : 0.55f)) * motion * (1f - 0.7f * AimBlend);
            float driftPitch = (Mathf.PerlinNoise(t * 0.7f, 0.31f) - 0.5f) * 2f * drift;
            float driftYaw = (Mathf.PerlinNoise(0.57f, t * 0.6f) - 0.5f) * 2f * drift;
            float driftRoll = (Mathf.PerlinNoise(t * 0.5f, 7.3f) - 0.5f) * 1.2f * drift;
            viewPunch = Mathf.Lerp(viewPunch, 0f, 1f - Mathf.Exp(-14f * dt));
            Vector3 jolt = shake > 0f ? Random.insideUnitSphere * shake * 3f : Vector3.zero;
            float roll = -player.Lean * 10f + Mathf.Sin(bobPhase) * 0.9f * bobScale + driftRoll;
            var rotation = Quaternion.Euler(pitch + driftPitch - viewPunch + jolt.x, yaw + driftYaw + jolt.y, roll + jolt.z);

            Vector3 eye = player.Position + player.LeanOffset + Vector3.up * eyeHeight + player.transform.forward * 0.08f;
            Vector3 bob = new Vector3(Mathf.Sin(bobPhase) * 0.016f, Mathf.Sin(bobPhase * 2f) * 0.022f, 0f) * bobScale;
            Vector3 position = eye + rotation * bob + (shake > 0f ? Random.insideUnitSphere * shake * 0.03f : Vector3.zero);
            cam.transform.SetPositionAndRotation(position, rotation);
            CameraPosition = position;

            float horizontal = Mathf.Clamp(settings.fieldOfView, 60f, 120f);
            float aspect = Mathf.Max(0.5f, cam.aspect);
            float vertical = 2f * Mathf.Atan(Mathf.Tan(horizontal * 0.5f * Mathf.Deg2Rad) / aspect) * Mathf.Rad2Deg;
            float zoom = Mathf.Lerp(1f, scoped ? scopeZoom : 0.8f, AimBlend) * (1f + sprintBlend * 0.04f) * (1f - punch * 0.05f);
            cam.fieldOfView = Mathf.Clamp(vertical * zoom, 10f, 110f);

            // ---- Recoil ----
            if (weapons != null && weapons.ShotsFired != lastShots)
            {
                int shots = Mathf.Clamp(weapons.ShotsFired - lastShots, 0, 3);
                lastShots = weapons.ShotsFired;
                float k = data != null ? Mathf.Clamp(data.kick, 0.3f, 3f) : 1f;
                k *= Mathf.Lerp(1f, 0.7f, AimBlend) * (player.IsCrouched ? 0.85f : 1f);
                for (int i = 0; i < shots; i++)
                {
                    kickBack += 0.03f * k;
                    kickPitch += 5f * k;
                    kickRoll += Random.Range(-4f, 4f) * k;
                    viewPunch += 0.45f * k;
                    // The view climbs (most of it comes back by itself) with a little sideways wander.
                    player.AddLookKick(0.35f + 0.5f * k, Random.Range(-0.3f, 0.3f) * k);
                }
                kickBack = Mathf.Min(kickBack, 0.09f);
                kickPitch = Mathf.Min(kickPitch, 14f);
            }
            float settle = 1f - Mathf.Exp(-Mathf.Lerp(11f, 17f, light) * dt);
            kickBack = Mathf.Lerp(kickBack, 0f, settle);
            kickPitch = Mathf.Lerp(kickPitch, 0f, settle);
            kickRoll = Mathf.Lerp(kickRoll, 0f, settle);

            // ---- Sway: the gun trails the view, heavier guns further and longer ----
            float turnYaw = Mathf.DeltaAngle(lastYaw, yaw), turnPitch = pitch - lastPitch;
            lastYaw = yaw;
            lastPitch = pitch;
            float lag = Mathf.Lerp(0.45f, 0.2f, light) * Mathf.Lerp(1f, 0.35f, AimBlend);
            sway = Vector2.Lerp(sway, Vector2.zero, 1f - Mathf.Exp(-Mathf.Lerp(8f, 15f, light) * dt));
            // x: pitch (+ muzzle down), y: yaw (+ muzzle right); both opposite to the turn, so the gun lags.
            sway += new Vector2(-turnPitch, -turnYaw) * lag;
            sway = Vector2.ClampMagnitude(sway, 7f);

            // ---- Wall: the gun comes back and down rather than poking through ----
            float reach = 0.3f + front;
            float pullTarget = 0f;
            RaycastHit hit;
            if (Physics.SphereCast(position, 0.05f, player.LookDirection, out hit, reach, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                pullTarget = Mathf.Clamp01((reach - hit.distance) / 0.35f);
            wallPull = Mathf.MoveTowards(wallPull, pullTarget, dt * 4f);

            PoseGun(weapons, dt, light, bobScale, t);
            PoseShield();
            PlaceLaserDot(weapons, position);
            if (flashlight != null && Gun != null)
            {
                flashlight.transform.position = Gun.TransformPoint(new Vector3(0.04f, -0.02f, front * 0.6f));
                flashlight.transform.rotation = Quaternion.LookRotation(player.LookDirection);
            }
        }

        static void PoseGun(WeaponController weapons, float dt, float light, float bobScale, float t)
        {
            if (gunPivot == null) return;
            bool shield = builtShield;
            Vector3 hip = shield ? new Vector3(0.16f, -0.17f, 0.38f) : pistol ? new Vector3(0.1f, -0.12f, 0.33f) : new Vector3(0.14f, -0.15f, 0.31f);
            // Iron sights: just over the top of the gun. An optic: its reticle on the screen centre.
            Vector3 ads = new Vector3(0f, -(sightHeight + (throughOptic ? 0f : 0.012f)), pistol ? 0.34f : 0.2f);
            Vector3 position = Vector3.Lerp(hip, ads, AimBlend);
            Vector3 angles = Vector3.zero;
            if (shield)
            {
                // Braced: the pistol rests on top of the shield's right edge.
                position = Vector3.Lerp(position, new Vector3(0.12f, -0.1f, 0.42f), braceBlend);
            }

            // Low ready: sprinting, or a wall in the way.
            Vector3 lowPosition = pistol ? new Vector3(0.08f, -0.25f, 0.24f) : new Vector3(0.1f, -0.19f, 0.27f);
            Vector3 lowAngles = pistol ? new Vector3(42f, -10f, 0f) : new Vector3(18f, -34f, 14f);
            float low = Mathf.Max(sprintBlend, wallPull * 0.8f);
            position = Vector3.Lerp(position, lowPosition, low);
            angles = Vector3.Lerp(angles, lowAngles, low);

            // Steps, breathing and the trailing sway.
            float hipShare = 1f - 0.7f * AimBlend;
            position += new Vector3(Mathf.Sin(bobPhase) * 0.012f, -Mathf.Abs(Mathf.Sin(bobPhase)) * 0.014f, 0f) * bobScale;
            position.y += Mathf.Sin(t * 1.6f) * 0.0025f * hipShare;
            angles.x += Mathf.Sin(t * 1.6f) * 0.35f * hipShare + Mathf.Sin(bobPhase * 2f) * 1.2f * bobScale;
            angles += new Vector3(sway.x, sway.y, -sway.y * 0.6f);
            position += new Vector3(sway.y * 0.002f, -sway.x * 0.0015f, 0f);

            // Reload: tip the gun and bring it in; draw: up from below the screen.
            float reload = weapons != null ? weapons.ReloadProgress : 0f;
            float r = Mathf.Sin(reload * Mathf.PI);
            angles += new Vector3(14f, 12f, 30f) * r;
            position += new Vector3(-0.03f, -0.045f, -0.03f) * r;
            float draw = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - drawStart) / Mathf.Max(0.05f, drawTime)));
            position.y -= 0.28f * draw * draw;
            angles.x += 45f * draw;

            // Melee: a short shove forward.
            float m = (Time.time - player.LastMeleeTime) / 0.35f;
            if (m >= 0f && m < 1f)
            {
                float s = Mathf.Sin(m * Mathf.PI);
                position += new Vector3(-0.04f, 0.03f, 0.12f) * s;
                angles += new Vector3(-12f, -20f, 25f) * s;
            }

            // Shot kick.
            position.z -= kickBack;
            angles += new Vector3(-kickPitch, 0f, kickRoll);

            gunPivot.localPosition = position;
            gunPivot.localRotation = Quaternion.Euler(angles);
            // Through a scope the gun is out of the picture.
            bool showGun = !Scoped;
            if (gunPivot.gameObject.activeSelf != showGun) gunPivot.gameObject.SetActive(showGun);

            // The support hand goes to the magazine and away for a fresh one during a reload.
            if (leftFore != null && leftFore.gameObject.activeSelf)
            {
                Vector3 target = support;
                if (reload > 0f)
                {
                    Vector3 mag = new Vector3(0f, -0.13f, 0.06f), away = new Vector3(-0.05f, -0.4f, -0.06f);
                    if (reload < 0.25f) target = Vector3.Lerp(support, mag, Mathf.SmoothStep(0f, 1f, reload / 0.25f));
                    else if (reload < 0.4f) target = Vector3.Lerp(mag, away, Mathf.SmoothStep(0f, 1f, (reload - 0.25f) / 0.15f));
                    else if (reload < 0.6f) target = Vector3.Lerp(away, mag, Mathf.SmoothStep(0f, 1f, (reload - 0.4f) / 0.2f));
                    else if (reload < 0.8f) target = mag;
                    else target = Vector3.Lerp(mag, support, Mathf.SmoothStep(0f, 1f, (reload - 0.8f) / 0.2f));
                }
                PlaceForearm(leftFore, leftRest, target, pistol ? new Vector3(-0.45f, -0.4f, -1f) : new Vector3(-0.45f, -0.5f, -0.9f));
            }
        }

        // Laser module: a red dot where your shots go, on whatever the view centre is on.
        static void PlaceLaserDot(WeaponController weapons, Vector3 eye)
        {
            bool show = weapons != null && weapons.Current != null && weapons.Current.HasLaser && !player.IsSprinting && !Scoped && wallPull < 0.5f
                && Time.time - drawStart >= drawTime && gunPivot != null && gunPivot.gameObject.activeSelf;
            if (show && laserDot == null)
            {
                laserDot = Shapes.Make(PrimitiveType.Sphere, "Laser Dot", viewRoot, Vector3.zero, Vector3.one * 0.02f, new Color(1f, 0.1f, 0.06f), false, 3f).transform;
                laserDot.SetParent(null, true);
            }
            if (laserDot == null) return;
            if (laserDot.gameObject.activeSelf != show) laserDot.gameObject.SetActive(show);
            if (!show) return;
            Vector3 point = player.AimPoint;
            float distance = Vector3.Distance(eye, point);
            // Sit just off the surface, a little bigger with distance so it stays visible.
            laserDot.position = point - player.LookDirection * 0.02f;
            laserDot.localScale = Vector3.one * Mathf.Clamp(0.012f + distance * 0.0035f, 0.012f, 0.06f);
        }

        static void PoseShield()
        {
            if (shieldPivot == null) return;
            // Carried low on the left; braced, up in front with the top edge just under your eyes.
            Vector3 carried = new Vector3(-0.3f, -0.66f, 0.44f), braced = new Vector3(-0.07f, -0.585f, 0.45f);
            float m = (Time.time - player.LastMeleeTime) / 0.35f;
            float bash = m >= 0f && m < 1f ? Mathf.Sin(m * Mathf.PI) : 0f;
            shieldPivot.localPosition = Vector3.Lerp(carried, braced, braceBlend) + new Vector3(0.04f, 0.03f, 0.14f) * bash;
            shieldPivot.localRotation = Quaternion.Euler(Vector3.Lerp(new Vector3(0f, 22f, 4f), new Vector3(0f, 4f, 0f), braceBlend));
        }

        // A forearm from the soldier model, its hand on a point on the gun and the elbow out
        // along elbowDirection (gun space), so it comes into view from the bottom of the screen.
        static void PlaceForearm(Transform forearm, Vector3 rest, Vector3 hand, Vector3 elbowDirection)
        {
            Vector3 back = elbowDirection.normalized;
            forearm.localPosition = hand + back * rest.magnitude;
            forearm.localRotation = Quaternion.FromToRotation(rest, -back);
        }

        static void Begin(Camera camera, PlayerController who)
        {
            player = who;
            cam = camera;
            body = who.GetComponent<CharacterController>();
            Active = true;
            cam.orthographic = false;
            cam.nearClipPlane = NearClip;
            viewRoot = new GameObject("First Person View").transform;
            viewRoot.SetParent(cam.transform, false);
            builtWeapon = null;
            eyeHeight = player.IsCrouched ? 1.05f : 1.6f;
            lastYaw = player.transform.eulerAngles.y;
            lastPitch = player.LookPitch;
            lastShots = player.Weapons != null ? player.Weapons.ShotsFired : 0;
            AimBlend = sprintBlend = wallPull = 0f;
            braceBlend = player.Health.Bracing ? 1f : 0f;
            sway = Vector2.zero;
            kickBack = kickPitch = kickRoll = viewPunch = 0f;
            flashlight = player.Parts != null ? player.Parts.flashlight : null;
            if (flashlight != null)
            {
                flashlightPosition = flashlight.transform.localPosition;
                flashlightRotation = flashlight.transform.localRotation;
            }
            hidden.Clear();
            HideBody();
        }

        // Back to the top-down camera (death, spectating, the end of the mission, the menus).
        public static void Stop()
        {
            if (!Active) return;
            Active = false;
            Scoped = false;
            AimBlend = 0f;
            foreach (var pair in hidden)
            {
                if (pair.Key == null) continue;
                pair.Key.shadowCastingMode = pair.Value;
                pair.Key.forceRenderingOff = false;
            }
            hidden.Clear();
            if (flashlight != null)
            {
                flashlight.transform.localPosition = flashlightPosition;
                flashlight.transform.localRotation = flashlightRotation;
            }
            flashlight = null;
            if (viewRoot != null) Object.Destroy(viewRoot.gameObject);
            if (laserDot != null) Object.Destroy(laserDot.gameObject);
            laserDot = null;
            viewRoot = gunPivot = leftFore = rightFore = shieldPivot = null;
            Muzzle = Gun = null;
            builtWeapon = null;
            if (cam != null)
            {
                cam.nearClipPlane = 0.5f;
                cam.fieldOfView = 50f;
            }
            cam = null;
            player = null;
        }

        // The gun (and shield) in view, rebuilt whenever the weapon changes.
        static void Build(WeaponData data)
        {
            builtWeapon = data;
            builtShield = player.Health.HasShield;
            if (gunPivot != null) Object.Destroy(gunPivot.gameObject);
            if (shieldPivot != null) Object.Destroy(shieldPivot.gameObject);
            gunPivot = shieldPivot = leftFore = rightFore = null;
            Muzzle = Gun = null;
            drawStart = Time.time;
            drawTime = data != null ? Mathf.Clamp(data.switchTime, 0.2f, 1f) : 0.4f;
            if (data != null)
            {
                gunPivot = new GameObject("View Gun").transform;
                gunPivot.SetParent(viewRoot, false);
                WeaponModels.GunInfo info;
                front = WeaponModels.Build(data, gunPivot, player.Loadout, null, out info);
                pistol = data.isSidearm || front < 0.35f;
                // Marksman rifles have their own scope; a magnified optic does the same for any gun.
                scoped = data.steadyLookAhead > 0f || info.zoom > 1f;
                scopeZoom = info.zoom > 1f ? 1f / info.zoom : 1f / (1f + data.steadyLookAhead * 0.22f);
                throughOptic = info.hasSight;
                sightHeight = info.hasSight ? info.sightY : TopOf(gunPivot);
                string imported = SaveManager.Settings.classicCharacters ? null : WeaponModels.ImportedModel(data.id);
                var model = ModelLibrary.Get(imported);
                var point = model != null ? model.Find("muzzle") : null;
                Muzzle = new GameObject("Muzzle").transform;
                Muzzle.SetParent(gunPivot, false);
                Muzzle.localPosition = new Vector3(0f, point != null ? point.pivot.y : 0.01f, front);
                Gun = gunPivot;
                BuildArms();
            }
            if (builtShield) BuildShield();
            Shapes.Toonify(viewRoot);
            foreach (var collider in viewRoot.GetComponentsInChildren<Collider>(true)) Object.Destroy(collider);
            foreach (var renderer in viewRoot.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        // The highest point of the gun above its grip: aiming down the sights looks just over it.
        static float TopOf(Transform root)
        {
            float top = 0.06f;
            bool any = false;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(i % 2 == 0 ? b.min.x : b.max.x, (i / 2) % 2 == 0 ? b.min.y : b.max.y, i < 4 ? b.min.z : b.max.z);
                    float y = root.InverseTransformPoint(filter.transform.TransformPoint(corner)).y;
                    if (!any || y > top) top = y;
                    any = true;
                }
            }
            return Mathf.Clamp(top, 0.02f, 0.25f);
        }

        // Gloved forearms from the soldier model in the officer's kit colours: the trigger hand on
        // the grip, the other on the handguard (or both on a pistol; the shield arm is hidden).
        static void BuildArms()
        {
            var model = SaveManager.Settings.classicCharacters ? null : ModelLibrary.Get("soldier");
            var look = PlayerController.OfficerAppearance(player.Officer, player.Loadout, true);
            look = VersusMatch.TeamColours(look, player.VersusSide);
            var recolor = CharacterFactory.SoldierColors(look);
            support = pistol ? new Vector3(-0.01f, -0.06f, 0.02f) : new Vector3(0f, -0.025f, Mathf.Min(0.24f, front * 0.45f));
            rightFore = Forearm(model, "foreR", "handR", recolor, look, ref rightRest);
            PlaceForearm(rightFore, rightRest, new Vector3(0f, -0.045f, -0.02f), pistol ? new Vector3(0.25f, -0.4f, -1f) : new Vector3(0.3f, -0.45f, -1f));
            leftFore = Forearm(model, "foreL", "handL", recolor, look, ref leftRest);
            PlaceForearm(leftFore, leftRest, support, pistol ? new Vector3(-0.45f, -0.4f, -1f) : new Vector3(-0.45f, -0.5f, -0.9f));
            if (builtShield) leftFore.gameObject.SetActive(false);
        }

        static Transform Forearm(ModelLibrary.Model model, string part, string handPoint, System.Func<string, Color, Color> recolor, Appearance look, ref Vector3 rest)
        {
            var pivot = new GameObject(part).transform;
            pivot.SetParent(gunPivot, false);
            var forearm = model != null ? model.Find(part) : null;
            var hand = model != null ? model.Find(handPoint) : null;
            if (forearm != null && hand != null && ModelLibrary.Spawn(model, part, pivot, Vector3.zero, recolor) != null)
            {
                rest = hand.pivot - forearm.pivot;
                return pivot;
            }
            // Classic characters (or no model file): a sleeve and a glove out of boxes.
            rest = new Vector3(0f, -0.26f, 0.09f);
            var sleeve = Shapes.Box("Sleeve", pivot, rest * 0.45f, new Vector3(0.08f, 0.08f, 0.08f), look.shirt, false).transform;
            sleeve.localRotation = Quaternion.FromToRotation(Vector3.up, rest);
            sleeve.localScale = new Vector3(0.08f, rest.magnitude * 0.9f, 0.08f);
            Shapes.Box("Glove", pivot, rest, new Vector3(0.075f, 0.09f, 0.085f), new Color(0.12f, 0.12f, 0.13f), false);
            return pivot;
        }

        static void BuildShield()
        {
            shieldPivot = new GameObject("View Shield").transform;
            shieldPivot.SetParent(viewRoot, false);
            var model = SaveManager.Settings.classicCharacters ? null : ModelLibrary.Get("police_shield");
            if (model == null || ModelLibrary.Spawn(model, "shield", shieldPivot, Vector3.zero, (slot, original) => new Color(0.07f, 0.08f, 0.1f)) == null)
                Shapes.Box("Shield", shieldPivot, Vector3.zero, new Vector3(0.5f, 1.05f, 0.06f), new Color(0.06f, 0.07f, 0.09f), false);
            PoseShield();
        }

        // Your own body isn't drawn (the camera is inside it) but still casts its shadow. Checked a few
        // times a second for parts that change, like the gun in your hands.
        static void HideBody()
        {
            refreshAt = Time.unscaledTime + 0.3f;
            if (player == null) return;
            foreach (var renderer in player.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || hidden.ContainsKey(renderer)) continue;
                // The flashlight's glow on the floor is something you'd see.
                if (renderer.name == "Flashlight Cone") continue;
                hidden[renderer] = renderer.shadowCastingMode;
                if (renderer.shadowCastingMode == ShadowCastingMode.Off) renderer.forceRenderingOff = true;
                else renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
        }
    }
}
