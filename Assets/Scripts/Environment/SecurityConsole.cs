using UnityEngine;

namespace Swat
{
    // A security terminal. Using it opens a small panel showing the camera and
    // alarm status, with buttons to disable cameras, silence the alarm, unlock
    // electronic doors or review footage (reveals suspects and evidence on the map).
    public class SecurityConsole : MonoBehaviour, IInteractable
    {
        public string Id { get; private set; }
        public string Title { get; private set; }
        public bool Used { get; private set; }
        public bool CanUnlockDoors { get; private set; }
        public bool CanReviewFootage { get; private set; }
        public bool FootageReviewed { get; private set; }

        public string Prompt { get { return "[E] Use " + Title + " (hold)"; } }
        public Vector3 InteractPosition { get { return transform.position + Vector3.up * 1.1f; } }

        public static SecurityConsole Create(Transform parent, string id, string title, Vector3 position, float yaw, bool unlockDoors, bool reviewFootage)
        {
            var go = new GameObject(title);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Shapes.Box("Desk", go.transform, new Vector3(0f, 0.45f, 0f), new Vector3(1.2f, 0.9f, 0.7f), new Color(0.3f, 0.32f, 0.35f));
            Shapes.Box("Screen", go.transform, new Vector3(0f, 1.15f, 0.15f), new Vector3(0.8f, 0.5f, 0.06f), new Color(0.05f, 0.05f, 0.06f), false);
            Shapes.Box("Display", go.transform, new Vector3(0f, 1.15f, 0.11f), new Vector3(0.72f, 0.42f, 0.02f), new Color(0.2f, 0.75f, 0.5f), false, 1.5f);
            var console = go.AddComponent<SecurityConsole>();
            console.Id = id;
            console.Title = title;
            console.CanUnlockDoors = unlockDoors;
            console.CanReviewFootage = reviewFootage;
            return console;
        }

        public float InteractDuration(PlayerController player) { return 1f; }
        public bool CanInteract(PlayerController player) { return true; }

        public void Interact(PlayerController player)
        {
            AudioManager.Play(Sound.Console, transform.position, 0.7f);
            if (!Used) MissionManager.Instance.ReportConsole(this);
            Used = true;
            UIManager.OpenConsole(this);
        }

        public void DisableCameras()
        {
            foreach (var cam in SecurityCamera.All) cam.Disable();
            UIManager.Notify("All security cameras disabled");
            MissionManager.Instance.ReportCameras();
        }

        public void SilenceAlarm()
        {
            if (AlarmSystem.Instance != null) AlarmSystem.Instance.Disable();
        }

        public void UnlockDoors()
        {
            int count = 0;
            foreach (var door in GameManager.Instance.Level.doors)
            {
                if (!door.Electronic || door.State != DoorState.Locked) continue;
                door.Unlock();
                count++;
            }
            UIManager.Notify(count > 0 ? count + " electronic door(s) unlocked" : "No electronic doors are locked");
        }

        public void ReviewFootage()
        {
            FootageReviewed = true;
            TacticalIntel.Instance.RevealAll(20f, true);
            UIManager.Notify("Footage reviewed: suspect positions and evidence marked on the tactical map (Tab)");
            MissionManager.Instance.ReportFootage();
        }
    }
}
