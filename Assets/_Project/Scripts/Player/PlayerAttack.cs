using System;
using System.Collections.Generic;
using APX.Combat;
using APX.Core;
using UnityEngine;

namespace APX.Player
{
    /// <summary>Short-range melee hitbox in front of the player. Each swing hits every target once.</summary>
    public sealed class PlayerAttack : MonoBehaviour
    {
        [SerializeField] InputReader input;
        [SerializeField] Vector2 hitboxSize = new(1.3f, 1.1f);
        [Tooltip("Hitbox centre relative to the player when facing right (mirrored when facing left).")]
        [SerializeField] Vector2 hitboxOffset = new(0.95f, 0.1f);
        [SerializeField, Min(0.01f)] float activeDuration = 0.12f;
        [SerializeField, Min(0f)] float cooldown = 0.28f;
        [SerializeField] LayerMask hitLayers = ~0;
        [Tooltip("Optional placeholder visual shown while the hitbox is active.")]
        [SerializeField] SpriteRenderer slashVisual;

        readonly List<Collider2D> _overlaps = new();
        readonly HashSet<IDamageable> _hitThisSwing = new();
        IFacingProvider _facing;
        ContactFilter2D _filter;
        float _activeTimer;
        float _cooldownTimer;

        public bool IsAttacking => _activeTimer > 0f;

        /// <summary>A swing started (e.g. to play the attack animation).</summary>
        public event Action Swung;

        void Awake()
        {
            _facing = GetComponent<IFacingProvider>();
            _filter = new ContactFilter2D { useTriggers = true };
            _filter.SetLayerMask(hitLayers);
            SetVisualActive(false);
        }

        void OnEnable()
        {
            input.Enable();
            input.AttackPressed += OnAttackPressed;
        }

        void OnDisable()
        {
            input.AttackPressed -= OnAttackPressed;
            _activeTimer = 0f;
            SetVisualActive(false);
        }

        void Update()
        {
            _cooldownTimer -= Time.deltaTime;
            if (!IsAttacking)
                return;

            Vector2 center = GetHitboxCenter();
            if (slashVisual != null)
                slashVisual.transform.position = center;

            ApplyHits(center);

            _activeTimer -= Time.deltaTime;
            if (!IsAttacking)
                SetVisualActive(false);
        }

        void OnAttackPressed()
        {
            if (_cooldownTimer > 0f)
                return;

            _activeTimer = activeDuration;
            _cooldownTimer = cooldown;
            _hitThisSwing.Clear();
            SetVisualActive(true);
            Swung?.Invoke();
        }

        void ApplyHits(Vector2 center)
        {
            int count = Physics2D.OverlapBox(center, hitboxSize, 0f, _filter, _overlaps);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _overlaps[i];
                IDamageable target = hit.GetComponentInParent<IDamageable>();
                if (target == null || target.Team == Team.Player || !_hitThisSwing.Add(target))
                    continue;

                Vector2 point = hit.ClosestPoint(center);
                if (target.TryReceiveDamage(new DamageInfo(Team.Player, gameObject, point)))
                    EventBus<AttackHitEvent>.Raise(new AttackHitEvent(point, target));
            }
        }

        Vector2 GetHitboxCenter()
        {
            int facing = _facing?.FacingDirection ?? 1;
            return (Vector2)transform.position + new Vector2(hitboxOffset.x * facing, hitboxOffset.y);
        }

        void SetVisualActive(bool active)
        {
            if (slashVisual != null)
                slashVisual.enabled = active;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.8f);
            Vector2 center = Application.isPlaying ? GetHitboxCenter() : (Vector2)transform.position + hitboxOffset;
            Gizmos.DrawWireCube(center, hitboxSize);
        }
    }
}
