using UnityEngine;

namespace APX.Combat
{
    /// <summary>
    /// Damages any <see cref="IDamageable"/> of the target team touching this object's colliders.
    /// Drop it on enemies, spikes, saws or a whole hazard tilemap.
    /// </summary>
    public sealed class DamageOnContact : MonoBehaviour
    {
        [SerializeField] Team sourceTeam = Team.Environment;
        [SerializeField] Team targetTeam = Team.Player;

        Collider2D _ownCollider;

        void Awake()
        {
            // Tilemaps merge their tiles into a composite; that is the shape to measure hits against.
            _ownCollider = TryGetComponent(out CompositeCollider2D composite) ? composite : GetComponent<Collider2D>();
        }

        void OnTriggerEnter2D(Collider2D other) => TryDamage(other);

        void OnTriggerStay2D(Collider2D other) => TryDamage(other);

        void OnCollisionEnter2D(Collision2D collision) => TryDamage(collision.collider);

        void OnCollisionStay2D(Collision2D collision) => TryDamage(collision.collider);

        void TryDamage(Collider2D other)
        {
            // Physics messages are also delivered to disabled behaviours.
            if (!isActiveAndEnabled)
                return;

            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target == null || target.Team != targetTeam)
                return;

            target.TryReceiveDamage(new DamageInfo(sourceTeam, gameObject, GetHitPoint(other)));
        }

        /// <summary>Point of this hazard nearest to the target, so receivers can tell where the hit came from.</summary>
        Vector2 GetHitPoint(Collider2D other)
        {
            Vector2 targetCenter = other.bounds.center;
            return _ownCollider != null ? _ownCollider.ClosestPoint(targetCenter) : (Vector2)transform.position;
        }
    }
}
