using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Swat
{
    public enum InputAction
    {
        MoveUp, MoveDown, MoveLeft, MoveRight, Fire, AltAim, Reload, Sprint, Crouch, Interact,
        SwitchWeapon, Flashlight, UseEquipment, TacticalMap, PlanningMode, ZoomPreset,
        Slot1, Slot2, Slot3, Slot4, Objectives, Pause, CommandWheel, Shout, Ability, FireMode,
        SelectOfficer1, SelectOfficer2, SelectOfficer3, SelectAllOfficers, ToggleFps, Screenshot,
    }

    // Gamepad buttons (Xbox-style names; read only when the Input System package is installed).
    public enum PadButton { None, South, East, West, North, LeftShoulder, RightShoulder, LeftTrigger, RightTrigger, LeftStick, RightStick, Start, Select, DpadUp, DpadDown, DpadLeft, DpadRight }

    // All gameplay input goes through here. Every action has a remappable
    // binding (keys and mouse buttons are both KeyCodes), saved with the
    // settings. Reads the Input System package when the project uses it,
    // otherwise the classic Input Manager, so it works in any project.
    // With the Input System package a gamepad also works (fixed layout), and
    // prompts switch to gamepad buttons while it is the last device used.
    public static class GameInput
    {
        static readonly KeyCode[] Defaults =
        {
            KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.R, KeyCode.LeftShift, KeyCode.C, KeyCode.E,
            KeyCode.Q, KeyCode.F, KeyCode.G, KeyCode.Tab, KeyCode.Space, KeyCode.V,
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.M, KeyCode.Escape, KeyCode.Z, KeyCode.X, KeyCode.T, KeyCode.B,
            KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F10, KeyCode.F12,
        };

        static readonly string[] Names =
        {
            "Move up", "Move down", "Move left", "Move right", "Fire", "Steady aim", "Reload", "Sprint", "Crouch", "Interact",
            "Switch weapon", "Flashlight", "Use equipment", "Tactical map", "Planning mode", "Camera zoom preset",
            "Primary weapon", "Sidearm", "Previous equipment", "Next equipment", "Objectives", "Pause", "Command wheel (hold)", "Shout compliance", "Role ability", "Fire mode",
            "Select officer 1", "Select officer 2", "Select officer 3", "Select whole squad", "Show FPS", "Screenshot",
        };

        static KeyCode[] bindings = (KeyCode[])Defaults.Clone();

        // Fixed gamepad layout, one entry per InputAction (None = keyboard/mouse only).
        static readonly PadButton[] PadBindings =
        {
            PadButton.None, PadButton.None, PadButton.None, PadButton.None, PadButton.RightTrigger, PadButton.LeftTrigger, PadButton.West, PadButton.LeftStick, PadButton.East, PadButton.South,
            PadButton.North, PadButton.DpadUp, PadButton.RightShoulder, PadButton.Select, PadButton.None, PadButton.None,
            PadButton.None, PadButton.None, PadButton.DpadLeft, PadButton.DpadRight, PadButton.None, PadButton.Start, PadButton.LeftShoulder, PadButton.RightStick, PadButton.DpadDown, PadButton.None,
            PadButton.None, PadButton.None, PadButton.None, PadButton.None, PadButton.None, PadButton.None,
        };

        static readonly string[] PadNames = { "", "A", "B", "X", "Y", "LB", "RB", "LT", "RT", "L3", "R3", "Start", "View", "D-pad Up", "D-pad Down", "D-pad Left", "D-pad Right" };

        // True while a gamepad was the last device used (switches back on mouse or keyboard input).
        public static bool UsingGamepad { get; private set; }
        public static PadButton PadBinding(InputAction action) { return PadBindings[(int)action]; }
        public static string PadName(PadButton button) { return PadNames[(int)button]; }

        // The key or button to show in prompts for the device in use.
        public static string PromptKey(InputAction action)
        {
            var pad = PadBindings[(int)action];
            if (UsingGamepad && pad != PadButton.None) return PadNames[(int)pad];
            return KeyName(Binding(action));
        }

        public static bool GamepadConnected
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
                return Gamepad.current != null;
#else
                return false;
#endif
            }
        }

        public static int ActionCount { get { return Defaults.Length; } }
        public static string DisplayName(InputAction action) { return Names[(int)action]; }
        public static KeyCode Binding(InputAction action) { return bindings[(int)action]; }

        public static void SetBinding(InputAction action, KeyCode key)
        {
            bindings[(int)action] = key;
        }

        public static void ResetToDefaults()
        {
            bindings = (KeyCode[])Defaults.Clone();
        }

        public static void Load(List<KeyBinding> saved)
        {
            ResetToDefaults();
            if (saved == null) return;
            foreach (var entry in saved)
            {
                for (int i = 0; i < Defaults.Length; i++)
                    if (((InputAction)i).ToString() == entry.action) bindings[i] = (KeyCode)entry.key;
            }
        }

        public static List<KeyBinding> Save()
        {
            var list = new List<KeyBinding>();
            for (int i = 0; i < bindings.Length; i++)
                list.Add(new KeyBinding { action = ((InputAction)i).ToString(), key = (int)bindings[i] });
            return list;
        }

        public static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Mouse0: return "Left mouse";
                case KeyCode.Mouse1: return "Right mouse";
                case KeyCode.Mouse2: return "Middle mouse";
                case KeyCode.Mouse3: return "Mouse 4";
                case KeyCode.Mouse4: return "Mouse 5";
                case KeyCode.LeftShift: return "Left Shift";
                case KeyCode.LeftControl: return "Left Ctrl";
                case KeyCode.Escape: return "Esc";
                default:
                    string name = key.ToString();
                    return name.StartsWith("Alpha") ? name.Substring(5) : name;
            }
        }

        public static bool Down(InputAction action) { return KeyDown(bindings[(int)action]) || PadDown(PadBindings[(int)action]); }
        public static bool Held(InputAction action) { return KeyHeld(bindings[(int)action]) || PadHeld(PadBindings[(int)action]); }
        public static bool Released(InputAction action) { return KeyUp(bindings[(int)action]) || PadUp(PadBindings[(int)action]); }

        public static Vector2 Move
        {
            get
            {
                float x = 0f, y = 0f;
                if (Held(InputAction.MoveRight) || KeyHeld(KeyCode.RightArrow)) x += 1f;
                if (Held(InputAction.MoveLeft) || KeyHeld(KeyCode.LeftArrow)) x -= 1f;
                if (Held(InputAction.MoveUp) || KeyHeld(KeyCode.UpArrow)) y += 1f;
                if (Held(InputAction.MoveDown) || KeyHeld(KeyCode.DownArrow)) y -= 1f;
                var keys = new Vector2(x, y);
                var stick = LeftStick;
                return Vector2.ClampMagnitude(stick.sqrMagnitude > keys.sqrMagnitude ? stick : keys, 1f);
            }
        }

        // The pointer used for aiming and the radial menus: the system cursor, the
        // sensitivity-scaled software cursor, or (on a gamepad) the aim point on screen.
        public static Vector2 MousePosition
        {
            get
            {
                if (UsingGamepad && padPointerValid) return padPointer;
                if (softwareCursor) return softCursor;
                return SystemMousePosition;
            }
        }

        public static Vector2 SystemMousePosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
                return Input.mousePosition;
