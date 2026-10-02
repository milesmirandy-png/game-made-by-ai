using UnityEngine;

namespace Swat
{
    // Tactical top-down camera: looks down at an angle, smoothly follows the
    // player, leans a little towards the mouse cursor and zooms with the wheel.
    public class CameraController : MonoBehaviour
    {
        [SerializeField, Range(40f, 75f)] float pitch = 55f;
        [SerializeField] float minDistance = 9f;
        [SerializeField] float maxDistance = 24f;
        [SerializeField] float startDistance = 15f;
        [SerializeField] float zoomStep = 2f;
        [SerializeField] float followSmoothTime = 0.12f;
        [SerializeField, Range(0f, 0.5f)] float lookAhead = 0.25f;
        [SerializeField] float maxLookAhead = 4f;

        public Camera Cam { get; private set; }

        Transform target;
        PlayerController player;
        Vector3 focus, focusVelocity;
        float distance, targetDistance, shake;

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
            cam.farClipPlane = 150f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.24f, 0.2f);

            var rig = cam.GetComponent<CameraController>();
            if (rig == null) rig = cam.gameObject.AddComponent<CameraController>();
            rig.Cam = cam;
            rig.distance = rig.targetDistance = rig.startDistance;
            return rig;
        }

        public void Follow(PlayerController newPlayer)
        {
            player = newPlayer;
            target = newPlayer != null ? newPlayer.transform : null;
            if (target != null)
            {
                focus = target.position;
                focusVelocity = Vector3.zero;
                Place();
            }
        }

        public void Shake(float amount)
        {
            shake = Mathf.Min(1f, shake + amount);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;
            var game = GameManager.Instance;
            bool playing = game != null && game.IsPlaying;

            if (playing)
            {
                float scroll = GameInput.Scroll;
                if (Mathf.Abs(scroll) > 0.01f) targetDistance = Mathf.Clamp(targetDistance - scroll * zoomStep, minDistance, maxDistance);
            }
            distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-10f * dt));

            Vector3 desired = target.position;
            if (playing && player != null && player.Health.IsAlive)
            {
                Vector3 ahead = player.AimPoint - target.position;
                ahead.y = 0f;
                desired += Vector3.ClampMagnitude(ahead * lookAhead, maxLookAhead);
            }
            focus = Vector3.SmoothDamp(focus, desired, ref focusVelocity, followSmoothTime, Mathf.Infinity, dt);
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
