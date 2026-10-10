using System.Collections.Generic;
using APX.Core;
using APX.Player;
using APX.Rooms;
using DG.Tweening;
using UnityEngine;

namespace APX.Level
{
    /// <summary>
    /// Shared rhythm for the <see cref="SpikeTrap"/>s of one room, so every trap on screen strikes at the same time.
    /// Calm until the player steps on any of them; the first strike then waits at least the first strike delay, and
    /// each later one a random pause (spikes down) between the configured minimum and maximum. After a while with
    /// nobody on a trap the group calms down again, so the next step gets the first strike delay again.
    /// Times are local: the traps' mask time scale (the accelerated-world curse) speeds the rhythm up.
    /// Also a danger source for the "Segundo canal" danger sense. Traps find the group in their parents and add
    /// one to their room when there is none.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpikeTrapGroup : MonoBehaviour, IDangerSource, IResettable
    {
        enum Phase
        {
            Calm,
            Waiting,
            Popping,
            Up,
            Retracting,
        }

        [Header("Rhythm")]
        [Tooltip("Minimum seconds between first stepping on a trap and the first strike.")]
        [SerializeField, Min(0f)] float firstStrikeDelay = 2f;
        [Tooltip("Seconds the spikes stay down between strikes: random between X (minimum) and Y (maximum).")]
        [SerializeField] Vector2 pauseBetweenStrikes = new(2f, 4f);
        [Tooltip("Seconds without anyone on a trap before the group calms down and the first strike delay applies again.")]
        [SerializeField, Min(0f)] float calmDownTime = 3f;

        [Header("Strike")]
        [Tooltip("Seconds for the spikes to shoot out. Keep it tiny: it should feel sudden.")]
        [SerializeField, Min(0.01f)] float popDuration = 0.05f;
        [SerializeField, Min(0f)] float upDuration = 0.4f;
        [SerializeField, Min(0.01f)] float retractDuration = 0.25f;

        [Header("Stepping On")]
        [Tooltip("How far the player's feet may be above (or below) a trap's surface and still count as on it.")]
        [SerializeField, Min(0f)] float contactTolerance = 0.15f;
        [Tooltip("Share of the player's width (centred) that counts as their feet.")]
        [SerializeField, Range(0.1f, 1f)] float feetWidth = 0.5f;

        readonly List<SpikeTrap> _traps = new();
        Room _room;
        ITimeScaleSource _timeSource;
        PlayerController _player;
        PlayerLife _playerLife;
        Collider2D _playerCollider;
        Phase _phase;
        float _phaseTime;
        float _timeToStrike;
        float _sinceContact;

        /// <summary>How far the spikes are out: 0 = hidden under the floor, 1 = fully up (overshoots a bit when popping).</summary>
        public float Extension { get; private set; }

        /// <summary>True while the spikes sink back after a strike.</summary>
        public bool IsRetracting => _phase == Phase.Retracting;

        float TimeScale => _timeSource.ScaleOrDefault();

        void Awake()
        {
            _room = GetComponentInParent<Room>(true);
            _timeSource = GetComponentInParent<ITimeScaleSource>(true);
            _player = FindAnyObjectByType<PlayerController>();
            if (_player != null)
            {
                _playerLife = _player.GetComponent<PlayerLife>();
                _playerCollider = _player.GetComponent<Collider2D>();
            }

            ResetState();
        }

        void OnEnable() => DangerSources.Register(this);

        void OnDisable() => DangerSources.Unregister(this);

        void OnValidate()
        {
            pauseBetweenStrikes.x = Mathf.Max(0f, pauseBetweenStrikes.x);
            pauseBetweenStrikes.y = Mathf.Max(pauseBetweenStrikes.x, pauseBetweenStrikes.y);
        }

        internal void Register(SpikeTrap trap)
        {
            if (!_traps.Contains(trap))
                _traps.Add(trap);

            // Spike traps carry their own mask time responder (like other world hazards); all share one rhythm.
            _timeSource ??= trap.TimeSource;
        }

        internal void Unregister(SpikeTrap trap) => _traps.Remove(trap);

        void Update()
        {
            if (_traps.Count == 0 || (_room != null && !_room.IsCurrent))
                return;

            float deltaTime = Time.deltaTime * TimeScale;
            if (IsPlayerOnAnyTrap())
                _sinceContact = 0f;
            else
                _sinceContact += deltaTime;

            switch (_phase)
            {
                case Phase.Calm:
                    if (_sinceContact <= 0f)
                    {
                        _phase = Phase.Waiting;
                        _timeToStrike = Mathf.Max(firstStrikeDelay, RandomPause());
                    }
                    break;

                case Phase.Waiting:
                    if (_sinceContact >= calmDownTime)
                    {
                        _phase = Phase.Calm;
                        break;
                    }

                    _timeToStrike -= deltaTime;
                    if (_timeToStrike <= 0f)
                        Strike();
                    break;

                default:
                    UpdateStrike(deltaTime);
                    break;
            }
        }

        public void ResetState()
        {
            _phase = Phase.Calm;
            _phaseTime = 0f;
            _timeToStrike = 0f;
            _sinceContact = float.PositiveInfinity;
            Extension = 0f;
        }

        public bool TryGetThreat(out float secondsUntilStrike)
        {
            secondsUntilStrike = float.PositiveInfinity;
            if (_phase == Phase.Calm || _traps.Count == 0 || (_room != null && !_room.IsCurrent))
                return false;

            // Mid-strike the spikes are out in plain sight; the sense just stays on until the next countdown.
            if (_phase == Phase.Waiting)
                secondsUntilStrike = _timeToStrike / Mathf.Max(TimeScale, 0.01f);
            return true;
        }

        void Strike()
        {
            SetPhase(Phase.Popping);
            foreach (SpikeTrap trap in _traps)
                trap.OnStrike();
        }

        void UpdateStrike(float deltaTime)
        {
            _phaseTime += deltaTime;
            switch (_phase)
            {
                case Phase.Popping:
                    Extension = DOVirtual.EasedValue(0f, 1f, Mathf.Clamp01(_phaseTime / popDuration), Ease.OutBack);
                    if (_phaseTime >= popDuration)
                        SetPhase(Phase.Up);
                    break;

                case Phase.Up:
                    Extension = 1f;
                    if (_phaseTime >= upDuration)
                        SetPhase(Phase.Retracting);
                    break;

                case Phase.Retracting:
                    Extension = DOVirtual.EasedValue(1f, 0f, Mathf.Clamp01(_phaseTime / retractDuration), Ease.InQuad);
                    if (_phaseTime >= retractDuration)
                    {
                        Extension = 0f;
                        SetPhase(Phase.Waiting);
                        _timeToStrike = RandomPause();
                    }
                    break;
            }
        }

        void SetPhase(Phase phase)
        {
            _phase = phase;
            _phaseTime = 0f;
        }

        float RandomPause() => Random.Range(pauseBetweenStrikes.x, pauseBetweenStrikes.y);

        bool IsPlayerOnAnyTrap()
        {
            if (_player == null || _playerCollider == null || !_player.IsGrounded || (_playerLife != null && _playerLife.IsDead))
                return false;

            Bounds bounds = _playerCollider.bounds;
            float halfFeet = bounds.extents.x * feetWidth;
            float feetLeft = bounds.center.x - halfFeet;
            float feetRight = bounds.center.x + halfFeet;
            float feetY = bounds.min.y;
            foreach (SpikeTrap trap in _traps)
            {
                if (trap.IsUnderFeet(feetLeft, feetRight, feetY, contactTolerance))
                    return true;
            }

            return false;
        }
    }
}
