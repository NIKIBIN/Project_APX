using DG.Tweening;
using InputPrompts;
using UnityEngine;
using UnityEngine.UIElements;

namespace APX.UI
{
    /// <summary>
    /// A big, pulsing button icon floating over something in the world, e.g. a quick-time event's prompt. It lives in
    /// the HUD next to the <see cref="SpeechBubbleView"/>, whose icons and camera it uses, and builds its own element,
    /// so it needs no setup: <see cref="For"/> adds it the first time it's needed. Runs on unscaled time.
    /// </summary>
    [DefaultExecutionOrder(100)] // After the target has moved this frame.
    [RequireComponent(typeof(UIDocument))]
    public sealed class ButtonPromptView : MonoBehaviour
    {
        [Tooltip("World-space offset from the target to the bottom of the icon.")]
        [SerializeField] Vector2 worldOffset = new(0f, 1.6f);
        [Tooltip("Text size the icon is drawn at; icons are sized relative to it.")]
        [SerializeField, Min(1f)] float fontSize = 34f;
        [SerializeField, Min(0f)] float pulseAmount = 0.12f;
        [SerializeField, Min(0f)] float pulseSpeed = 9f;
        [Tooltip("Seconds to pop in, and to burst away once pressed.")]
        [SerializeField, Min(0.01f)] float popDuration = 0.18f;

        SpeechBubbleView _hud;
        Label _label;
        Transform _target;
        string _token;
        float _shownAt;
        float _pressedAt;
        bool _isVisible;

        /// <summary>The prompt in <paramref name="hud"/>'s document, added on first use.</summary>
        public static ButtonPromptView For(SpeechBubbleView hud)
        {
            ButtonPromptView view = hud.GetComponent<ButtonPromptView>();
            if (view == null)
                view = hud.gameObject.AddComponent<ButtonPromptView>();
            return view;
        }

        void OnEnable()
        {
            _hud = GetComponent<SpeechBubbleView>();
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _label = new Label { name = "buttonPrompt", pickingMode = PickingMode.Ignore, enableRichText = true };
            _label.style.position = Position.Absolute;
            _label.style.fontSize = fontSize;
            _label.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-100f));
            _label.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(100f));
            _label.style.display = DisplayStyle.None;
            (root.Q("hudRoot") ?? root).Add(_label);
            ControlSchemeTracker.Changed += OnControlSchemeChanged;
        }

        void OnDisable()
        {
            ControlSchemeTracker.Changed -= OnControlSchemeChanged;
            _label?.RemoveFromHierarchy();
            _label = null;
            _isVisible = false;
        }

        /// <summary>Shows the icon for the {<paramref name="token"/>} prompt (e.g. "jump") over <paramref name="target"/>.</summary>
        public void Show(Transform target, string token)
        {
            _target = target;
            _token = token;
            _shownAt = Time.unscaledTime;
            _pressedAt = -1f;
            _isVisible = true;
            Refresh();
            _label.style.display = DisplayStyle.Flex;
            LateUpdate();
        }

        /// <summary>Takes the icon away: bursting if it was <paramref name="pressed"/>, at once otherwise.</summary>
        public void Hide(bool pressed)
        {
            if (!_isVisible)
                return;

            if (pressed)
                _pressedAt = Time.unscaledTime;
            else
                Vanish();
        }

        void LateUpdate()
        {
            if (!_isVisible || _label.panel == null)
                return;

            Camera worldCamera = _hud != null ? _hud.WorldCamera : null;
            if (_target == null || worldCamera == null)
            {
                Vanish();
                return;
            }

            float now = Time.unscaledTime;
            float scale;
            float opacity = 1f;
            if (_pressedAt >= 0f)
            {
                float burst = (now - _pressedAt) / popDuration;
                if (burst >= 1f)
                {
                    Vanish();
                    return;
                }

                scale = 1f + 0.5f * burst;
                opacity = 1f - burst;
            }
            else
            {
                float shown = now - _shownAt;
                float pop = DOVirtual.EasedValue(0f, 1f, Mathf.Clamp01(shown / popDuration), Ease.OutBack);
                scale = pop * (1f + pulseAmount * Mathf.Sin(shown * pulseSpeed));
            }

            Vector2 position = RuntimePanelUtils.CameraTransformWorldToPanel(_label.panel, _target.position + (Vector3)worldOffset, worldCamera);
            _label.style.left = position.x;
            _label.style.top = position.y;
            _label.style.scale = new Scale(new Vector3(scale, scale, 1f));
            _label.style.opacity = opacity;
        }

        void Vanish()
        {
            _isVisible = false;
            if (_label != null)
                _label.style.display = DisplayStyle.None;
        }

        void Refresh()
        {
            InputPromptIcons icons = _hud != null ? _hud.Icons : null;
            _label.text = icons != null ? icons.Format("{" + _token + "}", ControlSchemeTracker.Current) : _token;
        }

        void OnControlSchemeChanged(ControlScheme scheme)
        {
            if (_isVisible)
                Refresh();
        }
    }
}
