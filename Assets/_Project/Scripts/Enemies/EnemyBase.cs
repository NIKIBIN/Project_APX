using System;
using System.Collections;
using APX.Combat;
using APX.Core;
using APX.Rooms;
using UnityEngine;

namespace APX.Enemies
{
    /// <summary>
    /// Shared enemy plumbing: a state machine ticking on local (mask-scaled) time, room gating,
    /// hit points, death and room-reset support. Subclasses only declare their states and transitions.
    /// </summary>
    public abstract class EnemyBase : MonoBehaviour, IDamageable, IResettable
    {
        [SerializeField, Min(1)] int hitPoints = 1;
        [Tooltip("Seconds between dying and disappearing, so death feedback (flash, shrink) can play. Contact damage stops at once.")]
        [SerializeField, Min(0f)] float deathDuration = 0.18f;

        readonly StateMachine _stateMachine = new();
        IState _initialState;
        ITimeScaleSource _timeSource;
        Room _room;
        Vector3 _spawnPosition;
        DamageOnContact[] _contactDamage;
        bool[] _contactDamageWasEnabled;
        Coroutine _deathRoutine;
        int _remainingHitPoints;
        bool _initialized;

        public Team Team => Team.Enemy;

        public bool IsDead => _remainingHitPoints <= 0;

        public float DeathDuration => deathDuration;

        /// <summary>A hit was accepted (raised before <see cref="Died"/> on the killing blow).</summary>
        public event Action<DamageInfo> Damaged;

        public event Action Died;

        /// <summary>Restored by a room reset.</summary>
        public event Action Revived;

        /// <summary>Local time multiplier (1 when there is no <see cref="ITimeScaleSource"/> on this object or a parent).</summary>
        public float TimeScale => _timeSource.ScaleOrDefault();

        protected ITimeScaleSource TimeSource => _timeSource;

        /// <summary>Enemies only act while their room is the one on screen.</summary>
        protected bool IsSimulating => _room == null || _room.IsCurrent;

        protected virtual void Awake()
        {
            _timeSource = GetComponentInParent<ITimeScaleSource>(true);
            _room = GetComponentInParent<Room>(true);
            _spawnPosition = transform.position;
            _contactDamage = GetComponentsInChildren<DamageOnContact>(true);
            _contactDamageWasEnabled = new bool[_contactDamage.Length];
            _remainingHitPoints = hitPoints;
            _initialState = BuildStateMachine(_stateMachine);
            _stateMachine.SetState(_initialState);
            _initialized = true;
        }

        protected virtual void Update()
        {
            if (IsDead)
                return;

            if (!IsSimulating)
            {
                OnSimulationSuspended();
                return;
            }

            Sense();
            _stateMachine.Tick(Time.deltaTime * TimeScale);
        }

        /// <summary>Declares states and transitions on <paramref name="machine"/>; returns the initial state.</summary>
        protected abstract IState BuildStateMachine(StateMachine machine);

        /// <summary>Per-frame perception, run before the state machine ticks (e.g. target acquisition).</summary>
        protected virtual void Sense() { }

        /// <summary>Called every frame while the enemy's room is not on screen.</summary>
        protected virtual void OnSimulationSuspended() { }

        /// <summary>Stops the enemy and its contact damage at once; it disappears after <c>deathDuration</c>.</summary>
        protected virtual void OnDied()
        {
            DisableContactDamage();
            OnSimulationSuspended();
            Died?.Invoke();
            EventBus<EnemyDiedEvent>.Raise(new EnemyDiedEvent(this, transform.position));

            if (deathDuration > 0f && isActiveAndEnabled)
                _deathRoutine = StartCoroutine(DisappearAfterDelay());
            else
                gameObject.SetActive(false);
        }

        /// <summary>Restores subclass state; the base already restored position, hit points and the initial state.</summary>
        protected virtual void OnReset() { }

        public bool TryReceiveDamage(in DamageInfo damage)
        {
            if (damage.SourceTeam == Team.Enemy || _remainingHitPoints <= 0)
                return false;

            _remainingHitPoints--;
            Damaged?.Invoke(damage);
            if (_remainingHitPoints <= 0)
                OnDied();
            return true;
        }

        public void ResetState()
        {
            // Never initialised: it was disabled in the scene on purpose, leave it alone.
            if (!_initialized)
                return;

            if (_deathRoutine != null)
            {
                StopCoroutine(_deathRoutine);
                _deathRoutine = null;
            }

            if (IsDead)
                RestoreContactDamage();

            transform.position = _spawnPosition;
            _remainingHitPoints = hitPoints;
            gameObject.SetActive(true);
            OnReset();
            _stateMachine.SetState(_initialState, force: true);
            Revived?.Invoke();
        }

        IEnumerator DisappearAfterDelay()
        {
            yield return new WaitForSeconds(deathDuration);
            _deathRoutine = null;
            gameObject.SetActive(false);
        }

        // Only the damage stops: colliders stay as they are, since mask responders may be toggling them.
        void DisableContactDamage()
        {
            for (int i = 0; i < _contactDamage.Length; i++)
            {
                _contactDamageWasEnabled[i] = _contactDamage[i].enabled;
                _contactDamage[i].enabled = false;
            }
        }

        void RestoreContactDamage()
        {
            for (int i = 0; i < _contactDamage.Length; i++)
                _contactDamage[i].enabled = _contactDamageWasEnabled[i];
        }
    }
}
