using APX.Core;
using UnityEngine;

namespace APX.Enemies
{
    /// <summary>
    /// Walks back and forth on a platform, turning at walls, ledges or an optional distance limit.
    /// States: Walk -> Turn (pause, then flip) -> Walk.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class PatrolEnemy : EnemyBase
    {
        [SerializeField, Min(0f)] float moveSpeed = 2.5f;
        [Tooltip("Pause before turning around, in local time.")]
        [SerializeField, Min(0f)] float turnPause = 0.3f;
        [Tooltip("0 = unlimited; otherwise turn after walking this far from the spawn point.")]
        [SerializeField, Min(0f)] float maxDistanceFromSpawn;
        [SerializeField] bool startFacingRight = true;
        [SerializeField] LayerMask groundLayers = 1;
        [SerializeField, Min(0.01f)] float probeDistance = 0.15f;
        [Tooltip("Mirrored on the X axis to face the walking direction.")]
        [SerializeField] Transform visualRoot;

        Rigidbody2D _body;
        BoxCollider2D _collider;
        float _spawnX;

        public int Facing { get; private set; } = 1;

        internal float TurnPause => turnPause;

        protected override void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Kinematic;
            _collider = GetComponent<BoxCollider2D>();
            _spawnX = transform.position.x;
            SetFacing(startFacingRight ? 1 : -1);
            base.Awake();
        }

        protected override IState BuildStateMachine(StateMachine machine)
        {
            var walk = new PatrolWalkState(this);
            var turn = new PatrolTurnState(this);
            machine.AddTransition(walk, turn, () => walk.IsBlocked);
            machine.AddTransition(turn, walk, () => turn.IsComplete);
            return walk;
        }

        protected override void OnSimulationSuspended() => Stop();

        protected override void OnReset()
        {
            Stop();
            SetFacing(startFacingRight ? 1 : -1);
        }

        internal void Walk() => _body.linearVelocity = new Vector2(Facing * moveSpeed * TimeScale, 0f);

        internal void Stop() => _body.linearVelocity = Vector2.zero;

        internal void TurnAround() => SetFacing(-Facing);

        /// <summary>True when a wall, a ledge or the patrol limit is directly ahead.</summary>
        internal bool IsPathBlocked()
        {
            Bounds bounds = _collider.bounds;
            float frontX = Facing > 0 ? bounds.max.x : bounds.min.x;

            bool wallAhead = Physics2D.Raycast(new Vector2(frontX, bounds.center.y), new Vector2(Facing, 0f), probeDistance, groundLayers);
            bool groundAhead = Physics2D.Raycast(new Vector2(frontX + Facing * probeDistance, bounds.min.y + 0.05f), Vector2.down, 0.3f, groundLayers);
            bool beyondLimit = maxDistanceFromSpawn > 0f && (transform.position.x - _spawnX) * Facing >= maxDistanceFromSpawn;

            return wallAhead || !groundAhead || beyondLimit;
        }

        void SetFacing(int direction)
        {
            Facing = direction;
            if (visualRoot != null)
            {
                Vector3 scale = visualRoot.localScale;
                scale.x = Mathf.Abs(scale.x) * direction;
                visualRoot.localScale = scale;
            }
        }

        void OnDrawGizmosSelected()
        {
            if (maxDistanceFromSpawn <= 0f)
                return;

            float originX = Application.isPlaying ? _spawnX : transform.position.x;
            Vector3 y = Vector3.up * transform.position.y;
            Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.9f);
            Gizmos.DrawLine(new Vector3(originX - maxDistanceFromSpawn, 0f) + y, new Vector3(originX + maxDistanceFromSpawn, 0f) + y);
        }
    }
}
