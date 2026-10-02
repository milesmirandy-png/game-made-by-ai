using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    public enum DoorState { Closed, Open, Locked, Wedged, Breached, Disabled }

    // An interactive door. Closed doors open with E (AI opens them too).
    // Locked doors can be breached (if marked with yellow stripes), have their
    // lock picked (if allowed), or be unlocked from a security console (if
    // electronic). Wedged doors can't be opened until the wedge is removed.
    // Disabled doors are sealed. Blocked doors carve the NavMesh through a
    // NavMeshObstacle, so the navigation mesh is never rebuilt.
    public class DoorController : MonoBehaviour, IInteractable
    {
        const float VisualHeight = 1.4f;
        const float SolidHeight = 2.4f;
        const float SwingSpeed = 420f;

        public string Id { get; private set; }
        public DoorState State { get; private set; }
        public bool Breachable { get; private set; }
        public bool Pickable { get; set; }
        public bool Electronic { get; private set; }
        public float Width { get; private set; }
        public bool ChargePlaced { get { return detonateAt > 0f; } }
        public bool IsPassable { get { return State == DoorState.Open || State == DoorState.Breached; } }
        public bool Blocks { get { return State == DoorState.Locked || State == DoorState.Wedged || State == DoorState.Disabled; } }
        public RoomController RoomFront { get; set; }
        public RoomController RoomBack { get; set; }
        public int Area { get; set; }
        public event System.Action<DoorController> Opened;

        Transform hinge;
        GameObject leaf, charge, wedge;
        NavMeshObstacle obstacle;
        float angle, targetAngle, detonateAt;
        bool chargeByPlayer;

        public Vector3 InteractPosition { get { return transform.position + Vector3.up * 1.1f; } }

        public string Prompt
        {
            get
            {
                var player = GameManager.Instance.Player;
                switch (State)
                {
                    case DoorState.Closed: return "[E] Open door";
                    case DoorState.Open: return "[E] Close door";
                    case DoorState.Wedged: return "[E] Remove wedge (hold)";
                    case DoorState.Disabled: return "Sealed shut";
                    case DoorState.Locked:
                        if (Breachable && player != null && player.Weapons.Inventory.CountOf(EquipmentKind.BreachingCharge) > 0) return "[E] Place breaching charge (hold)";
                        if (Pickable) return "[E] Pick the lock (hold)";
                        if (Electronic) return "Electronic lock - use a security console";
                        return Breachable ? "Locked - needs a breaching charge" : "Locked";
                    default: return string.Empty;
                }
            }
        }

        public static DoorController Create(Transform parent, string id, Vector3 center, bool alongX, float width, DoorState state, bool breachable, bool electronic)
        {
            var go = new GameObject("Door " + id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);

            var door = go.AddComponent<DoorController>();
            door.Id = id;
            door.State = state;
            door.Breachable = breachable;
            door.Electronic = electronic;
            door.Width = width;

            door.hinge = new GameObject("Hinge").transform;
            door.hinge.SetParent(go.transform, false);
            door.hinge.localPosition = new Vector3(-width * 0.5f, 0f, 0f);

            Color color = electronic ? new Color(0.35f, 0.38f, 0.42f) : state == DoorState.Locked ? new Color(0.45f, 0.22f, 0.16f) : new Color(0.55f, 0.4f, 0.26f);
            if (state == DoorState.Disabled) color = new Color(0.3f, 0.3f, 0.3f);
            door.leaf = Shapes.Box("Leaf", door.hinge, new Vector3(width * 0.5f, VisualHeight * 0.5f, 0f), new Vector3(width - 0.06f, VisualHeight, 0.08f), color);
            var box = door.leaf.GetComponent<BoxCollider>();
            box.size = new Vector3(1f, SolidHeight / VisualHeight, 1f);
            box.center = new Vector3(0f, box.size.y * 0.5f - 0.5f, 0f);
            if (breachable)
            {
                Shapes.Box("Breach Mark", door.leaf.transform, new Vector3(0f, 0.3f, 0f), new Vector3(1.02f, 0.08f, 1.2f), new Color(1f, 0.8f, 0.1f), false, 0.6f);
                Shapes.Box("Breach Mark", door.leaf.transform, new Vector3(0f, -0.1f, 0f), new Vector3(1.02f, 0.08f, 1.2f), new Color(1f, 0.8f, 0.1f), false, 0.6f);
            }
            if (electronic)
                Shapes.Box("Keypad", door.leaf.transform, new Vector3(0.38f, 0.1f, 0f), new Vector3(0.08f, 0.12f, 1.4f), new Color(0.9f, 0.2f, 0.15f), false, 2f);

            door.obstacle = go.AddComponent<NavMeshObstacle>();
            door.obstacle.shape = NavMeshObstacleShape.Box;
            door.obstacle.size = new Vector3(width, SolidHeight, 0.4f);
            door.obstacle.center = new Vector3(0f, SolidHeight * 0.5f, 0f);
            door.obstacle.carving = true;
            door.obstacle.enabled = door.Blocks;

            door.enabled = false; // only updates while swinging or counting down
            return door;
        }

        public void RefreshNavMeshCarving()
        {
            obstacle.enabled = false;
            obstacle.enabled = Blocks || ChargePlaced;
        }

        void SetState(DoorState next)
        {
            State = next;
            obstacle.enabled = Blocks || ChargePlaced;
        }

        public float InteractDuration(PlayerController player)
        {
            switch (State)
            {
                case DoorState.Wedged: return 1f;
                case DoorState.Locked:
                    if (Breachable && player.Weapons.Inventory.CountOf(EquipmentKind.BreachingCharge) > 0)
                        return player.Officer.role == OfficerRole.Breacher ? 0.4f : 1.5f;
                    return Pickable ? (player.Officer.role == OfficerRole.Breacher ? 1.5f : 3f) : 0f;
                default: return 0f;
            }
        }

        public bool CanInteract(PlayerController player)
        {
            return State != DoorState.Breached && !ChargePlaced;
        }

        public void Interact(PlayerController player)
        {
            switch (State)
            {
                case DoorState.Closed: Open(player.Position); break;
                case DoorState.Open: Close(); break;
                case DoorState.Wedged: RemoveWedge(); break;
                case DoorState.Locked:
                    if (Breachable && player.Weapons.Inventory.Consume(EquipmentKind.BreachingCharge)) PlaceCharge(player.Position, true);
                    else if (Pickable) PickLock(player.Position);
                    else
                    {
                        AudioManager.Play(Sound.DoorLocked, transform.position, 0.8f);
                        UIManager.Notify(Electronic ? "Electronic lock. Find a security console." : "This door is locked.");
                    }
                    break;
                case DoorState.Disabled:
                    AudioManager.Play(Sound.DoorLocked, transform.position, 0.8f);
                    break;
            }
        }

        // Swings away from whoever opened it.
        public void Open(Vector3 from)
        {
            if (State != DoorState.Closed) return;
            SetState(DoorState.Open);
            float side = Vector3.Dot(from - transform.position, transform.forward) > 0f ? 1f : -1f;
            targetAngle = 95f * side;
            enabled = true;
            AudioManager.Play(Sound.DoorOpen, transform.position, 0.6f);
            Noise.Emit(transform.position, 5f, NoiseKind.Door);
            MissionManager.Instance.ReportDoor(this);
            if (Opened != null) Opened(this);
        }

        public void Close()
        {
            if (State != DoorState.Open) return;
            SetState(DoorState.Closed);
            targetAngle = 0f;
            enabled = true;
            AudioManager.Play(Sound.DoorOpen, transform.position, 0.5f, 0.8f);
        }

        public void PickLock(Vector3 from)
        {
            if (State != DoorState.Locked) return;
            AudioManager.Play(Sound.Lockpick, transform.position, 0.7f);
            Noise.Emit(transform.position, 6f, NoiseKind.Door);
            SetState(DoorState.Closed);
            UIManager.Notify("Lock picked");
            Open(from);
        }

        public void Unlock()
        {
            if (State != DoorState.Locked) return;
            SetState(DoorState.Closed);
            AudioManager.Play(Sound.Unlock, transform.position, 0.6f);
        }

        // Used by the mission randomizer. Randomly locked doors can always be
        // breached or picked, so a mission never becomes impossible.
        public void SetLocked(bool pickable)
        {
            if (State != DoorState.Closed) return;
            SetState(DoorState.Locked);
            Pickable = pickable;
            Shapes.Box("Lock", leaf.transform, new Vector3(0.38f, 0.15f, 0f), new Vector3(0.06f, 0.08f, 1.3f), new Color(0.75f, 0.6f, 0.2f), false);
        }

        public bool CanWedge { get { return State == DoorState.Closed; } }

        public void PlaceWedge()
        {
            if (State != DoorState.Closed) return;
            SetState(DoorState.Wedged);
            wedge = Shapes.Box("Door Wedge", transform, new Vector3(0f, 0.06f, 0.2f), new Vector3(0.2f, 0.1f, 0.25f), new Color(0.95f, 0.75f, 0.1f), false);
            AudioManager.Play(Sound.Wedge, transform.position, 0.8f);
            UIManager.Notify("Door wedged");
        }

        public void RemoveWedge()
        {
            if (State != DoorState.Wedged) return;
            SetState(DoorState.Closed);
            if (wedge != null) Destroy(wedge);
            AudioManager.Play(Sound.Wedge, transform.position, 0.6f, 1.2f);
        }

        // The breacher's kick: forces a locked (non-electronic) door open, loudly.
        public bool Kick(Vector3 from)
        {
            if (State == DoorState.Closed)
            {
                Open(from);
                return true;
            }
            if (State != DoorState.Locked || Electronic) return false;
            SetState(DoorState.Closed);
            AudioManager.Play(Sound.Kick, transform.position, 1f);
            Noise.Emit(transform.position, 15f, NoiseKind.Explosion);
            Open(from);
            return true;
        }

        public void PlaceCharge(Vector3 from, bool byPlayer)
        {
            if (State != DoorState.Locked && State != DoorState.Closed && State != DoorState.Wedged) return;
            if (ChargePlaced) return;
            detonateAt = Time.time + 2.5f;
            chargeByPlayer = byPlayer;
            obstacle.enabled = true;
            float side = Vector3.Dot(from - transform.position, transform.forward) < 0f ? -1f : 1f;
            charge = Shapes.Box("Breaching Charge", transform, new Vector3(0f, 1f, 0.07f * side), new Vector3(0.5f, 0.3f, 0.06f), new Color(0.9f, 0.15f, 0.1f), false, 2f);
            AudioManager.Play(Sound.Click, transform.position, 0.8f);
            if (byPlayer) UIManager.Notify("Breaching charge set. Stand back!");
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
            SetState(DoorState.Breached);
            Destroy(charge);
            if (wedge != null) Destroy(wedge);
            leaf.SetActive(false);
            enabled = false;

            Vector3 center = transform.position + Vector3.up;
            var effects = EffectsManager.Instance;
            effects.Burst(center, transform.forward, new Color(0.45f, 0.3f, 0.18f), 14, 5f, 0.12f);
            effects.Burst(center, -transform.forward, new Color(0.45f, 0.3f, 0.18f), 8, 4f, 0.1f);
            effects.FlashLight(center, new Color(1f, 0.7f, 0.4f), 6f, 10f, 0.25f);
            AudioManager.Play(Sound.Breach, center, 1f);
            Noise.Emit(center, 30f, NoiseKind.Explosion);
            AIManager.Instance.Stun(center, 4f, 3.5f, true);
            MissionManager.Instance.ReportBreach(this, chargeByPlayer);
            if (Opened != null) Opened(this);

            var game = GameManager.Instance;
            game.CameraRig.Shake(0.5f);
            foreach (var target in AIManager.Instance.PoliceTargets)
            {
                if (!target.IsAlive || Vector3.Distance(target.Position, transform.position) > 1.6f) continue;
                target.Damageable.TakeDamage(new DamageInfo { amount = 25f, point = center, direction = target.Position - transform.position, attacker = Team.Environment });
            }
        }

        // The room on the other side from a position.
        public RoomController FarRoom(Vector3 from)
        {
            return Vector3.Dot(from - transform.position, transform.forward) > 0f ? RoomBack : RoomFront;
        }
    }
}
