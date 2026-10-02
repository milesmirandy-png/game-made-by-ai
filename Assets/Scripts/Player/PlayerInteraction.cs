using UnityEngine;

namespace Swat
{
    // Finds the best thing to use near the player and handles E (tap or hold,
    // with a progress bar for longer actions) and X to shout compliance.
    public class PlayerInteraction : MonoBehaviour
    {
        [SerializeField] float range = 2f;
        [SerializeField] float shoutCooldown = 1.5f;

        public IInteractable Current { get; private set; }
        public bool IsInteracting { get { return holding != null; } }
        public float Progress { get { return holding != null && holdDuration > 0f ? holdTime / holdDuration : 0f; } }
        public float LastShoutTime { get; private set; }

        readonly Collider[] nearby = new Collider[32];
        PlayerController player;
        IInteractable holding;
        float holdTime, holdDuration, nextScan;

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
                holding = null;
                return;
            }

            if (Time.time >= nextScan && holding == null)
            {
                nextScan = Time.time + 0.1f;
                Current = FindBest();
            }

            if (holding != null)
            {
                // Keep holding E, stay close, and the action completes.
                bool stillValid = GameInput.Held(InputAction.Interact) && holding.CanInteract(player)
                    && Flat(holding.InteractPosition - player.Position) < range + 0.6f;
                if (!stillValid)
                {
                    holding = null;
                }
                else
                {
                    holdTime += Time.deltaTime;
                    if (holdTime >= holdDuration)
                    {
                        var target = holding;
                        holding = null;
                        target.Interact(player);
                        nextScan = 0f;
                    }
                }
            }
            else if (Current != null && GameInput.Down(InputAction.Interact) && Current.CanInteract(player))
            {
                float duration = Current.InteractDuration(player);
                if (duration <= 0.01f)
                {
                    Current.Interact(player);
                    nextScan = 0f;
                }
                else
                {
                    holding = Current;
                    holdTime = 0f;
                    holdDuration = duration;
                }
            }

            if (GameInput.Down(InputAction.Shout) && Time.time - LastShoutTime > shoutCooldown)
            {
                LastShoutTime = Time.time;
                AudioManager.Play2D(Sound.Shout, 0.6f, 1f, SoundCategory.Voice);
                AIManager.Instance.Shout(player.ChestPosition, player.Position, true);
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

        static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
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
