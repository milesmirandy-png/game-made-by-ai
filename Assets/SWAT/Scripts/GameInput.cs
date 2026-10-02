using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Swat
{
    // Reads the keyboard and mouse through whichever input system the project
    // has turned on (the new Input System package or the old Input Manager),
    // so the game works in any Unity project without changing settings.
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

        // Mouse movement this frame, in the same units as the old "Mouse X/Y" axes.
        public static Vector2 Look
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse != null ? mouse.delta.ReadValue() * 0.1f : Vector2.zero;
#else
                return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#endif
            }
        }

        public static bool FireHeld { get { return MouseButton(0, false); } }
        public static bool FirePressed { get { return MouseButton(0, true); } }
        public static bool AimHeld { get { return MouseButton(1, false); } }
        public static bool Sprint { get { return Held(KeyCode.LeftShift); } }
        public static bool Reload { get { return Down(KeyCode.R); } }
        public static bool Interact { get { return Down(KeyCode.E); } }
        public static bool Shout { get { return Down(KeyCode.F); } }
        public static bool Flashbang { get { return Down(KeyCode.G); } }
        public static bool Pause { get { return Down(KeyCode.Escape) || Down(KeyCode.P); } }
        public static bool Confirm { get { return Down(KeyCode.Space) || Down(KeyCode.Return) || FirePressed; } }

        static bool MouseButton(int button, bool thisFrameOnly)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return false;
            var control = button == 0 ? mouse.leftButton : mouse.rightButton;
            return thisFrameOnly ? control.wasPressedThisFrame : control.isPressed;
#else
            return thisFrameOnly ? Input.GetMouseButtonDown(button) : Input.GetMouseButton(button);
#endif
        }

        static bool Held(KeyCode code)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            var key = ToKey(code);
            return keyboard != null && key != Key.None && keyboard[key].isPressed;
#else
            return Input.GetKey(code);
#endif
        }

        static bool Down(KeyCode code)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            var key = ToKey(code);
            return keyboard != null && key != Key.None && keyboard[key].wasPressedThisFrame;
#else
            return Input.GetKeyDown(code);
#endif
        }

#if ENABLE_INPUT_SYSTEM
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
                case KeyCode.G: return Key.G;
                case KeyCode.P: return Key.P;
                case KeyCode.R: return Key.R;
                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.Space: return Key.Space;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.Escape: return Key.Escape;
                default: return Key.None;
            }
        }
#endif
    }
}
