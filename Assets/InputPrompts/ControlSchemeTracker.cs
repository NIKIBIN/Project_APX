using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace InputPrompts
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
        static ControlScheme? s_override;
        static ControlScheme s_detected;

        public static ControlScheme Current { get; private set; }

        /// <summary>Set by testing tools: the scheme forced regardless of real input, or null to follow input again.</summary>
        public static ControlScheme? Override
        {
            get => s_override;
            set
            {
                s_override = value;
                Publish(value ?? s_detected);
            }
        }

        public static event Action<ControlScheme> Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Domain reload may be off: drop the previous play session's listeners and subscribers.
            StopListening();
            Changed = null;
            s_override = null;
            s_detected = ControlScheme.KeyboardMouse;
            Current = ControlScheme.KeyboardMouse;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            s_detected = Gamepad.current != null && Keyboard.current == null ? ControlScheme.Gamepad : ControlScheme.KeyboardMouse;
            Current = s_detected;
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
            if (s_detected == ControlScheme.Gamepad || device is not Gamepad gamepad)
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
            if (change == InputDeviceChange.Removed && device is Gamepad && s_detected == ControlScheme.Gamepad && Gamepad.all.Count == 0)
                Set(ControlScheme.KeyboardMouse);
        }

        static ControlScheme SchemeOf(InputDevice device) =>
            device is Gamepad or Joystick ? ControlScheme.Gamepad : ControlScheme.KeyboardMouse;

        static void Set(ControlScheme scheme)
        {
            s_detected = scheme;
            if (s_override == null)
                Publish(scheme);
        }

        static void Publish(ControlScheme scheme)
        {
            if (scheme == Current)
                return;

            Current = scheme;
            Changed?.Invoke(scheme);
        }
    }
}
