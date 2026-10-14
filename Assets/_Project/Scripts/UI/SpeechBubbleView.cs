using System.Collections.Generic;
using APX.Core;
using APX.Cutscenes;
using APX.Hints;
using APX.Rooms;
using DG.Tweening;
using InputPrompts;
using UnityEngine;
using UnityEngine.UIElements;

namespace APX.UI
{
    /// <summary>
    /// Screen-space speech bubble (UI Toolkit) that points at someone in the world. Normally it carries the hint
    /// companion's hint: it waits until the companion has fully appeared at its place (and no cutscene is playing),
    /// grows out of it, and shrinks back into it when the companion leaves. The first time it opens in each room the
    /// hint is typed out one character at a time; afterwards it shows at once. Cutscenes borrow it for lines spoken by
    /// anyone (see <see cref="Speak"/>), always typed. Text may use {action} button icons, rich text tags and animated
    /// effects (see <see cref="HintText"/>). Runs on unscaled time.
    /// </summary>
    [DefaultExecutionOrder(100)] // After speakers have moved this frame.
    [RequireComponent(typeof(UIDocument))]
    public sealed class SpeechBubbleView : MonoBehaviour
    {
        const string HiddenClass = "hint-bubble--hidden";

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
        [Tooltip("World-space offset from the speaker to the tip of the bubble (unless a cutscene line gives its own).")]
        [SerializeField] Vector2 worldOffset = new(0f, 0.6f);
        [SerializeField, Min(0f)] float screenMargin = 12f;
        [Tooltip("Button icons for {action} prompts in the text.")]
        [SerializeField] InputPromptIcons inputIcons;

        [Header("Appear")]
        [Tooltip("Seconds after the companion has settled at its place before the hint opens.")]
        [SerializeField, Min(0f)] float openDelay = 0.2f;
        [Tooltip("Seconds to grow out of the speaker.")]
        [SerializeField, Min(0.01f)] float openDuration = 0.3f;
        [Tooltip("Seconds to shrink back into the speaker.")]
        [SerializeField, Min(0.01f)] float closeDuration = 0.15f;

        [Header("Typing")]
        [SerializeField, Min(1f)] float charactersPerSecond = 40f;
        [Tooltip("Extra pause after . ! ? and ...")]
        [SerializeField, Min(0f)] float sentencePause = 0.2f;
        [Tooltip("Extra pause after , ; :")]
        [SerializeField, Min(0f)] float commaPause = 0.08f;

        [Header("Text Effects")]
        [Tooltip("Tuning for <wave>, <shake>, <pulse> and <rainbow> in the text.")]
        [SerializeField] HintText.EffectSettings textEffects = HintText.EffectSettings.Default;

        readonly HashSet<Object> _typedRooms = new();
        readonly HintText _text = new();
        VisualElement _bubble;
        Label _label;
        State _state;
        float _timer;
        float _reveal;
        int _shownCount;
        bool _isTyping;
        int _typedCount;
        float _typeTimer;
        Object _typingRoom;
        bool _isCutscenePlaying;

        // Who the bubble points at: the companion for hints, anyone for cutscene lines.
        Transform _speaker;
        Vector2 _speakerOffset;
        bool _isSpeech;

        public InputPromptIcons Icons => inputIcons;

        public Camera WorldCamera => worldCamera;

        /// <summary>A cutscene line is on screen and still being typed out.</summary>
        public bool IsTyping => _isSpeech && _isTyping;

        /// <summary>The bubble is on screen, including while it grows or shrinks.</summary>
        public bool IsShowing => _state == State.Opening || _state == State.Open || _state == State.Closing;

        void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _bubble = root.Q("hintBubble");
            _label = root.Q<Label>("hintText");
            _label.enableRichText = true;
            _label.PostProcessTextVertices = glyphs => _text.ApplyEffects(glyphs, Time.unscaledTime, textEffects);
            if (worldCamera == null)
                worldCamera = Camera.main;

