using UnityEngine;

namespace Swat
{
    // A piece of evidence to secure (hold E).
    public class EvidenceItem : MonoBehaviour, IInteractable
    {
        public bool Secured { get; private set; }
        public bool Revealed { get; set; }
        GameObject marker;

        public string Prompt { get { return "[E] Secure evidence (hold)"; } }
        public Vector3 InteractPosition { get { return transform.position + Vector3.up * 0.5f; } }

        public static EvidenceItem Create(Transform parent, Vector3 position)
        {
            var go = new GameObject("Evidence");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var item = go.AddComponent<EvidenceItem>();
            Shapes.Box("Case", go.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0.6f, 0.4f, 0.45f), new Color(0.25f, 0.2f, 0.12f));
            Shapes.Box("Tag", go.transform, new Vector3(0f, 0.41f, 0f), new Vector3(0.3f, 0.02f, 0.2f), new Color(1f, 0.85f, 0.1f), false, 1.5f);
            item.marker = Shapes.Box("Marker", go.transform, new Vector3(0f, 1.6f, 0f), new Vector3(0.2f, 0.2f, 0.2f), new Color(1f, 0.85f, 0.1f), false, 3f);
            item.marker.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            return item;
        }

        public float InteractDuration(PlayerController player) { return 1.5f; }
        public bool CanInteract(PlayerController player) { return !Secured; }

        public void Interact(PlayerController player)
        {
            Secured = true;
            marker.SetActive(false);
            AudioManager.Play(Sound.Rescue, transform.position, 0.7f);
            MissionManager.Instance.ReportEvidence();
        }
    }
}
