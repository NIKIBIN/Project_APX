using APX.Core;
using APX.Masks;
using UnityEngine;
using UnityEngine.UIElements;

namespace APX.UI
{
    /// <summary>HUD badge showing the mask currently worn.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MaskHudView : MonoBehaviour
    {
        [SerializeField] MaskManager maskManager;

        Label _label;
        VisualElement _chip;

        void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _label = root.Q<Label>("maskHudLabel");
            _chip = root.Q("maskHudChip");

            EventBus<MaskChangedEvent>.Subscribe(OnMaskChanged);
            Refresh(MaskManager.CurrentMask);
        }

        void OnDisable() => EventBus<MaskChangedEvent>.Unsubscribe(OnMaskChanged);

        void OnMaskChanged(MaskChangedEvent evt) => Refresh(evt.Current);

        void Refresh(MaskType mask)
        {
            if (maskManager != null && maskManager.TryGetDefinition(mask, out MaskDefinition definition))
            {
                _label.text = definition.DisplayName;
                _chip.style.backgroundColor = definition.Color; // Data-driven colour, so set from code.
            }
            else
            {
                _label.text = mask.ToString();
                _chip.style.backgroundColor = StyleKeyword.Null;
            }
        }
    }
}
