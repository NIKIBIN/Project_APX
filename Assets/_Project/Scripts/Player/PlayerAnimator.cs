using UnityEngine;

namespace APX.Player
{
    /// <summary>
    /// Feeds the player's movement state to the Animator on the sprite, and plays the attack, damaged and
    /// death states when <see cref="PlayerAttack"/> and <see cref="PlayerLife"/> say so. Facing is not handled here:
    /// <see cref="PlayerController"/> already mirrors the visual root.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerAnimator : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int IsGroundedId = Animator.StringToHash("IsGrounded");
        static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
        static readonly int RunSpeedId = Animator.StringToHash("RunSpeed");
        static readonly int IdleState = Animator.StringToHash("Idle");
        static readonly int DamagedState = Animator.StringToHash("Damaged");
        static readonly int DeathState = Animator.StringToHash("Death");
        static readonly int AttackState = Animator.StringToHash("Attack");

        [Tooltip("Defaults to the PlayerController on a parent.")]
        [SerializeField] PlayerController controller;
        [Tooltip("Defaults to the PlayerLife on a parent.")]
        [SerializeField] PlayerLife life;
        [Tooltip("Defaults to the PlayerAttack on a parent.")]
        [SerializeField] PlayerAttack attack;
        [Tooltip("Run animation playback speed range, scaled by how fast the player moves (stops the feet sliding while accelerating).")]
        [SerializeField] Vector2 runAnimationSpeed = new(0.5f, 1f);

        Animator _animator;

        void Awake()
        {
            _animator = GetComponent<Animator>();
            if (controller == null)
                controller = GetComponentInParent<PlayerController>();
            if (life == null)
                life = GetComponentInParent<PlayerLife>();
            if (attack == null)
                attack = GetComponentInParent<PlayerAttack>();
        }

        void OnEnable()
        {
            if (attack != null)
                attack.Swung += OnSwung;

            if (life == null)
                return;

            life.Died += OnDied;
            life.DeathPoseStarted += OnDeathPoseStarted;
            life.Respawned += OnRespawned;
        }

        void OnDisable()
        {
            if (attack != null)
                attack.Swung -= OnSwung;

            if (life == null)
                return;

            life.Died -= OnDied;
            life.DeathPoseStarted -= OnDeathPoseStarted;
            life.Respawned -= OnRespawned;
        }

        void Update()
        {
            _animator.SetFloat(SpeedId, Mathf.Abs(controller.Velocity.x));
            _animator.SetBool(IsGroundedId, controller.IsGrounded);
            _animator.SetFloat(VerticalSpeedId, controller.Velocity.y);

            float speedRatio = Mathf.Abs(controller.Velocity.x) / Mathf.Max(0.01f, controller.MaxRunSpeed);
            _animator.SetFloat(RunSpeedId, Mathf.Lerp(runAnimationSpeed.x, runAnimationSpeed.y, speedRatio));
        }

        // Attack hands back to Idle when it ends; Idle then moves on to Run, Jump or Fall as needed.
        void OnSwung() => _animator.Play(AttackState, 0, 0f);

        // Damaged and Death have no outgoing transitions, so they hold until respawn.
        void OnDied() => _animator.Play(DamagedState, 0, 0f);

        void OnDeathPoseStarted() => _animator.Play(DeathState, 0, 0f);

        void OnRespawned() => _animator.Play(IdleState, 0, 0f);
    }
}
