using System;
using System.Collections.Generic;
using APX.Core;
using UnityEngine;

namespace APX.Rooms
{
    /// <summary>
    /// A self-contained screen. Its bounds are centred on the transform; children (Grid/Tilemaps,
    /// enemies, hazards) implementing <see cref="IResettable"/> are reset when the room restarts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Room : MonoBehaviour
    {
        const float FallbackEdgeMargin = 1f;

        static readonly List<Room> s_activeRooms = new();

        [Tooltip("Room size in world units (32x18 = one 16:9 screen of 1-unit tiles).")]
        [SerializeField] Vector2 size = new(32f, 18f);
        [Tooltip("Respawn candidates; the one closest to where the player entered is used.")]
        [SerializeField] Transform[] spawnPoints = Array.Empty<Transform>();

        IResettable[] _resettables = Array.Empty<IResettable>();

        public static IReadOnlyList<Room> All => s_activeRooms;

        public Vector2 Size => size;

        public Vector2 Center => transform.position;

        public Rect Bounds => new((Vector2)transform.position - size * 0.5f, size);

        /// <summary>True while this is the room on screen. Set by the <see cref="RoomManager"/>.</summary>
        public bool IsCurrent { get; internal set; }

        void Awake() => _resettables = GetComponentsInChildren<IResettable>(true);

        void OnEnable() => s_activeRooms.Add(this);

        void OnDisable() => s_activeRooms.Remove(this);

        public bool Contains(Vector2 point) => Bounds.Contains(point);

        public void ResetRoom()
        {
            foreach (IResettable resettable in _resettables)
                resettable.ResetState();
        }

        /// <summary>Closest spawn point to <paramref name="entryPoint"/>, or the entry point itself pulled inside the room.</summary>
        public Vector2 GetRespawnPoint(Vector2 entryPoint)
        {
            Transform closest = null;
            float closestSqrDistance = float.MaxValue;
            foreach (Transform point in spawnPoints)
            {
                if (point == null)
                    continue;

                float sqrDistance = ((Vector2)point.position - entryPoint).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closest = point;
                }
            }

            if (closest != null)
                return closest.position;

            Rect bounds = Bounds;
            return new Vector2(
                Mathf.Clamp(entryPoint.x, bounds.xMin + FallbackEdgeMargin, bounds.xMax - FallbackEdgeMargin),
                Mathf.Clamp(entryPoint.y, bounds.yMin + FallbackEdgeMargin, bounds.yMax - FallbackEdgeMargin));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_activeRooms.Clear();

        void OnDrawGizmos()
        {
            Rect bounds = Bounds;
            Gizmos.color = IsCurrent ? new Color(0.3f, 1f, 0.45f, 0.9f) : new Color(1f, 1f, 1f, 0.35f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(new Vector3(bounds.xMin + 0.5f, bounds.yMax - 0.5f, 0f), name);
#endif

            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            foreach (Transform point in spawnPoints)
            {
                if (point != null)
                    Gizmos.DrawWireSphere(point.position, 0.35f);
            }
        }
    }
}
