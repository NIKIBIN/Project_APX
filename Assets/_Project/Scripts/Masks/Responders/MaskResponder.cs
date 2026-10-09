using APX.Core;
using UnityEngine;

namespace APX.Masks
{
    /// <summary>
    /// Base for modular components attached to scenario objects. Each one watches a single condition,
    /// "mask X is worn" (or, inverted, "mask X is NOT worn"), and subclasses only implement the effect.
    /// </summary>
    public abstract class MaskResponder : MonoBehaviour
    {
        [SerializeField] MaskType mask = MaskType.SecondChannel;
        [Tooltip("Apply the effect while the mask is NOT worn (e.g. a fake wall that vanishes when the mask is on).")]
        [SerializeField] bool invert;

        public MaskType Mask => mask;

        public bool IsConditionMet { get; private set; }

        protected virtual void OnEnable()
        {
            EventBus<MaskChangedEvent>.Subscribe(OnMaskChanged);
            Evaluate(MaskManager.CurrentMask);
        }

        protected virtual void OnDisable() => EventBus<MaskChangedEvent>.Unsubscribe(OnMaskChanged);

        /// <param name="conditionMet">True while the configured mask condition holds.</param>
        protected abstract void Apply(bool conditionMet);

        /// <summary>Lets subclasses pick a sensible default mask from <c>Reset()</c>.</summary>
        protected void SetDefaultMask(MaskType value) => mask = value;

        void OnMaskChanged(MaskChangedEvent evt) => Evaluate(evt.Current);

        void Evaluate(MaskType current)
        {
            IsConditionMet = (current == mask) != invert;
            Apply(IsConditionMet);
        }
    }
}
