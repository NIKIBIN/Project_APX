using APX.Core;
using UnityEngine;

namespace APX.Masks
{
    public readonly struct MaskChangedEvent : IEvent
    {
        public readonly MaskType Previous;
        public readonly MaskType Current;

        public MaskChangedEvent(MaskType previous, MaskType current)
        {
            Previous = previous;
            Current = current;
        }
    }

    /// <summary>Raised when the hint companion is summoned (player chose "no mask") or dismissed.</summary>
    public readonly struct CompanionSummonChangedEvent : IEvent
    {
        public readonly bool IsSummoned;

        public CompanionSummonChangedEvent(bool isSummoned) => IsSummoned = isSummoned;
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
