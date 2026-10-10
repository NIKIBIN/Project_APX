using System.Collections.Generic;
using APX.Core;
using APX.Cutscenes;
using APX.Hints;
using APX.Rooms;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

namespace APX.UI
{
    /// <summary>
    /// Screen-space speech bubble (UI Toolkit) that tracks the hint companion in the world. It waits until the
    /// companion has fully appeared at its place beside the player (and no cutscene is playing), then grows out
    /// of the companion; it shrinks back into it when dismissed. The first time it opens in each room the hint is
    /// typed out one character at a time; afterwards it shows at once. Runs on unscaled time.
    /// </summary>
    [DefaultExecutionOrder(100)] // After the companion has moved this frame.
    [RequireComponent(typeof(UIDocument))]
    public sealed class HintBubbleView : MonoBehaviour
    {
        const string HiddenClass = "hint-bubble--hidden";
        const string HiddenTextTag = "<alpha=#00>";

        enum State
        {
            Hidden,
            Waiting,
            Opening,
            Open,
            Closing,
        }

        [SerializeField] HintCompanion companion;
        [Tooltip("Defaults to Camera.main.")]
        [SerializeField] Camera worldCamera;
        [Tooltip("World-space offset from the companion to the tip of the bubble.")]
        [SerializeField] Vector2 worldOffset = new(0f, 0.6f);
        [SerializeField, Min(0f)] float screenMargin = 12f;

        [Header("Appear")]
        [Tooltip("Seconds after the companion has settled beside the player before the bubble opens.")]
        [SerializeField, Min(0f)] float openDelay = 0.2f;
        [Tooltip("Seconds to grow out of the companion.")]
        [SerializeField, Min(0.01f)] float openDuration = 0.3f;
        [Tooltip("Seconds to shrink back into the companion when it leaves.")]
        [SerializeField, Min(0.01f)] float closeDuration = 0.15f;

        [Header("Typing (first time in each room)")]
        [SerializeField, Min(1f)] float charactersPerSecond = 40f;
        [Tooltip("Extra pause after . ! ? and ...")]
        [SerializeField, Min(0f)] float sentencePause = 0.2f;
        [Tooltip("Extra pause after , ; :")]
        [SerializeField, Min(0f)] float commaPause = 0.08f;

        readonly HashSet<Object> _typedRooms = new();
        VisualElement _bubble;
        Label _text;
        State _state;
        float _timer;
        float _reveal;
        string _hint = string.Empty;
        bool _isTyping;
        int _typedCount;
        float _typeTimer;
        Object _typingRoom;
        bool _isCutscenePlaying;

        void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _bubble = root.Q("hintBubble");
            _text = root.Q<Label>("hintText");
            _text.enableRichText = true;
            if (worldCamera == null)
                worldCamera = Camera.main;

            companion.HintChanged += OnHintChanged;
            EventBus<CutsceneStartedEvent>.Subscribe(OnCutsceneStarted);
            EventBus<CutsceneEndedEvent>.Subscribe(OnCutsceneEnded);
            _hint = companion.CurrentHint ?? string.Empty;
            SetState(State.Hidden);
        }

        void OnDisable()
        {
            companion.HintChanged -= OnHintChanged;
            EventBus<CutsceneStartedEvent>.Unsubscribe(OnCutsceneStarted);
            EventBus<CutsceneEndedEvent>.Unsubscribe(OnCutsceneEnded);
        }

        void LateUpdate()
        {
            float deltaTime = Time.unscaledDeltaTime;
            UpdateState(deltaTime);
            if (_state == State.Hidden || _state == State.Waiting)
                return;

            if (_isTyping)
                UpdateTyping(deltaTime);
            ApplyLook();
        }

        void UpdateState(float deltaTime)
        {
            bool summoned = companion.IsSummoned;
            switch (_state)
            {
                case State.Hidden:
                    if (summoned)
                        SetState(State.Waiting);
                    break;

                case State.Waiting:
                    if (!summoned)
                    {
                        SetState(State.Hidden);
                        break;
                    }

                    // The delay only counts down while the companion is settled and nothing else has the stage.
                    if (companion.IsSettled && !_isCutscenePlaying)
                    {
                        _timer += deltaTime;
                        if (_timer >= openDelay)
                            Open();
                    }
                    else
                    {
                        _timer = 0f;
                    }

                    break;

                case State.Opening:
                    if (!summoned)
                    {
                        SetState(State.Closing);
                        break;
                    }

                    _timer += deltaTime;
                    _reveal = Mathf.Clamp01(_timer / openDuration);
                    if (_reveal >= 1f)
                        SetState(State.Open);
                    break;

                case State.Open:
                    if (!summoned)
                        SetState(State.Closing);
                    break;

                case State.Closing:
                    if (summoned)
                    {
                        // Summoned again mid-close: wait for it to settle once more.
                        SetState(State.Waiting);
                        break;
                    }

                    _timer += deltaTime;
                    _reveal = Mathf.Min(_reveal, 1f - Mathf.Clamp01(_timer / closeDuration));
                    if (_reveal <= 0f)
                        SetState(State.Hidden);
                    break;
            }
        }

