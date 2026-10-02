using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // One entry on the command wheel.
    public struct CommandOption
    {
        public string label;
        public SquadOrder order;
        public DoorAction action;
        public bool enabled;
        public string reason; // why it's disabled
    }

    // The player's link to the squad: officer selection (F1-F3 individual,
    // F4 everyone), the radial command wheel (hold Z), door stacks and
    // coordinated entries, radio chatter and the Team Leader's Coordinate buff.
    public class SquadCommandManager : MonoBehaviour
    {
        public struct RadioLine
        {
            public string speaker, text;
            public float time;
        }

        public static SquadCommandManager Instance { get; private set; }

        public bool WheelOpen { get; private set; }
        public Vector3 WheelPoint { get; private set; }
        public DoorController WheelDoor { get; private set; }
        public Vector2 WheelCenter { get; private set; } // screen position (GUI coordinates, y down)
        public int Hovered { get; private set; }
        public readonly List<CommandOption> Options = new List<CommandOption>();
        public readonly List<RadioLine> RadioLog = new List<RadioLine>();
        public Vector3 Heading { get; private set; }
        public string LastOrder { get; private set; }
        public float LastOrderTime { get; private set; }

        // Team Leader: squad reacts faster permanently, and much faster while Coordinate is active.
        public float ResponseBoost
        {
            get
            {
                float boost = LeaderInCharge ? 1.2f : 1f;
                if (Time.time < coordinateUntil) boost *= 1.5f;
                return boost;
            }
        }

        public float AccuracyBoost { get { return Time.time < coordinateUntil ? 1.2f : 1f; } }
        public bool CoordinateActive { get { return Time.time < coordinateUntil; } }

        bool LeaderInCharge
        {
            get
            {
                var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
                return player != null && player.Officer.role == OfficerRole.Leader;
            }
        }

        readonly Dictionary<DoorController, float> pendingClear = new Dictionary<DoorController, float>();
        readonly Dictionary<DoorController, DoorAction> stackActions = new Dictionary<DoorController, DoorAction>();
        readonly HashSet<DoorController> openWhenOrdered = new HashSet<DoorController>();
        readonly HashSet<DoorController> subscribed = new HashSet<DoorController>();
        readonly List<DoorController> scratch = new List<DoorController>();
        float coordinateUntil;
        Vector3 lastPlayerPosition;

        void Awake()
        {
            Instance = this;
            Heading = Vector3.forward;
        }

        public List<SquadAI> Squad { get { return AIManager.Instance.Officers; } }

        // Called when a mission starts.
        public void Begin()
        {
            WheelOpen = false;
            pendingClear.Clear();
            stackActions.Clear();
            openWhenOrdered.Clear();
            foreach (var door in subscribed) if (door != null) door.Opened -= OnDoorOpened;
            subscribed.Clear();
            RadioLog.Clear();
            pendingLines.Clear();
            coordinateUntil = 0f;
            LastOrder = null;
            var player = GameManager.Instance.Player;
            Heading = player != null ? player.transform.forward : Vector3.forward;
            if (player != null) lastPlayerPosition = player.Position;
        }

        public void StartCoordinate(float seconds)
        {
            coordinateUntil = Time.time + seconds;
        }

        // ---- Selection ----

        public bool AnySelected
        {
            get
            {
                foreach (var officer in Squad) if (officer.Selected && officer.IsAlive) return true;
                return false;
            }
        }

        public string SelectionLabel
        {
            get
            {
                if (!AnySelected) return "Whole squad";
                var names = new List<string>();
                foreach (var officer in Squad) if (officer.Selected && officer.IsAlive) names.Add(officer.Data.callsign);
                return string.Join(", ", names.ToArray());
            }
        }

        public void ToggleSelect(int index)
        {
            if (index < 0 || index >= Squad.Count) return;
            var officer = Squad[index];
            if (!officer.IsAlive)
            {
                UIManager.Notify(officer.Data.callsign + " is down");
                return;
            }
            officer.Selected = !officer.Selected;
            AudioManager.Ui(Sound.UiSelect, 0.4f);
        }

        public void SelectAll()
        {
            foreach (var officer in Squad) officer.Selected = false;
            AudioManager.Ui(Sound.UiSelect, 0.4f);
        }

        // The officers an order applies to: the selected ones, or everyone if nobody is selected.
        public List<SquadAI> Targets()
        {
            var list = new List<SquadAI>();
            bool any = AnySelected;
            foreach (var officer in Squad)
                if (officer.IsAlive && (!any || officer.Selected)) list.Add(officer);
            return list;
        }

        // ---- Orders ----

        public void Issue(SquadOrder order, Vector3 point, DoorController door, DoorAction action)
        {
            var targets = Targets();
            if (targets.Count == 0)
            {
                UIManager.Notify("No squadmates available", true);
                return;
            }
            if (order == SquadOrder.Stack && door == null)
            {
                UIManager.Notify("Point at a door to stack up");
                return;
            }
            if (order == SquadOrder.Stack)
            {
                stackActions[door] = action;
                if (door.IsPassable) openWhenOrdered.Add(door);
                else openWhenOrdered.Remove(door);
                pendingClear.Remove(door);
                if (subscribed.Add(door)) door.Opened += OnDoorOpened;
            }
            foreach (var officer in targets) officer.Command(order, point, door, action);

            LastOrder = OrderText(order, action) + "  (" + SelectionLabel + ")";
            LastOrderTime = Time.unscaledTime;
            AudioManager.Play2D(Sound.RadioOrder, 0.5f, 1f, SoundCategory.Interface);
            SayPlayer(PlayerLine(order, action));
            Acknowledge(targets[0], order, action);
            if (order == SquadOrder.Stack && door != null) MissionManager.Instance.ReportTarget(ObjectiveType.TrainingCommandSquad, door.Id);
            MissionManager.Instance.Stats.ordersGiven++;
        }

        // Planning mode: queue a waypoint for the selected officers.
        public void PlanWaypoint(Vector3 point, bool append)
        {
            var targets = Targets();
            if (targets.Count == 0) return;
            for (int i = 0; i < targets.Count; i++)
            {
                Vector3 spot = SquadFormation.Spread(point, i);
                if (append) targets[i].AddWaypoint(spot);
                else targets[i].Command(SquadOrder.MoveTo, point, null, DoorAction.None);
            }
            LastOrder = (append ? "Waypoint added" : "Move to waypoint") + "  (" + SelectionLabel + ")";
            LastOrderTime = Time.unscaledTime;
            AudioManager.RadioChirp(1.1f);
        }

        void Acknowledge(SquadAI officer, SquadOrder order, DoorAction action)
        {
            string text;
            switch (order)
            {
                case SquadOrder.Follow: text = "Copy, on you."; break;
                case SquadOrder.Hold: text = "Holding here."; break;
                case SquadOrder.Regroup: text = "Regrouping on you."; break;
                case SquadOrder.MoveTo: text = "Moving."; break;
                case SquadOrder.Cover: text = "Covering that area."; break;
                case SquadOrder.StayBehind: text = "Staying back, holding fire."; break;
                case SquadOrder.ReturnToPlayer: text = "Coming back to you."; break;
                case SquadOrder.AssistCivilians: text = "Moving to assist civilians."; break;
                case SquadOrder.Wait: text = "Standing by."; break;
                case SquadOrder.Stack:
                    text = action == DoorAction.Breach ? "Stacking up for breach." : action == DoorAction.Flash ? "Stacking up, flash and clear."
                        : action == DoorAction.Open ? "Stacking up, open and clear." : "Stacking up on the door.";
                    break;
                default: text = "Copy."; break;
            }
            // The reply comes a moment after the order, like a real radio exchange.
            pendingLines.Add(new PendingLine { officer = officer, text = text, at = Time.unscaledTime + 0.55f });
        }

        struct PendingLine
        {
            public SquadAI officer;
            public string text;
            public float at;
        }

        readonly List<PendingLine> pendingLines = new List<PendingLine>();
        float lastCivilianCall = -100f;

        static string PlayerLine(SquadOrder order, DoorAction action)
        {
            if (order == SquadOrder.Stack)
                return action == DoorAction.Breach ? "Breach and clear." : action == DoorAction.Flash ? "Flash and clear." : action == DoorAction.Open ? "Open and clear." : "Stack up.";
            switch (order)
            {
                case SquadOrder.Follow: return "On me.";
                case SquadOrder.Hold: return "Hold here.";
                case SquadOrder.Regroup: return "Regroup.";
                case SquadOrder.MoveTo: return "Move.";
                case SquadOrder.Cover: return "Cover that area.";
                case SquadOrder.StayBehind: return "Stay back.";
                case SquadOrder.ReturnToPlayer: return "Back to me.";
                case SquadOrder.AssistCivilians: return "Help the civilians.";
                default: return "Wait.";
            }
        }

        // The team leader speaking on the radio.
        public void SayPlayer(string text)
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            AddLine(player != null ? player.Officer.callsign : "Lead", text, 3);
        }

        // A squadmate who can see a newly found civilian reports it.
        public void CivilianLocated(CivilianAI civilian)
        {
            if (Time.unscaledTime - lastCivilianCall < 6f) return;
            SquadAI nearest = null;
            float best = 15f;
            foreach (var officer in Squad)
            {
                if (!officer.IsAlive) continue;
                float distance = Vector3.Distance(officer.Position, civilian.Position);
                if (distance < best) { best = distance; nearest = officer; }
            }
            if (nearest == null) return;
            lastCivilianCall = Time.unscaledTime;
            Say(nearest, civilian.State == CivilianState.Injured ? "Civilian located, they're hurt." : civilian.State == CivilianState.Captive ? "Hostage located!" : "Civilian located.", true);
        }

        public static string OrderText(SquadOrder order, DoorAction action)
        {
            if (order == SquadOrder.Stack)
            {
                switch (action)
                {
                    case DoorAction.Open: return "Open & clear";
                    case DoorAction.Breach: return "Breach & clear";
                    case DoorAction.Flash: return "Flash & clear";
                    default: return "Stack up";
                }
            }
            switch (order)
            {
                case SquadOrder.Follow: return "Follow me";
                case SquadOrder.Hold: return "Hold position";
                case SquadOrder.Regroup: return "Regroup";
                case SquadOrder.MoveTo: return "Move here";
                case SquadOrder.Cover: return "Cover area";
                case SquadOrder.StayBehind: return "Stay behind";
                case SquadOrder.ReturnToPlayer: return "Return to me";
                case SquadOrder.AssistCivilians: return "Assist civilians";
                case SquadOrder.Wait: return "Wait";
                default: return order.ToString();
            }
        }

        // ---- Radio ----

        public void Radio(SquadAI officer, string text) { Say(officer, text, true); }
        public void Radio(SquadAI officer, string text, bool chirp) { Say(officer, text, chirp); }

        void Say(SquadAI officer, string text, bool chirp)
        {
            string speaker = officer != null ? officer.Data.callsign : "TOC";
            if (!AddLine(speaker, text, officer != null ? officer.Index : 4)) return;
            if (chirp) AudioManager.RadioChirp(officer != null ? 0.95f + officer.Index * 0.06f : 1f);
        }

        bool AddLine(string speaker, string text, int voice)
        {
            // Don't repeat the same line from the same speaker within a few seconds.
            for (int i = RadioLog.Count - 1; i >= 0; i--)
                if (RadioLog[i].speaker == speaker && RadioLog[i].text == text && Time.unscaledTime - RadioLog[i].time < 4f) return false;
            RadioLog.Add(new RadioLine { speaker = speaker, text = text, time = Time.unscaledTime });
            if (RadioLog.Count > 6) RadioLog.RemoveAt(0);
            AudioManager.RadioVoice(voice);
            return true;
        }

        // ---- Stacks and entries ----

        // Position in the follow formation / move spread among the living squad.
        public int SlotFor(SquadAI officer)
        {
            int slot = 0;
            foreach (var other in Squad)
            {
                if (other == officer) return slot;
                if (other.IsAlive) slot++;
            }
            return slot;
        }

        // Position in the stack on the officer's door (0 = point man, who performs the door action).
        public int StackIndex(SquadAI officer)
        {
            var door = officer.StackDoor;
            if (door == null) return 0;
            int index = 0;
            foreach (var other in Squad)
            {
                if (other == officer) return index;
                if (other.IsAlive && other.StackDoor == door) index++;
            }
            return index;
        }

        public bool StackReady(DoorController door)
        {
            foreach (var officer in Squad)
                if (officer.IsAlive && officer.StackDoor == door && !officer.IsStacked) return false;
            return true;
        }

        public void NoteDoorAlreadyOpen(DoorController door)
        {
            openWhenOrdered.Add(door);
        }

        void OnDoorOpened(DoorController door)
        {
            DoorOpened(door);
        }

        // The door the squad is stacked on is now open: flow in after a short beat.
        public void DoorOpened(DoorController door)
        {
            if (door == null || pendingClear.ContainsKey(door)) return;
            DoorAction action;
            stackActions.TryGetValue(door, out action);
            // A plain "stack up" on a door that was already open just holds the stack.
            if (action == DoorAction.None && openWhenOrdered.Contains(door)) return;
            bool anyStacked = false;
            foreach (var officer in Squad) if (officer.IsAlive && officer.StackDoor == door) { anyStacked = true; break; }
            if (!anyStacked) return;
            pendingClear[door] = Time.time + 0.35f;
        }

        // Wait for a flashbang to go off before entering.
        public void DelayClear(DoorController door, float seconds)
        {
            pendingClear[door] = Time.time + seconds;
        }

        public SquadAI ChargeCarrier(DoorController door)
        {
            return Carrier(door, EquipmentKind.BreachingCharge, OfficerRole.Breacher);
        }

        public SquadAI FlashCarrier(DoorController door)
        {
            return Carrier(door, EquipmentKind.Flashbang, OfficerRole.Tactical);
        }

        SquadAI Carrier(DoorController door, EquipmentKind kind, OfficerRole preferred)
        {
            SquadAI best = null;
            foreach (var officer in Squad)
            {
                if (!officer.IsAlive || officer.StackDoor != door || officer.Inventory.CountOf(kind) <= 0) continue;
                if (best == null || (officer.Data.role == preferred && best.Data.role != preferred)) best = officer;
            }
            return best;
        }

        public bool IsClaimed(EnemyAI enemy, SquadAI asking)
        {
            foreach (var officer in Squad)
                if (officer != asking && officer.IsAlive && officer.RestrainTarget == enemy) return true;
            return false;
        }

        // ---- Per frame ----

        void Update()
        {
            var game = GameManager.Instance;
            if (game == null || !game.IsPlaying || game.Player == null)
            {
                WheelOpen = false;
                return;
            }
            TrackHeading(game.Player);
            ProcessPendingClears();
            for (int i = pendingLines.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < pendingLines[i].at) continue;
                var line = pendingLines[i];
                pendingLines.RemoveAt(i);
                if (line.officer != null && line.officer.IsAlive) Say(line.officer, line.text, true);
            }

            if (game.MapOpen || game.ConsoleOpen)
            {
                WheelOpen = false;
                return;
            }

            if (GameInput.Down(InputAction.SelectOfficer1)) ToggleSelect(0);
            if (GameInput.Down(InputAction.SelectOfficer2)) ToggleSelect(1);
            if (GameInput.Down(InputAction.SelectOfficer3)) ToggleSelect(2);
            if (GameInput.Down(InputAction.SelectAllOfficers)) SelectAll();

            if (!WheelOpen)
            {
                if (GameInput.Down(InputAction.CommandWheel) && game.Player.IsAlive)
                {
                    if (Squad.Count == 0) UIManager.Notify("You deployed without squadmates");
                    else OpenWheel(game);
                }
                return;
            }
            UpdateWheel();
        }

        void TrackHeading(PlayerController player)
        {
            Vector3 delta = player.Position - lastPlayerPosition;
            delta.y = 0f;
            lastPlayerPosition = player.Position;
            if (delta.sqrMagnitude > 0.0004f)
                Heading = Vector3.Slerp(Heading, delta.normalized, 0.08f);
        }

        void ProcessPendingClears()
        {
            if (pendingClear.Count == 0) return;
            scratch.Clear();
            foreach (var pair in pendingClear) if (Time.time >= pair.Value) scratch.Add(pair.Key);
            foreach (var door in scratch)
            {
                pendingClear.Remove(door);
                stackActions.Remove(door);
                int slot = 0;
                foreach (var officer in Squad)
                {
                    if (!officer.IsAlive || officer.StackDoor != door) continue;
                    officer.BeginClear(door, slot++);
                }
                if (slot > 0) Say(FirstAlive(), "Moving in!", true);
            }
        }

        SquadAI FirstAlive()
        {
            foreach (var officer in Squad) if (officer.IsAlive) return officer;
            return null;
        }

        // ---- Command wheel ----

        void OpenWheel(GameManager game)
        {
            Vector2 mouse = GameInput.MousePosition;
            Vector3 point;
            if (!game.CameraRig.ScreenToGround(mouse, 0f, out point)) point = game.Player.Position;
            WheelPoint = point;
            WheelDoor = AIManager.Instance.FindDoor(point, 2.5f, d => d.State != DoorState.Disabled);
            float margin = 235f * UITheme.Scale;
            WheelCenter = new Vector2(Mathf.Clamp(mouse.x, margin, Screen.width - margin), Mathf.Clamp(Screen.height - mouse.y, margin, Screen.height - margin));
            BuildOptions();
            Hovered = -1;
            WheelOpen = true;
            AudioManager.Ui(Sound.UiHover, 0.4f);
        }

        void BuildOptions()
        {
            Options.Clear();
            var targets = Targets();
            bool charge = false, flash = false;
            foreach (var officer in targets)
            {
                if (officer.Inventory.CountOf(EquipmentKind.BreachingCharge) > 0) charge = true;
                if (officer.Inventory.CountOf(EquipmentKind.Flashbang) > 0) flash = true;
            }
            var door = WheelDoor;
            if (door != null)
            {
                Add(SquadOrder.Stack, DoorAction.None, true, null);
                Add(SquadOrder.Stack, DoorAction.Open, door.State != DoorState.Breached, "Door is already breached");
                bool breachable = door.Breachable && (door.State == DoorState.Locked || door.State == DoorState.Closed || door.State == DoorState.Wedged);
                Add(SquadOrder.Stack, DoorAction.Breach, breachable && charge, !breachable ? "This door can't be breached" : "Nobody selected has a charge");
                Add(SquadOrder.Stack, DoorAction.Flash, flash && door.State != DoorState.Wedged, !flash ? "Nobody selected has a flashbang" : "Remove the wedge first");
                Add(SquadOrder.MoveTo, DoorAction.None, true, null);
                Add(SquadOrder.Cover, DoorAction.None, true, null);
                Add(SquadOrder.Hold, DoorAction.None, true, null);
                Add(SquadOrder.Follow, DoorAction.None, true, null);
            }
            else
            {
                Add(SquadOrder.Follow, DoorAction.None, true, null);
                Add(SquadOrder.Hold, DoorAction.None, true, null);
                Add(SquadOrder.Regroup, DoorAction.None, true, null);
                Add(SquadOrder.MoveTo, DoorAction.None, true, null);
                Add(SquadOrder.Cover, DoorAction.None, true, null);
                Add(SquadOrder.Stack, DoorAction.None, false, "Point the cursor at a door");
                Add(SquadOrder.StayBehind, DoorAction.None, true, null);
                Add(SquadOrder.ReturnToPlayer, DoorAction.None, true, null);
                Add(SquadOrder.AssistCivilians, DoorAction.None, true, null);
                Add(SquadOrder.Wait, DoorAction.None, true, null);
            }
        }

        void Add(SquadOrder order, DoorAction action, bool enabled, string reason)
        {
            Options.Add(new CommandOption { label = OrderText(order, action), order = order, action = action, enabled = enabled, reason = enabled ? null : reason });
        }

        void UpdateWheel()
        {
            Vector2 mouse = GameInput.MousePosition;
            Vector2 gui = new Vector2(mouse.x, Screen.height - mouse.y);
            Vector2 offset = gui - WheelCenter;
            int previous = Hovered;
            if (offset.magnitude < 45f * UITheme.Scale) Hovered = -1;
            else
            {
                // Option 0 at the top, clockwise.
                float angle = Mathf.Atan2(offset.x, -offset.y) * Mathf.Rad2Deg;
                if (angle < 0f) angle += 360f;
                float step = 360f / Options.Count;
                Hovered = Mathf.FloorToInt((angle + step * 0.5f) / step) % Options.Count;
            }
            if (Hovered != previous && Hovered >= 0) AudioManager.Ui(Sound.UiHover, 0.25f);

            int number = GameInput.NumberPressed;
            if (number >= 0 && number < Options.Count)
            {
                Choose(number);
                return;
            }
            if (GameInput.KeyDown(KeyCode.Mouse0) && Hovered >= 0)
            {
                Choose(Hovered);
                return;
            }
            if (GameInput.Cancel || GameInput.KeyDown(KeyCode.Mouse1))
            {
                WheelOpen = false;
                return;
            }
            if (!GameInput.Held(InputAction.CommandWheel))
            {
                if (Hovered >= 0) Choose(Hovered);
                else WheelOpen = false;
            }
        }

        void Choose(int index)
        {
            var option = Options[index];
            if (!option.enabled)
            {
                UIManager.Notify(option.reason ?? "Not available", true);
                AudioManager.Ui(Sound.Empty, 0.5f);
                WheelOpen = false;
                return;
            }
            WheelOpen = false;
            AudioManager.Ui(Sound.UiSelect, 0.5f);
            Vector3 point = WheelPoint;
            Issue(option.order, point, option.order == SquadOrder.Stack ? WheelDoor : null, option.action);
        }

        void OnDestroy()
        {
            foreach (var door in subscribed) if (door != null) door.Opened -= OnDoorOpened;
        }
    }
}
