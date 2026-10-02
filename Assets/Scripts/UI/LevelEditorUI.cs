using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // The level creator: a top-down grid editor (1 m cells) for building
    // custom levels. Drag out rooms (walls appear automatically), click walls
    // to add doors, place the team start, suspects, civilians, objectives,
    // security devices and props, then press Play to run the level through the
    // normal briefing, squad and loadout screens. Levels are saved as JSON.
    //
    // Mouse: left = use tool, right = erase, middle-drag or Select-drag on empty
    // space = pan, wheel = zoom. Keys: 1-7 tools, R rotate, Delete remove,
    // Ctrl+Z undo, Ctrl+S save, WASD/arrows pan, Esc back.
    public class LevelEditorUI
    {
        enum Tool { Select, Room, Door, People, Objects, Props, Erase }

        static readonly string[] ToolNames = { "Select / move", "Room", "Door", "People", "Objectives", "Props", "Erase" };
        static readonly string[] ToolHints =
        {
            "Click to select. Drag people and objects to move them. Drag empty space to pan.",
            "Drag a rectangle to add a room. Walls are built where rooms meet each other or the outside.",
            "Click a wall to add a door (two cells wide, away from corners). Click a door again to change its type.",
            "Click to place. R turns the next placement (and the selected person).",
            "Click to place. The Team Start goes outside; the van parks to its right.",
            "Click to place furniture and cover. R turns it.",
            "Click anything to remove it. Right-click erases with any tool.",
        };
        static readonly CustomObjectType[] PeopleTypes =
        {
            CustomObjectType.TeamStart, CustomObjectType.ArmedSuspect, CustomObjectType.UnarmedSuspect, CustomObjectType.NervousSuspect,
            CustomObjectType.Guard, CustomObjectType.ArmoredSuspect, CustomObjectType.Leader, CustomObjectType.Civilian,
            CustomObjectType.Hostage, CustomObjectType.InjuredCivilian, CustomObjectType.HidingCivilian,
        };
        static readonly CustomObjectType[] ObjectiveTypes =
        {
            CustomObjectType.TeamStart, CustomObjectType.Extraction, CustomObjectType.SafeZone, CustomObjectType.Evidence,
            CustomObjectType.Console, CustomObjectType.Camera, CustomObjectType.AlarmPanel,
        };
        static readonly CustomObjectType[] PropTypes =
        {
            CustomObjectType.Desk, CustomObjectType.Table, CustomObjectType.Shelf, CustomObjectType.Crate, CustomObjectType.Couch,
            CustomObjectType.Plant, CustomObjectType.Counter, CustomObjectType.Bed, CustomObjectType.Car, CustomObjectType.Lamp,
        };
        static readonly string[] DoorTypeNames = { "Open doorway", "Door", "Locked (pick or breach)", "Locked (breach only)", "Electronic lock" };
        static readonly string[] TimeNames = { "Day", "Evening", "Night" };
        static readonly string[] DifficultyNames = { "1 star", "2 stars", "3 stars" };
        static readonly string[] SquadNames = { "Solo", "1 squadmate", "2 squadmates", "3 squadmates" };
        static readonly string[] GroundNames = { "Grass", "Asphalt" };

        // Kept across screens so returning from a test run continues where you left off.
        static CustomLevel level;
        static bool dirty, viewFitted;
        static Vector2 pan;
        static float zoom = 18f;

        Tool tool = Tool.Room;
        CustomRoomType roomType = CustomRoomType.Office;
        CustomDoorType doorType = CustomDoorType.Door;
        CustomObjectType placeType = CustomObjectType.TeamStart;
        int placeYaw;
        int selRoom = -1, selDoor = -1, selObject = -1;
        bool draggingRoom, panning, movingObject;
        Vector2Int dragStart, dragEnd;
        Rect canvas;
        CustomLevelGeometry geometry;
        readonly List<string> errors = new List<string>(), warnings = new List<string>();
        readonly List<string> undo = new List<string>();
        bool validationStale = true;
        string status;
        float statusTime = -10f;

        // Overlays
        bool browserOpen, confirmLeave;
        List<CustomLevel> browserLevels;
        int browserPage;
        string confirmDelete;
        System.Action pendingAction;
        GUIStyle fieldStyle;

        public void Draw(GameManager game)
        {
            if (level == null)
            {
                // Start with the most recently saved level (the example level the first time).
                var saved = CustomLevelStore.List();
                Load(saved.Count > 0 ? saved[0] : CustomLevelStore.Sample(), false);
            }
            if (geometry == null || validationStale) Refresh();
            float w = UITheme.Width, h = UITheme.Height;
            if (fieldStyle == null)
            {
                fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(16 * Mathf.Clamp(SaveManager.Settings.textSize, 0.85f, 1.4f)) };
                fieldStyle.padding = new RectOffset(8, 8, 6, 6);
            }

            canvas = new Rect(300f, 64f, w - 300f - 360f, h - 64f - 38f);
            if (!viewFitted) FitView();

            UITheme.Fill(new Rect(0f, 0f, w, h), new Color(0.03f, 0.04f, 0.06f, 1f));
            bool overlay = browserOpen || confirmLeave;
            if (!overlay) HandleCanvasInput(game);
            DrawCanvas();
            DrawTopBar(game, w);
            DrawToolbar(h);
            DrawRightPanel(w, h);
            DrawStatusBar(w, h);
            if (browserOpen) DrawBrowser(game, w, h);
            if (confirmLeave) DrawConfirm(w, h);
        }

        // ---- Level management ----

        void Load(CustomLevel next, bool announce)
        {
            // Edit a copy so unsaved changes never leak into the saved/cached version.
            level = next.Clone();
            dirty = false;
            viewFitted = false;
            undo.Clear();
            selRoom = selDoor = selObject = -1;
            validationStale = true;
            if (announce) Say("Opened \"" + level.name + "\"");
        }

        void Refresh()
        {
            geometry = new CustomLevelGeometry(level);
            CustomLevelValidator.Validate(level, errors, warnings);
            validationStale = false;
        }

        void Changed()
        {
            dirty = true;
            validationStale = true;
            geometry = new CustomLevelGeometry(level);
        }

        void PushUndo()
        {
            undo.Add(JsonUtility.ToJson(level));
            if (undo.Count > 40) undo.RemoveAt(0);
        }

        void Undo()
        {
            if (undo.Count == 0) { Say("Nothing to undo"); return; }
            string id = level.id;
            level = JsonUtility.FromJson<CustomLevel>(undo[undo.Count - 1]);
            level.id = id;
            undo.RemoveAt(undo.Count - 1);
            selRoom = selDoor = selObject = -1;
            Changed();
            Say("Undone");
        }

        bool Save()
        {
            if (string.IsNullOrEmpty(level.name)) level.name = "Untitled Level";
            string error;
            if (!CustomLevelStore.Save(level, out error))
            {
                Say("Could not save: " + error);
                return false;
            }
            dirty = false;
            Say("Saved \"" + level.name + "\"");
            return true;
        }

        void Play(GameManager game)
        {
            Refresh();
            if (errors.Count > 0)
            {
                Say("Fix the problems listed on the right before playing.");
                AudioManager.Ui(Sound.Empty, 0.5f);
                return;
            }
            if (!Save()) return;
            game.PlayCustomLevel(level);
        }

        // Runs an action now, or after asking if there are unsaved changes.
        void Guarded(System.Action action)
        {
            if (!dirty) { action(); return; }
            pendingAction = action;
            confirmLeave = true;
        }

        void Say(string text)
        {
            status = text;
            statusTime = Time.unscaledTime;
        }

        // ---- View ----

        void FitView()
        {
            float lw = geometry.Width, lh = geometry.Height;
            zoom = Mathf.Clamp(Mathf.Min((canvas.width - 60f) / lw, (canvas.height - 60f) / lh), 6f, 40f);
            pan = new Vector2((canvas.width - lw * zoom) * 0.5f, (canvas.height - lh * zoom) * 0.5f);
            viewFitted = true;
        }

        Vector2 ToScreen(float x, float z)
        {
            return new Vector2(canvas.x + pan.x + x * zoom, canvas.y + pan.y + (geometry.Height - z) * zoom);
        }

        Vector2 ToWorld(Vector2 screen)
        {
            return new Vector2((screen.x - canvas.x - pan.x) / zoom, geometry.Height - (screen.y - canvas.y - pan.y) / zoom);
        }

        Rect WorldRect(float x0, float z0, float x1, float z1)
        {
            Vector2 a = ToScreen(x0, z1), b = ToScreen(x1, z0);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        Vector2Int Cell(Vector2 world)
        {
            return new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(world.x), 0, geometry.Width - 1), Mathf.Clamp(Mathf.FloorToInt(world.y), 0, geometry.Height - 1));
        }

        static Vector2 Snap(Vector2 world) { return new Vector2(Mathf.Floor(world.x) + 0.5f, Mathf.Floor(world.y) + 0.5f); }

        // ---- Input ----

        void HandleCanvasInput(GameManager game)
        {
            var e = Event.current;
            bool typing = GUIUtility.keyboardControl != 0;

            if (e.type == EventType.Repaint && !typing)
            {
                Vector2 move = Vector2.zero;
                if (GameInput.KeyHeld(KeyCode.A) || GameInput.KeyHeld(KeyCode.LeftArrow)) move.x += 1f;
                if (GameInput.KeyHeld(KeyCode.D) || GameInput.KeyHeld(KeyCode.RightArrow)) move.x -= 1f;
                if (GameInput.KeyHeld(KeyCode.W) || GameInput.KeyHeld(KeyCode.UpArrow)) move.y += 1f;
                if (GameInput.KeyHeld(KeyCode.S) || GameInput.KeyHeld(KeyCode.DownArrow)) move.y -= 1f;
                if (!(e.control || e.command)) pan += move * 700f * Time.unscaledDeltaTime;
            }

            if (e.type == EventType.KeyDown && !typing) HandleKey(e, game);

            Vector2 mouse = e.mousePosition;
            Vector2 world = ToWorld(mouse);
            bool inside = canvas.Contains(mouse);

            if (e.type == EventType.ScrollWheel && inside)
            {
                float factor = e.delta.y > 0f ? 0.88f : 1.12f;
                float next = Mathf.Clamp(zoom * factor, 5f, 48f);
                pan = mouse - new Vector2(canvas.x, canvas.y) - new Vector2(world.x * next, (geometry.Height - world.y) * next);
                zoom = next;
                e.Use();
                return;
            }

            if (e.type == EventType.MouseDown && inside)
            {
                GUIUtility.keyboardControl = 0;
                if (e.button == 2) { panning = true; e.Use(); return; }
                if (e.button == 1) { EraseAt(world); e.Use(); return; }
                if (e.button != 0) return;
                switch (tool)
                {
                    case Tool.Select: SelectAt(world, true); break;
                    case Tool.Room:
                        draggingRoom = true;
                        dragStart = dragEnd = Cell(world);
                        break;
                    case Tool.Door: DoorAt(world); break;
                    case Tool.People:
                    case Tool.Objects:
                    case Tool.Props: PlaceAt(world); break;
                    case Tool.Erase: EraseAt(world); break;
                }
                e.Use();
                return;
            }

            if (e.type == EventType.MouseDrag)
            {
                if (panning) { pan += e.delta; e.Use(); return; }
                if (draggingRoom) { dragEnd = Cell(world); e.Use(); return; }
                if (movingObject && selObject >= 0 && selObject < level.objects.Count)
                {
                    var snapped = Snap(world);
                    var o = level.objects[selObject];
                    if (!Mathf.Approximately(o.x, snapped.x) || !Mathf.Approximately(o.z, snapped.y))
                    {
                        o.x = Mathf.Clamp(snapped.x, 0.5f, geometry.Width - 0.5f);
                        o.z = Mathf.Clamp(snapped.y, CustomStartMin(o), geometry.Height - 0.5f);
                        Changed();
                    }
                    e.Use();
                    return;
                }
            }

            if (e.type == EventType.MouseUp)
            {
                if (draggingRoom)
                {
                    draggingRoom = false;
                    AddRoom(dragStart, dragEnd);
                    e.Use();
                }
                panning = false;
                movingObject = false;
            }
        }

        // The team start may sit below the grid (in the margin) so the van has room.
        static float CustomStartMin(CustomObject o) { return CustomLevelValidator.AllowedInMargin(o.type) ? -CustomLevelValidator.Margin + 0.5f : 0.5f; }

        void HandleKey(Event e, GameManager game)
        {
            bool ctrl = e.control || e.command;
            switch (e.keyCode)
            {
                case KeyCode.Alpha1: tool = Tool.Select; break;
                case KeyCode.Alpha2: tool = Tool.Room; break;
                case KeyCode.Alpha3: tool = Tool.Door; break;
                case KeyCode.Alpha4: tool = Tool.People; break;
                case KeyCode.Alpha5: tool = Tool.Objects; break;
                case KeyCode.Alpha6: tool = Tool.Props; break;
                case KeyCode.Alpha7: tool = Tool.Erase; break;
                case KeyCode.R: Rotate(); break;
                case KeyCode.Delete:
                case KeyCode.Backspace: DeleteSelection(); break;
                case KeyCode.Z: if (ctrl) Undo(); else return; break;
                case KeyCode.S: if (ctrl) Save(); else return; break;
                case KeyCode.F: FitView(); break;
                case KeyCode.Escape:
                    if (selRoom >= 0 || selDoor >= 0 || selObject >= 0) selRoom = selDoor = selObject = -1;
                    else Guarded(game.GoToMainMenu);
                    break;
                default: return;
            }
            e.Use();
        }

        void Rotate()
        {
            if (selObject >= 0 && selObject < level.objects.Count)
            {
                PushUndo();
                var o = level.objects[selObject];
                o.yaw = (o.yaw + (o.type == CustomObjectType.Camera ? 45 : 90)) % 360;
                Changed();
                return;
            }
            placeYaw = (placeYaw + (placeType == CustomObjectType.Camera ? 45 : 90)) % 360;
            Say("Facing " + placeYaw + " degrees");
        }

        // ---- Editing operations ----

        void AddRoom(Vector2Int a, Vector2Int b)
        {
            int x0 = Mathf.Min(a.x, b.x), z0 = Mathf.Min(a.y, b.y);
            int w = Mathf.Abs(a.x - b.x) + 1, hh = Mathf.Abs(a.y - b.y) + 1;
            if (w < 2 || hh < 2) { Say("Rooms need to be at least 2 x 2 cells: drag a bigger rectangle."); return; }
            if (Overlaps(x0, z0, w, hh, -1)) { Say("Rooms can't overlap. Drag next to an existing room to share a wall."); AudioManager.Ui(Sound.Empty, 0.4f); return; }
            PushUndo();
            level.rooms.Add(new CustomRoom { x = x0, z = z0, w = w, h = hh, type = roomType });
            CleanDoors();
            Changed();
            selRoom = level.rooms.Count - 1;
            selDoor = selObject = -1;
            AudioManager.Ui(Sound.UiSelect, 0.35f);
            Say("Added " + CustomLevelBuilder.RoomTypeNames[(int)roomType] + " (" + w + " x " + hh + " m)");
        }

        bool Overlaps(int x, int z, int w, int h, int ignore)
        {
            for (int i = 0; i < level.rooms.Count; i++)
            {
                if (i == ignore) continue;
                var r = level.rooms[i];
                if (x < r.x + r.w && x + w > r.x && z < r.z + r.h && z + h > r.z) return true;
            }
            return false;
        }

        // Removes doors left without a wall after rooms change.
        void CleanDoors()
        {
            var geo = new CustomLevelGeometry(level);
            int removed = level.doors.RemoveAll(d => !geo.DoorFits(d));
            if (removed > 0) Say(removed + " door(s) no longer on a wall were removed.");
        }

        void DoorAt(Vector2 world)
        {
            int gx = Mathf.RoundToInt(world.x), gz = Mathf.RoundToInt(world.y);
            float dx = Mathf.Abs(world.x - gx), dz = Mathf.Abs(world.y - gz);
            // Try the wall line closest to the cursor first.
            bool[] order = dz <= dx ? new[] { false, true } : new[] { true, false };
            string reason = "Click on a wall.";
            foreach (bool alongZ in order)
            {
                int existing = FindDoor(gx, gz, alongZ, 0);
                if (existing >= 0)
                {
                    PushUndo();
                    var door = level.doors[existing];
                    door.type = (CustomDoorType)(((int)door.type + 1) % DoorTypeNames.Length);
                    doorType = door.type;
                    selDoor = existing;
                    selRoom = selObject = -1;
                    Changed();
                    Say("Door: " + DoorTypeNames[(int)door.type]);
                    return;
                }
                if (!geometry.DoorFits(gx, gz, alongZ, out reason)) continue;
                if (FindDoor(gx, gz, alongZ, 1) >= 0) { reason = "Too close to another door."; continue; }
                PushUndo();
                level.doors.Add(new CustomDoor { x = gx, z = gz, alongZ = alongZ, type = doorType });
                selDoor = level.doors.Count - 1;
                selRoom = selObject = -1;
                Changed();
                AudioManager.Play2D(Sound.DoorHandle, 0.4f, 1f, SoundCategory.Interface);
                Say("Added " + DoorTypeNames[(int)doorType].ToLowerInvariant());
                return;
            }
            Say(reason);
            AudioManager.Ui(Sound.Empty, 0.4f);
        }

        // A door on the same wall line within 'spacing' cells of (x, z); 0 = exactly there.
        int FindDoor(int x, int z, bool alongZ, int spacing)
        {
            for (int i = 0; i < level.doors.Count; i++)
            {
                var d = level.doors[i];
                if (d.alongZ != alongZ) continue;
                int line = alongZ ? d.x : d.z, along = alongZ ? d.z : d.x;
                int myLine = alongZ ? x : z, myAlong = alongZ ? z : x;
                if (line == myLine && Mathf.Abs(along - myAlong) <= spacing) return i;
            }
            return -1;
        }

        void PlaceAt(Vector2 world)
        {
            var p = Snap(world);
            if (p.x < 0f || p.x > geometry.Width) return;
            bool single = placeType == CustomObjectType.TeamStart || placeType == CustomObjectType.Extraction || placeType == CustomObjectType.Leader;
            float minZ = CustomStartMin(new CustomObject { type = placeType });
            if (p.y < minZ || p.y > geometry.Height) { Say("Outside the map."); return; }
            if (!single && level.objects.Count >= CustomLevelValidator.MaxObjects) { Say("Object limit reached."); return; }
            PushUndo();
            CustomObject target = single ? level.Find(placeType) : null;
            if (target == null)
            {
                target = new CustomObject { type = placeType };
                level.objects.Add(target);
            }
            target.x = p.x;
            target.z = p.y;
            target.yaw = placeYaw;
            selObject = level.objects.IndexOf(target);
            selRoom = selDoor = -1;
            Changed();
            AudioManager.Ui(Sound.UiHover, 0.35f);
        }

        void SelectAt(Vector2 world, bool allowMove)
        {
            selRoom = selDoor = selObject = -1;
            int obj = ObjectAt(world);
            if (obj >= 0)
            {
                selObject = obj;
                if (allowMove) { movingObject = true; PushUndo(); }
                return;
            }
            int door = DoorNear(world);
            if (door >= 0) { selDoor = door; return; }
            int room = geometry.RoomAt(world.x, world.y);
            if (room >= 0) { selRoom = room; return; }
            panning = true;
        }

        int ObjectAt(Vector2 world)
        {
            for (int i = level.objects.Count - 1; i >= 0; i--)
            {
                var o = level.objects[i];
                float radius = CustomLevel.IsZone(o.type) ? 2f : 0.6f;
                if (Vector2.Distance(new Vector2(o.x, o.z), world) < radius) return i;
            }
            return -1;
        }

        int DoorNear(Vector2 world)
        {
            for (int i = 0; i < level.doors.Count; i++)
            {
                var d = level.doors[i];
                var center = new Vector2(d.x, d.z);
                var size = d.alongZ ? new Vector2(0.6f, 1.2f) : new Vector2(1.2f, 0.6f);
                if (Mathf.Abs(world.x - center.x) < size.x && Mathf.Abs(world.y - center.y) < size.y) return i;
            }
            return -1;
        }

        void EraseAt(Vector2 world)
        {
            int obj = ObjectAt(world);
            if (obj >= 0)
            {
                PushUndo();
                Say("Removed " + ObjectName(level.objects[obj].type).ToLowerInvariant());
                level.objects.RemoveAt(obj);
                ClearSelection();
                Changed();
                return;
            }
            int door = DoorNear(world);
            if (door >= 0)
            {
                PushUndo();
                level.doors.RemoveAt(door);
                ClearSelection();
                Changed();
                Say("Removed door");
                return;
            }
            int room = geometry.RoomAt(world.x, world.y);
            if (room >= 0)
            {
                PushUndo();
                Say("Removed " + CustomLevelBuilder.RoomName(level, room));
                level.rooms.RemoveAt(room);
                CleanDoors();
                ClearSelection();
                Changed();
            }
        }

        void DeleteSelection()
        {
            if (selObject >= 0 && selObject < level.objects.Count) { PushUndo(); level.objects.RemoveAt(selObject); }
            else if (selDoor >= 0 && selDoor < level.doors.Count) { PushUndo(); level.doors.RemoveAt(selDoor); }
            else if (selRoom >= 0 && selRoom < level.rooms.Count) { PushUndo(); level.rooms.RemoveAt(selRoom); CleanDoors(); }
            else return;
            ClearSelection();
            Changed();
        }

        void ClearSelection() { selRoom = selDoor = selObject = -1; }

        // ---- Canvas drawing ----

        void DrawCanvas()
        {
            float lw = geometry.Width, lh = geometry.Height;
            UITheme.Fill(canvas, new Color(0.05f, 0.07f, 0.09f, 1f));
            // The play area and the ground around it.
            UITheme.Fill(WorldRect(-10f, -10f, lw + 10f, lh + 10f), level.grassGround ? new Color(0.12f, 0.18f, 0.12f) : new Color(0.12f, 0.13f, 0.15f));
            UITheme.Fill(WorldRect(0f, 0f, lw, lh), level.grassGround ? new Color(0.15f, 0.22f, 0.15f) : new Color(0.15f, 0.16f, 0.18f));

            // Grid
            if (zoom >= 9f)
                for (int x = 0; x <= lw; x++)
                {
                    var a = ToScreen(x, 0f);
                    UITheme.Fill(new Rect(a.x, a.y - lh * zoom, 1f, lh * zoom), new Color(1f, 1f, 1f, x % 5 == 0 ? 0.12f : 0.05f));
                }
            if (zoom >= 9f)
                for (int z = 0; z <= lh; z++)
                {
                    var a = ToScreen(0f, z);
                    UITheme.Fill(new Rect(a.x, a.y, lw * zoom, 1f), new Color(1f, 1f, 1f, z % 5 == 0 ? 0.12f : 0.05f));
                }
            UITheme.Frame(WorldRect(0f, 0f, lw, lh), new Color(1f, 1f, 1f, 0.3f), 2f);

            // Rooms
            for (int i = 0; i < level.rooms.Count; i++)
            {
                var r = level.rooms[i];
                var rect = WorldRect(r.x, r.z, r.x + r.w, r.z + r.h);
                var color = CustomLevelBuilder.FloorColors[Mathf.Clamp((int)r.type, 0, CustomLevelBuilder.FloorColors.Length - 1)];
                UITheme.Fill(rect, new Color(color.r * 0.8f, color.g * 0.8f, color.b * 0.8f, 0.95f));
                if (i == selRoom) UITheme.Frame(rect, UITheme.Accent, 3f);
                if (rect.width > 60f && rect.height > 26f)
                    UITheme.ShadowText(new Rect(rect.x + 4f, rect.y + 3f, rect.width - 8f, 20f), CustomLevelBuilder.RoomName(level, i), Mathf.Clamp(Mathf.RoundToInt(zoom * 0.7f), 11, 16), new Color(1f, 1f, 1f, 0.85f));
            }

            // Walls (the same segments the game builds)
            float thick = Mathf.Max(2f, zoom * 0.2f);
            foreach (var s in geometry.Segments())
            {
                var color = s.exterior ? new Color(0.85f, 0.82f, 0.76f) : new Color(0.7f, 0.72f, 0.75f);
                float t = s.exterior ? thick * 1.4f : thick;
                if (s.alongZ)
                {
                    Vector2 a = ToScreen(s.line, s.to);
                    UITheme.Fill(new Rect(a.x - t * 0.5f, a.y, t, (s.to - s.from) * zoom), color);
                }
                else
                {
                    Vector2 a = ToScreen(s.from, s.line);
                    UITheme.Fill(new Rect(a.x, a.y - t * 0.5f, (s.to - s.from) * zoom, t), color);
                }
            }

            // Doors
            for (int i = 0; i < level.doors.Count; i++)
            {
                var d = level.doors[i];
                bool fits = geometry.DoorFits(d);
                Vector2 c = ToScreen(d.x, d.z);
                float len = 1.6f * zoom, t = Mathf.Max(5f, zoom * 0.35f);
                var rect = d.alongZ ? new Rect(c.x - t * 0.5f, c.y - len * 0.5f, t, len) : new Rect(c.x - len * 0.5f, c.y - t * 0.5f, len, t);
                UITheme.Fill(rect, fits ? DoorColor(d.type) : UITheme.Bad);
                if (i == selDoor) UITheme.Frame(new Rect(rect.x - 3f, rect.y - 3f, rect.width + 6f, rect.height + 6f), UITheme.Accent, 2f);
            }

            // Van preview from the team start
            var start = level.Find(CustomObjectType.TeamStart);
            if (start != null)
            {
                Vector3 parking, back, right;
                CustomLevelGeometry.VanPath(start, out parking, out back, out right);
                Vector3 far = parking + back * 26f;
                UITheme.LineTo(ToScreen(parking.x, parking.z), ToScreen(far.x, far.z), new Color(0.4f, 0.6f, 1f, 0.25f), 2.6f * zoom);
                bool sideways = start.yaw % 180 != 0;
                float vw = sideways ? 6.2f : 2.4f, vd = sideways ? 2.4f : 6.2f;
                var van = WorldRect(parking.x - vw * 0.5f, parking.z - vd * 0.5f, parking.x + vw * 0.5f, parking.z + vd * 0.5f);
                UITheme.Fill(van, new Color(0.15f, 0.2f, 0.35f, 0.8f));
                UITheme.Frame(van, new Color(0.5f, 0.7f, 1f, 0.8f), 1.5f);
                if (van.width > 30f) UITheme.Text(van, "VAN", 11, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter, true);
            }

            // Objects
            for (int i = 0; i < level.objects.Count; i++) DrawObject(level.objects[i], i == selObject, 1f);

            DrawToolPreview();
        }

        static Color DoorColor(CustomDoorType type)
        {
            switch (type)
            {
                case CustomDoorType.Doorway: return new Color(0.25f, 0.28f, 0.32f);
                case CustomDoorType.LockedPickable: return new Color(0.95f, 0.6f, 0.2f);
                case CustomDoorType.LockedBreachable: return new Color(0.9f, 0.3f, 0.25f);
                case CustomDoorType.Electronic: return new Color(0.3f, 0.85f, 0.95f);
                default: return new Color(0.75f, 0.55f, 0.35f);
            }
        }

        void DrawObject(CustomObject o, bool selected, float alpha)
        {
            Vector2 c = ToScreen(o.x, o.z);
            float r = Mathf.Clamp(zoom * 0.38f, 5f, 14f);
            var color = ObjectColor(o.type);
            color.a *= alpha;
            Vector2 facing = new Vector2(Mathf.Sin(o.yaw * Mathf.Deg2Rad), -Mathf.Cos(o.yaw * Mathf.Deg2Rad));

            if (CustomLevel.IsZone(o.type))
            {
                float half = o.type == CustomObjectType.Extraction ? 2.5f : 2f, halfZ = o.type == CustomObjectType.Extraction ? 2.75f : 2f;
                var rect = WorldRect(o.x - half, o.z - halfZ, o.x + half, o.z + halfZ);
                UITheme.Fill(rect, new Color(color.r, color.g, color.b, 0.18f * alpha));
                UITheme.Frame(rect, color, 2f);
                UITheme.Text(new Rect(rect.x, rect.y + 2f, rect.width, 18f), o.type == CustomObjectType.Extraction ? "EXTRACTION" : "SAFE ZONE", 11, color, TextAnchor.UpperCenter, true);
            }
            else if (CustomLevel.IsProp(o.type) && o.type != CustomObjectType.Lamp && o.type != CustomObjectType.Plant)
            {
                Vector2 size = PropSize(o.type);
                if (o.yaw % 180 != 0) size = new Vector2(size.y, size.x);
                var rect = WorldRect(o.x - size.x * 0.5f, o.z - size.y * 0.5f, o.x + size.x * 0.5f, o.z + size.y * 0.5f);
                UITheme.Fill(rect, color);
                UITheme.Frame(rect, new Color(0f, 0f, 0f, 0.5f * alpha));
                UITheme.LineTo(c, c + facing * Mathf.Max(rect.width, rect.height) * 0.45f, new Color(1f, 1f, 1f, 0.35f * alpha), 2f);
            }
            else if (o.type == CustomObjectType.TeamStart)
            {
                UITheme.Dot(c, r + 3f, color);
                UITheme.LineTo(c, c + facing * (r + 12f), color, 3f);
                UITheme.Text(new Rect(c.x - 20f, c.y - 9f, 40f, 18f), "TEAM", 9, Color.black, TextAnchor.MiddleCenter, true);
            }
            else
            {
                UITheme.Dot(c, r, color);
                if (CustomLevel.IsSuspect(o.type) || CustomLevel.IsCivilian(o.type) || o.type == CustomObjectType.Camera)
                    UITheme.LineTo(c, c + facing * (r + 7f), color, 2f);
                UITheme.Text(new Rect(c.x - r, c.y - r, r * 2f, r * 2f), ObjectLetter(o.type), Mathf.RoundToInt(r * 1.1f), new Color(0f, 0f, 0f, 0.85f * alpha), TextAnchor.MiddleCenter, true);
            }
            if (selected) UITheme.Ring(c, r + 6f, UITheme.Accent, 2f);
        }

        void DrawToolPreview()
        {
            Vector2 mouse = Event.current.mousePosition;
            if (!canvas.Contains(mouse) || browserOpen || confirmLeave) return;
            Vector2 world = ToWorld(mouse);
            switch (tool)
            {
                case Tool.Room:
                {
                    var a = draggingRoom ? dragStart : Cell(world);
                    var b = draggingRoom ? dragEnd : a;
                    int x0 = Mathf.Min(a.x, b.x), z0 = Mathf.Min(a.y, b.y), w = Mathf.Abs(a.x - b.x) + 1, h = Mathf.Abs(a.y - b.y) + 1;
                    bool ok = !Overlaps(x0, z0, w, h, -1) && (!draggingRoom || (w >= 2 && h >= 2));
                    var rect = WorldRect(x0, z0, x0 + w, z0 + h);
                    UITheme.Fill(rect, ok ? new Color(0.36f, 0.62f, 0.95f, 0.3f) : new Color(0.95f, 0.3f, 0.25f, 0.3f));
                    UITheme.Frame(rect, ok ? UITheme.Accent : UITheme.Bad, 2f);
                    if (draggingRoom) UITheme.ShadowText(new Rect(rect.x, rect.yMax + 2f, 200f, 20f), w + " x " + h + " m", 13, Color.white);
                    break;
                }
                case Tool.Door:
                {
                    int gx = Mathf.RoundToInt(world.x), gz = Mathf.RoundToInt(world.y);
                    bool alongZ = Mathf.Abs(world.y - gz) > Mathf.Abs(world.x - gx);
                    string reason;
                    bool ok = geometry.DoorFits(gx, gz, alongZ, out reason) || geometry.DoorFits(gx, gz, !alongZ, out reason);
                    if (!geometry.DoorFits(gx, gz, alongZ, out reason) && ok) alongZ = !alongZ;
                    Vector2 c = ToScreen(gx, gz);
                    float len = 1.6f * zoom, t = Mathf.Max(6f, zoom * 0.4f);
                    var rect = alongZ ? new Rect(c.x - t * 0.5f, c.y - len * 0.5f, t, len) : new Rect(c.x - len * 0.5f, c.y - t * 0.5f, len, t);
                    UITheme.Frame(rect, ok ? UITheme.Good : new Color(1f, 1f, 1f, 0.3f), 2f);
                    break;
                }
                case Tool.People:
                case Tool.Objects:
                case Tool.Props:
                {
                    var p = Snap(world);
                    DrawObject(new CustomObject { type = placeType, x = p.x, z = p.y, yaw = placeYaw }, false, 0.5f);
                    break;
                }
            }
        }

        static Vector2 PropSize(CustomObjectType type)
        {
            switch (type)
            {
                case CustomObjectType.Desk: return new Vector2(1.6f, 0.85f);
                case CustomObjectType.Table: return new Vector2(1.6f, 0.9f);
                case CustomObjectType.Shelf: return new Vector2(2f, 0.5f);
                case CustomObjectType.Crate: return new Vector2(1.1f, 1.1f);
                case CustomObjectType.Couch: return new Vector2(2.2f, 0.9f);
                case CustomObjectType.Counter: return new Vector2(2.4f, 0.7f);
                case CustomObjectType.Bed: return new Vector2(1.5f, 2.1f);
                case CustomObjectType.Car: return new Vector2(1.9f, 4.3f);
                default: return new Vector2(0.8f, 0.8f);
            }
        }

        public static Color ObjectColor(CustomObjectType type)
        {
            if (type == CustomObjectType.TeamStart) return new Color(0.4f, 0.75f, 1f);
            if (type == CustomObjectType.Leader) return new Color(0.75f, 0.1f, 0.15f);
            if (CustomLevel.IsSuspect(type)) return new Color(0.95f, 0.35f, 0.3f);
            if (type == CustomObjectType.Hostage || type == CustomObjectType.InjuredCivilian) return new Color(1f, 0.78f, 0.3f);
            if (CustomLevel.IsCivilian(type)) return new Color(0.4f, 0.85f, 0.55f);
            switch (type)
            {
                case CustomObjectType.Evidence: return new Color(1f, 0.9f, 0.3f);
                case CustomObjectType.Console: return new Color(0.3f, 0.85f, 0.95f);
                case CustomObjectType.Camera: return new Color(0.85f, 0.4f, 0.4f);
                case CustomObjectType.AlarmPanel: return new Color(1f, 0.55f, 0.2f);
                case CustomObjectType.Extraction: return new Color(0.35f, 0.6f, 1f);
                case CustomObjectType.SafeZone: return new Color(0.35f, 0.9f, 0.5f);
                case CustomObjectType.Plant: return new Color(0.3f, 0.6f, 0.3f);
                case CustomObjectType.Lamp: return new Color(1f, 0.85f, 0.55f);
                case CustomObjectType.Car: return new Color(0.45f, 0.5f, 0.6f);
                default: return new Color(0.6f, 0.5f, 0.4f);
            }
        }

        static string ObjectLetter(CustomObjectType type)
        {
            switch (type)
            {
                case CustomObjectType.ArmedSuspect: return "A";
                case CustomObjectType.UnarmedSuspect: return "U";
                case CustomObjectType.NervousSuspect: return "N";
                case CustomObjectType.Guard: return "G";
                case CustomObjectType.ArmoredSuspect: return "R";
                case CustomObjectType.Leader: return "L";
                case CustomObjectType.Civilian: return "C";
                case CustomObjectType.Hostage: return "H";
                case CustomObjectType.InjuredCivilian: return "+";
                case CustomObjectType.HidingCivilian: return "S";
                case CustomObjectType.Evidence: return "E";
                case CustomObjectType.Console: return "PC";
                case CustomObjectType.Camera: return "c";
                case CustomObjectType.AlarmPanel: return "!";
                case CustomObjectType.Plant: return "";
                case CustomObjectType.Lamp: return "";
                default: return "";
            }
        }

        public static string ObjectName(CustomObjectType type)
        {
            switch (type)
            {
                case CustomObjectType.TeamStart: return "Team Start";
                case CustomObjectType.ArmedSuspect: return "Armed Suspect";
                case CustomObjectType.UnarmedSuspect: return "Unarmed Suspect";
                case CustomObjectType.NervousSuspect: return "Nervous Suspect";
                case CustomObjectType.Guard: return "Hostile Guard";
                case CustomObjectType.ArmoredSuspect: return "Armored Suspect";
                case CustomObjectType.Leader: return "Leader";
                case CustomObjectType.Civilian: return "Civilian";
                case CustomObjectType.Hostage: return "Hostage";
                case CustomObjectType.InjuredCivilian: return "Injured Civilian";
                case CustomObjectType.HidingCivilian: return "Hiding Civilian";
                case CustomObjectType.Evidence: return "Evidence";
                case CustomObjectType.Console: return "Security Console";
                case CustomObjectType.Camera: return "Security Camera";
                case CustomObjectType.AlarmPanel: return "Alarm Panel";
                case CustomObjectType.Extraction: return "Extraction Zone";
                case CustomObjectType.SafeZone: return "Safe Zone";
                default: return type.ToString();
            }
        }

        // ---- Panels ----

        void DrawTopBar(GameManager game, float w)
        {
            var bar = new Rect(0f, 0f, w, 58f);
            UITheme.Fill(bar, new Color(0.04f, 0.06f, 0.1f, 1f));
            UITheme.Fill(new Rect(0f, 56f, w, 2f), UITheme.Accent);
            UITheme.Text(new Rect(20f, 8f, 280f, 26f), "LEVEL CREATOR", 22, UITheme.TextColor, TextAnchor.UpperLeft, true);
            UITheme.Text(new Rect(20f, 33f, 280f, 20f), (string.IsNullOrEmpty(level.name) ? "Untitled" : level.name) + (dirty ? "  (unsaved)" : ""), 14, dirty ? UITheme.Warn : UITheme.Dim);
            float x = 310f;
            if (UITheme.Button(new Rect(x, 9f, 110f, 40f), "New", true, false, 16)) Guarded(() => Load(CustomLevelStore.NewLevel(), true));
            x += 118f;
            if (UITheme.Button(new Rect(x, 9f, 110f, 40f), "Open...", true, false, 16)) { browserLevels = CustomLevelStore.List(); browserPage = 0; confirmDelete = null; browserOpen = true; }
            x += 118f;
            if (UITheme.Button(new Rect(x, 9f, 110f, 40f), "Save", true, dirty, 16)) Save();
            x += 118f;
            if (UITheme.Button(new Rect(x, 9f, 110f, 40f), "Undo", undo.Count > 0, false, 16)) Undo();
            x += 118f;
            if (UITheme.Button(new Rect(x, 9f, 110f, 40f), "Fit view", true, false, 16)) FitView();
            if (UITheme.Button(new Rect(w - 360f, 9f, 170f, 40f), "Play level  >", errors.Count == 0, true, 17)) Play(game);
            if (UITheme.Button(new Rect(w - 180f, 9f, 165f, 40f), "< Main menu", true, false, 16)) Guarded(game.GoToMainMenu);
        }

        void DrawToolbar(float h)
        {
            var panel = new Rect(0f, 58f, 300f, h - 58f);
            UITheme.Fill(panel, new Color(0.04f, 0.06f, 0.1f, 1f));
            UITheme.Fill(new Rect(298f, 58f, 2f, h - 58f), new Color(UITheme.Line.r, UITheme.Line.g, UITheme.Line.b, 0.6f));
            float x = 14f, y = 70f, bw = 272f;
            UITheme.Text(new Rect(x, y, bw, 20f), "TOOLS", 13, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 22f;
            for (int i = 0; i < ToolNames.Length; i++)
            {
                float col = i % 2 == 0 ? x : x + 138f;
                if (UITheme.Button(new Rect(col, y, 134f, 32f), (i + 1) + "  " + ToolNames[i], true, (int)tool == i, 14))
                {
                    tool = (Tool)i;
                    if (tool == Tool.People && !System.Array.Exists(PeopleTypes, t => t == placeType)) placeType = CustomObjectType.ArmedSuspect;
                    if (tool == Tool.Objects && !System.Array.Exists(ObjectiveTypes, t => t == placeType)) placeType = CustomObjectType.TeamStart;
                    if (tool == Tool.Props && !System.Array.Exists(PropTypes, t => t == placeType)) placeType = CustomObjectType.Desk;
                }
                if (i % 2 == 1) y += 36f;
            }
            y += 46f;
            UITheme.Text(new Rect(x, y, bw, 56f), ToolHints[(int)tool], 13, UITheme.Dim);
            y += 62f;

            switch (tool)
            {
                case Tool.Room:
                    UITheme.Text(new Rect(x, y, bw, 20f), "ROOM TYPE", 13, UITheme.Accent, TextAnchor.UpperLeft, true);
                    y += 22f;
                    for (int i = 0; i < CustomLevelBuilder.RoomTypeNames.Length; i++)
                    {
                        float col = i % 2 == 0 ? x : x + 138f;
                        var r = new Rect(col, y, 134f, 30f);
                        if (UITheme.Button(r, string.Empty, true, (int)roomType == i, 13)) roomType = (CustomRoomType)i;
                        UITheme.Fill(new Rect(r.x + 8f, r.y + 8f, 14f, 14f), CustomLevelBuilder.FloorColors[i]);
                        UITheme.Text(new Rect(r.x + 28f, r.y, r.width - 30f, r.height), CustomLevelBuilder.RoomTypeNames[i], 13, UITheme.TextColor, TextAnchor.MiddleLeft);
                        if (i % 2 == 1) y += 34f;
                    }
                    break;
                case Tool.Door:
                    UITheme.Text(new Rect(x, y, bw, 20f), "DOOR TYPE", 13, UITheme.Accent, TextAnchor.UpperLeft, true);
                    y += 22f;
                    for (int i = 0; i < DoorTypeNames.Length; i++)
                    {
                        var r = new Rect(x, y, bw, 32f);
                        if (UITheme.Button(r, string.Empty, true, (int)doorType == i, 14)) doorType = (CustomDoorType)i;
                        UITheme.Fill(new Rect(r.x + 10f, r.y + 13f, 22f, 6f), DoorColor((CustomDoorType)i));
                        UITheme.Text(new Rect(r.x + 40f, r.y, r.width - 44f, r.height), DoorTypeNames[i], 14, UITheme.TextColor, TextAnchor.MiddleLeft);
                        y += 36f;
                    }
                    UITheme.Text(new Rect(x, y + 4f, bw, 60f), "Locked doors can be breached with a charge (and the first kind picked). Electronic locks open from a security console.", 12, UITheme.Faint);
                    break;
                case Tool.People: Palette(PeopleTypes, x, y, bw); break;
                case Tool.Objects: Palette(ObjectiveTypes, x, y, bw); break;
                case Tool.Props: Palette(PropTypes, x, y, bw); break;
            }
        }

        void Palette(CustomObjectType[] types, float x, float y, float bw)
        {
            UITheme.Text(new Rect(x, y, bw, 20f), "PLACE  (R to turn: " + placeYaw + " deg)", 13, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 22f;
            for (int i = 0; i < types.Length; i++)
            {
                float col = i % 2 == 0 ? x : x + 138f;
                var r = new Rect(col, y, 134f, 30f);
                if (UITheme.Button(r, string.Empty, true, placeType == types[i], 13)) placeType = types[i];
                UITheme.Dot(new Vector2(r.x + 15f, r.center.y), 7f, ObjectColor(types[i]));
                UITheme.Text(new Rect(r.x + 28f, r.y, r.width - 30f, r.height), ObjectName(types[i]), 12, UITheme.TextColor, TextAnchor.MiddleLeft);
                if (i % 2 == 1) y += 34f;
            }
        }

        void DrawRightPanel(float w, float h)
        {
            var panel = new Rect(w - 360f, 58f, 360f, h - 58f);
            UITheme.Fill(panel, new Color(0.04f, 0.06f, 0.1f, 1f));
            UITheme.Fill(new Rect(panel.x, 58f, 2f, h - 58f), new Color(UITheme.Line.r, UITheme.Line.g, UITheme.Line.b, 0.6f));
            float x = panel.x + 16f, cw = panel.width - 32f, y = 70f;

            if (selRoom >= 0 && selRoom < level.rooms.Count) y = RoomProperties(x, y, cw);
            else if (selDoor >= 0 && selDoor < level.doors.Count) y = DoorProperties(x, y, cw);
            else if (selObject >= 0 && selObject < level.objects.Count) y = ObjectProperties(x, y, cw);
            else y = LevelSettings(x, y, cw);

            // Summary, objectives and problems.
            y += 10f;
            UITheme.Fill(new Rect(x, y, cw, 1f), new Color(UITheme.Line.r, UITheme.Line.g, UITheme.Line.b, 0.6f));
            y += 8f;
            UITheme.Text(new Rect(x, y, cw, 20f), level.rooms.Count + " rooms   " + level.doors.Count + " doors   " + level.SuspectCount + " suspects   " + level.CivilianCount + " civilians", 13, UITheme.Dim);
            y += 24f;
            UITheme.Text(new Rect(x, y, cw, 20f), "OBJECTIVES (generated)", 13, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 20f;
            foreach (var line in ObjectivePreview())
            {
                UITheme.Text(new Rect(x + 6f, y, cw - 6f, 18f), "-  " + line, 13, UITheme.TextColor);
                y += 18f;
            }
            y += 8f;
            if (errors.Count == 0 && warnings.Count == 0) UITheme.Text(new Rect(x, y, cw, 20f), "Ready to play.", 14, UITheme.Good, TextAnchor.UpperLeft, true);
            foreach (var error in errors)
            {
                if (y > h - 80f) break;
                float th = UITheme.TextHeight(error, 13, cw - 14f);
                UITheme.Fill(new Rect(x, y + 3f, 4f, th - 4f), UITheme.Bad);
                UITheme.Text(new Rect(x + 12f, y, cw - 14f, th), error, 13, UITheme.Bad);
                y += th + 4f;
            }
            foreach (var warning in warnings)
            {
                if (y > h - 80f) break;
                float th = UITheme.TextHeight(warning, 13, cw - 14f);
                UITheme.Fill(new Rect(x, y + 3f, 4f, th - 4f), UITheme.Warn);
                UITheme.Text(new Rect(x + 12f, y, cw - 14f, th), warning, 13, UITheme.Warn);
                y += th + 4f;
            }
        }

        float LevelSettings(float x, float y, float cw)
        {
            UITheme.Text(new Rect(x, y, cw, 20f), "LEVEL SETTINGS", 13, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 24f;
            y = Field(x, y, cw, "Name", ref level.name, 40);
            y = Field(x, y, cw, "Author", ref level.author, 30);
            y = Field(x, y, cw, "Briefing", ref level.description, 160);
            int time = Mathf.Clamp(level.timeOfDay, 0, 2);
            if (Choice(ref y, x, cw, "Time of day", ref time, TimeNames)) { level.timeOfDay = time; }
            int stars = Mathf.Clamp(level.difficulty, 1, 3) - 1;
            if (Choice(ref y, x, cw, "Difficulty", ref stars, DifficultyNames)) level.difficulty = stars + 1;
            int squad = Mathf.Clamp(level.squad, 0, 3);
            if (Choice(ref y, x, cw, "Squad", ref squad, SquadNames)) level.squad = squad;
            var parNames = new[] { "3 min", "5 min", "8 min", "10 min", "15 min" };
            int[] parValues = { 180, 300, 480, 600, 900 };
            int par = System.Array.IndexOf(parValues, level.parTime);
            if (par < 0) par = 2;
            if (Choice(ref y, x, cw, "Par time", ref par, parNames)) level.parTime = parValues[par];
            int ground = level.grassGround ? 0 : 1;
            if (Choice(ref y, x, cw, "Ground", ref ground, GroundNames)) level.grassGround = ground == 0;
            var sizes = new[] { "24 x 18", "32 x 24", "40 x 30", "48 x 36", "56 x 42", "64 x 48" };
            int[] widths = { 24, 32, 40, 48, 56, 64 };
            int size = System.Array.IndexOf(widths, level.width);
            if (size < 0) size = 2;
            if (Choice(ref y, x, cw, "Map size", ref size, sizes))
            {
                level.width = widths[size];
                level.height = Mathf.RoundToInt(widths[size] * 0.75f);
                geometry = new CustomLevelGeometry(level);
                viewFitted = false;
            }
            bool outage = UITheme.Toggle(new Rect(x, y, cw, 28f), "Power outage (dark rooms)", level.powerOutage);
            if (outage != level.powerOutage) { PushUndo(); level.powerOutage = outage; Changed(); }
            y += 34f;
            return y;
        }

        float Field(float x, float y, float cw, string label, ref string value, int max)
        {
            UITheme.Text(new Rect(x, y, 80f, 30f), label, 14, UITheme.Dim, TextAnchor.MiddleLeft);
            string next = GUI.TextField(new Rect(x + 84f, y, cw - 84f, 30f), value ?? "", max, fieldStyle);
            if (next != (value ?? ""))
            {
                value = next;
                dirty = true;
            }
            return y + 36f;
        }

        bool Choice(ref float y, float x, float cw, string label, ref int value, string[] options)
        {
            int next = UITheme.Stepper(new Rect(x, y, cw, 30f), label, Mathf.Clamp(value, 0, options.Length - 1), options, 0.34f);
            y += 34f;
            if (next == value) return false;
            PushUndo();
            value = next;
            Changed();
            return true;
        }

        float RoomProperties(float x, float y, float cw)
        {
            var room = level.rooms[selRoom];
            UITheme.Text(new Rect(x, y, cw, 20f), "ROOM", 13, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 24f;
            string name = room.name ?? "";
            y = Field(x, y, cw, "Name", ref name, 30);
            if (name != (room.name ?? "")) { room.name = name; validationStale = true; }
            int type = (int)room.type;
            if (Choice(ref y, x, cw, "Type", ref type, CustomLevelBuilder.RoomTypeNames)) room.type = (CustomRoomType)type;
            UITheme.Text(new Rect(x, y, cw, 20f), room.w + " x " + room.h + " m  at  (" + room.x + ", " + room.z + ")", 13, UITheme.Dim);
            y += 26f;
            if (UITheme.Button(new Rect(x, y, cw * 0.5f - 4f, 32f), "Delete room", true, false, 14)) DeleteSelection();
            if (UITheme.Button(new Rect(x + cw * 0.5f + 4f, y, cw * 0.5f - 4f, 32f), "Deselect", true, false, 14)) ClearSelection();
            return y + 40f;
        }

        float DoorProperties(float x, float y, float cw)
        {
            var door = level.doors[selDoor];
            UITheme.Text(new Rect(x, y, cw, 20f), "DOOR", 13, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 24f;
            int type = (int)door.type;
            if (Choice(ref y, x, cw, "Type", ref type, DoorTypeNames)) door.type = (CustomDoorType)type;
            int a, b;
            geometry.DoorSides(door, out a, out b);
            UITheme.Text(new Rect(x, y, cw, 36f), "Connects " + (a >= 0 ? CustomLevelBuilder.RoomName(level, a) : "outside") + " and " + (b >= 0 ? CustomLevelBuilder.RoomName(level, b) : "outside"), 13, UITheme.Dim);
            y += 40f;
            if (UITheme.Button(new Rect(x, y, cw * 0.5f - 4f, 32f), "Delete door", true, false, 14)) DeleteSelection();
            if (UITheme.Button(new Rect(x + cw * 0.5f + 4f, y, cw * 0.5f - 4f, 32f), "Deselect", true, false, 14)) ClearSelection();
            return y + 40f;
        }

        float ObjectProperties(float x, float y, float cw)
        {
            var o = level.objects[selObject];
            UITheme.Text(new Rect(x, y, cw, 20f), ObjectName(o.type).ToUpperInvariant(), 13, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 24f;
            int room = geometry.RoomAt(o.x, o.z);
            UITheme.Text(new Rect(x, y, cw, 20f), "In: " + (room >= 0 ? CustomLevelBuilder.RoomName(level, room) : "outside") + "   facing " + o.yaw + " deg", 13, UITheme.Dim);
            y += 26f;
            if (UITheme.Button(new Rect(x, y, cw * 0.5f - 4f, 32f), "Turn (R)", true, false, 14)) Rotate();
            if (UITheme.Button(new Rect(x + cw * 0.5f + 4f, y, cw * 0.5f - 4f, 32f), "Delete", true, false, 14)) DeleteSelection();
            y += 38f;
            if (UITheme.Button(new Rect(x, y, cw, 30f), "Deselect", true, false, 14)) ClearSelection();
            return y + 36f;
        }

        List<string> ObjectivePreview()
        {
            var lines = new List<string> { "Enter the building" };
            int suspects = level.SuspectCount, civilians = level.CivilianCount, evidence = level.Count(CustomObjectType.Evidence);
            if (suspects > 0) lines.Add("Secure all " + suspects + " suspect(s)");
            if (level.Count(CustomObjectType.Leader) > 0) lines.Add("Arrest the leader");
            if (civilians > 0) lines.Add("Evacuate all " + civilians + " civilian(s)");
            if (evidence > 0) lines.Add("Secure " + evidence + " piece(s) of evidence");
            lines.Add("Return to the SWAT van");
            return lines;
        }

        void DrawStatusBar(float w, float h)
        {
            var bar = new Rect(300f, h - 38f, w - 660f, 38f);
            UITheme.Fill(bar, new Color(0.04f, 0.06f, 0.1f, 1f));
            Vector2 world = ToWorld(Event.current.mousePosition);
            string pos = canvas.Contains(Event.current.mousePosition) ? "(" + Mathf.FloorToInt(world.x) + ", " + Mathf.FloorToInt(world.y) + ")   " : "";
            bool recent = Time.unscaledTime - statusTime < 5f && !string.IsNullOrEmpty(status);
            UITheme.Text(new Rect(bar.x + 12f, bar.y, bar.width - 24f, bar.height), pos + (recent ? status : "Left: use tool   Right: erase   Wheel: zoom   Middle-drag or WASD: pan   R: turn   Ctrl+Z: undo   Ctrl+S: save"), 13, recent ? UITheme.TextColor : UITheme.Faint, TextAnchor.MiddleLeft);
        }

        // ---- Overlays ----

        void DrawBrowser(GameManager game, float w, float h)
        {
            UITheme.Fill(new Rect(0f, 0f, w, h), new Color(0f, 0f, 0f, 0.6f));
            var rect = new Rect(w * 0.5f - 460f, h * 0.5f - 360f, 920f, 720f);
            UITheme.Panel(rect);
            UITheme.Header(new Rect(rect.x + 26f, rect.y + 20f, rect.width - 52f, 50f), "Custom Levels", "Saved in " + CustomLevelStore.Folder);
            const int perPage = 8;
            int pages = Mathf.Max(1, Mathf.CeilToInt(browserLevels.Count / (float)perPage));
            browserPage = Mathf.Clamp(browserPage, 0, pages - 1);
            float y = rect.y + 90f;
            if (browserLevels.Count == 0) UITheme.Text(new Rect(rect.x + 26f, y, rect.width - 52f, 30f), "No saved levels yet. Press New to start one.", 16, UITheme.Dim);
            for (int i = browserPage * perPage; i < browserLevels.Count && i < (browserPage + 1) * perPage; i++)
            {
                var entry = browserLevels[i];
                var row = new Rect(rect.x + 26f, y, rect.width - 52f, 64f);
                UITheme.Fill(row, entry.id == level.id ? new Color(UITheme.AccentDim.r, UITheme.AccentDim.g, UITheme.AccentDim.b, 0.6f) : UITheme.PanelLight);
                UITheme.Text(new Rect(row.x + 14f, row.y + 6f, 460f, 26f), entry.name, 18, UITheme.TextColor, TextAnchor.UpperLeft, true);
                UITheme.Text(new Rect(row.x + 14f, row.y + 34f, 480f, 22f), entry.rooms.Count + " rooms  |  " + entry.SuspectCount + " suspects  |  " + entry.CivilianCount + " civilians  |  " + TimeNames[Mathf.Clamp(entry.timeOfDay, 0, 2)] + (string.IsNullOrEmpty(entry.modified) ? "" : "  |  " + entry.modified), 13, UITheme.Dim);
                float bx = row.xMax - 404f;
                var captured = entry;
                if (UITheme.Button(new Rect(bx, row.y + 14f, 92f, 36f), "Edit", true, false, 15))
                    Guarded(() => { Load(CustomLevelStore.Get(captured.id) ?? captured, true); browserOpen = false; });
                if (UITheme.Button(new Rect(bx + 100f, row.y + 14f, 92f, 36f), "Play", true, true, 15))
                    Guarded(() =>
                    {
                        var copy = (CustomLevelStore.Get(captured.id) ?? captured).Clone();
                        copy.id = captured.id;
                        var errs = new List<string>();
                        if (!CustomLevelValidator.Validate(copy, errs, new List<string>())) { Load(copy, true); browserOpen = false; Say("This level has problems to fix before it can be played."); }
                        else { Load(copy, false); browserOpen = false; game.PlayCustomLevel(copy); }
                    });
                if (UITheme.Button(new Rect(bx + 200f, row.y + 14f, 92f, 36f), "Copy", true, false, 15))
                {
                    var copy = captured.Clone();
                    copy.id = null;
                    copy.name = captured.name + " (copy)";
                    string error;
                    if (CustomLevelStore.Save(copy, out error)) { browserLevels = CustomLevelStore.List(); Say("Copied"); }
                }
                bool confirming = confirmDelete == captured.id;
                if (UITheme.Button(new Rect(bx + 300f, row.y + 14f, 92f, 36f), confirming ? "Sure?" : "Delete", true, confirming, 15))
                {
                    if (confirming)
                    {
                        CustomLevelStore.Delete(captured.id);
                        if (level.id == captured.id) { level.id = null; dirty = true; }
                        browserLevels = CustomLevelStore.List();
                        confirmDelete = null;
                    }
                    else confirmDelete = captured.id;
                }
                y += 72f;
            }
            float by = rect.yMax - 66f;
            if (pages > 1)
            {
                if (UITheme.Button(new Rect(rect.x + 26f, by, 110f, 42f), "< Prev", browserPage > 0, false, 15)) browserPage--;
                UITheme.Text(new Rect(rect.x + 140f, by, 120f, 42f), (browserPage + 1) + " / " + pages, 15, UITheme.Dim, TextAnchor.MiddleCenter);
                if (UITheme.Button(new Rect(rect.x + 264f, by, 110f, 42f), "Next >", browserPage < pages - 1, false, 15)) browserPage++;
            }
            if (UITheme.Button(new Rect(rect.xMax - 360f, by, 160f, 42f), "New level", true, false, 16)) Guarded(() => { Load(CustomLevelStore.NewLevel(), true); browserOpen = false; });
            if (UITheme.Button(new Rect(rect.xMax - 190f, by, 164f, 42f), "Close", true, true, 16)) browserOpen = false;
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { browserOpen = false; e.Use(); }
        }

        void DrawConfirm(float w, float h)
        {
            UITheme.Fill(new Rect(0f, 0f, w, h), new Color(0f, 0f, 0f, 0.6f));
            var rect = new Rect(w * 0.5f - 280f, h * 0.5f - 110f, 560f, 220f);
            UITheme.Panel(rect);
            UITheme.Text(new Rect(rect.x + 26f, rect.y + 22f, rect.width - 52f, 30f), "Unsaved changes", 22, UITheme.TextColor, TextAnchor.UpperLeft, true);
            UITheme.Text(new Rect(rect.x + 26f, rect.y + 60f, rect.width - 52f, 50f), "\"" + level.name + "\" has changes that haven't been saved.", 16, UITheme.Dim);
            float y = rect.yMax - 70f;
            if (UITheme.Button(new Rect(rect.x + 26f, y, 160f, 44f), "Save first", true, true, 16))
            {
                confirmLeave = false;
                if (Save() && pendingAction != null) pendingAction();
                pendingAction = null;
            }
            if (UITheme.Button(new Rect(rect.x + 196f, y, 160f, 44f), "Discard", true, false, 16))
            {
                confirmLeave = false;
                dirty = false;
                if (pendingAction != null) pendingAction();
                pendingAction = null;
            }
            if (UITheme.Button(new Rect(rect.xMax - 186f, y, 160f, 44f), "Cancel", true, false, 16)) { confirmLeave = false; pendingAction = null; }
        }
    }
}
