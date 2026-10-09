using UnityEngine;

namespace APX.Player
{
    /// <summary>
    /// Tuning data for <see cref="PlayerController"/>. Jumps are authored as height + time to apex;
    /// gravity and launch velocity are derived from them.
    /// </summary>
    [CreateAssetMenu(menuName = "APX/Player Movement Config", fileName = "PlayerMovementConfig")]
    public sealed class PlayerMovementConfig : ScriptableObject
    {
        [Header("Run")]
        [SerializeField, Min(0f)] float maxRunSpeed = 8f;
        [SerializeField, Min(0f)] float groundAcceleration = 90f;
        [SerializeField, Min(0f)] float groundDeceleration = 110f;
        [SerializeField, Min(0f)] float airAcceleration = 65f;
        [SerializeField, Min(0f)] float airDeceleration = 45f;
        [SerializeField, Range(0f, 0.9f)] float inputDeadZone = 0.2f;

        [Header("Jump")]
        [Tooltip("Apex height of a full (held) jump, in world units.")]
        [SerializeField, Min(0.1f)] float jumpHeight = 3.6f;
        [Tooltip("Seconds to reach the apex of a full jump.")]
        [SerializeField, Min(0.05f)] float timeToApex = 0.38f;
        [Tooltip("Grace period to still jump after walking off a ledge.")]
        [SerializeField, Min(0f)] float coyoteTime = 0.1f;
        [Tooltip("How long a jump press is remembered before touching the ground.")]
        [SerializeField, Min(0f)] float jumpBufferTime = 0.12f;

        [Header("Gravity")]
        [Tooltip("Gravity multiplier once falling (past the apex or off a ledge).")]
        [SerializeField, Min(1f)] float fallGravityMultiplier = 1.9f;
        [Tooltip("Gravity multiplier while still rising after the jump button was released (variable jump height).")]
        [SerializeField, Min(1f)] float jumpCutGravityMultiplier = 3f;
        [SerializeField, Min(0f)] float maxFallSpeed = 22f;

        [Header("Polish")]
        [Tooltip("Vertical speed below which a held jump counts as being at its apex.")]
        [SerializeField, Min(0f)] float apexThreshold = 2.5f;
        [Tooltip("Gravity multiplier at the apex of a held jump: the player floats briefly, which makes jumps easier " +
                 "to aim. Adds a few centimetres to the jump height.")]
        [SerializeField, Range(0.1f, 1f)] float apexGravityMultiplier = 0.5f;
        [Tooltip("How far the player can be nudged sideways around a ceiling corner instead of bonking on it. 0 = off.")]
        [SerializeField, Min(0f)] float cornerCorrection = 0.25f;
        [Tooltip("Reversing direction faster than this (relative to max run speed) counts as a skid.")]
        [SerializeField, Range(0f, 1f)] float skidSpeedRatio = 0.6f;

        [Header("Ground Probe")]
        [SerializeField] LayerMask groundLayers = 1;
        [SerializeField, Min(0.01f)] float groundProbeDepth = 0.1f;
        [Tooltip("Probe width relative to the collider width (narrower avoids snagging on walls).")]
        [SerializeField, Range(0.1f, 1f)] float groundProbeWidth = 0.9f;

        public float MaxRunSpeed => maxRunSpeed;
        public float GroundAcceleration => groundAcceleration;
        public float GroundDeceleration => groundDeceleration;
        public float AirAcceleration => airAcceleration;
        public float AirDeceleration => airDeceleration;
        public float InputDeadZone => inputDeadZone;
        public float CoyoteTime => coyoteTime;
        public float JumpBufferTime => jumpBufferTime;
        public float FallGravityMultiplier => fallGravityMultiplier;
        public float JumpCutGravityMultiplier => jumpCutGravityMultiplier;
        public float MaxFallSpeed => maxFallSpeed;
        public float ApexThreshold => apexThreshold;
        public float ApexGravityMultiplier => apexGravityMultiplier;
        public float CornerCorrection => cornerCorrection;
        public float SkidSpeedRatio => skidSpeedRatio;
        public LayerMask GroundLayers => groundLayers;
        public float GroundProbeDepth => groundProbeDepth;
        public float GroundProbeWidth => groundProbeWidth;

        public float Gravity => 2f * jumpHeight / (timeToApex * timeToApex);
        public float JumpVelocity => Gravity * timeToApex;
    }
}
