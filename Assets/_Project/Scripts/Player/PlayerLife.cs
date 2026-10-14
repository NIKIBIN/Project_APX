using System;
using APX.Combat;
using APX.Core;
using UnityEngine;

namespace APX.Player
{
    /// <summary>
    /// One-hit-kill life model. A hit takes control away and knocks the player back; once they land, the
    /// death pose plays for a while and then <see cref="IsReadyToRespawn"/> turns on. Pressing jump skips the
    /// rest (after a short moment to see what happened), and <see cref="QuickDeath"/> shortens the pose for retries.
    /// Respawning is delegated to whoever listens to <see cref="PlayerDiedEvent"/> (the RoomManager restarts the room).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerLife : MonoBehaviour, IDamageable
    {
        enum DeathPhase
        {
            Alive,
            KnockedBack,
            Down,
            OutOfLevel,
        }

        // The knockback needs a moment to leave the ground before a landing can be detected.
        const float MinKnockbackTime = 0.1f;

        [Header("Hit")]
        [Tooltip("Launch velocity away from the hit: x is horizontal speed, y is upward speed.")]
        [SerializeField] Vector2 knockback = new(4f, 7f);
        [Tooltip("Stops waiting for a landing after this many seconds (e.g. knocked into a long fall).")]
        [SerializeField, Min(MinKnockbackTime)] float maxKnockbackTime = 2f;

        [Header("Death")]
        [Tooltip("Seconds the death animation plays after landing before the room restarts.")]
        [SerializeField, Min(0f)] float deathAnimationTime = 1.2f;
        [Tooltip("Death animation seconds while QuickDeath is on (e.g. retrying the same room).")]
        [SerializeField, Min(0f)] float quickDeathAnimationTime = 0.2f;
        [Tooltip("Seconds before the room restarts after falling out of the level.")]
        [SerializeField, Min(0f)] float outOfLevelTime = 0.3f;
        [Tooltip("Real seconds after the hit before the jump button can skip the rest of the death.")]
        [SerializeField, Min(0f)] float skipDelay = 0.25f;

        PlayerController _controller;
        PlayerAttack _attack;
        Rigidbody2D _body;
        DeathPhase _phase;
        float _phaseTimer;
        float _diedAt;

        public bool IsDead => _phase != DeathPhase.Alive;

        /// <summary>Plays the short death animation, e.g. for retries of the same room. Set by the RoomManager.</summary>
        public bool QuickDeath { get; set; }

        /// <summary>True once the death has played out and the room can restart.</summary>
        public bool IsReadyToRespawn { get; private set; }

        public Team Team => Team.Player;

        /// <summary>Hit taken (or fell out of the level): control is gone.</summary>
        public event Action Died;

        /// <summary>Landed after the knockback: the death animation starts.</summary>
        public event Action DeathPoseStarted;

        public event Action Respawned;

        void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _attack = GetComponent<PlayerAttack>();
            _body = GetComponent<Rigidbody2D>();
        }

        void OnEnable()
        {
            if (_controller.Input != null)
                _controller.Input.JumpPressed += OnJumpPressed;
        }

        void OnDisable()
        {
            if (_controller.Input != null)
                _controller.Input.JumpPressed -= OnJumpPressed;
        }

        void Update()
        {
            if (!IsDead || IsReadyToRespawn)
                return;

            _phaseTimer += Time.deltaTime;
            switch (_phase)
            {
                case DeathPhase.KnockedBack:
                    bool landed = _phaseTimer >= MinKnockbackTime && _controller.IsGrounded;
                    if (landed || _phaseTimer >= maxKnockbackTime)
                        EnterPhase(DeathPhase.Down);
                    break;
                case DeathPhase.Down:
                    IsReadyToRespawn = _phaseTimer >= (QuickDeath ? quickDeathAnimationTime : deathAnimationTime);
                    break;
                case DeathPhase.OutOfLevel:
                    IsReadyToRespawn = _phaseTimer >= outOfLevelTime;
                    break;
            }
        }

        public bool TryReceiveDamage(in DamageInfo damage)
        {
            if (IsDead || damage.SourceTeam == Team.Player)
                return false;

            Kill(damage.Point, damage.Source);
            return true;
        }

        /// <summary>Kills the player, knocking them away from <paramref name="hitPoint"/>.</summary>
        /// <param name="killer">What dealt the hit, if known (e.g. so a hidden killer can be shown).</param>
        public void Kill(Vector2 hitPoint, GameObject killer = null)
        {
            if (IsDead)
                return;

            // A hit from straight above or below (spikes, a crushing door) knocks the player backwards.
            float offset = transform.position.x - hitPoint.x;
            float direction = Mathf.Abs(offset) > 0.1f ? Mathf.Sign(offset) : -_controller.FacingDirection;
            _controller.FaceTowards(transform.position.x - direction);

            BeginDeath(DeathPhase.KnockedBack, killer);
            _controller.Launch(new Vector2(direction * knockback.x, knockback.y));
        }

        /// <summary>Kills a player who fell out of the level, or ends a knockback that carried them out of it.</summary>
        public void FallOutOfLevel()
        {
            if (_phase == DeathPhase.OutOfLevel)
                return;

            if (IsDead)
                EnterPhase(DeathPhase.OutOfLevel);
            else
                BeginDeath(DeathPhase.OutOfLevel);

            // Nothing to see off-screen; stop falling forever.
            _body.simulated = false;
        }

        public void Respawn(Vector2 position)
        {
            _body.simulated = true;
            _controller.Teleport(position);
            _phase = DeathPhase.Alive;
            IsReadyToRespawn = false;
            SetControllable(true);
            Respawned?.Invoke();
            EventBus<PlayerRespawnedEvent>.Raise(new PlayerRespawnedEvent(this, position));
        }

        // A fresh press only: a jump held while dying doesn't count.
        void OnJumpPressed()
        {
            if (IsDead && !IsReadyToRespawn && Time.unscaledTime - _diedAt >= skipDelay)
                IsReadyToRespawn = true;
        }

        void BeginDeath(DeathPhase phase, GameObject killer = null)
        {
            _phase = phase;
            _phaseTimer = 0f;
            _diedAt = Time.unscaledTime;
            IsReadyToRespawn = false;
            SetControllable(false);
            Died?.Invoke();
            EventBus<PlayerDiedEvent>.Raise(new PlayerDiedEvent(this, transform.position, killer));
        }

        void EnterPhase(DeathPhase phase)
        {
            _phase = phase;
            _phaseTimer = 0f;
            if (phase == DeathPhase.Down)
                DeathPoseStarted?.Invoke();
        }

        void SetControllable(bool controllable)
        {
            _controller.SetControl(controllable);
            if (_attack != null)
                _attack.enabled = controllable;
        }
    }
}
