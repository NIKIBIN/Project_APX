using APX.Hints;
using UnityEngine;
using UnityEngine.UIElements;

namespace APX.UI
{
    /// <summary>Screen-space speech bubble (UI Toolkit) that tracks the hint companion in the world.</summary>
    [DefaultExecutionOrder(100)] // After the companion has moved this frame.
    [RequireComponent(typeof(UIDocument))]
    public sealed class HintBubbleView : MonoBehaviour
    {
        const string HiddenClass = "hint-bubble--hidden";

        [SerializeField] HintCompanion companion;
        [Tooltip("Defaults to Camera.main.")]
        [SerializeField] Camera worldCamera;
        [Tooltip("World-space offset from the companion to the tip of the bubble.")]
        [SerializeField] Vector2 worldOffset = new(0f, 0.6f);
        [SerializeField, Min(0f)] float screenMargin = 12f;

        VisualElement _bubble;
        Label _text;

        void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _bubble = root.Q("hintBubble");
            _text = root.Q<Label>("hintText");
            if (worldCamera == null)
                worldCamera = Camera.main;

            companion.HintChanged += OnHintChanged;
            companion.SummonStateChanged += OnSummonStateChanged;
            OnHintChanged(companion.CurrentHint);
            OnSummonStateChanged(companion.IsSummoned);
        }

        void OnDisable()
        {
            companion.HintChanged -= OnHintChanged;
            companion.SummonStateChanged -= OnSummonStateChanged;
        }

        void LateUpdate()
        {
            if (!companion.IsSummoned || worldCamera == null || _bubble.panel == null)
                return;

            Vector3 worldPosition = companion.transform.position + (Vector3)worldOffset;
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
        }

        void OnHintChanged(string hint) => _text.text = hint;

        void OnSummonStateChanged(bool summoned) => _bubble.EnableInClassList(HiddenClass, !summoned);

        static float SafeSize(float value) => float.IsNaN(value) ? 0f : value;
    }
}
