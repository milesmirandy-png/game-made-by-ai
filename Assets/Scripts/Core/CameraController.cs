using UnityEngine;

namespace Swat
{
    // Angled top-down tactical camera. In missions it smoothly follows the
    // team leader, leans towards the mouse cursor, zooms with the wheel
    // between configurable limits, cycles zoom presets with V and stays inside
    // the map bounds. In menus it glides between showcase shots of the HQ.
    public class CameraController : MonoBehaviour
    {
        public static readonly string[] PresetNames = { "Close (indoor)", "Standard", "Wide (outdoor)" };
        static readonly float[] PresetDistances = { 11f, 15f, 21f };

        [SerializeField, Range(40f, 75f)] float pitch = 55f;
        [SerializeField] float minDistance = 8f;
        [SerializeField] float maxDistance = 26f;
        [SerializeField] float zoomStep = 2f;
        [SerializeField] float maxLookAhead = 4f;

        public Camera Cam { get; private set; }
        public int Preset { get; private set; }

        // Pixel art: how far (in game pixels) the true camera position is from the
        // pixel-snapped one this frame; the UI shifts the image by this much.
        public static Vector2 PixelOffset { get; private set; }
        // Orthographic half-height per metre of follow distance (matches the 50 degree perspective framing).
        const float OrthoPerDistance = 0.4663f;

        PlayerController player;
        Vector3 focus, focusVelocity;
        float distance, targetDistance, shake;
        Vector3 kick;
        Bounds bounds;
        bool hasBounds;

        // Showcase (menus).
        bool showcase;
        Vector3 showPosition, showTarget, showFromPosition, showFromTarget;
        float showBlend = 1f;

        // Uses the scene's main camera if there is one, so any render pipeline
        // settings on it are kept.
        public static CameraController Create()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            if (cam.transform.parent != null) cam.transform.SetParent(null, true);
            cam.orthographic = false;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 160f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UITheme.Background;

            var rig = cam.GetComponent<CameraController>();
            if (rig == null) rig = cam.gameObject.AddComponent<CameraController>();
            rig.Cam = cam;
            rig.SetPreset(Mathf.Clamp(SaveManager.Settings.zoomPreset, 0, PresetDistances.Length - 1), true);
            return rig;
        }

        public void Follow(PlayerController newPlayer, Bounds limits)
        {
            showcase = false;
            player = newPlayer;
            bounds = limits;
            hasBounds = limits.size.sqrMagnitude > 1f;
            if (player != null) Snap();
        }

        public void Snap()
        {
            if (player == null) return;
            focus = player.Position;
            focusVelocity = Vector3.zero;
            distance = targetDistance;
            Place();
        }

        public void ShowcaseShot(Vector3 position, Vector3 target, bool instant)
        {
            player = null;
            if (!showcase || instant)
            {
                showFromPosition = position;
                showFromTarget = target;
                showBlend = 1f;
            }
            else
            {
                showFromPosition = transform.position;
                showFromTarget = transform.position + transform.forward * 10f;
                showBlend = 0f;
            }
            showcase = true;
            showPosition = position;
            showTarget = target;
            if (instant) transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
        }

        public void SetPreset(int preset, bool instant)
        {
            Preset = Mathf.Clamp(preset, 0, PresetDistances.Length - 1);
            targetDistance = PresetDistances[Preset];
            if (instant) distance = targetDistance;
        }

        // Camera Shake setting: Off, Low (default) or Medium.
        public void Shake(float amount)
        {
            int level = SaveManager.Settings.cameraShake;
            float scale = level <= 0 ? 0f : level == 1 ? 0.5f : 1f;
            shake = Mathf.Min(1f, shake + amount * scale);
        }

