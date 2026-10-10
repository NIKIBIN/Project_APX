using APX.Core;
using UnityEngine;

namespace APX.Level
{
    /// <summary>A heavy door starts rumbling (about to open, or about to drop shut).</summary>
    public readonly struct DoorRumbledEvent : IEvent
    {
        /// <summary>Centre of the face that meets the floor (or ceiling).</summary>
        public readonly Vector2 Contact;
        public readonly float Width;

        public DoorRumbledEvent(Vector2 contact, float width)
        {
            Contact = contact;
            Width = width;
        }
    }

    /// <summary>The room starts shaking: the screen rumbles and debris falls from the ceiling until <see cref="RumbleStoppedEvent"/>.</summary>
    public readonly struct RumbleStartedEvent : IEvent
    {
        /// <summary>The shaking area (e.g. the room's bounds); debris falls from its top edge.</summary>
        public readonly Rect Area;

        public RumbleStartedEvent(Rect area) => Area = area;
    }

    public readonly struct RumbleStoppedEvent : IEvent
    {
    }

    /// <summary>A spike trap's spikes shot out of the floor.</summary>
    public readonly struct SpikeTrapPoppedEvent : IEvent
    {
        /// <summary>Centre of the trap on the floor surface.</summary>
        public readonly Vector2 Surface;
        public readonly float Width;

        public SpikeTrapPoppedEvent(Vector2 surface, float width)
        {
            Surface = surface;
            Width = width;
        }
    }

    /// <summary>A heavy door finished closing and hit the floor (or ceiling).</summary>
    public readonly struct DoorSlammedEvent : IEvent
    {
        public readonly Vector2 Contact;
        public readonly float Width;

        public DoorSlammedEvent(Vector2 contact, float width)
        {
            Contact = contact;
            Width = width;
        }
    }
}
