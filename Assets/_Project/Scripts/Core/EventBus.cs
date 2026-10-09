using System;
using System.Collections.Generic;
using UnityEngine;

namespace APX.Core
{
    /// <summary>Marker interface for payloads published through <see cref="EventBus{T}"/>.</summary>
    public interface IEvent { }

    /// <summary>
    /// Type-safe static publish/subscribe channel. Publishers and subscribers only share the event
    /// struct, never a reference to each other.
    /// </summary>
    public static class EventBus<T> where T : struct, IEvent
    {
        static Action<T> s_handlers;

        static EventBus() => EventBusRegistry.Register(Clear);

        public static void Subscribe(Action<T> handler) => s_handlers += handler;

        public static void Unsubscribe(Action<T> handler) => s_handlers -= handler;

        public static void Raise(T evt) => s_handlers?.Invoke(evt);

        static void Clear() => s_handlers = null;
    }

    /// <summary>
    /// Clears every bus when entering play mode. Required because the project runs with
    /// Domain Reload disabled, so static handlers would otherwise survive between sessions.
    /// </summary>
    static class EventBusRegistry
    {
        static readonly List<Action> s_clearActions = new();

        internal static void Register(Action clear) => s_clearActions.Add(clear);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearAll()
        {
            foreach (Action clear in s_clearActions)
                clear();
        }
    }
}