        // Recoil: the view jolts away from where the gun points, then springs back.
        // Scaled by the Camera Shake setting like everything else.
        public void Kick(Vector3 direction, float amount)
        {
            int level = SaveManager.Settings.cameraShake;
            float scale = level <= 0 ? 0f : level == 1 ? 0.6f : 1f;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f || scale <= 0f) return;
            kick = Vector3.ClampMagnitude(kick - direction.normalized * amount * scale, 0.5f);
        }

        // Automatic indoor/outdoor zoom: the chosen preset indoors, one step wider outside.
        public void SetEnvironment(bool indoor)
        {
            if (!SaveManager.Settings.autoIndoorZoom || player == null) return;
            int preset = SaveManager.Settings.zoomPreset;
            SetPreset(indoor ? preset : Mathf.Min(preset + 1, PresetDistances.Length - 1), false);
        }

        // Where a screen point (pixels, origin bottom-left) hits the horizontal plane at the given height.
        public bool ScreenToGround(Vector2 screen, float height, out Vector3 point)
        {
            Vector2 viewport = QualityManager.ScreenToViewport(screen);
            Ray ray = Cam.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
            var plane = new Plane(Vector3.up, new Vector3(0f, height, 0f));
            float enter;
            if (plane.Raycast(ray, out enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }
            point = Vector3.zero;
            return false;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (showcase)
            {
                showBlend = Mathf.MoveTowards(showBlend, 1f, dt * 0.8f);
                float t = Mathf.SmoothStep(0f, 1f, showBlend);
                // A slow drift keeps menu shots alive.
                Vector3 drift = new Vector3(Mathf.Sin(Time.unscaledTime * 0.15f), 0f, Mathf.Cos(Time.unscaledTime * 0.11f)) * 0.4f;
                Vector3 position = Vector3.Lerp(showFromPosition, showPosition, t) + drift;
                Vector3 target = Vector3.Lerp(showFromTarget, showTarget, t);
                transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
                ApplyProjection(Vector3.Distance(position, target));
                SnapToPixels();
                return;
            }
            if (player == null) return;

            var game = GameManager.Instance;
            bool live = game != null && game.AcceptsGameplayInput;
            var settings = SaveManager.Settings;
            if (live)
            {
                float scroll = GameInput.Scroll;
                if (Mathf.Abs(scroll) > 0.01f) targetDistance = Mathf.Clamp(targetDistance - scroll * zoomStep * settings.zoomSpeed, minDistance, maxDistance);
                if (GameInput.Down(InputAction.ZoomPreset))
                {
                    SetPreset((Preset + 1) % PresetDistances.Length, false);
                    settings.zoomPreset = Preset;
                    UIManager.Notify("Camera: " + PresetNames[Preset]);
                }
            }
            distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-10f * dt));

            Vector3 desired = player.Position;
            if (live && player.IsAlive)
            {
                Vector3 ahead = player.AimPoint - player.Position;
                ahead.y = 0f;
                // Marksman weapons let the view reach further while steady aiming.
                float reach = player.IsSteadyAiming && player.Weapons != null ? player.Weapons.Current.Data.steadyLookAhead : 0f;
                float factor = Mathf.Clamp(settings.lookAhead, 0f, 0.5f) + (reach > 0f ? 0.3f : 0f);
                desired += Vector3.ClampMagnitude(ahead * factor, maxLookAhead + reach);
            }
            UpdateEdgeScroll(live && settings.edgeScrolling && !GameInput.UsingGamepad, dt);
            desired += edgeOffset;
            if (hasBounds)
            {
                desired.x = Mathf.Clamp(desired.x, bounds.min.x, bounds.max.x);
                desired.z = Mathf.Clamp(desired.z, bounds.min.z, bounds.max.z);
            }
            float smoothing = Mathf.Clamp(settings.cameraSmoothing, 0f, 0.4f);
            if (smoothing <= 0.005f) { focus = desired; focusVelocity = Vector3.zero; }
            else focus = Vector3.SmoothDamp(focus, desired, ref focusVelocity, smoothing, Mathf.Infinity, Mathf.Max(dt, 0.0001f));
            Place();

            if (shake > 0f)
            {
                transform.position += Random.insideUnitSphere * shake * 0.35f;
                shake = Mathf.MoveTowards(shake, 0f, dt * 3f);
            }
            if (kick.sqrMagnitude > 0.000001f)
            {
                transform.position += kick;
                kick = Vector3.Lerp(kick, Vector3.zero, 1f - Mathf.Exp(-16f * dt));
            }
            ApplyProjection(distance);
            SnapToPixels();
        }

        // Pixel art uses an orthographic camera (a flat, sprite-like top-down view);
        // smooth keeps the 50 degree perspective camera.
        void ApplyProjection(float viewDistance)
        {
            bool pixel = QualityManager.PixelArt;
            if (Cam.orthographic != pixel) Cam.orthographic = pixel;
            if (pixel) Cam.orthographicSize = Mathf.Max(2f, viewDistance * OrthoPerDistance);
        }

        // Moves the camera onto the game-pixel grid so still scenery never shimmers
        // as it scrolls; the leftover fraction is applied when the image is drawn.
        void SnapToPixels()
        {
            PixelOffset = Vector2.zero;
            var quality = QualityManager.Instance;
            if (!Cam.orthographic || quality == null || quality.ScaledView == null || quality.PixelFactor <= 0) return;
            float unit = 2f * Cam.orthographicSize / quality.ScaledView.height;
            if (unit <= 0f) return;
            Vector3 p = transform.position, right = transform.right, up = transform.up;
            float x = Vector3.Dot(p, right), y = Vector3.Dot(p, up);
            float sx = Mathf.Round(x / unit) * unit, sy = Mathf.Round(y / unit) * unit;
            transform.position = p + right * (sx - x) + up * (sy - y);
            PixelOffset = new Vector2((x - sx) / unit, (y - sy) / unit);
        }

        // Optional edge scrolling: pushing the cursor against a screen edge pans
        // the view up to 12 m that way; it eases back once the cursor leaves the edge.
        Vector3 edgeOffset;
        float edgeIdle;

        void UpdateEdgeScroll(bool enabledNow, float dt)
        {
            Vector2 mouse = GameInput.MousePosition;
            Vector3 push = Vector3.zero;
            if (enabledNow)
            {
                const float margin = 12f;
                if (mouse.x <= margin) push.x = -1f;
                else if (mouse.x >= Screen.width - margin) push.x = 1f;
                if (mouse.y <= margin) push.z = -1f;
                else if (mouse.y >= Screen.height - margin) push.z = 1f;
            }
            if (push.sqrMagnitude > 0f)
            {
                edgeIdle = 0f;
                edgeOffset = Vector3.ClampMagnitude(edgeOffset + push.normalized * 14f * dt, 12f);
            }
            else
            {
                edgeIdle += dt;
                if (edgeIdle > 0.8f) edgeOffset = Vector3.MoveTowards(edgeOffset, Vector3.zero, 10f * dt);
            }
        }

        void Place()
        {
            var rotation = Quaternion.Euler(pitch, 0f, 0f);
            transform.SetPositionAndRotation(focus + rotation * new Vector3(0f, 0f, -distance), rotation);
        }
    }
}
