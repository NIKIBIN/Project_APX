using APX.Core;
using UnityEngine;

namespace APX.Player
{
    public readonly struct PlayerDiedEvent : IEvent
    {
        public readonly PlayerLife Player;
        public readonly Vector2 Position;

        public PlayerDiedEvent(PlayerLife player, Vector2 position)
        {
            Player = player;
            Position = position;
        }
    }

    public readonly struct PlayerRespawnedEvent : IEvent
    {
        public readonly PlayerLife Player;
        public readonly Vector2 Position;

        public PlayerRespawnedEvent(PlayerLife player, Vector2 position)
        {
            Player = player;
            Position = position;
        }
    }

    /// <summary>The player left the ground with a jump. <see cref="Feet"/> is the bottom of the collider.</summary>
    public readonly struct PlayerJumpedEvent : IEvent
    {
        public readonly Vector2 Feet;

        public PlayerJumpedEvent(Vector2 feet) => Feet = feet;
    }

    public readonly struct PlayerLandedEvent : IEvent
    {
        public readonly Vector2 Feet;

        /// <summary>Fastest downward speed reached during the fall (positive).</summary>
        public readonly float ImpactSpeed;

        public PlayerLandedEvent(Vector2 feet, float impactSpeed)
        {
            Feet = feet;
            ImpactSpeed = impactSpeed;
        }
    }

    /// <summary>
    /// The "Segundo canal" danger sense changed level (the orb behind the player's head). Hook sounds here:
    /// one per level, or a cue when it goes back to <see cref="DangerLevel.None"/>.
    /// </summary>
    public readonly struct DangerSenseChangedEvent : IEvent
    {
        public readonly DangerLevel Previous;
        public readonly DangerLevel Current;

        public DangerSenseChangedEvent(DangerLevel previous, DangerLevel current)
        {
            Previous = previous;
            Current = current;
        }
    }

    /// <summary>The player reversed direction while running fast.</summary>
    public readonly struct PlayerSkiddedEvent : IEvent
    {
        public readonly Vector2 Feet;
        public readonly int NewDirection;

        public PlayerSkiddedEvent(Vector2 feet, int newDirection)
        {
            Feet = feet;
            NewDirection = newDirection;
        }
    }
}
