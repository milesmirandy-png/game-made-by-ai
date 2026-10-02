using UnityEngine;

namespace Swat
{
    // Finds the nearest thing the player can use (door, civilian, surrendered
    // suspect) and handles E to use it and F to shout at suspects.
    public class PlayerInteraction : MonoBehaviour
    {
        [SerializeField] float range = 1.9f;
        [SerializeField] float shoutCooldown = 1.5f;

        public IInteractable Current { get; private set; }
        public float LastShoutTime { get; private set; }

        readonly Collider[] nearby = new Collider[24];
        PlayerController player;
        float nextScan;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            LastShoutTime = -10f;
        }

        public void Tick(bool active)
        {
            if (!active)
            {
                Current = null;
                return;
            }

            // Scanning ten times a second is plenty and keeps physics queries cheap.
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 0.1f;
                Current = FindBest();
            }
            if (Current != null && GameInput.Interact && Current.CanInteract(player))
            {
                Current.Interact(player);
                nextScan = 0f;
            }

            if (GameInput.Shout && Time.time - LastShoutTime > shoutCooldown)
            {
                LastShoutTime = Time.time;
                AudioManager.Play2D(Sound.Shout, 0.6f);
                AIManager.Instance.Shout(player);
            }
        }

        IInteractable FindBest()
        {
            Vector3 chest = player.ChestPosition;
            int count = Physics.OverlapSphereNonAlloc(chest, range, nearby, Layers.ShootableMask, QueryTriggerInteraction.Ignore);
            IInteractable best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var candidate = nearby[i].GetComponentInParent<IInteractable>();
                if (candidate == null || !candidate.CanInteract(player)) continue;

                Vector3 to = candidate.InteractPosition - player.Position;
                to.y = 0f;
                float distance = to.magnitude;
                float facing = distance > 0.01f ? Vector3.Dot(player.AimDirection, to / distance) : 1f;
                float score = distance - facing * 0.8f;
                if (score >= bestScore || !InReach(chest, candidate)) continue;
                best = candidate;
                bestScore = score;
            }
            return best;
        }

        // No using things through walls.
        static bool InReach(Vector3 from, IInteractable candidate)
        {
            RaycastHit hit;
            if (!Physics.Linecast(from, candidate.InteractPosition, out hit, Layers.WorldMask, QueryTriggerInteraction.Ignore)) return true;
            var owner = candidate as Component;
            return owner != null && hit.collider.transform.IsChildOf(owner.transform);
        }
    }
}
