using System;
using APX.Core;
using UnityEngine;

namespace APX.Combat
{
    /// <summary>
    /// Straight-flying projectile moving on local (mask-scaled) time. Poolable: the launcher passes
    /// a release callback that is used instead of Destroy.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Projectile : MonoBehaviour, IDamageable
    {
        [SerializeField] Team team = Team.Enemy;
        [SerializeField] Team targetTeam = Team.Player;
        [SerializeField, Min(0.01f)] float lifetime = 6f;
        [Tooltip("Radius used to detect walls (obstacle layers).")]
        [SerializeField, Min(0.01f)] float obstacleCheckRadius = 0.15f;
        [SerializeField] LayerMask obstacleLayers = 1;
        [Tooltip("Lets the player's melee attack destroy this projectile.")]
        [SerializeField] bool destroyableByAttacks = true;

        Rigidbody2D _body;
        ITimeScaleSource _ownTimeSource;
        ITimeScaleSource _timeSource;
        Action<Projectile> _release;
        Vector2 _velocity;
        float _age;

        public Team Team => team;

        public bool InFlight { get; private set; }

        void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Kinematic;
            _ownTimeSource = GetComponent<ITimeScaleSource>();
        }

        /// <param name="timeSource">Time scale to follow when the projectile has no source of its own (usually the shooter's).</param>
        /// <param name="release">Called instead of Destroy when the projectile ends; null destroys it.</param>
        public void Launch(Vector2 position, Vector2 velocity, ITimeScaleSource timeSource, Action<Projectile> release)
        {
            transform.SetPositionAndRotation(position, Quaternion.FromToRotation(Vector3.right, velocity));
            _body.position = position;
            _velocity = velocity;
            _timeSource = _ownTimeSource ?? timeSource;
            _release = release;
            _age = 0f;
            InFlight = true;
        }

        void FixedUpdate()
        {
            if (!InFlight)
                return;

            float scale = _timeSource.ScaleOrDefault();
            _body.linearVelocity = _velocity * scale;
            _age += Time.fixedDeltaTime * scale;

            if (_age >= lifetime || Physics2D.OverlapCircle(_body.position, obstacleCheckRadius, obstacleLayers))
                Despawn();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!InFlight)
                return;

            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target == null || target.Team != targetTeam)
                return;

            if (target.TryReceiveDamage(new DamageInfo(team, gameObject, transform.position)))
                Despawn();
        }

        public bool TryReceiveDamage(in DamageInfo damage)
        {
            if (!destroyableByAttacks || !InFlight || damage.SourceTeam == team)
                return false;

            Despawn();
            return true;
        }

        public void Despawn()
        {
            if (!InFlight)
                return;

            InFlight = false;
            _body.linearVelocity = Vector2.zero;

            if (_release != null)
                _release(this);
            else
                Destroy(gameObject);
        }
    }
}
