using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // A wall-mounted camera that sweeps back and forth. If it watches an
    // officer for long enough it triggers the alarm. Disable it by hand
    // (hold E) or from a security console.
    public class SecurityCamera : MonoBehaviour, IInteractable
    {
        public static readonly List<SecurityCamera> All = new List<SecurityCamera>();

        const float Range = 13f;
        const float FieldOfView = 70f;
        const float Sweep = 50f;
        const float DetectTime = 1.4f;

        public bool Active { get; private set; }
        public float Detection { get; private set; }
        Transform head;
        Renderer led;
        float baseYaw, nextCheck, phase;
        bool seeing;

        public string Prompt { get { return "[E] Disable camera (hold)"; } }
        public Vector3 InteractPosition { get { return transform.position + Vector3.up * 1.3f; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            All.Clear();
        }

        public static SecurityCamera Create(Transform parent, Vector3 position, float yaw, bool active)
        {
            var go = new GameObject("Security Camera");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var cam = go.AddComponent<SecurityCamera>();
            Shapes.Box("Mount", go.transform, new Vector3(0f, 1.3f, -0.05f), new Vector3(0.12f, 0.5f, 0.12f), new Color(0.6f, 0.6f, 0.6f));
            cam.head = new GameObject("Head").transform;
            cam.head.SetParent(go.transform, false);
            cam.head.localPosition = new Vector3(0f, 1.6f, 0f);
            Shapes.Box("Body", cam.head, new Vector3(0f, 0f, 0.12f), new Vector3(0.18f, 0.16f, 0.36f), new Color(0.85f, 0.85f, 0.82f), false);
            Shapes.Box("Lens", cam.head, new Vector3(0f, 0f, 0.31f), new Vector3(0.1f, 0.1f, 0.04f), new Color(0.05f, 0.05f, 0.08f), false);
            cam.led = Shapes.Box("LED", cam.head, new Vector3(0.06f, 0.09f, 0.2f), new Vector3(0.04f, 0.03f, 0.04f), Color.red, false, 3f).GetComponent<Renderer>();
            cam.baseYaw = yaw;
            cam.phase = Random.value * 10f;
            cam.Active = active;
            cam.SetLed();
            All.Add(cam);
            return cam;
        }

        public void Disable()
        {
            if (!Active) return;
            Active = false;
            Detection = 0f;
            SetLed();
            AudioManager.Play(Sound.Console, transform.position, 0.5f);
        }

        void SetLed()
        {
            led.sharedMaterial = Active ? Shapes.Mat(Color.red, 3f) : Shapes.Mat(new Color(0.15f, 0.15f, 0.15f));
        }

        void Update()
        {
            var game = GameManager.Instance;
            if (!Active || game == null || !game.IsPlaying) return;
            phase += Time.deltaTime;
            float yaw = baseYaw + Mathf.Sin(phase * 0.5f) * Sweep;
            head.rotation = Quaternion.Euler(15f, yaw, 0f);

            if (Time.time >= nextCheck)
            {
                nextCheck = Time.time + 0.3f;
                seeing = false;
                Vector3 eye = head.position;
                Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                foreach (var target in AIManager.Instance.PoliceTargets)
                {
                    if (!target.IsAlive) continue;
                    float visibility = AIVisibility.VisibilityOf(target.Position, target.IsCrouched, target.FlashlightOn);
                    if (AIVisibility.CanSee(eye, forward, FieldOfView, Range, target.ChestPosition, visibility)) { seeing = true; break; }
                }
            }

            Detection = Mathf.MoveTowards(Detection, seeing ? DetectTime : 0f, Time.deltaTime);
            led.enabled = !seeing || Mathf.Repeat(Time.time * 8f, 1f) < 0.5f;
            if (Detection < DetectTime) return;
            Detection = 0f;
            var alarm = AlarmSystem.Instance;
            if (alarm != null && alarm.State == AlarmState.Armed)
            {
                AudioManager.Play(Sound.CameraAlert, transform.position, 0.8f);
                alarm.Trigger(transform.position, "a security camera spotted the team");
            }
        }

        public float InteractDuration(PlayerController player) { return 1.5f; }
        public bool CanInteract(PlayerController player) { return Active; }

        public void Interact(PlayerController player)
        {
            Disable();
            UIManager.Notify("Camera disabled");
            MissionManager.Instance.ReportCameras();
        }

        void OnDestroy()
        {
            All.Remove(this);
        }
    }
}
