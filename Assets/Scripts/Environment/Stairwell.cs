using UnityEngine;

namespace Swat
{
    // Moves the team between floors. Floors are laid out side by side in the
    // world; taking the stairs teleports the player, plus any squadmates and
    // escorted civilians who are following.
    public class Stairwell : MonoBehaviour, IInteractable
    {
        public Vector3 Destination { get; private set; }
        public float DestinationYaw { get; private set; }
        public int DestinationArea { get; private set; }
        public string Label { get; private set; }

        public string Prompt { get { return "[E] " + Label; } }
        public Vector3 InteractPosition { get { return transform.position + Vector3.up; } }

        public static Stairwell Create(Transform parent, Vector3 position, string label, Vector3 destination, float yaw, int destinationArea)
        {
            var go = new GameObject("Stairs: " + label);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var stairs = go.AddComponent<Stairwell>();
            stairs.Destination = destination;
            stairs.DestinationYaw = yaw;
            stairs.DestinationArea = destinationArea;
            stairs.Label = label;
            var steps = new Color(0.45f, 0.45f, 0.47f);
            for (int i = 0; i < 4; i++)
                Shapes.Box("Step", go.transform, new Vector3(0f, 0.1f + i * 0.18f, -0.6f + i * 0.4f), new Vector3(1.6f, 0.2f + i * 0.36f, 0.4f), steps);
            Shapes.Box("Sign", go.transform, new Vector3(0f, 2.2f, 0.8f), new Vector3(0.6f, 0.25f, 0.05f), new Color(0.1f, 0.7f, 0.3f), false, 1.5f);
            return stairs;
        }

        public float InteractDuration(PlayerController player) { return 0f; }
        public bool CanInteract(PlayerController player) { return true; }

        public void Interact(PlayerController player)
        {
            GameManager.Instance.UseStairs(this);
        }
    }
}
