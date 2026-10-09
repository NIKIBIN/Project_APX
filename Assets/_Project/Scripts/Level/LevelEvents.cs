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
