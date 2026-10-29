// Compile-check stubs of the Unity Input System members the mods use (Key enum order as in Input System 1.x).
using System.Collections.Generic;
namespace UnityEngine.InputSystem.Controls
{
    public class ButtonControl { public bool isPressed, wasPressedThisFrame, wasReleasedThisFrame; }
    public class KeyControl : ButtonControl { public Key keyCode; }
    public class Vector2Control { public Vector2 ReadValue() { return default; } }
}
namespace UnityEngine.InputSystem
{
    using UnityEngine.InputSystem.Controls;
    public enum Key
    {
        None, Space, Enter, Tab, Backquote, Quote, Semicolon, Comma, Period, Slash, Backslash, LeftBracket, RightBracket, Minus, Equals,
        A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9, Digit0,
        LeftShift, RightShift, LeftAlt, RightAlt, LeftCtrl, RightCtrl, LeftMeta, RightMeta, ContextMenu, Escape,
        LeftArrow, RightArrow, UpArrow, DownArrow, Backspace, PageDown, PageUp, Home, End, Insert, Delete, CapsLock, NumLock, PrintScreen, ScrollLock, Pause,
        NumpadEnter, NumpadDivide, NumpadMultiply, NumpadPlus, NumpadMinus, NumpadPeriod, NumpadEquals,
        Numpad0, Numpad1, Numpad2, Numpad3, Numpad4, Numpad5, Numpad6, Numpad7, Numpad8, Numpad9,
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12, OEM1, OEM2, OEM3, OEM4, OEM5,
        AltGr = RightAlt, LeftWindows = LeftMeta, RightWindows = RightMeta, LeftApple = LeftMeta, RightApple = RightMeta, LeftCommand = LeftMeta, RightCommand = RightMeta,
    }
    public class Keyboard
    {
        public static Keyboard current;
        public KeyControl this[Key k] { get { return null; } }
        public IReadOnlyList<KeyControl> allKeys;
        public KeyControl aKey, bKey, cKey, dKey, eKey, fKey, gKey, hKey, iKey, jKey, kKey, lKey, mKey, nKey, oKey, pKey, qKey, rKey, sKey, tKey, uKey, vKey, wKey, xKey, yKey, zKey;
        public KeyControl spaceKey, escapeKey, enterKey, tabKey, leftShiftKey, rightShiftKey, leftCtrlKey, upArrowKey, downArrowKey, leftArrowKey, rightArrowKey, digit1Key, digit2Key, digit3Key, f1Key, f2Key, insertKey, deleteKey, backquoteKey;
    }
    public class Mouse { public static Mouse current; public ButtonControl leftButton, rightButton, middleButton; public Vector2Control position, delta, scroll; }
}
