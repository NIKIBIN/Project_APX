using System.Collections;
using APX.Core;
using APX.Player;
using UnityEngine;

namespace APX.Rooms
{
    /// <summary>
    /// Tracks the room the player is in, switches room when the player crosses an edge of the current
    /// one, and restarts the current room when the player dies (one-hit kill).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class RoomManager : MonoBehaviour
    {
        [SerializeField] PlayerLife player;
        [Tooltip("Used only when the player does not start inside any room.")]
        [SerializeField] Room startingRoom;
        [Tooltip("Component implementing IRespawnTransition (e.g. the HUD's IrisTransitionView). " +
                 "Found automatically when empty; without one the room restarts instantly.")]
        [SerializeField] MonoBehaviour respawnTransition;
        [Tooltip("Reset rooms when entering and leaving them, so every visit starts fresh.")]
        [SerializeField] bool resetRoomsOnTransition = true;

        Vector2 _entryPoint;
        Coroutine _restartRoutine;
        IRespawnTransition _transition;

        public static Room CurrentRoom { get; private set; }

        Vector2 PlayerPosition => player.transform.position;

        void Awake()
        {
            if (player == null)
                player = FindAnyObjectByType<PlayerLife>();

            _transition = respawnTransition as IRespawnTransition ?? FindTransition();
        }

        void OnValidate()
        {
            if (respawnTransition != null && respawnTransition is not IRespawnTransition)
            {
                Debug.LogWarning($"{respawnTransition.name} does not implement {nameof(IRespawnTransition)}.", this);
                respawnTransition = null;
            }
        }

        void OnEnable() => EventBus<PlayerDiedEvent>.Subscribe(OnPlayerDied);

        void OnDisable() => EventBus<PlayerDiedEvent>.Unsubscribe(OnPlayerDied);

        void Start()
        {
            Room initial = FindRoomAt(PlayerPosition);
            if (initial == null)
                initial = startingRoom;

            if (initial == null)
            {
                Debug.LogError($"{nameof(RoomManager)}: the player is not inside any {nameof(Room)}.", this);
                enabled = false;
                return;
            }

            EnterRoom(initial, PlayerPosition);
        }

        void LateUpdate()
        {
            if (CurrentRoom == null)
                return;

            Vector2 position = PlayerPosition;
            bool belowRoom = position.y < CurrentRoom.Bounds.yMin;
            if (player.IsDead)
            {
                // A knockback can carry the body into a pit; it never changes rooms.
                if (belowRoom)
                    player.FallOutOfLevel();
                return;
            }

            if (CurrentRoom.Contains(position))
                return;

            Room next = FindRoomAt(position);
            if (next != null)
                EnterRoom(next, position);
            else if (belowRoom)
                player.FallOutOfLevel(); // Fell into a pit with no room below.
        }

        void EnterRoom(Room next, Vector2 entryPoint)
        {
            Room previous = CurrentRoom;
            if (previous != null)
            {
                previous.IsCurrent = false;
                if (resetRoomsOnTransition)
                    previous.ResetRoom();
            }

            CurrentRoom = next;
            next.IsCurrent = true;
            _entryPoint = entryPoint;
            if (resetRoomsOnTransition)
                next.ResetRoom();

            EventBus<RoomChangedEvent>.Raise(new RoomChangedEvent(previous, next));
        }

        void OnPlayerDied(PlayerDiedEvent evt)
        {
            if (_restartRoutine != null)
                StopCoroutine(_restartRoutine);
            _restartRoutine = StartCoroutine(RestartCurrentRoom());
        }

        IEnumerator RestartCurrentRoom()
        {
            // Let the death play out (knockback, landing, death animation). Waiting at least a frame
            // also keeps the restart out of the physics callback that killed the player.
            do
                yield return null;
            while (!player.IsReadyToRespawn);

            if (_transition != null)
                yield return _transition.Close(GetPlayerPosition);

            Room room = CurrentRoom;
            room.ResetRoom();
            player.Respawn(room.GetRespawnPoint(_entryPoint));
            EventBus<RoomRestartedEvent>.Raise(new RoomRestartedEvent(room));

            // The player already has control while the screen opens up around them.
            if (_transition != null)
                yield return _transition.Open(GetPlayerPosition);

            _restartRoutine = null;
        }

        Vector2 GetPlayerPosition() => PlayerPosition;

        static IRespawnTransition FindTransition()
        {
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            {
                if (behaviour is IRespawnTransition transition)
                    return transition;
            }

            return null;
        }

        static Room FindRoomAt(Vector2 position)
        {
            foreach (Room room in Room.All)
            {
                if (room.Contains(position))
                    return room;
            }

            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => CurrentRoom = null;
    }
}
