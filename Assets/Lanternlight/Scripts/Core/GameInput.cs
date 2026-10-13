using UnityEngine;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Lanternlight
{
    /// <summary>
    /// All player input goes through here. Works with the classic Input Manager and,
    /// if a project only has the new Input System enabled, with that instead -
    /// so the game runs no matter which input setting the Unity project uses.
    /// </summary>
    public static class GameInput
    {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
        public static Vector2 MouseScreen => Input.mousePosition;
        public static bool BeamHeld => Input.GetMouseButton(0);
        public static bool ClickPressed => Input.GetMouseButtonDown(0);
        public static bool FlarePressed => Input.GetKeyDown(KeyCode.Space);
        public static bool PausePressed => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
        public static bool QuitPressed => Input.GetKeyDown(KeyCode.Q);
        public static bool ConfirmPressed => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        public static bool Choice1Pressed => Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
        public static bool Choice2Pressed => Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
#else
        public static Vector2 MouseScreen => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        public static bool BeamHeld => Mouse.current != null && Mouse.current.leftButton.isPressed;
        public static bool ClickPressed => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public static bool FlarePressed => Key(k => k.spaceKey.wasPressedThisFrame);
        public static bool PausePressed => Key(k => k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame);
        public static bool QuitPressed => Key(k => k.qKey.wasPressedThisFrame);
        public static bool ConfirmPressed => Key(k => k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
        public static bool Choice1Pressed => Key(k => k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame);
        public static bool Choice2Pressed => Key(k => k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame);

        static bool Key(System.Func<Keyboard, bool> test)
        {
            return Keyboard.current != null && test(Keyboard.current);
        }
#endif

        /// <summary>Mouse position in world space for the given camera.</summary>
        public static Vector2 MouseWorld(Camera cam)
        {
            Vector2 screen = MouseScreen;
            return cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
        }

        /// <summary>"Continue" on menu screens: a click or the Enter key.</summary>
        public static bool AdvancePressed => ClickPressed || ConfirmPressed;
    }
}
