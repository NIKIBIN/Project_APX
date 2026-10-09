using APX.Core;

namespace APX.Rooms
{
    public readonly struct RoomChangedEvent : IEvent
    {
        /// <summary>Room the player left; null on the very first room.</summary>
        public readonly Room Previous;
        public readonly Room Current;

        public RoomChangedEvent(Room previous, Room current)
        {
            Previous = previous;
            Current = current;
        }
    }

    /// <summary>Raised after the current room was reset and the player respawned in it.</summary>
    public readonly struct RoomRestartedEvent : IEvent
    {
        public readonly Room Room;

        public RoomRestartedEvent(Room room) => Room = room;
    }
}
