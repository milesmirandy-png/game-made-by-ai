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
        [SerializeField] float followSmoothTime = 0.12f;
        [SerializeField] float maxLookAhead = 4f;

        public Camera Cam { get; private set; }
        public int Preset { get; private set; }

        PlayerController player;
        Vector3 focus, focusVelocity;
        float distance, targetDistance, shake;
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

        public void Shake(float amount)
        {
            shake = Mathf.Min(1f, shake + amount);
        }

        // Where a screen point (pixels, origin bottom-left) hits the horizontal plane at the given height.
        public bool ScreenToGround(Vector2 screen, float height, out Vector3 point)
        {
            Ray ray = Cam.ViewportPointToRay(new Vector3(screen.x / Mathf.Max(1, Screen.width), screen.y / Mathf.Max(1, Screen.height), 0f));
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
                desired += Vector3.ClampMagnitude(ahead * Mathf.Clamp(settings.lookAhead, 0f, 0.5f), maxLookAhead);
            }
            if (hasBounds)
            {
                desired.x = Mathf.Clamp(desired.x, bounds.min.x, bounds.max.x);
                desired.z = Mathf.Clamp(desired.z, bounds.min.z, bounds.max.z);
            }
            focus = Vector3.SmoothDamp(focus, desired, ref focusVelocity, followSmoothTime, Mathf.Infinity, Mathf.Max(dt, 0.0001f));
            Place();

            if (shake > 0f)
            {
                transform.position += Random.insideUnitSphere * shake * 0.35f;
                shake = Mathf.MoveTowards(shake, 0f, dt * 3f);
            }
        }

        void Place()
        {
            var rotation = Quaternion.Euler(pitch, 0f, 0f);
            transform.SetPositionAndRotation(focus + rotation * new Vector3(0f, 0f, -distance), rotation);
        }
    }
}
