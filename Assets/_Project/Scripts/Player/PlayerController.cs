using System;
using APX.Core;
using UnityEngine;

namespace APX.Player
{
    /// <summary>
    /// Platformer locomotion: run, jump with coyote time and jump buffering, variable jump height and
    /// fast fall, plus apex hang and ceiling corner correction. Gravity is integrated here (not by Physics2D)
    /// so jump arcs are tuned as height and time-to-apex. The player has no local time source, so mask
    /// effects on world time never slow it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class PlayerController : MonoBehaviour, IFacingProvider
    {
        const float CornerSkin = 0.02f;
        const float CornerStep = 0.05f;

        static readonly int[] s_cornerSides = { 1, -1 };

        [SerializeField] InputReader input;
        [SerializeField] PlayerMovementConfig config;
        [Tooltip("Mirrored on the X axis to face the movement direction.")]
        [SerializeField] Transform visualRoot;

        Rigidbody2D _body;
        Collider2D _collider;
        float _moveInput;
        float _coyoteTimer;
        float _jumpBufferTimer;
        bool _isJumping;
        bool _isJumpCut;
        bool _isSkidding;
        float _peakFallSpeed;

        public bool IsGrounded { get; private set; }

        /// <summary>False while input is ignored (e.g. dying). Gravity and collisions keep running.</summary>
        public bool HasControl { get; private set; } = true;

        public int FacingDirection { get; private set; } = 1;

        public Vector2 Velocity => _body.linearVelocity;

        public float MaxRunSpeed => config.MaxRunSpeed;

        public event Action Jumped;

        /// <summary>Touched down; the argument is the fastest downward speed reached during the fall.</summary>
        public event Action<float> Landed;

        Vector2 Feet
        {
            get
            {
                Bounds bounds = _collider.bounds;
                return new Vector2(bounds.center.x, bounds.min.y);
            }
        }

        void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _body.gravityScale = 0f;
            _body.freezeRotation = true;

            if (config == null)
            {
                Debug.LogWarning($"{name}: no {nameof(PlayerMovementConfig)} assigned, using defaults.", this);
                config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
            }
        }

        void OnEnable()
        {
            input.Enable();
            input.JumpPressed += OnJumpPressed;
            input.JumpReleased += OnJumpReleased;
        }

        void OnDisable()
        {
            input.JumpPressed -= OnJumpPressed;
            input.JumpReleased -= OnJumpReleased;
            ResetMotionState();
        }

        void Update()
        {
            if (!HasControl)
            {
                _moveInput = 0f;
                return;
            }

            float horizontal = input.Move.x;
            _moveInput = Mathf.Abs(horizontal) >= config.InputDeadZone ? horizontal : 0f;

            if (_moveInput != 0f)
                SetFacing(_moveInput > 0f ? 1 : -1);
        }

        void FixedUpdate()
        {
            float deltaTime = Time.fixedDeltaTime;
            Vector2 velocity = _body.linearVelocity;

            bool wasGrounded = IsGrounded;
            IsGrounded = velocity.y <= 0.01f && ProbeGround();
            if (IsGrounded)
            {
                if (!wasGrounded)
                    Land();
                _coyoteTimer = config.CoyoteTime;
                _isJumping = false;
                _isJumpCut = false;
            }
            else
            {
                _coyoteTimer -= deltaTime;
                _peakFallSpeed = Mathf.Max(_peakFallSpeed, -velocity.y);
            }

            _jumpBufferTimer -= deltaTime;
            DetectSkid(velocity.x);

            // Without control, momentum (e.g. a knockback) carries through the air and bleeds off on the ground.
            if (HasControl || IsGrounded)
                velocity.x = Mathf.MoveTowards(velocity.x, _moveInput * config.MaxRunSpeed, GetHorizontalRate() * deltaTime);

            if (HasControl && _jumpBufferTimer > 0f && _coyoteTimer > 0f)
                velocity.y = StartJump();

            velocity.y = Mathf.Max(velocity.y - GetGravity(velocity.y) * deltaTime, -config.MaxFallSpeed);
            if (velocity.y > 0f)
                TryCornerCorrection(velocity.y * deltaTime);

            _body.linearVelocity = velocity;
        }

        /// <summary>Instantly moves the player, clearing momentum and buffered input.</summary>
        public void Teleport(Vector2 position)
        {
            transform.position = position;
            _body.position = position;
            _body.linearVelocity = Vector2.zero;
            ResetMotionState();
        }

        /// <summary>Gives or takes away input control; physics keeps running either way.</summary>
        public void SetControl(bool hasControl)
        {
            HasControl = hasControl;
            _moveInput = 0f;
            _jumpBufferTimer = 0f;
        }

        /// <summary>Replaces the current velocity, e.g. for a knockback. Cancels any jump in progress.</summary>
        public void Launch(Vector2 velocity)
        {
            ResetMotionState();
            _body.linearVelocity = velocity;
        }

        public void FaceTowards(float worldX)
        {
            float offset = worldX - transform.position.x;
            if (Mathf.Abs(offset) > 0.01f)
                SetFacing(offset > 0f ? 1 : -1);
        }

