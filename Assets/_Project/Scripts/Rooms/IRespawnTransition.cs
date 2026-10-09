using System;
using System.Collections;
using UnityEngine;

namespace APX.Rooms
{
    /// <summary>
    /// Screen transition played around a room restart. The focus point is read every frame, so the
    /// transition can follow the player. Either coroutine may be stopped halfway by another death.
    /// </summary>
    public interface IRespawnTransition
    {
        /// <summary>Covers the screen; it must end fully covered.</summary>
        IEnumerator Close(Func<Vector2> worldFocus);

        /// <summary>Reveals the screen again after the room was reset.</summary>
        IEnumerator Open(Func<Vector2> worldFocus);
    }
}
