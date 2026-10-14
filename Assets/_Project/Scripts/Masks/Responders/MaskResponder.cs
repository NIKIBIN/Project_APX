using System.Collections.Generic;
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
        static readonly List<MaskResponder> s_active = new();

        [SerializeField] MaskType mask = MaskType.SecondChannel;
        [Tooltip("Apply the effect while the mask is NOT worn (e.g. a fake wall that vanishes when the mask is on).")]
        [SerializeField] bool invert;

        /// <summary>Every enabled responder, e.g. for effects that show what a mask change touched.</summary>
        public static IReadOnlyList<MaskResponder> Active => s_active;

        public MaskType Mask => mask;

        public bool IsConditionMet { get; private set; }

        /// <summary>Whether the condition holds while <paramref name="worn"/> is the mask worn.</summary>
        public bool IsMetFor(MaskType worn) => (worn == mask) != invert;

        protected virtual void OnEnable()
        {
            s_active.Add(this);
            EventBus<MaskChangedEvent>.Subscribe(OnMaskChanged);
            Evaluate(MaskManager.CurrentMask);
        }

        protected virtual void OnDisable()
        {
            s_active.Remove(this);
            EventBus<MaskChangedEvent>.Unsubscribe(OnMaskChanged);
        }

        /// <param name="conditionMet">True while the configured mask condition holds.</param>
        protected abstract void Apply(bool conditionMet);

        /// <summary>Lets subclasses pick a sensible default mask from <c>Reset()</c>.</summary>
        protected void SetDefaultMask(MaskType value) => mask = value;

        void OnMaskChanged(MaskChangedEvent evt) => Evaluate(evt.Current);

        void Evaluate(MaskType current)
        {
            IsConditionMet = IsMetFor(current);
            Apply(IsConditionMet);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_active.Clear();
    }
}
