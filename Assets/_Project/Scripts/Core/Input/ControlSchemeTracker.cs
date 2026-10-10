using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace APX.Core
{
    /// <summary>The kind of controller the player is using, for on-screen button prompts.</summary>
    public enum ControlScheme
    {
        KeyboardMouse,
        Gamepad,
    }

    /// <summary>
    /// Follows the controller the player used last: any button press (keyboard key, mouse button, gamepad button)
    /// or a deliberate stick push switches it. Mouse movement and small stick drift don't, so a bumped mouse or a
    /// resting pad won't make prompts flicker. Unplugging the last gamepad falls back to keyboard and mouse.
    /// </summary>
    public static class ControlSchemeTracker
    {
        // A stick has to be pushed at least this far (0..1) to count as using the gamepad.
        const float StickThreshold = 0.5f;

        static IDisposable s_buttonListener;
        static bool s_listening;

        public static ControlScheme Current { get; private set; }

        public static event Action<ControlScheme> Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Domain reload may be off: drop the previous play session's listeners and subscribers.
            StopListening();
            Changed = null;
            Current = ControlScheme.KeyboardMouse;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            Current = Gamepad.current != null && Keyboard.current == null ? ControlScheme.Gamepad : ControlScheme.KeyboardMouse;
            s_buttonListener = InputSystem.onAnyButtonPress.Call(OnButtonPressed);
            InputSystem.onEvent += OnInputEvent;
            InputSystem.onDeviceChange += OnDeviceChange;
            Application.quitting += StopListening;
            s_listening = true;
        }

        static void StopListening()
        {
            s_buttonListener?.Dispose();
            s_buttonListener = null;
            if (!s_listening)
                return;

            InputSystem.onEvent -= OnInputEvent;
            InputSystem.onDeviceChange -= OnDeviceChange;
            Application.quitting -= StopListening;
            s_listening = false;
        }

        static void OnButtonPressed(InputControl control) => Set(SchemeOf(control.device));

        // Button presses come through OnButtonPressed; this only catches stick pushes.
        static void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (Current == ControlScheme.Gamepad || device is not Gamepad gamepad)
                return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
                return;

            if (IsPushed(gamepad.leftStick, eventPtr) || IsPushed(gamepad.rightStick, eventPtr))
                Set(ControlScheme.Gamepad);
        }

        static bool IsPushed(StickControl stick, InputEventPtr eventPtr) =>
            stick.ReadValueFromEvent(eventPtr, out Vector2 value) && value.sqrMagnitude >= StickThreshold * StickThreshold;

        static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Removed && device is Gamepad && Current == ControlScheme.Gamepad && Gamepad.all.Count == 0)
                Set(ControlScheme.KeyboardMouse);
        }

        static ControlScheme SchemeOf(InputDevice device) =>
            device is Gamepad or Joystick ? ControlScheme.Gamepad : ControlScheme.KeyboardMouse;

        static void Set(ControlScheme scheme)
        {
            if (scheme == Current)
                return;

            Current = scheme;
            Changed?.Invoke(scheme);
        }
    }
}
