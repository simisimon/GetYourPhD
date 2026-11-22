using UnityEngine;
using UnityEngine.InputSystem;

namespace GetYourPhD
{
    /// <summary>
    /// Keyboard + gamepad bindings in one place.
    /// Keyboard + mouse: A/D or arrows move, Space/W/Up jump, left click drink Mate, right click throw Mate, Q camera flash, E/S/Down interact.
    /// Gamepad: stick/d-pad move, South jump, West throw, North drink, East flash, right shoulder interact.
    /// </summary>
    public static class GameInput
    {
        // e.g. while the keypad or grade screen is open. Always reads true while the pause menu is open.
        static bool locked;
        public static bool Locked { get => locked || PauseMenu.IsPaused; set => locked = value; }

        static Keyboard Kb => Keyboard.current;
        static Gamepad Gp => Gamepad.current;

        public static float Horizontal
        {
            get
            {
                if (Locked) return 0f;
                float x = 0f;
                if (Kb != null)
                {
                    if (Kb.aKey.isPressed || Kb.leftArrowKey.isPressed) x -= 1f;
                    if (Kb.dKey.isPressed || Kb.rightArrowKey.isPressed) x += 1f;
                }
                if (Gp != null)
                {
                    float g = Gp.leftStick.x.ReadValue() + Gp.dpad.x.ReadValue();
                    if (Mathf.Abs(g) > 0.25f) x = Mathf.Sign(g);
                }
                return Mathf.Clamp(x, -1f, 1f);
            }
        }

        /// <summary>Up = +1, down = -1 (W/S, arrows, stick, d-pad). Used for accelerate/brake in Level 3.</summary>
        public static float Vertical
        {
            get
            {
                if (Locked) return 0f;
                float y = 0f;
                if (Kb != null)
                {
                    if (Kb.sKey.isPressed || Kb.downArrowKey.isPressed) y -= 1f;
                    if (Kb.wKey.isPressed || Kb.upArrowKey.isPressed) y += 1f;
                }
                if (Gp != null)
                {
                    float g = Gp.leftStick.y.ReadValue() + Gp.dpad.y.ReadValue();
                    if (Gp.rightTrigger.isPressed || Gp.buttonSouth.isPressed) g += 1f;
                    if (Gp.leftTrigger.isPressed || Gp.buttonWest.isPressed) g -= 1f;
                    if (Mathf.Abs(g) > 0.25f) y = Mathf.Sign(g);
                }
                return Mathf.Clamp(y, -1f, 1f);
            }
        }

        public static bool JumpPressed => !Locked && (
            (Kb != null && (Kb.spaceKey.wasPressedThisFrame || Kb.wKey.wasPressedThisFrame || Kb.upArrowKey.wasPressedThisFrame)) ||
            (Gp != null && Gp.buttonSouth.wasPressedThisFrame));

        public static bool JumpHeld => !Locked && (
            (Kb != null && (Kb.spaceKey.isPressed || Kb.wKey.isPressed || Kb.upArrowKey.isPressed)) ||
            (Gp != null && Gp.buttonSouth.isPressed));

        static Mouse Ms => Mouse.current;

        // Throw Mate: right mouse button. Drink Mate: left mouse button.
        public static bool ThrowPressed => !Locked && (
            (Ms != null && Ms.rightButton.wasPressedThisFrame) ||
            (Gp != null && Gp.buttonWest.wasPressedThisFrame));

        public static bool DrinkPressed => !Locked && (
            (Ms != null && Ms.leftButton.wasPressedThisFrame) ||
            (Gp != null && Gp.buttonNorth.wasPressedThisFrame));

        public static bool FlashPressed => !Locked && (
            (Kb != null && Kb.qKey.wasPressedThisFrame) ||
            (Gp != null && Gp.buttonEast.wasPressedThisFrame));

        public static bool InteractPressed => !Locked && (
            (Kb != null && (Kb.eKey.wasPressedThisFrame || Kb.sKey.wasPressedThisFrame || Kb.downArrowKey.wasPressedThisFrame)) ||
            (Gp != null && (Gp.rightShoulder.wasPressedThisFrame || Gp.dpad.down.wasPressedThisFrame)));

        public static bool InteractHeld => !Locked && (
            (Kb != null && (Kb.eKey.isPressed || Kb.sKey.isPressed || Kb.downArrowKey.isPressed)) ||
            (Gp != null && (Gp.rightShoulder.isPressed || Gp.dpad.down.isPressed)));

        // Menu input works even while gameplay input is locked (but not while the pause menu is open; it reads its own keys).
        public static bool ConfirmPressed => !PauseMenu.IsPaused && (
            (Kb != null && (Kb.enterKey.wasPressedThisFrame || Kb.numpadEnterKey.wasPressedThisFrame)) ||
            (Gp != null && Gp.buttonSouth.wasPressedThisFrame));

        public static bool RetryPressed => !PauseMenu.IsPaused && (
            (Kb != null && Kb.rKey.wasPressedThisFrame) ||
            (Gp != null && Gp.buttonWest.wasPressedThisFrame));

        /// <summary>Organizer skip: Ctrl+Shift+P.</summary>
        public static bool OrganizerSkipPressed =>
            Kb != null && Kb.pKey.wasPressedThisFrame &&
            (Kb.leftCtrlKey.isPressed || Kb.rightCtrlKey.isPressed) &&
            (Kb.leftShiftKey.isPressed || Kb.rightShiftKey.isPressed);
    }
}
