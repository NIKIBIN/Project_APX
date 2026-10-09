using APX.Core;
using UnityEngine;

namespace APX.Enemies
{
    public readonly struct EnemyDiedEvent : IEvent
    {
        public readonly EnemyBase Enemy;
        public readonly Vector2 Position;

        public EnemyDiedEvent(EnemyBase enemy, Vector2 position)
        {
            Enemy = enemy;
            Position = position;
        }
    }
}
