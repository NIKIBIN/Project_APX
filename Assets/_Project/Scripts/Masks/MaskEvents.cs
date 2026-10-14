using APX.Core;
using UnityEngine;

namespace APX.Masks
{
    public readonly struct MaskChangedEvent : IEvent
    {
        public readonly MaskType Previous;
        public readonly MaskType Current;

        /// <summary>The game set the mask (scene start, respawn), not the player: presentation can stay quiet.</summary>
        public readonly bool IsReset;

        public MaskChangedEvent(MaskType previous, MaskType current, bool isReset = false)
        {
            Previous = previous;
            Current = current;
            IsReset = isReset;
        }
    }

    /// <summary>Raised when the hint companion is summoned (player chose "no mask") or dismissed.</summary>
    public readonly struct CompanionSummonChangedEvent : IEvent
    {
        public readonly bool IsSummoned;

        public CompanionSummonChangedEvent(bool isSummoned) => IsSummoned = isSummoned;
    }

    /// <summary>The player touched a mask lying in the level (e.g. a <c>MaskPickup</c> altar).</summary>
    public readonly struct MaskPickedUpEvent : IEvent
    {
        public readonly MaskType Mask;
        public readonly Vector2 Position;
        public readonly Color Color;

        public MaskPickedUpEvent(MaskType mask, Vector2 position, Color color)
        {
            Mask = mask;
            Position = position;
            Color = color;
        }
    }

    /// <summary>A mask became available in the radial menu for the first time.</summary>
    public readonly struct MaskUnlockedEvent : IEvent
    {
        public readonly MaskType Mask;

        public MaskUnlockedEvent(MaskType mask) => Mask = mask;
    }

    /// <summary>A mask burst into magical particles (appearing, swapping or coming off).</summary>
    public readonly struct MaskBurstEvent : IEvent
    {
        public readonly Vector2 Position;
        public readonly Color Color;

        public MaskBurstEvent(Vector2 position, Color color)
        {
            Position = position;
            Color = color;
        }
    }
}