        float StartJump()
        {
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            _isJumping = true;
            // A buffered tap that was already released still produces a short hop.
            _isJumpCut = !input.IsJumpHeld;
            IsGrounded = false;
            Jumped?.Invoke();
            EventBus<PlayerJumpedEvent>.Raise(new PlayerJumpedEvent(Feet));
            // Gravity is applied in this same step; the half-step bonus cancels the discrete integration
            // error so the apex matches the authored jump height at any fixed timestep.
            return config.JumpVelocity + 0.5f * config.Gravity * Time.fixedDeltaTime;
        }

        float GetGravity(float verticalVelocity)
        {
            // While rising after an early release: heavier gravity (variable jump height).
            if (_isJumping && _isJumpCut && verticalVelocity >= 0f)
                return config.Gravity * config.JumpCutGravityMultiplier;

            // Fast fall: heavier gravity past the apex.
            float gravity = verticalVelocity < 0f ? config.Gravity * config.FallGravityMultiplier : config.Gravity;

            // Apex hang: a held jump floats briefly at the top, which makes it easier to aim.
            if (_isJumping && !_isJumpCut && Mathf.Abs(verticalVelocity) < config.ApexThreshold)
                gravity *= config.ApexGravityMultiplier;

            return gravity;
        }

        void Land()
        {
            float impactSpeed = _peakFallSpeed;
            _peakFallSpeed = 0f;
            Landed?.Invoke(impactSpeed);
            EventBus<PlayerLandedEvent>.Raise(new PlayerLandedEvent(Feet, impactSpeed));
        }

        void DetectSkid(float horizontalVelocity)
        {
            bool reversing = IsGrounded && HasControl && _moveInput * horizontalVelocity < 0f
                && Mathf.Abs(horizontalVelocity) >= config.MaxRunSpeed * config.SkidSpeedRatio;
            if (reversing && !_isSkidding)
                EventBus<PlayerSkiddedEvent>.Raise(new PlayerSkiddedEvent(Feet, _moveInput > 0f ? 1 : -1));
            _isSkidding = reversing;
        }

        /// <summary>
        /// When the head is about to clip a ceiling corner, slides the player sideways (up to the configured
        /// distance) so the jump continues instead of stopping dead. A full ceiling is left alone.
        /// </summary>
        void TryCornerCorrection(float rise)
        {
            if (config.CornerCorrection <= 0f)
                return;

            Bounds bounds = _collider.bounds;
            Vector2 center = bounds.center;
            Vector2 size = (Vector2)bounds.size - Vector2.one * (CornerSkin * 2f);
            float distance = rise + CornerSkin;
            if (!IsBlockedAbove(center, size, distance))
                return;

            for (float offset = CornerStep; offset <= config.CornerCorrection + 0.001f; offset += CornerStep)
            {
                foreach (int side in s_cornerSides)
                {
                    Vector2 shifted = center + new Vector2(side * offset, 0f);
                    if (Physics2D.OverlapBox(shifted, size, 0f, config.GroundLayers) == null
                        && !IsBlockedAbove(shifted, size, distance))
                    {
                        _body.position += new Vector2(side * offset, 0f);
                        return;
                    }
                }
            }
        }

        bool IsBlockedAbove(Vector2 center, Vector2 size, float distance) =>
            Physics2D.BoxCast(center, size, 0f, Vector2.up, distance, config.GroundLayers).collider != null;

        float GetHorizontalRate()
        {
            bool accelerating = _moveInput != 0f;
            if (IsGrounded)
                return accelerating ? config.GroundAcceleration : config.GroundDeceleration;
            return accelerating ? config.AirAcceleration : config.AirDeceleration;
        }

        bool ProbeGround()
        {
            GetGroundProbe(out Vector2 center, out Vector2 size);
            return Physics2D.OverlapBox(center, size, 0f, config.GroundLayers) != null;
        }

        void GetGroundProbe(out Vector2 center, out Vector2 size)
        {
            Bounds bounds = _collider.bounds;
            center = new Vector2(bounds.center.x, bounds.min.y);
            size = new Vector2(bounds.size.x * config.GroundProbeWidth, config.GroundProbeDepth);
        }

        void OnJumpPressed()
        {
            if (HasControl)
                _jumpBufferTimer = config.JumpBufferTime;
        }

        void OnJumpReleased()
        {
            if (_isJumping)
                _isJumpCut = true;
        }

        void SetFacing(int direction)
        {
            if (direction == FacingDirection)
                return;

            FacingDirection = direction;
            if (visualRoot != null)
            {
                Vector3 scale = visualRoot.localScale;
                scale.x = Mathf.Abs(scale.x) * direction;
                visualRoot.localScale = scale;
            }
        }

        void ResetMotionState()
        {
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            _isJumping = false;
            _isJumpCut = false;
            _isSkidding = false;
            _peakFallSpeed = 0f;
            IsGrounded = false;
        }

        void OnDrawGizmosSelected()
        {
            if (config == null || _collider == null)
                return;

            GetGroundProbe(out Vector2 center, out Vector2 size);
            Gizmos.color = IsGrounded ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(center, size);
        }
    }
}
