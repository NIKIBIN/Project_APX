using APX.Core;
using APX.Rooms;
using UnityEngine;

namespace APX.Level
{
    /// <summary>Moves a kinematic body along waypoints on local (mask-scaled) time: saws, moving hazards...</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class WaypointMover : MonoBehaviour, IResettable
    {
        [Tooltip("Waypoints relative to the start position.")]
        [SerializeField] Vector2[] waypoints = { Vector2.zero, new(4f, 0f) };
        [SerializeField, Min(0f)] float speed = 4f;
        [Tooltip("Go back and forth instead of looping to the first waypoint.")]
        [SerializeField] bool pingPong = true;
        [SerializeField, Min(0f)] float waitAtWaypoint;

        Rigidbody2D _body;
        ITimeScaleSource _timeSource;
        Room _room;
        Vector2 _origin;
        int _targetIndex;
        int _step;
        float _waitTimer;
        bool _initialized;

        void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Kinematic;
            _timeSource = GetComponentInParent<ITimeScaleSource>(true);
            _room = GetComponentInParent<Room>(true);
            _origin = transform.position;
            _initialized = true;
            ResetState();
        }

        void FixedUpdate()
        {
            if (waypoints.Length < 2 || (_room != null && !_room.IsCurrent))
                return;

            float deltaTime = Time.fixedDeltaTime * _timeSource.ScaleOrDefault();
            if (_waitTimer > 0f)
            {
                _waitTimer -= deltaTime;
                return;
            }

            Vector2 target = _origin + waypoints[_targetIndex];
            Vector2 next = Vector2.MoveTowards(_body.position, target, speed * deltaTime);
            _body.MovePosition(next);

            if ((next - target).sqrMagnitude < 0.0001f)
                AdvanceWaypoint();
        }

        public void ResetState()
        {
            if (!_initialized)
                return;

            _targetIndex = Mathf.Min(1, waypoints.Length - 1);
            _step = 1;
            _waitTimer = 0f;

            Vector2 start = _origin + (waypoints.Length > 0 ? waypoints[0] : Vector2.zero);
            transform.position = start;
            _body.position = start;
        }

        void AdvanceWaypoint()
        {
            _waitTimer = waitAtWaypoint;

            if (!pingPong)
            {
                _targetIndex = (_targetIndex + 1) % waypoints.Length;
                return;
            }

            int next = _targetIndex + _step;
            if (next < 0 || next >= waypoints.Length)
                _step = -_step;
            _targetIndex += _step;
        }

        void OnDrawGizmosSelected()
        {
            if (waypoints.Length == 0)
                return;

            Vector2 origin = Application.isPlaying ? _origin : (Vector2)transform.position;
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
            for (int i = 0; i < waypoints.Length; i++)
            {
                Gizmos.DrawWireSphere(origin + waypoints[i], 0.2f);
                if (i > 0)
                    Gizmos.DrawLine(origin + waypoints[i - 1], origin + waypoints[i]);
            }
        }
    }
}
