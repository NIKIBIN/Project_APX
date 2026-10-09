using APX.Core;
using APX.Cutscenes;
using APX.Masks;
using APX.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace APX.UI
{
    /// <summary>Radial menu slot directions; also the segment order of <see cref="RadialRing"/>.</summary>
    public enum RadialDirection
    {
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3,
    }

    /// <summary>
    /// Hold-to-open radial mask selector (UI Toolkit). While held, time slows down and a virtual cursor
    /// appears at the screen centre; dragging up/right/down/left highlights a slot and releasing confirms.
    /// The cursor is driven by pointer delta (or the right stick) so it behaves the same on desktop and WebGL.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class RadialMenuController : MonoBehaviour
    {
        const int SlotCount = RadialRing.SegmentCount;
        const string HiddenClass = "radial-overlay--hidden";
        const string HoveredClass = "radial-slot--hovered";
        const string EquippedClass = "radial-slot--equipped";

        // Indexed by RadialDirection, in panel space (+y is down).
        static readonly Vector2[] s_slotDirections = { Vector2.down, Vector2.right, Vector2.up, Vector2.left };
        static readonly string[] s_slotElementNames = { "slotUp", "slotRight", "slotDown", "slotLeft" };

        [SerializeField] InputReader input;
        [SerializeField] MaskManager maskManager;

        [Header("Slots")]
        [SerializeField] MaskType upSlot = MaskType.NiceAndSlow;
        [SerializeField] MaskType rightSlot = MaskType.SecondChannel;
        [SerializeField] MaskType downSlot = MaskType.Differentiate;
        [Tooltip("None = unequip every mask and summon the hint companion.")]
        [SerializeField] MaskType leftSlot = MaskType.None;

        [Header("Slow Motion")]
        [SerializeField, Range(0.01f, 1f)] float slowMotionScale = 0.2f;

        [Header("Selection")]
        [Tooltip("Maximum virtual cursor distance from the centre, in panel pixels.")]
        [SerializeField, Min(1f)] float cursorRadius = 150f;
        [Tooltip("Cursor distance from the centre before a slot is highlighted, in panel pixels.")]
        [SerializeField, Min(0f)] float deadZone = 35f;
        [SerializeField, Min(0.01f)] float pointerSensitivity = 1f;
        [SerializeField, Range(0.1f, 0.95f)] float stickDeadZone = 0.4f;

        [Header("Text")]
        [SerializeField] string idleTitleFormat = "Usando: {0}";
        [SerializeField] string idleDescription = "Arraste o mouse e solte o Tab para escolher.";

        [Header("Hardware Cursor")]
        [SerializeField] bool hideCursorDuringGameplay = true;

        readonly Label[] _slotLabels = new Label[SlotCount];
        readonly MaskType[] _slotMasks = new MaskType[SlotCount];
        VisualElement _overlay;
        VisualElement _menu;
        VisualElement _cursor;
        RadialRing _ring;
        Label _title;
        Label _description;
        Vector2 _cursorOffset;
        int _hoveredSlot = -1;
        bool _isPlayerDead;
        bool _isCutscenePlaying;

        public bool IsOpen { get; private set; }

        void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _overlay = root.Q("radialOverlay");
            _menu = root.Q("radialMenu");
            _ring = root.Q<RadialRing>("radialRing");
            _cursor = root.Q("radialCursor");
            _title = root.Q<Label>("radialTitle");
            _description = root.Q<Label>("radialDescription");
            for (int i = 0; i < SlotCount; i++)
                _slotLabels[i] = root.Q<Label>(s_slotElementNames[i]);

            _menu.RegisterCallback<GeometryChangedEvent>(OnMenuGeometryChanged);
            _overlay.AddToClassList(HiddenClass);

            input.Enable();
            input.MaskMenuOpened += Open;
            input.MaskMenuClosed += Confirm;
            EventBus<PlayerDiedEvent>.Subscribe(OnPlayerDied);
            EventBus<PlayerRespawnedEvent>.Subscribe(OnPlayerRespawned);
            EventBus<CutsceneStartedEvent>.Subscribe(OnCutsceneStarted);
            EventBus<CutsceneEndedEvent>.Subscribe(OnCutsceneEnded);
            SetCursorCaptured(false);
        }

        void OnDisable()
        {
            input.MaskMenuOpened -= Open;
            input.MaskMenuClosed -= Confirm;
            EventBus<PlayerDiedEvent>.Unsubscribe(OnPlayerDied);
            EventBus<PlayerRespawnedEvent>.Unsubscribe(OnPlayerRespawned);
            EventBus<CutsceneStartedEvent>.Unsubscribe(OnCutsceneStarted);
            EventBus<CutsceneEndedEvent>.Unsubscribe(OnCutsceneEnded);
            _menu?.UnregisterCallback<GeometryChangedEvent>(OnMenuGeometryChanged);
            Close(confirm: false);
        }

        void Update()
        {
            if (!IsOpen)
                return;

            UpdateCursorOffset();
            SetHoveredSlot(GetSlotUnderCursor());
        }

        void Open()
        {
            if (IsOpen || maskManager == null || maskManager.IsTransitioning || _isPlayerDead || _isCutscenePlaying)
                return;

            IsOpen = true;
            _cursorOffset = Vector2.zero;
            GameTime.SetTimeScale(slowMotionScale);
            input.SetAttackEnabled(false);
            SetCursorCaptured(true);

            RefreshSlots();
            SetHoveredSlot(-1, force: true);
            UpdateCursorVisual();
            _overlay.RemoveFromClassList(HiddenClass);
        }

        void Confirm() => Close(confirm: true);

        void OnPlayerDied(PlayerDiedEvent evt)
        {
            _isPlayerDead = true;
            Close(confirm: false);
        }

        void OnPlayerRespawned(PlayerRespawnedEvent evt) => _isPlayerDead = false;

        void OnCutsceneStarted(CutsceneStartedEvent evt)
        {
            _isCutscenePlaying = true;
            Close(confirm: false);
        }

        void OnCutsceneEnded(CutsceneEndedEvent evt) => _isCutscenePlaying = false;

        void Close(bool confirm)
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            if (confirm && _hoveredSlot >= 0)
                maskManager.Select(_slotMasks[_hoveredSlot]);

            GameTime.ResetTimeScale();
            input.SetAttackEnabled(true);
            SetCursorCaptured(false);
            _overlay.AddToClassList(HiddenClass);
        }

        void RefreshSlots()
        {
            _slotMasks[(int)RadialDirection.Up] = upSlot;
            _slotMasks[(int)RadialDirection.Right] = rightSlot;
            _slotMasks[(int)RadialDirection.Down] = downSlot;
            _slotMasks[(int)RadialDirection.Left] = leftSlot;

            _ring.EquippedIndex = -1;
            for (int i = 0; i < SlotCount; i++)
            {
                MaskType mask = _slotMasks[i];
                bool hasDefinition = maskManager.TryGetDefinition(mask, out MaskDefinition definition);
                bool equipped = mask == MaskManager.CurrentMask;

                _slotLabels[i].text = hasDefinition ? definition.DisplayName : mask.ToString();
                _slotLabels[i].EnableInClassList(EquippedClass, equipped);
                _ring.SetSegmentColor(i, hasDefinition ? definition.Color : Color.gray);
                if (equipped)
                    _ring.EquippedIndex = i;
            }
        }

        void UpdateCursorOffset()
        {
            Vector2 stick = input.MenuStick;
            if (stick.sqrMagnitude >= stickDeadZone * stickDeadZone)
            {
                _cursorOffset = new Vector2(stick.x, -stick.y).normalized * cursorRadius;
            }
            else
            {
                // Screen y points up, panel y points down.
                Vector2 delta = input.PointerDelta * (pointerSensitivity * GetScreenToPanelScale());
                _cursorOffset += new Vector2(delta.x, -delta.y);
            }

            _cursorOffset = Vector2.ClampMagnitude(_cursorOffset, cursorRadius);
            UpdateCursorVisual();
        }

        int GetSlotUnderCursor()
        {
            if (_cursorOffset.magnitude < deadZone)
                return -1;

            if (Mathf.Abs(_cursorOffset.x) > Mathf.Abs(_cursorOffset.y))
                return (int)(_cursorOffset.x > 0f ? RadialDirection.Right : RadialDirection.Left);

            return (int)(_cursorOffset.y < 0f ? RadialDirection.Up : RadialDirection.Down);
        }

        void SetHoveredSlot(int slot, bool force = false)
        {
            if (slot == _hoveredSlot && !force)
                return;

            _hoveredSlot = slot;
            _ring.HoveredIndex = slot;
            for (int i = 0; i < SlotCount; i++)
                _slotLabels[i].EnableInClassList(HoveredClass, i == slot);

            if (slot >= 0)
            {
                MaskType mask = _slotMasks[slot];
                bool hasDefinition = maskManager.TryGetDefinition(mask, out MaskDefinition definition);
                _title.text = hasDefinition ? definition.DisplayName : mask.ToString();
                _description.text = hasDefinition ? definition.Description : string.Empty;
            }
            else
            {
                MaskType current = MaskManager.CurrentMask;
                string currentName = maskManager.TryGetDefinition(current, out MaskDefinition definition)
                    ? definition.DisplayName
                    : current.ToString();
                _title.text = string.Format(idleTitleFormat, currentName);
                _description.text = idleDescription;
            }
        }

        void UpdateCursorVisual() => _cursor.style.translate = new Translate(_cursorOffset.x, _cursorOffset.y);

        void OnMenuGeometryChanged(GeometryChangedEvent evt)
        {
            // Labels sit in the middle of their ring segment; positions depend on the resolved size.
            Vector2 center = _menu.contentRect.center;
            float radius = _ring.MidRadius;
            for (int i = 0; i < SlotCount; i++)
            {
                Vector2 position = center + s_slotDirections[i] * radius;
                _slotLabels[i].style.left = position.x;
                _slotLabels[i].style.top = position.y;
            }
        }

        float GetScreenToPanelScale()
        {
            float panelWidth = _overlay.panel != null ? _overlay.panel.visualTree.layout.width : 0f;
            return panelWidth > 0f && Screen.width > 0 ? panelWidth / Screen.width : 1f;
        }

        void SetCursorCaptured(bool captured)
        {
            // While captured, the menu draws its own (virtual) cursor.
            UnityEngine.Cursor.visible = !captured && !hideCursorDuringGameplay;
#if !UNITY_WEBGL || UNITY_EDITOR
            // Locking keeps pointer deltas flowing at screen edges. Browsers show a pointer-lock banner
            // on every lock, so WebGL relies on the virtual cursor alone.
            UnityEngine.Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
#endif
        }
    }
}
