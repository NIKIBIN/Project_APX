using System.Collections.Generic;
using APX.Combat;
using APX.Core;
using UnityEngine;
using UnityEngine.Pool;

namespace APX.Enemies
{
    /// <summary>
    /// Stationary turret. When the player is in range (and visible) it winds up, then fires a projectile
    /// at the player's current position. States: Idle (cooldown) -> Windup (telegraph) -> Fire -> Idle.
    /// </summary>
    public sealed class ShooterEnemy : EnemyBase
    {
        [Header("Targeting")]
        [SerializeField] LayerMask targetLayers;
        [Tooltip("The default covers a whole 32x18 room; the turret only acts while its room is on screen anyway.")]
        [SerializeField, Min(0f)] float detectionRange = 40f;
        [SerializeField] bool requireLineOfSight = true;
        [SerializeField] LayerMask obstacleLayers = 1;

        [Header("Firing")]
        [SerializeField] Projectile projectilePrefab;
        [SerializeField] Transform muzzle;
        [SerializeField, Min(0f)] float projectileSpeed = 7f;
        [SerializeField, Min(0f)] float fireCooldown = 1.6f;
        [Tooltip("Telegraph time before each shot, in local time.")]
        [SerializeField, Min(0f)] float windupDuration = 0.45f;

        [Header("Feedback")]
        [SerializeField] SpriteRenderer bodyRenderer;
        [SerializeField] Color windupColor = new(1f, 0.95f, 0.4f);
        [Tooltip("Mirrored on the X axis to face the target.")]
        [SerializeField] Transform visualRoot;

        readonly List<Projectile> _liveProjectiles = new();
        ObjectPool<Projectile> _pool;
        Color _baseColor;

        public Transform Target { get; private set; }

        internal bool HasTarget => Target != null;

        internal float FireCooldown => fireCooldown;

        internal float WindupDuration => windupDuration;

        Vector2 MuzzlePosition => muzzle != null ? (Vector2)muzzle.position : (Vector2)transform.position;

        protected override void Awake()
        {
            if (bodyRenderer != null)
                _baseColor = bodyRenderer.color;

            _pool = new ObjectPool<Projectile>(
                CreateProjectile,
                projectile => projectile.gameObject.SetActive(true),
                projectile => projectile.gameObject.SetActive(false),
                projectile => { if (projectile != null) Destroy(projectile.gameObject); },
                collectionCheck: false,
                defaultCapacity: 8,
                maxSize: 32);

            base.Awake();
        }

        void OnDestroy() => _pool?.Dispose();

        protected override IState BuildStateMachine(StateMachine machine)
        {
            var idle = new ShooterIdleState(this);
            var windup = new ShooterWindupState(this);
            var fire = new ShooterFireState(this);
            machine.AddTransition(idle, windup, () => idle.IsReady && HasTarget);
            machine.AddTransition(windup, idle, () => !HasTarget);
            machine.AddTransition(windup, fire, () => windup.IsComplete);
            machine.AddTransition(fire, idle, () => true);
            return idle;
        }

        protected override void Sense()
        {
            Collider2D hit = Physics2D.OverlapCircle(transform.position, detectionRange, targetLayers);
            Target = hit != null && HasLineOfSight(hit.transform.position) ? hit.transform : null;

            if (Target != null)
                Face(Target.position.x >= transform.position.x ? 1 : -1);
        }

        protected override void OnReset()
        {
            SetTelegraph(0f);
            // Despawn releases each projectile, which removes it from the list.
            for (int i = _liveProjectiles.Count - 1; i >= 0; i--)
                _liveProjectiles[i].Despawn();
        }

        internal void Fire()
        {
            if (Target == null || projectilePrefab == null)
                return;

            Vector2 origin = MuzzlePosition;
            Vector2 direction = ((Vector2)Target.position - origin).normalized;
            Projectile projectile = _pool.Get();
            _liveProjectiles.Add(projectile);
            projectile.Launch(origin, direction * projectileSpeed, TimeSource, ReleaseProjectile);
        }

        /// <param name="amount">0 = normal colour, 1 = fully telegraphed.</param>
        internal void SetTelegraph(float amount)
        {
            if (bodyRenderer == null)
                return;

            Color color = Color.Lerp(_baseColor, windupColor, amount);
            color.a = bodyRenderer.color.a; // Alpha belongs to mask visibility.
            bodyRenderer.color = color;
        }

        bool HasLineOfSight(Vector2 targetPosition) =>
            !requireLineOfSight || !Physics2D.Linecast(MuzzlePosition, targetPosition, obstacleLayers);

        Projectile CreateProjectile()
        {
            // Parented to the room (not the shooter) so shots keep flying if the shooter dies.
            Projectile projectile = Instantiate(projectilePrefab, transform.parent);
            projectile.gameObject.SetActive(false);
            return projectile;
        }

        void ReleaseProjectile(Projectile projectile)
        {
            _liveProjectiles.Remove(projectile);
            _pool.Release(projectile);
        }

        void Face(int direction)
        {
            if (visualRoot == null)
                return;

            Vector3 scale = visualRoot.localScale;
            scale.x = Mathf.Abs(scale.x) * direction;
            visualRoot.localScale = scale;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }
    }
}
