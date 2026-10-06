using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    public enum DoorState { Closed, Open, Locked, Wedged, Breached, Disabled }

    // An interactive door. Closed doors open with E (AI opens them too).
    // Locked doors can have their lock picked (if allowed), be kicked in
    // (anyone can try; a Breacher never fails), have the lock blown with a
    // shotgun from up close, take a breaching charge (G, yellow stripes), or be
    // unlocked from a security console (electronic doors only take the console
    // or a charge). Wedged doors can't be opened until the wedge is removed.
    // Disabled doors are sealed. Some doors are trapped on the far side: a
    // flash device goes off when police open them, unless someone spotted it
    // first (recon camera or a squadmate's mirror) and disarmed it. Blocked
    // doors carve the NavMesh through a NavMeshObstacle, so the navigation mesh
    // is never rebuilt.
    public class DoorController : MonoBehaviour, IInteractable
    {
        // Drawn low for the top-down view, full height in first person (the collider is full height either
        // way). Built low; RegisterView lets the level switch it (V).
        const float VisualHeight = 1.4f;
        const float FirstPersonHeight = ViewMode.FirstPersonDoorHeight - 0.03f;
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
        public bool Trapped { get; private set; }
        public bool TrapKnown { get; private set; }
        public RoomController RoomFront { get; set; }
        public RoomController RoomBack { get; set; }
        public int Area { get; set; }
        public event System.Action<DoorController> Opened;

        Transform hinge;
        GameObject leaf, charge, wedge, trapMarker, trapWire, frameLeft, frameRight, frameTop;
        int trapSide;   // which face the device is on: +1 the forward side, -1 the back
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
                    case DoorState.Closed: return TrapKnown ? "[E] Disarm Trap (hold)" : "[E] Open Door";
                    case DoorState.Open: return "[E] Close Door";
                    case DoorState.Wedged: return "[E] Remove Wedge (hold)";
                    case DoorState.Locked:
                        if (TrapKnown) return "[E] Disarm Trap (hold)";
                        if (Pickable) return "[E] Pick Lock (hold)";
                        if (!Electronic) return "[E] Kick Door (hold)";
                        return "[E] Try Door";
                    default: return string.Empty;
                }
            }
        }

        // What you can tell by looking at the door (shown under the prompt, and
        // instead of a prompt for doors that can't be used).
        public string StatusText
        {
            get
            {
                if (TrapKnown && (State == DoorState.Closed || State == DoorState.Locked)) return "TRAPPED: a flash device on the far side - disarm it first";
                switch (State)
                {
                    case DoorState.Locked:
                        if (Electronic) return "Electronic lock: use a security console or a breaching charge (G)";
                        if (Breachable && Pickable) return "Locked: pick it, kick it, shotgun the lock or charge it (G)";
                        if (Breachable) return "Locked: kick it, shotgun the lock or charge it (G)";
                        if (Pickable) return "Locked: pick it, kick it or shotgun the lock";
                        return "Locked: kick it or shotgun the lock";
                    case DoorState.Wedged: return "Wedged shut";
                    case DoorState.Disabled: return "Sealed shut";
                    case DoorState.Breached: return "Breached";
                    case DoorState.Closed: return Breachable ? "Closed (breachable)" : null;
                    default: return null;
                }
            }
        }

        // Full height in first person: the leaf, the frame posts and a frame top.
        public void RegisterView(ViewParts view)
        {
            view.AddStretch(leaf, VisualHeight, FirstPersonHeight, SolidHeight, 0f, ProceduralTextures.TileMeters(Electronic || State == DoorState.Disabled ? SurfaceKind.Metal : SurfaceKind.Wood));
            view.AddStretch(frameLeft, VisualHeight + 0.06f, FirstPersonHeight + 0.06f, 0f);
            view.AddStretch(frameRight, VisualHeight + 0.06f, FirstPersonHeight + 0.06f, 0f);
            view.AddFirstPersonOnly(frameTop);
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
            Shapes.ApplySurface(door.leaf, color, electronic || state == DoorState.Disabled ? SurfaceKind.Metal : SurfaceKind.Wood);
            SurfaceTag.Set(door.leaf, electronic || state == DoorState.Disabled ? Swat.Surface.Metal : Swat.Surface.Wood);
            // Handles on both faces, and a frame so doorways read clearly from above.
            var handle = new Color(0.72f, 0.72f, 0.7f);
            Shapes.Box("Handle", door.leaf.transform, new Vector3(0.4f, -0.05f, 0.9f), new Vector3(0.08f, 0.03f, 0.9f), handle, false);
            Shapes.Box("Handle", door.leaf.transform, new Vector3(0.4f, -0.05f, -0.9f), new Vector3(0.08f, 0.03f, 0.9f), handle, false);
            var frame = new Color(0.2f, 0.21f, 0.23f);
            float frameHeight = VisualHeight + 0.06f;
            door.frameLeft = Shapes.Box("Frame", go.transform, new Vector3(-width * 0.5f, frameHeight * 0.5f, 0f), new Vector3(0.08f, frameHeight, 0.24f), frame, false);
            door.frameRight = Shapes.Box("Frame", go.transform, new Vector3(width * 0.5f, frameHeight * 0.5f, 0f), new Vector3(0.08f, frameHeight, 0.24f), frame, false);
            door.frameTop = Shapes.Box("Frame Top", go.transform, new Vector3(0f, FirstPersonHeight + 0.06f, 0f), new Vector3(width + 0.08f, 0.08f, 0.24f), frame, false);
            door.frameTop.SetActive(false);
            Shapes.Box("Threshold", go.transform, new Vector3(0f, 0.035f, 0f), new Vector3(width, 0.012f, 0.24f), frame, false);
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
            bool breacher = player.Officer.role == OfficerRole.Breacher;
            if (TrapKnown && (State == DoorState.Closed || State == DoorState.Locked)) return breacher ? 1.2f : 2.5f;
            switch (State)
            {
                case DoorState.Wedged: return 1f;
                case DoorState.Locked:
                    if (Pickable) return breacher ? 1.5f : 3f;
                    return Electronic ? 0f : 0.6f; // winding up a kick
                default: return 0f;
            }
        }

        public bool CanInteract(PlayerController player)
        {
            // Online matches keep every door as the match set it, so all players see the same doors.
            if (NetSession.Online && VersusMatch.Active) return false;
            return State != DoorState.Breached && State != DoorState.Disabled && !ChargePlaced;
        }

        public void Interact(PlayerController player)
        {
            if (TrapKnown && (State == DoorState.Closed || State == DoorState.Locked))
            {
                DisarmTrap();
                return;
            }
            switch (State)
            {
                case DoorState.Closed: Open(player.Position); break;
                case DoorState.Open: Close(); break;
                case DoorState.Wedged: RemoveWedge(); break;
                case DoorState.Locked:
                    if (Pickable) PickLock(player.Position);
                    else if (!Electronic) TryKick(player.Position, player.Officer.role == OfficerRole.Breacher ? 1f : 0.4f, true);
                    else
                    {
                        AudioManager.Play(Sound.DoorHandle, transform.position, 0.8f);
                        AudioManager.Play(Sound.DoorLocked, transform.position, 0.7f);
                        UIManager.Notify(StatusText);
                    }
                    break;
            }
        }

        // ---- Kicks, shotgun breaching and traps ----

        // A kick at a locked door: it may hold (loud either way, so the room knows you're coming).
        public bool TryKick(Vector3 from, float chance, bool byPlayer)
        {
            if (State != DoorState.Locked || Electronic) return false;
            if (Random.value >= chance)
            {
                AudioManager.Play(Sound.Kick, transform.position, 0.9f, Random.Range(0.85f, 0.95f));
                Noise.Emit(transform.position, 14f, NoiseKind.Explosion);
                if (byPlayer)
                {
                    GameManager.Instance.CameraRig.Shake(0.15f);
                    UIManager.Notify("The door holds. Kick again, or try another way.");
                }
                return false;
            }
            Kick(from);
            return true;
        }

        // A breaching round at the lock from up close blows it out and the door swings in.
        public bool ShotgunBreach(Vector3 from, bool byPlayer)
        {
            if (Electronic || (State != DoorState.Locked && State != DoorState.Closed))
            {
                if (byPlayer && State == DoorState.Wedged) UIManager.Notify("The wedge holds the door shut");
                return false;
            }
            SetState(DoorState.Closed);
            Vector3 lockPoint = transform.position + transform.right * (Width * 0.25f) + Vector3.up;
            Vector3 toward = from - transform.position;
            toward.y = 0f;
            EffectsManager.Instance.Burst(lockPoint, toward.sqrMagnitude > 0.01f ? toward.normalized : transform.forward, new Color(0.45f, 0.32f, 0.2f), 10, 4f, 0.08f);
            AudioManager.Play(Sound.Kick, transform.position, 0.8f, 1.2f);
            Noise.Emit(transform.position, 12f, NoiseKind.Explosion);
            StunBehind(from, 1.2f);
            if (byPlayer) UIManager.Notify("Lock blown");
            Open(from);
            MissionManager.Instance.ReportBreach(this, byPlayer);
            return true;
        }

        // Whoever stands right behind a door that's forced open gets knocked off balance by it.
        void StunBehind(Vector3 from, float seconds)
        {
            Vector3 away = transform.position - from;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) return;
            AIManager.Instance.Stun(transform.position + away.normalized * 0.9f + Vector3.up * 1.4f, 1.4f, seconds, false);
        }

        // Mission setup: a flash device on one face of a closed door.
        public void SetTrap(int side)
        {
            if (Electronic || (State != DoorState.Closed && State != DoorState.Locked)) return;
            Trapped = true;
            trapSide = side >= 0 ? 1 : -1;
        }

        // Spotted with a recon camera or a mirror: shown on the door from now on.
        public bool RevealTrap()
        {
            if (!Trapped || TrapKnown) return false;
            TrapKnown = true;
            trapMarker = Shapes.Box("Door Trap", transform, new Vector3(Width * 0.3f, 0.35f, 0.09f * trapSide), new Vector3(0.18f, 0.12f, 0.05f), new Color(0.95f, 0.2f, 0.15f), false, 2f);
            trapWire = Shapes.Box("Trap Wire", transform, new Vector3(0f, 0.35f, 0.09f * trapSide), new Vector3(Width * 0.6f, 0.015f, 0.015f), new Color(0.9f, 0.85f, 0.3f), false, 1.5f);
            return true;
        }

        void ClearTrap()
        {
            Trapped = TrapKnown = false;
            if (trapMarker != null) Destroy(trapMarker);
            if (trapWire != null) Destroy(trapWire);
        }

        public void DisarmTrap()
        {
            if (!Trapped) return;
            ClearTrap();
            AudioManager.Play(Sound.Click, transform.position, 0.8f, 0.8f);
            UIManager.Notify("Trap disarmed");
        }

        // Opened from the side without the device: it goes off in the opener's face.
        void SpringTrap(Vector3 from)
        {
            ClearTrap();
            Vector3 toward = from - transform.position;
            toward.y = 0f;
            Vector3 at = transform.position + (toward.sqrMagnitude > 0.01f ? toward.normalized : transform.forward) * 0.7f;
            var flash = GameData.Equipment("flashbang");
            if (flash != null) Flashbang.Detonate(at, flash, 1f, false);
            Noise.Emit(transform.position, 25f, NoiseKind.Alarm);
            UIManager.Notify("The door was trapped! Mirror doors before you open them.", true);
        }

        bool TrapFacesAway(Vector3 from)
        {
            float side = Vector3.Dot(from - transform.position, transform.forward) >= 0f ? 1f : -1f;
            return side != trapSide;
        }

        // Swings away from whoever opened it.
        public void Open(Vector3 from)
        {
            if (State != DoorState.Closed) return;
            if (Trapped && TrapFacesAway(from)) SpringTrap(from);
            SetState(DoorState.Open);
            float side = Vector3.Dot(from - transform.position, transform.forward) > 0f ? 1f : -1f;
            targetAngle = 95f * side;
            enabled = true;
            AudioManager.Play(Electronic ? Sound.DoorOpenMetal : Sound.DoorOpen, transform.position, 0.6f, Random.Range(0.92f, 1.08f));
            AudioManager.Play(Sound.DoorHandle, transform.position, 0.4f, Random.Range(0.9f, 1.1f));
            Noise.Emit(transform.position, 5f, NoiseKind.Door);
            MissionManager.Instance.ReportDoor(this);
            if (Opened != null) Opened(this);
        }

        // Game modes: every usable door swings fully open (sealed doors stay shut).
        public void OpenForMatch()
        {
            if (State == DoorState.Disabled || State == DoorState.Breached || State == DoorState.Open) return;
            SetState(DoorState.Open);
            targetAngle = 95f;
            enabled = true;
        }

        public void Close()
        {
            if (State != DoorState.Open) return;
            SetState(DoorState.Closed);
            targetAngle = 0f;
            enabled = true;
            AudioManager.Play(Sound.DoorClose, transform.position, 0.6f, Random.Range(0.92f, 1.08f));
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
            StunBehind(from, 1.5f);
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
            ClearTrap(); // the blast wrecks any trap along with the door
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
