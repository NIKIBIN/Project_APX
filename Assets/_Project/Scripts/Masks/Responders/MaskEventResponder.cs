using UnityEngine;
using UnityEngine.Events;

namespace APX.Masks
{
    /// <summary>
    /// Designer hook: raises UnityEvents when the mask condition changes (audio, VFX, doors...).
    /// Also fires once on enable so listeners start in sync.
    /// </summary>
    public sealed class MaskEventResponder : MaskResponder
    {
        [SerializeField] UnityEvent onConditionMet = new();
        [SerializeField] UnityEvent onConditionLost = new();
        [SerializeField] UnityEvent<bool> onConditionChanged = new();

        protected override void Apply(bool conditionMet)
        {
            if (conditionMet)
                onConditionMet.Invoke();
            else
                onConditionLost.Invoke();

            onConditionChanged.Invoke(conditionMet);
        }
    }
}