#endif
            }
        }

        // ---- Per-frame device handling ----

        static bool softwareCursor, padPointerValid;
        static Vector2 softCursor, padPointer;
#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
        static Vector2 lastMouse;
        const bool mouseAxisMissing = false;
#else
        static bool mouseAxisMissing;
#endif

        // The player sets this each frame so gamepad radial menus and the camera know where the aim is.
        public static void SetGamepadPointer(Vector2 screen)
        {
            padPointer = screen;
            padPointerValid = true;
        }

        // Called once per frame by the UI manager. "aiming" is true while the
        // player is aiming in a mission (not in menus, planning or consoles).
        public static void Tick(bool aiming, float dt)
        {
            UpdateDevice();
            if (!aiming) padPointerValid = false;

            // Mouse sensitivity other than 1.0 uses a locked, hidden cursor and a
            // software pointer moved by scaled mouse deltas (1.0 keeps the system cursor).
            float sensitivity = SaveManager.Settings.mouseSensitivity;
            bool wantSoftware = aiming && !UsingGamepad && !mouseAxisMissing && Mathf.Abs(sensitivity - 1f) > 0.02f;
            if (wantSoftware != softwareCursor)
            {
                softwareCursor = wantSoftware;
                if (wantSoftware) softCursor = SystemMousePosition;
                Cursor.lockState = wantSoftware ? CursorLockMode.Locked : aiming ? CursorLockMode.Confined : CursorLockMode.None;
            }
            if (softwareCursor)
            {
                softCursor += MouseDelta * sensitivity;
                softCursor.x = Mathf.Clamp(softCursor.x, 0f, Screen.width - 1f);
                softCursor.y = Mathf.Clamp(softCursor.y, 0f, Screen.height - 1f);
            }
            if (UsingGamepad && !aiming) MoveMenuCursor(dt);
        }

        static Vector2 MouseDelta
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
#else
                // The default "Mouse X/Y" axes report pixel deltas multiplied by their 0.1 sensitivity.
                try { return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f; }
                catch (System.ArgumentException)
                {
                    mouseAxisMissing = true;
                    return Vector2.zero;
                }
