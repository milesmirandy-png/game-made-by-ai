using UnityEngine;

namespace Swat
{
    // The building's front door. Press E to kick it in. It's loud, so
    // suspects nearby will turn towards the noise.
    public class BreachDoor : MonoBehaviour, IInteractable
    {
        bool breached;
        float swing;

        public bool CanInteract { get { return !breached; } }
        public string Prompt { get { return "[E] Breach door"; } }

        public void Interact(PlayerController player)
        {
            if (breached) return;
            breached = true;
            player.Play(Sfx.Breach, 0.9f);
            Vector3 middle = transform.position + transform.right * 0.8f + Vector3.up * 1.1f;
            Effects.Burst(middle, Vector3.forward, new Color(0.4f, 0.27f, 0.16f), 10, 3f, 0.06f);
            GameManager.Instance.ReportNoise(middle, 16f);
        }

        void Update()
        {
            if (!breached || swing >= 1f) return;
            swing = Mathf.Min(1f, swing + Time.deltaTime * 7f);
            transform.localRotation = Quaternion.Euler(0f, -95f * swing, 0f);
        }
    }
}
