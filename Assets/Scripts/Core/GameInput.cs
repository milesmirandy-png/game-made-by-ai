using UnityEngine;
#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Swat
{
    // One place that reads the keyboard and mouse. Uses the Input System
    // package when the project has it switched on, otherwise the classic
    // Input Manager, so the game runs in any project without changing settings.
    public static class GameInput
    {
        public static Vector2 Move
        {
            get
            {
                float x = 0f, y = 0f;
                if (Held(KeyCode.D) || Held(KeyCode.RightArrow)) x += 1f;
                if (Held(KeyCode.A) || Held(KeyCode.LeftArrow)) x -= 1f;
                if (Held(KeyCode.W) || Held(KeyCode.UpArrow)) y += 1f;
                if (Held(KeyCode.S) || Held(KeyCode.DownArrow)) y -= 1f;
                return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            }
        }

        public static Vector2 MousePosition
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

        public static bool FireHeld { get { return MouseButton(false); } }
        public static bool FirePressed { get { return MouseButton(true); } }
        public static bool Sprint { get { return Held(KeyCode.LeftShift); } }
        public static bool Reload { get { return Down(KeyCode.R); } }
        public static bool Interact { get { return Down(KeyCode.E); } }
        public static bool SwitchItem { get { return Down(KeyCode.Q); } }
        public static bool Shout { get { return Down(KeyCode.F); } }
        public static bool Pause { get { return Down(KeyCode.Escape) || Down(KeyCode.P); } }
        public static bool Confirm { get { return Down(KeyCode.Space) || Down(KeyCode.Return); } }
        public static bool ToggleFps { get { return Down(KeyCode.F3); } }

        // Number keys 1-7 pick a slot directly. Returns -1 when none was pressed.
        public static int SlotPressed
        {
            get
            {
                for (int i = 0; i < 7; i++)
                    if (Down(KeyCode.Alpha1 + i)) return i;
                return -1;
            }
        }

        static bool MouseButton(bool thisFrameOnly)
        {
#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return false;
            return thisFrameOnly ? mouse.leftButton.wasPressedThisFrame : mouse.leftButton.isPressed;
#else
            return thisFrameOnly ? Input.GetMouseButtonDown(0) : Input.GetMouseButton(0);
#endif
        }

        static bool Held(KeyCode code)
        {
#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            var key = ToKey(code);
            return keyboard != null && key != Key.None && keyboard[key].isPressed;
#else
            return Input.GetKey(code);
#endif
        }

        static bool Down(KeyCode code)
        {
#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            var key = ToKey(code);
            return keyboard != null && key != Key.None && keyboard[key].wasPressedThisFrame;
#else
            return Input.GetKeyDown(code);
#endif
        }

#if ENABLE_INPUT_SYSTEM && SWAT_INPUT_SYSTEM
        static Key ToKey(KeyCode code)
        {
            switch (code)
            {
                case KeyCode.W: return Key.W;
                case KeyCode.A: return Key.A;
                case KeyCode.S: return Key.S;
                case KeyCode.D: return Key.D;
                case KeyCode.E: return Key.E;
                case KeyCode.F: return Key.F;
                case KeyCode.P: return Key.P;
                case KeyCode.Q: return Key.Q;
                case KeyCode.R: return Key.R;
                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.Space: return Key.Space;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.F3: return Key.F3;
                case KeyCode.Alpha1: return Key.Digit1;
                case KeyCode.Alpha2: return Key.Digit2;
                case KeyCode.Alpha3: return Key.Digit3;
                case KeyCode.Alpha4: return Key.Digit4;
                case KeyCode.Alpha5: return Key.Digit5;
                case KeyCode.Alpha6: return Key.Digit6;
                case KeyCode.Alpha7: return Key.Digit7;
                default: return Key.None;
            }
        }
#endif
    }
}
