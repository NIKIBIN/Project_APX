using APX.Core;
using APX.Player;
using APX.Rooms;
using UnityEngine;

namespace APX.Enemies
{
    /// <summary>
    /// "Segundo canal" danger sense for a hidden enemy (or any hidden threat with a collider): within reach of the
    /// player it counts as a danger, and the time it would take to touch the player at the current closing speed
    /// drives the orb from "in danger" up to "about to strike".
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class ProximityDangerSource : MonoBehaviour, IDangerSource
    {
        [Tooltip("Gap to the player, in world units, under which the player senses this threat.")]
        [SerializeField, Min(0f)] float reach = 3f;
        [Tooltip("Seconds over which the closing speed is smoothed, so the orb doesn't flicker.")]
        [SerializeField, Min(0.01f)] float smoothing = 0.1f;
        [Tooltip("Closing speeds below this (world units per second) count as not approaching.")]
        [SerializeField, Min(0f)] float minClosingSpeed = 0.1f;

        Collider2D _collider;
        Collider2D _playerCollider;
        PlayerLife _player;
        EnemyBase _enemy;
        Room _room;
        float _gap;
        float _closingSpeed;
        bool _isTracking;

        bool IsActive =>
            _player != null && !_player.IsDead && _playerCollider != null && _collider.enabled
            && (_enemy == null || !_enemy.IsDead) && (_room == null || _room.IsCurrent);

        void Awake()
        {
            _collider = GetComponent<Collider2D>();
            _enemy = GetComponentInParent<EnemyBase>();
            _room = GetComponentInParent<Room>(true);
            _player = FindAnyObjectByType<PlayerLife>();
            if (_player != null)
                _playerCollider = _player.GetComponent<Collider2D>();
        }

        void OnEnable() => DangerSources.Register(this);

        void OnDisable()
        {
            DangerSources.Unregister(this);
            _isTracking = false;
        }

        void Update()
        {
            if (!IsActive)
            {
                _isTracking = false;
                return;
            }

            float gap = Mathf.Max(0f, _collider.Distance(_playerCollider).distance);
            // Real time, so the estimate stays in real seconds through the radial menu's slow motion.
            float deltaTime = Time.unscaledDeltaTime;
            if (_isTracking && deltaTime > 0f)
            {
                float speed = (_gap - gap) / deltaTime;
                _closingSpeed = Mathf.Lerp(_closingSpeed, speed, 1f - Mathf.Exp(-deltaTime / smoothing));
            }
            else
            {
                _closingSpeed = 0f;
            }

            _gap = gap;
            _isTracking = true;
        }

        public bool TryGetThreat(out float secondsUntilStrike)
        {
            secondsUntilStrike = float.PositiveInfinity;
            if (!_isTracking || !IsActive || _gap > reach)
                return false;

            if (_gap <= 0f)
                secondsUntilStrike = 0f;
            else if (_closingSpeed > minClosingSpeed)
                secondsUntilStrike = _gap / _closingSpeed;
            return true;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.54f, 0.81f, 0.94f, 0.6f);
            Collider2D own = _collider != null ? _collider : GetComponent<Collider2D>();
            if (own == null)
                return;

            Bounds bounds = own.bounds;
            Gizmos.DrawWireCube(bounds.center, bounds.size + Vector3.one * (reach * 2f));
        }
    }
}