            companion.HintChanged += OnHintChanged;
            ControlSchemeTracker.Changed += OnControlSchemeChanged;
            EventBus<CutsceneStartedEvent>.Subscribe(OnCutsceneStarted);
            EventBus<CutsceneEndedEvent>.Subscribe(OnCutsceneEnded);
            ReturnToHints();
            SetState(State.Hidden);
        }

        void OnDisable()
        {
            if (_label != null)
                _label.PostProcessTextVertices = null;
            companion.HintChanged -= OnHintChanged;
            ControlSchemeTracker.Changed -= OnControlSchemeChanged;
            EventBus<CutsceneStartedEvent>.Unsubscribe(OnCutsceneStarted);
            EventBus<CutsceneEndedEvent>.Unsubscribe(OnCutsceneEnded);
        }

        void LateUpdate()
        {
            float deltaTime = Time.unscaledDeltaTime;
            if (_isSpeech)
                UpdateSpeechState(deltaTime);
            else
                UpdateHintState(deltaTime);

            if (_state == State.Hidden || _state == State.Waiting)
                return;

            if (_isTyping)
                UpdateTyping(deltaTime);
            ApplyLook();

            // Animated effects are re-applied whenever the text repaints.
            if (_text.HasEffects)
                _label.MarkDirtyRepaint();
        }

        /// <summary>
        /// Cutscene line: types <paramref name="text"/> in the bubble over <paramref name="speaker"/>, growing out of
        /// them (or, if the bubble is already theirs, replacing the text in place). It stays until
        /// <see cref="EndSpeech"/>.
        /// </summary>
        /// <param name="offset">World offset from the speaker to the tip of the bubble; defaults to the hint's.</param>
        public void Speak(Transform speaker, string text, Vector2? offset = null)
        {
            bool sameSpeaker = _isSpeech && speaker == _speaker && (_state == State.Opening || _state == State.Open);
            _isSpeech = true;
            _speaker = speaker;
            _speakerOffset = offset ?? worldOffset;
            _text.SetText(text);
            if (sameSpeaker)
            {
                StartTyping(true);
                return;
            }

            SetState(State.Opening);
            _typingRoom = null;
            StartTyping(_text.Length > 0);
            ApplyLook();
        }

        /// <summary>Shows the rest of the text being typed at once.</summary>
        public void FinishTyping()
        {
            if (!_isTyping)
                return;

            _typedCount = _text.Length;
            ShowText(_text.Length);
            FinishedTyping();
        }

        /// <summary>Closes a cutscene line's bubble; the hint takes it back once it has shrunk away.</summary>
        public void EndSpeech()
        {
            if (!_isSpeech)
                return;

            if (_state == State.Opening || _state == State.Open)
                SetState(State.Closing);
            else if (_state != State.Closing)
                ReturnToHints();
        }

        /// <summary>Types the current room's hint out again, popping the bubble back open if it's showing (for testing).</summary>
        public void ReplayHint()
        {
            if (_isSpeech)
                return;

            if (RoomManager.CurrentRoom != null)
                _typedRooms.Remove(RoomManager.CurrentRoom);
            if (_state == State.Opening || _state == State.Open)
                OpenHint();
        }

        void UpdateHintState(float deltaTime)
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
                            OpenHint();
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

                    Grow(deltaTime);
                    break;

                case State.Open:
                    if (!summoned)
                        SetState(State.Closing);
                    break;

                case State.Closing:
                    if (summoned && !_isCutscenePlaying)
                    {
                        // Summoned again mid-close: wait for it to settle once more.
                        SetState(State.Waiting);
                        break;
                    }