#endif
            }
        }

        static void UpdateDevice()
        {
#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
            var pad = Gamepad.current;
            Vector2 mouse = SystemMousePosition;
            bool mouseMoved = (mouse - lastMouse).sqrMagnitude > 16f && !softwareCursor;
            lastMouse = mouse;
            var keyboard = Keyboard.current;
            var pointer = Mouse.current;
            bool desktopInput = mouseMoved || (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                || (pointer != null && (pointer.leftButton.wasPressedThisFrame || pointer.rightButton.wasPressedThisFrame || (softwareCursor && pointer.delta.ReadValue().sqrMagnitude > 4f)));
            if (pad == null) { UsingGamepad = false; return; }
            bool padInput = pad.leftStick.ReadValue().sqrMagnitude > 0.25f || pad.rightStick.ReadValue().sqrMagnitude > 0.25f;
            for (int i = 1; i < PadNames.Length && !padInput; i++) padInput = PadDown((PadButton)i);
            if (padInput) UsingGamepad = true;
            else if (desktopInput) UsingGamepad = false;
#else
            UsingGamepad = false;
#endif
        }

        // In menus the left stick moves the system cursor and A clicks (see UITheme.Button).
        static void MoveMenuCursor(float dt)
        {
#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
            var mouse = Mouse.current;
            Vector2 stick = LeftStick;
            if (mouse == null || stick.sqrMagnitude < 0.01f) return;
            float speed = 900f * Mathf.Max(1f, Screen.height / 1080f) * Mathf.Clamp(SaveManager.Settings.controllerSensitivity, 0.3f, 2f);
            Vector2 next = mouse.position.ReadValue() + stick * stick.magnitude * speed * dt;
            next.x = Mathf.Clamp(next.x, 0f, Screen.width - 1f);
            next.y = Mathf.Clamp(next.y, 0f, Screen.height - 1f);
            mouse.WarpCursorPosition(next);
            lastMouse = next;
#endif
        }

        // ---- Gamepad ----

        const float StickDeadZone = 0.2f;

        public static Vector2 LeftStick { get { return ReadStick(true); } }
        public static Vector2 RightStick { get { return ReadStick(false); } }

        static Vector2 ReadStick(bool left)
        {
#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
            var pad = Gamepad.current;
            if (pad == null) return Vector2.zero;
            Vector2 v = left ? pad.leftStick.ReadValue() : pad.rightStick.ReadValue();
            float m = v.magnitude;
            if (m < StickDeadZone) return Vector2.zero;
            return v / m * Mathf.Clamp01((m - StickDeadZone) / (1f - StickDeadZone));
#else
            return Vector2.zero;
#endif
        }

        // Gamepad "click" for menus: A pressed this frame.
        public static bool PadSubmit { get { return PadDown(PadButton.South); } }
        // D-pad left/right this frame, for adjusting sliders with a gamepad.
        public static int PadHorizontal { get { return PadDown(PadButton.DpadRight) ? 1 : PadDown(PadButton.DpadLeft) ? -1 : 0; } }

        public static bool PadDown(PadButton button)
        {
#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
            var control = PadControl(button);
            return control != null && control.wasPressedThisFrame;
#else
            return false;
#endif
        }

        public static bool PadHeld(PadButton button)
        {
#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
            var control = PadControl(button);
            return control != null && control.isPressed;
#else
            return false;
#endif
        }

        public static bool PadUp(PadButton button)
        {
#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
            var control = PadControl(button);
            return control != null && control.wasReleasedThisFrame;
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
        static UnityEngine.InputSystem.Controls.ButtonControl PadControl(PadButton button)
        {
            var pad = Gamepad.current;
            if (pad == null) return null;
            switch (button)
            {
                case PadButton.South: return pad.buttonSouth;
                case PadButton.East: return pad.buttonEast;
                case PadButton.West: return pad.buttonWest;
                case PadButton.North: return pad.buttonNorth;
                case PadButton.LeftShoulder: return pad.leftShoulder;
                case PadButton.RightShoulder: return pad.rightShoulder;
                case PadButton.LeftTrigger: return pad.leftTrigger;
                case PadButton.RightTrigger: return pad.rightTrigger;
                case PadButton.LeftStick: return pad.leftStickButton;
                case PadButton.RightStick: return pad.rightStickButton;
                case PadButton.Start: return pad.startButton;
                case PadButton.Select: return pad.selectButton;
                case PadButton.DpadUp: return pad.dpad.up;
                case PadButton.DpadDown: return pad.dpad.down;
                case PadButton.DpadLeft: return pad.dpad.left;
                case PadButton.DpadRight: return pad.dpad.right;
                default: return null;
            }
        }
#endif

        // +1 for one wheel notch away from you, -1 towards you.
        public static float Scroll
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
                var mouse = Mouse.current;
                if (mouse == null) return 0f;
                float y = mouse.scroll.ReadValue().y;
                return Mathf.Abs(y) > 10f ? y / 120f : y; // older Input System versions report 120 per notch
#else
                return Input.mouseScrollDelta.y;
#endif
            }
        }

        public static bool LeftClick { get { return KeyDown(KeyCode.Mouse0); } }
        public static bool RightClick { get { return KeyDown(KeyCode.Mouse1); } }
        public static bool Confirm { get { return KeyDown(KeyCode.Return) || KeyDown(KeyCode.KeypadEnter) || PadDown(PadButton.South) || PadDown(PadButton.Start); } }
        public static bool Cancel { get { return KeyDown(KeyCode.Escape) || PadDown(PadButton.East); } }

        // Number keys pressed this frame (1-9, 0 = 10). Used by the command wheel. Returns -1 if none.
        public static int NumberPressed
        {
            get
            {
                for (int i = 0; i < 10; i++)
                    if (KeyDown(i == 9 ? KeyCode.Alpha0 : KeyCode.Alpha1 + i)) return i;
                return -1;
            }
        }

        public static bool KeyHeld(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
            var mouseButton = MouseButton(key);
            if (mouseButton != null) return mouseButton.isPressed;
            var keyboard = Keyboard.current;
            var mapped = ToKey(key);
            return keyboard != null && mapped != Key.None && keyboard[mapped].isPressed;
#else
            return Input.GetKey(key);
#endif
        }

        public static bool KeyDown(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
            var mouseButton = MouseButton(key);
            if (mouseButton != null) return mouseButton.wasPressedThisFrame;
            var keyboard = Keyboard.current;
            var mapped = ToKey(key);
            return keyboard != null && mapped != Key.None && keyboard[mapped].wasPressedThisFrame;
#else
            return Input.GetKeyDown(key);
#endif
        }

        public static bool KeyUp(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
            var mouseButton = MouseButton(key);
            if (mouseButton != null) return mouseButton.wasReleasedThisFrame;
            var keyboard = Keyboard.current;
            var mapped = ToKey(key);
            return keyboard != null && mapped != Key.None && keyboard[mapped].wasReleasedThisFrame;
#else
            return Input.GetKeyUp(key);
#endif
        }

