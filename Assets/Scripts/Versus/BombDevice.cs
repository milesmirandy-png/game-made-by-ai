using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Rapid Deployment: one of the devices the suspects defend. A plain box with a blinking red lamp
    // until SWAT disarms it (hold E for four seconds), then the lamp turns green. It never goes off
    // on screen: running out the clock just ends the round for the suspects.
    public class BombDevice : MonoBehaviour, IInteractable
    {
        public static readonly List<BombDevice> All = new List<BombDevice>();

        public int Index { get; private set; }
        public bool Disarmed { get; private set; }
        public string Prompt { get { return "[E] Disarm Device (hold)"; } }
        public Vector3 InteractPosition { get { return transform.position + Vector3.up * 0.5f; } }
        VersusMatch match;
        Renderer lamp;
        Material red, green, off;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            All.Clear();
        }

        public static BombDevice Create(Transform parent, VersusMatch match, int index, Vector3 position)
        {
            var go = new GameObject("Device " + (index + 1));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var device = go.AddComponent<BombDevice>();
            device.match = match;
            device.Index = index;
            Shapes.Box("Case", go.transform, new Vector3(0f, 0.22f, 0f), new Vector3(0.62f, 0.44f, 0.42f), new Color(0.22f, 0.24f, 0.22f), false);
            Shapes.Box("Panel", go.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.5f, 0.03f, 0.3f), new Color(0.1f, 0.1f, 0.11f), false);
            device.lamp = Shapes.Box("Lamp", go.transform, new Vector3(0.18f, 0.5f, 0f), new Vector3(0.08f, 0.06f, 0.08f), Color.red, false, 3f).GetComponent<Renderer>();
            device.red = Shapes.Mat(new Color(1f, 0.1f, 0.08f), 3f);
            device.green = Shapes.Mat(new Color(0.2f, 1f, 0.35f), 2.5f);
            device.off = Shapes.Mat(new Color(0.25f, 0.1f, 0.1f));
            All.Add(device);
            return device;
        }

        public void SetDisarmed(bool disarmed)
        {
            Disarmed = disarmed;
            if (lamp != null) lamp.sharedMaterial = disarmed ? green : red;
        }

        void Update()
        {
            if (Disarmed || lamp == null) return;
            lamp.sharedMaterial = Mathf.Repeat(Time.time, 1f) < 0.5f ? red : off;
        }

        public float InteractDuration(PlayerController player) { return VersusMatch.DisarmTime; }

        public bool CanInteract(PlayerController player)
        {
            return !Disarmed && match != null && VersusMatch.Active && match.MySide == 0 && !match.RoundOver && player != null && player.IsAlive;
        }

        public void Interact(PlayerController player)
        {
            if (CanInteract(player)) match.RequestDisarm(Index);
        }

        void OnDestroy()
        {
            All.Remove(this);
        }
    }
}