                    Shrink(deltaTime);
                    break;
            }
        }

        void UpdateSpeechState(float deltaTime)
        {
            if (_state == State.Opening)
            {
                Grow(deltaTime);
            }
            else if (_state == State.Closing)
            {
                Shrink(deltaTime);
                if (_state == State.Hidden)
                    ReturnToHints();
            }
        }

        void Grow(float deltaTime)
        {
            _timer += deltaTime;
            _reveal = Mathf.Clamp01(_timer / openDuration);
            if (_reveal >= 1f)
                SetState(State.Open);
        }

        void Shrink(float deltaTime)
        {
            _timer += deltaTime;
            _reveal = Mathf.Min(_reveal, 1f - Mathf.Clamp01(_timer / closeDuration));
            if (_reveal <= 0f)
                SetState(State.Hidden);
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
        void OpenHint()
        {
            SetState(State.Opening);
            Room room = RoomManager.CurrentRoom;
            _typingRoom = room;
            StartTyping(_text.Length > 0 && (room == null || !_typedRooms.Contains(room)));
            ApplyLook();
        }

        void ReturnToHints()
        {
            _isSpeech = false;
            _speaker = companion.transform;
            _speakerOffset = worldOffset;
            _text.SetText(companion.CurrentHint);
        }

        void StartTyping(bool typed)
        {
            _isTyping = typed;
            _typedCount = 0;
            _typeTimer = 0f;
            ShowText(typed ? 0 : _text.Length);
        }

        void UpdateTyping(float deltaTime)
        {
            // Starts once the bubble has mostly grown in.
            if (_state == State.Opening && _reveal < 0.6f)
                return;

            _typeTimer += deltaTime;
            int count = _typedCount;
            while (count < _text.Length)
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

            if (_typedCount >= _text.Length)
                FinishedTyping();
        }

        void FinishedTyping()
        {
            _isTyping = false;
            if (!_isSpeech && _typingRoom != null)
                _typedRooms.Add(_typingRoom);
        }

        float PauseAfter(int index)
        {
            if (index < 0 || index >= _text.Length)
                return 0f;

            // Only at the end of a word, so "..." or "?!" pause once.
            bool nextIsSpace = index + 1 >= _text.Length || char.IsWhiteSpace(_text.CharAt(index + 1));
            if (!nextIsSpace)
                return 0f;

            return _text.CharAt(index) switch
            {
                '.' or '!' or '?' => sentencePause,
                ',' or ';' or ':' => commaPause,
                _ => 0f,
            };
        }

        /// <summary>Shows the first <paramref name="visibleCount"/> characters and icons; the rest stay laid out but
        /// invisible, so the bubble keeps its final size while typing.</summary>
        void ShowText(int visibleCount)
        {
            _shownCount = Mathf.Clamp(visibleCount, 0, _text.Length);
            _label.text = _text.Render(_shownCount, ControlSchemeTracker.Current, inputIcons);
        }

        void ApplyLook()
        {
            if (worldCamera == null || _bubble.panel == null || _speaker == null)
                return;

            // Grows from the speaker's centre up to its place, scaling from the tail.
            float grow = _state == State.Closing
                ? DOVirtual.EasedValue(0f, 1f, _reveal, Ease.InQuad)
                : DOVirtual.EasedValue(0f, 1f, _reveal, Ease.OutBack);
            Vector3 worldPosition = _speaker.position + (Vector3)(_speakerOffset * Mathf.Clamp01(grow));
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
            // A cutscene line has the bubble; the hint is picked up again when it's done.
            if (_isSpeech)
                return;

            _text.SetText(hint);
            if (_state != State.Opening && _state != State.Open)
                return;

            // A new room's hint while open: pop out of the companion again (typing it if it's new here), or, if it
            // has to fly to the room's companion spot first, wait until it gets there.
            if (companion.IsSettled)
                OpenHint();
            else
                SetState(State.Waiting);
        }

        // Swaps the button icons in place, keeping how much of the text has been typed so far.
        void OnControlSchemeChanged(ControlScheme scheme)
        {
            if (IsShowing)
                ShowText(_shownCount);
        }

        void OnCutsceneStarted(CutsceneStartedEvent evt)
        {
            _isCutscenePlaying = true;

            // The hint steps aside while a cutscene plays.
            if (!_isSpeech && (_state == State.Opening || _state == State.Open))
                SetState(State.Closing);
        }

        void OnCutsceneEnded(CutsceneEndedEvent evt)
        {
            _isCutscenePlaying = false;
            EndSpeech();
        }

        static float SafeSize(float value) => float.IsNaN(value) ? 0f : value;
    }
}
