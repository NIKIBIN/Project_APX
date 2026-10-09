using APX.Core;
using UnityEngine;

namespace APX.Combat
{
    /// <summary>A player attack connected with something that accepted the hit (enemy, switch...).</summary>
    public readonly struct AttackHitEvent : IEvent
    {
        public readonly Vector2 Point;
        public readonly IDamageable Target;

        public AttackHitEvent(Vector2 point, IDamageable target)
        {
            Point = point;
            Target = target;
        }
    }
}
