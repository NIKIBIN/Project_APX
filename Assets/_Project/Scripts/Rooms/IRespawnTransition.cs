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
        /// <param name="quick">Play a shorter version, e.g. when retrying the same room.</param>
        IEnumerator Close(Func<Vector2> worldFocus, bool quick = false);

        /// <summary>Reveals the screen again after the room was reset.</summary>
        /// <param name="quick">Play a shorter version, e.g. when retrying the same room.</param>
        IEnumerator Open(Func<Vector2> worldFocus, bool quick = false);

        /// <summary>Covers the screen at once, e.g. so a scene can open with <see cref="Open"/>.</summary>
        void Cover();
    }
}
