using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace APX.Core
{
    /// <summary>
    /// Single source of player input, shared as an asset so gameplay and UI never talk to devices
    /// directly. Bindings are declared in code (keyboard/mouse + gamepad) to avoid generated wrappers.
    /// Consumers call <see cref="Enable"/> from their own OnEnable.
    /// </summary>
    [CreateAssetMenu(menuName = "APX/Input Reader", fileName = "InputReader")]
    public sealed class InputReader : ScriptableObject
    {
        public event Action JumpPressed;
        public event Action JumpReleased;
        public event Action AttackPressed;
        public event Action MaskMenuOpened;
        public event Action MaskMenuClosed;

        // NonSerialized matters: the editor's script-reload serialization also captures private fields,
        // and would restore a half-built map (no runtime state, no callbacks) that ignores Enable().
        [NonSerialized] InputActionMap _map;
        [NonSerialized] InputAction _move;
        [NonSerialized] InputAction _jump;
        [NonSerialized] InputAction _attack;
        [NonSerialized] InputAction _maskMenu;
        [NonSerialized] InputAction _pointerDelta;
        [NonSerialized] InputAction _menuStick;

        public Vector2 Move => _move?.ReadValue<Vector2>() ?? Vector2.zero;

        public bool IsJumpHeld => _jump != null && _jump.IsPressed();

        public bool IsMaskMenuHeld => _maskMenu != null && _maskMenu.IsPressed();

        /// <summary>Pointer movement this frame, in screen pixels (y up).</summary>
        public Vector2 PointerDelta => _pointerDelta?.ReadValue<Vector2>() ?? Vector2.zero;

        /// <summary>Gamepad stick used to pick radial menu slots.</summary>
        public Vector2 MenuStick => _menuStick?.ReadValue<Vector2>() ?? Vector2.zero;

        void OnDisable()
        {
            _map?.Dispose();
            _map = null;
        }

        /// <summary>
        /// Builds and enables the actions. Idempotent; also safe across play sessions, since the Input
        /// System disables the map when play mode ends and it is simply re-enabled here.
        /// </summary>
        public void Enable()
        {
            if (_map == null)
                Build();

            _map.Enable();
        }

        /// <summary>Lets UI temporarily claim the attack button (e.g. while the radial menu is open).</summary>
        public void SetAttackEnabled(bool enabled)
        {
            if (_attack == null)
                return;

            if (enabled)
                _attack.Enable();
            else
                _attack.Disable();
        }

        void Build()
        {
            _map = new InputActionMap("Gameplay");

            _move = _map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick");
            _move.AddBinding("<Gamepad>/dpad");

            _jump = AddButton("Jump", "<Keyboard>/space", "<Keyboard>/z", "<Gamepad>/buttonSouth");
            _attack = AddButton("Attack", "<Keyboard>/x", "<Keyboard>/j", "<Mouse>/leftButton", "<Gamepad>/buttonWest");
            _maskMenu = AddButton("MaskMenu", "<Keyboard>/tab", "<Gamepad>/leftShoulder");

            _pointerDelta = _map.AddAction("PointerDelta", InputActionType.PassThrough, "<Pointer>/delta", expectedControlLayout: "Vector2");
            _menuStick = _map.AddAction("MenuStick", InputActionType.Value, "<Gamepad>/rightStick", expectedControlLayout: "Vector2");

            _jump.performed += _ => JumpPressed?.Invoke();
            _jump.canceled += _ => JumpReleased?.Invoke();
            _attack.performed += _ => AttackPressed?.Invoke();
            _maskMenu.performed += _ => MaskMenuOpened?.Invoke();
            _maskMenu.canceled += _ => MaskMenuClosed?.Invoke();
        }

        InputAction AddButton(string actionName, params string[] bindings)
        {
            InputAction action = _map.AddAction(actionName, InputActionType.Button);
            foreach (string path in bindings)
                action.AddBinding(path);
            return action;
        }
    }
}