        void SetState(State state)
        {
            _state = state;
            _timer = 0f;
            switch (state)
            {
                case State.Hidden:
                case State.Waiting:
                    _reveal = 0f;
                    _isTyping = false;
                    _bubble.EnableInClassList(HiddenClass, true);
                    break;

                case State.Opening:
                    _reveal = 0f;
                    _bubble.EnableInClassList(HiddenClass, false);
                    break;

                case State.Closing:
                    _isTyping = false;
                    break;
            }
        }

        /// <summary>Grows the bubble out of the companion; types the hint if this room hasn't shown it before.</summary>
        void Open()
        {
            SetState(State.Opening);
            Room room = RoomManager.CurrentRoom;
            _typingRoom = room;
            _isTyping = !string.IsNullOrEmpty(_hint) && (room == null || !_typedRooms.Contains(room));
            _typedCount = 0;
            _typeTimer = 0f;
            ShowText(_isTyping ? 0 : _hint.Length);
            ApplyLook();
        }

        void UpdateTyping(float deltaTime)
        {
            // Starts once the bubble has mostly grown in.
            if (_state == State.Opening && _reveal < 0.6f)
                return;

            _typeTimer += deltaTime;
            int count = _typedCount;
            while (count < _hint.Length)
            {
                float delay = 1f / charactersPerSecond + PauseAfter(count - 1);
                if (_typeTimer < delay)
                    break;

                _typeTimer -= delay;
                count++;
            }

            if (count != _typedCount)
            {
                _typedCount = count;
                ShowText(count);
            }

            if (_typedCount >= _hint.Length)
            {
                _isTyping = false;
                if (_typingRoom != null)
                    _typedRooms.Add(_typingRoom);
            }
        }

        float PauseAfter(int index)
        {
            if (index < 0 || index >= _hint.Length)
                return 0f;

            // Only at the end of a word, so "..." or "?!" pause once.
            bool nextIsSpace = index + 1 >= _hint.Length || char.IsWhiteSpace(_hint[index + 1]);
            if (!nextIsSpace)
                return 0f;

            return _hint[index] switch
            {
                '.' or '!' or '?' => sentencePause,
                ',' or ';' or ':' => commaPause,
                _ => 0f,
            };
        }

        /// <summary>Shows the first <paramref name="visibleCount"/> characters; the rest stay laid out but invisible,
        /// so the bubble keeps its final size while typing.</summary>
        void ShowText(int visibleCount)
        {
            visibleCount = Mathf.Clamp(visibleCount, 0, _hint.Length);
            _text.text = visibleCount >= _hint.Length
                ? _hint
                : _hint.Substring(0, visibleCount) + HiddenTextTag + _hint.Substring(visibleCount);
        }

        void ApplyLook()
        {
            if (worldCamera == null || _bubble.panel == null)
                return;

            // Grows from the companion's centre up to its place, scaling from the tail.
            float grow = _state == State.Closing
                ? DOVirtual.EasedValue(0f, 1f, _reveal, Ease.InQuad)
                : DOVirtual.EasedValue(0f, 1f, _reveal, Ease.OutBack);
            Vector3 worldPosition = companion.transform.position + (Vector3)(worldOffset * Mathf.Clamp01(grow));
            Vector2 anchor = RuntimePanelUtils.CameraTransformWorldToPanel(_bubble.panel, worldPosition, worldCamera);

            // Keep the whole bubble on screen; its pivot is bottom-centre (see the USS translate).
            Rect panelRect = _bubble.panel.visualTree.layout;
            float halfWidth = SafeSize(_bubble.resolvedStyle.width) * 0.5f;
            float height = SafeSize(_bubble.resolvedStyle.height);
            float minX = halfWidth + screenMargin;
            anchor.x = Mathf.Clamp(anchor.x, minX, Mathf.Max(minX, panelRect.width - halfWidth - screenMargin));
            anchor.y = Mathf.Max(anchor.y, height + screenMargin);

            _bubble.style.left = anchor.x;
            _bubble.style.top = anchor.y;
            float scale = Mathf.Max(0f, grow);
            _bubble.style.scale = new Scale(new Vector3(scale, scale, 1f));
            _bubble.style.opacity = Mathf.Clamp01(_reveal * 3f);
        }

        void OnHintChanged(string hint)
        {
            _hint = hint ?? string.Empty;

            // A new room's hint while open: pop out of the companion again (typing it if it's new here).
            if (_state == State.Opening || _state == State.Open)
                Open();
        }

        void OnCutsceneStarted(CutsceneStartedEvent evt) => _isCutscenePlaying = true;

        void OnCutsceneEnded(CutsceneEndedEvent evt) => _isCutscenePlaying = false;

        static float SafeSize(float value) => float.IsNaN(value) ? 0f : value;
    }
}
