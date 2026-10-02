using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    public enum AlarmState { Off, Armed, Triggered, Disabled }

    // The building alarm. Cameras, guards or panels can trigger it; when it
    // goes off, suspects that respond to alarms converge, emergency lights
    // flash and the mission status updates. It can be disabled at a panel or console.
    public class AlarmSystem : MonoBehaviour, IInteractable
    {
        public static AlarmSystem Instance { get; private set; }

        public AlarmState State { get; private set; }
        public bool HasTriggered { get; private set; }
        readonly List<Light> beacons = new List<Light>();
        readonly List<Renderer> beaconGlows = new List<Renderer>();

        public string Prompt { get { return State == AlarmState.Triggered ? "[E] Silence Alarm (hold)" : "[E] Disable Alarm (hold)"; } }
        public Vector3 InteractPosition { get { return transform.position + Vector3.up * 1.2f; } }

        public static AlarmSystem Create(Transform parent, Vector3 panelPosition, float panelYaw, IList<Vector3> beaconPositions, bool armed)
        {
            var go = new GameObject("Alarm Panel");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(panelPosition, Quaternion.Euler(0f, panelYaw, 0f));
            Shapes.Box("Panel", go.transform, new Vector3(0f, 1.2f, 0f), new Vector3(0.5f, 0.6f, 0.12f), new Color(0.75f, 0.75f, 0.72f));
            Shapes.Box("Light", go.transform, new Vector3(0f, 1.45f, -0.07f), new Vector3(0.12f, 0.08f, 0.02f), new Color(0.9f, 0.15f, 0.1f), false, 2f);
            var alarm = go.AddComponent<AlarmSystem>();
            alarm.State = armed ? AlarmState.Armed : AlarmState.Off;
            foreach (var position in beaconPositions)
            {
                var glow = Shapes.Box("Emergency Beacon", parent, position, new Vector3(0.25f, 0.15f, 0.25f), new Color(1f, 0.15f, 0.1f), false, 0.2f);
                alarm.beaconGlows.Add(glow.GetComponent<Renderer>());
                var light = Shapes.PointLight(glow.transform.parent, position + Vector3.down * 0.3f, new Color(1f, 0.15f, 0.1f), 0f, 9f);
                light.enabled = false;
                alarm.beacons.Add(light);
            }
            Instance = alarm;
            alarm.enabled = false;
            return alarm;
        }

        public void Trigger(Vector3 source, string reason)
        {
            if (State != AlarmState.Armed) return;
            State = AlarmState.Triggered;
            HasTriggered = true;
            enabled = true;
            AudioManager.Instance.SetAlarm(true);
            UIManager.Notify("ALARM TRIGGERED: " + reason, true);
            AIManager.Instance.OnAlarm(source);
            MissionManager.Instance.ReportAlarm();
        }

        public void Disable()
        {
            if (State == AlarmState.Disabled) return;
            bool wasRinging = State == AlarmState.Triggered;
            State = AlarmState.Disabled;
            AudioManager.Instance.SetAlarm(false);
            enabled = false;
            foreach (var light in beacons) light.enabled = false;
            foreach (var glow in beaconGlows) glow.sharedMaterial = Shapes.Mat(new Color(0.4f, 0.1f, 0.1f));
            UIManager.Notify(wasRinging ? "Alarm silenced" : "Alarm system disabled");
            AudioManager.Play(Sound.Console, transform.position, 0.7f);
        }

        void Update()
        {
            // Pulse the emergency lights while the alarm rings.
            bool on = Mathf.Repeat(Time.time * 1.5f, 1f) < 0.5f;
            bool dynamic = QualityManager.Current.dynamicLights;
            for (int i = 0; i < beacons.Count; i++)
            {
                beacons[i].enabled = on && dynamic;
                beacons[i].intensity = 2f * Shapes.PointLightScale;
                beaconGlows[i].sharedMaterial = Shapes.Mat(new Color(1f, 0.15f, 0.1f), on ? 3f : 0.2f);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public float InteractDuration(PlayerController player) { return 2f; }
        public bool CanInteract(PlayerController player) { return State == AlarmState.Armed || State == AlarmState.Triggered; }
        public void Interact(PlayerController player) { Disable(); }
    }
}
