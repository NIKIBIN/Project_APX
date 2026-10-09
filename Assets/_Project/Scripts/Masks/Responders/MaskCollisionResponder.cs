using UnityEngine;

namespace APX.Masks
{
    /// <summary>
    /// Enables the 2D colliders in this object only while the mask condition is met. Covers both
    /// solidity (platforms) and interaction (attack/trigger targets such as switches).
    /// </summary>
    public sealed class MaskCollisionResponder : MaskResponder
    {
        Collider2D[] _colliders;

        void Awake() => _colliders = GetComponentsInChildren<Collider2D>(true);

        protected override void Apply(bool conditionMet)
        {
            foreach (Collider2D target in _colliders)
                target.enabled = conditionMet;
        }
    }
}
