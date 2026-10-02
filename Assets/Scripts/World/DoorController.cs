using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    public enum DoorState { Closed, Open, Locked, Destroyed }

    // An interactive door. Closed doors open with E (and suspects or civilians
    // open them as they walk through). Locked doors need a breaching charge,
    // which blows them off their hinges. Locked doors carve the NavMesh, so the
    // AI paths around them until they are breached.
    public class DoorController : MonoBehaviour, IInteractable
    {
        const float VisualHeight = 1.4f;   // matches the low "cutaway" walls
        const float SolidHeight = 2.4f;    // blocks bullets and sight to full height
        const float SwingSpeed = 420f;
        const float ChargeFuse = 2.5f;

        public DoorState State { get; private set; }
        public bool Breachable { get; private set; }
        public bool ChargePlaced { get { return detonateAt > 0f; } }

        Transform hinge;
        GameObject leaf;
        GameObject charge;
        NavMeshObstacle obstacle;
        float angle, targetAngle, detonateAt;

        public string Prompt
        {
            get
            {
                switch (State)
                {
                    case DoorState.Closed: return "[E] Open door";
                    case DoorState.Open: return "[E] Close door";
                    case DoorState.Locked:
                        if (!Breachable) return "Locked";
                        var player = GameManager.Instance.Player;
                        int charges = player != null ? player.Weapons.CountOf(EquipmentKind.BreachingCharge) : 0;
                        return charges > 0 ? "[E] Place breaching charge (" + charges + " left)" : "Locked - you need a breaching charge";
                    default: return string.Empty;
                }
            }
        }

        public Vector3 InteractPosition { get { return transform.position + Vector3.up * 1.1f; } }

        public static DoorController Create(Transform parent, Vector3 center, bool alongX, float width, DoorState state, bool breachable)
        {
            var go = new GameObject(state == DoorState.Locked ? "Door (Locked)" : "Door");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);

            var door = go.AddComponent<DoorController>();
            door.State = state;
            door.Breachable = breachable;

            door.hinge = new GameObject("Hinge").transform;
            door.hinge.SetParent(go.transform, false);
            door.hinge.localPosition = new Vector3(-width * 0.5f, 0f, 0f);

            Color color = state == DoorState.Locked ? new Color(0.45f, 0.22f, 0.16f) : new Color(0.55f, 0.4f, 0.26f);
            door.leaf = Shapes.Box("Leaf", door.hinge, new Vector3(width * 0.5f, VisualHeight * 0.5f, 0f), new Vector3(width - 0.06f, VisualHeight, 0.08f), color);
            var box = door.leaf.GetComponent<BoxCollider>();
            box.size = new Vector3(1f, SolidHeight / VisualHeight, 1f);
            box.center = new Vector3(0f, box.size.y * 0.5f - 0.5f, 0f);
            if (breachable)
            {
                // Yellow stripes mark doors you can breach.
                Shapes.Box("Breach Mark", door.leaf.transform, new Vector3(0f, 0.3f, 0f), new Vector3(1.02f, 0.08f, 1.2f), new Color(1f, 0.8f, 0.1f), false, 0.6f);
                Shapes.Box("Breach Mark", door.leaf.transform, new Vector3(0f, -0.1f, 0f), new Vector3(1.02f, 0.08f, 1.2f), new Color(1f, 0.8f, 0.1f), false, 0.6f);
            }

            door.obstacle = go.AddComponent<NavMeshObstacle>();
            door.obstacle.shape = NavMeshObstacleShape.Box;
            door.obstacle.size = new Vector3(width, SolidHeight, 0.4f);
            door.obstacle.center = new Vector3(0f, SolidHeight * 0.5f, 0f);
            door.obstacle.carving = true;
            door.obstacle.enabled = state == DoorState.Locked;

            door.enabled = false; // only updates while swinging or counting down
            return door;
        }

        public void RefreshNavMeshCarving()
        {
            if (!obstacle.enabled) return;
            obstacle.enabled = false;
            obstacle.enabled = true;
        }

        public bool CanInteract(PlayerController player)
        {
            return (State == DoorState.Closed || State == DoorState.Open || State == DoorState.Locked) && !ChargePlaced;
        }

        public void Interact(PlayerController player)
        {
            switch (State)
            {
                case DoorState.Closed:
                    Open(player.Position);
                    break;
                case DoorState.Open:
                    Close();
                    break;
                case DoorState.Locked:
                    if (Breachable && player.Weapons.TryUseBreachingCharge()) PlaceCharge();
                    else
                    {
                        AudioManager.Play(Sound.DoorLocked, transform.position, 0.8f);
                        UIManager.Notify(Breachable ? "Locked. You're out of breaching charges." : "This door is locked.");
                    }
                    break;
            }
        }

        // Swings away from whoever opened it.
        public void Open(Vector3 from)
        {
            if (State != DoorState.Closed) return;
            State = DoorState.Open;
            float side = Vector3.Dot(from - transform.position, transform.forward) > 0f ? 1f : -1f;
            targetAngle = 95f * side;
            enabled = true;
            AudioManager.Play(Sound.DoorOpen, transform.position, 0.6f);
            Noise.Emit(transform.position, 5f, NoiseKind.Door);
        }

        public void Close()
        {
            if (State != DoorState.Open) return;
            State = DoorState.Closed;
            targetAngle = 0f;
            enabled = true;
            AudioManager.Play(Sound.DoorOpen, transform.position, 0.5f, 0.8f);
        }

        public void PlaceCharge()
        {
            if (State != DoorState.Locked || ChargePlaced) return;
            detonateAt = Time.time + ChargeFuse;
            var player = GameManager.Instance.Player;
            float side = player != null && Vector3.Dot(player.Position - transform.position, transform.forward) < 0f ? -1f : 1f;
            charge = Shapes.Box("Breaching Charge", transform, new Vector3(0f, 1f, 0.07f * side), new Vector3(0.5f, 0.3f, 0.06f), new Color(0.9f, 0.15f, 0.1f), false, 2f);
            AudioManager.Play(Sound.Click, transform.position, 0.8f);
            UIManager.Notify("Breaching charge set. Stand back!");
            enabled = true;
        }

        void Update()
        {
            if (ChargePlaced)
            {
                charge.SetActive(Mathf.Repeat(Time.time * 4f, 1f) < 0.6f);
                if (Time.time >= detonateAt) Detonate();
                return;
            }

            angle = Mathf.MoveTowards(angle, targetAngle, SwingSpeed * Time.deltaTime);
            hinge.localRotation = Quaternion.Euler(0f, angle, 0f);
            if (Mathf.Approximately(angle, targetAngle)) enabled = false;
        }

        void Detonate()
        {
            detonateAt = 0f;
            State = DoorState.Destroyed;
            Destroy(charge);
            leaf.SetActive(false);
            obstacle.enabled = false;
            enabled = false;

            Vector3 center = transform.position + Vector3.up;
            var effects = EffectsManager.Instance;
            effects.Burst(center, transform.forward, new Color(0.45f, 0.3f, 0.18f), 14, 5f, 0.12f);
            effects.Burst(center, -transform.forward, new Color(0.45f, 0.3f, 0.18f), 8, 4f, 0.1f);
            effects.FlashLight(center, new Color(1f, 0.7f, 0.4f), 6f, 10f, 0.25f);
            AudioManager.Play(Sound.Breach, center, 1f);
            Noise.Emit(center, 30f, NoiseKind.Explosion);
            // The blast stuns anyone right behind the door.
            AIManager.Instance.Stun(center, 4f, 3.5f);

            var game = GameManager.Instance;
            game.CameraRig.Shake(0.5f);
            var player = game.Player;
            if (player != null && Vector3.Distance(player.Position, transform.position) < 1.6f)
                player.Health.TakeDamage(new DamageInfo { amount = 25f, point = center, direction = player.Position - transform.position, attacker = Team.Environment });
        }
    }
}
