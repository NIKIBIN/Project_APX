using UnityEngine;

namespace APX.Combat
{
    public enum Team
    {
        Neutral,
        Player,
        Enemy,
        Environment,
    }

    public readonly struct DamageInfo
    {
        public readonly Team SourceTeam;
        public readonly GameObject Source;
        public readonly Vector2 Point;

        public DamageInfo(Team sourceTeam, GameObject source, Vector2 point)
        {
            SourceTeam = sourceTeam;
            Source = source;
            Point = point;
        }
    }

    /// <summary>
    /// Anything that reacts to being hit. There is no shared HP model: each receiver decides what a
    /// hit means (the player dies instantly, enemies lose a hit point, switches toggle...).
    /// </summary>
    public interface IDamageable
    {
        Team Team { get; }

        /// <returns>True if the hit was accepted (lets attackers react, e.g. projectiles despawn).</returns>
        bool TryReceiveDamage(in DamageInfo damage);
    }
}