#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
        static UnityEngine.InputSystem.Controls.ButtonControl MouseButton(KeyCode key)
        {
            var mouse = Mouse.current;
            if (mouse == null) return null;
            switch (key)
            {
                case KeyCode.Mouse0: return mouse.leftButton;
                case KeyCode.Mouse1: return mouse.rightButton;
                case KeyCode.Mouse2: return mouse.middleButton;
                case KeyCode.Mouse3: return mouse.backButton;
                case KeyCode.Mouse4: return mouse.forwardButton;
                default: return null;
            }
        }

        static Key ToKey(KeyCode code)
        {
            switch (code)
            {
                case KeyCode.A: return Key.A;
                case KeyCode.B: return Key.B;
                case KeyCode.C: return Key.C;
                case KeyCode.D: return Key.D;
                case KeyCode.E: return Key.E;
                case KeyCode.F: return Key.F;
                case KeyCode.G: return Key.G;
                case KeyCode.H: return Key.H;
                case KeyCode.I: return Key.I;
                case KeyCode.J: return Key.J;
                case KeyCode.K: return Key.K;
                case KeyCode.L: return Key.L;
                case KeyCode.M: return Key.M;
                case KeyCode.N: return Key.N;
                case KeyCode.O: return Key.O;
                case KeyCode.P: return Key.P;
                case KeyCode.Q: return Key.Q;
                case KeyCode.R: return Key.R;
                case KeyCode.S: return Key.S;
                case KeyCode.T: return Key.T;
                case KeyCode.U: return Key.U;
                case KeyCode.V: return Key.V;
                case KeyCode.W: return Key.W;
                case KeyCode.X: return Key.X;
                case KeyCode.Y: return Key.Y;
                case KeyCode.Z: return Key.Z;
                case KeyCode.Alpha0: return Key.Digit0;
                case KeyCode.Alpha1: return Key.Digit1;
                case KeyCode.Alpha2: return Key.Digit2;
                case KeyCode.Alpha3: return Key.Digit3;
                case KeyCode.Alpha4: return Key.Digit4;
                case KeyCode.Alpha5: return Key.Digit5;
                case KeyCode.Alpha6: return Key.Digit6;
                case KeyCode.Alpha7: return Key.Digit7;
                case KeyCode.Alpha8: return Key.Digit8;
                case KeyCode.Alpha9: return Key.Digit9;
                case KeyCode.F1: return Key.F1;
                case KeyCode.F2: return Key.F2;
                case KeyCode.F3: return Key.F3;
                case KeyCode.F4: return Key.F4;
                case KeyCode.F5: return Key.F5;
                case KeyCode.F6: return Key.F6;
                case KeyCode.F7: return Key.F7;
                case KeyCode.F8: return Key.F8;
                case KeyCode.F9: return Key.F9;
                case KeyCode.F10: return Key.F10;
                case KeyCode.F11: return Key.F11;
                case KeyCode.F12: return Key.F12;
                case KeyCode.Keypad0: return Key.Numpad0;
                case KeyCode.Keypad1: return Key.Numpad1;
                case KeyCode.Keypad2: return Key.Numpad2;
                case KeyCode.Keypad3: return Key.Numpad3;
                case KeyCode.Keypad4: return Key.Numpad4;
                case KeyCode.Keypad5: return Key.Numpad5;
                case KeyCode.Keypad6: return Key.Numpad6;
                case KeyCode.Keypad7: return Key.Numpad7;
                case KeyCode.Keypad8: return Key.Numpad8;
                case KeyCode.Keypad9: return Key.Numpad9;
                case KeyCode.Space: return Key.Space;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.Tab: return Key.Tab;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.Backspace: return Key.Backspace;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.RightShift: return Key.RightShift;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.RightControl: return Key.RightCtrl;
                case KeyCode.LeftAlt: return Key.LeftAlt;
                case KeyCode.RightAlt: return Key.RightAlt;
                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;
                case KeyCode.BackQuote: return Key.Backquote;
                case KeyCode.Minus: return Key.Minus;
                case KeyCode.Equals: return Key.Equals;
                case KeyCode.LeftBracket: return Key.LeftBracket;
                case KeyCode.RightBracket: return Key.RightBracket;
                case KeyCode.Semicolon: return Key.Semicolon;
                case KeyCode.Quote: return Key.Quote;
                case KeyCode.Comma: return Key.Comma;
                case KeyCode.Period: return Key.Period;
                case KeyCode.Slash: return Key.Slash;
                case KeyCode.Backslash: return Key.Backslash;
                case KeyCode.CapsLock: return Key.CapsLock;
                case KeyCode.KeypadEnter: return Key.NumpadEnter;
                case KeyCode.Insert: return Key.Insert;
                case KeyCode.Delete: return Key.Delete;
                case KeyCode.Home: return Key.Home;
                case KeyCode.End: return Key.End;
                case KeyCode.PageUp: return Key.PageUp;
                case KeyCode.PageDown: return Key.PageDown;
                default: return Key.None;
            }
        }
#endif
    }
}
