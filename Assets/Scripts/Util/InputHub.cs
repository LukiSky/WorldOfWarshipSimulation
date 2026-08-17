using UnityEngine;
using UnityEngine.InputSystem;

namespace Naval
{
    /// <summary>
    /// Thin wrapper over the new Input System so the rest of the game never touches device APIs.
    /// The project is configured for the Input System package only, legacy UnityEngine.Input is unavailable.
    /// </summary>
    public static class InputHub
    {
        static Mouse M => Mouse.current;
        static Keyboard K => Keyboard.current;

        public static Vector2 MousePosition => M != null ? M.position.ReadValue() : Vector2.zero;
        public static Vector2 MouseDelta => M != null ? M.delta.ReadValue() : Vector2.zero;
        public static float Scroll => M != null ? M.scroll.ReadValue().y : 0f;

        public static bool LeftDown => M != null && M.leftButton.wasPressedThisFrame;
        public static bool LeftHeld => M != null && M.leftButton.isPressed;
        public static bool LeftUp => M != null && M.leftButton.wasReleasedThisFrame;
        public static bool RightDown => M != null && M.rightButton.wasPressedThisFrame;
        public static bool RightHeld => M != null && M.rightButton.isPressed;
        public static bool MiddleHeld => M != null && M.middleButton.isPressed;
        public static bool MiddleDown => M != null && M.middleButton.wasPressedThisFrame;

        public static bool Shift => K != null && (K.leftShiftKey.isPressed || K.rightShiftKey.isPressed);
        public static bool Ctrl => K != null && (K.leftCtrlKey.isPressed || K.rightCtrlKey.isPressed);
        public static bool Alt => K != null && (K.leftAltKey.isPressed || K.rightAltKey.isPressed);

        public static bool Key(Key k) => K != null && K[k].isPressed;
        public static bool KeyDown(Key k) => K != null && K[k].wasPressedThisFrame;
        public static bool KeyUp(Key k) => K != null && K[k].wasReleasedThisFrame;

        /// <summary>Returns 1..9 / 0 for the number row, or -1.</summary>
        public static int NumberRowDown()
        {
            if (K == null) return -1;
            if (K.digit1Key.wasPressedThisFrame) return 1;
            if (K.digit2Key.wasPressedThisFrame) return 2;
            if (K.digit3Key.wasPressedThisFrame) return 3;
            if (K.digit4Key.wasPressedThisFrame) return 4;
            if (K.digit5Key.wasPressedThisFrame) return 5;
            if (K.digit6Key.wasPressedThisFrame) return 6;
            if (K.digit7Key.wasPressedThisFrame) return 7;
            if (K.digit8Key.wasPressedThisFrame) return 8;
            if (K.digit9Key.wasPressedThisFrame) return 9;
            if (K.digit0Key.wasPressedThisFrame) return 0;
            return -1;
        }

        public static Vector2 MoveAxis()
        {
            if (K == null) return Vector2.zero;
            Vector2 v = Vector2.zero;
            if (K.wKey.isPressed || K.upArrowKey.isPressed) v.y += 1f;
            if (K.sKey.isPressed || K.downArrowKey.isPressed) v.y -= 1f;
            if (K.aKey.isPressed || K.leftArrowKey.isPressed) v.x -= 1f;
            if (K.dKey.isPressed || K.rightArrowKey.isPressed) v.x += 1f;
            return v;
        }
    }
}
