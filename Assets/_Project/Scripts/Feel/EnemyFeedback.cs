using APX.Combat;
using APX.Enemies;
using DG.Tweening;
using UnityEngine;

namespace APX.Feel
{
    /// <summary>
    /// Hit and death feedback for an enemy: a white flash and a knock away from the hit, then a quick
    /// pop-and-shrink when it dies. Purely visual; the enemy's behaviour is untouched.
    /// </summary>
    [RequireComponent(typeof(EnemyBase))]
    public sealed class EnemyFeedback : MonoBehaviour
    {
        [Tooltip("Defaults to a SpriteFlash on this object.")]
        [SerializeField] SpriteFlash flash;
        [Tooltip("Knocked back on hits. Defaults to a child named \"Visual\".")]
        [SerializeField] Transform visual;

        [Header("Hit")]
        [SerializeField] Color flashColor = Color.white;
        [SerializeField, Min(0f)] float flashDuration = 0.12f;
        [Tooltip("How far the visual is knocked away from the hit, in world units.")]
        [SerializeField, Min(0f)] float knockDistance = 0.2f;
        [SerializeField, Min(0f)] float knockDuration = 0.18f;

        [Header("Death")]
        [Tooltip("Brief swell before shrinking away (1 = none).")]
        [SerializeField, Min(1f)] float deathSwell = 1.25f;
        [Tooltip("Share of the death time spent swelling; the rest shrinks to nothing.")]
        [SerializeField, Range(0.05f, 0.9f)] float swellPortion = 0.3f;

        EnemyBase _enemy;
        Vector3 _baseScale;
        Vector3 _visualBasePosition;
        Sequence _deathTween;
        Tween _knockTween;

        void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
            if (flash == null)
                flash = GetComponent<SpriteFlash>();
            if (visual == null)
                visual = transform.Find("Visual");

            _baseScale = transform.localScale;
            if (visual != null)
                _visualBasePosition = visual.localPosition;
        }

        void OnEnable()
        {
            _enemy.Damaged += OnDamaged;
            _enemy.Died += OnDied;
            _enemy.Revived += OnRevived;
        }

        void OnDisable()
        {
            _enemy.Damaged -= OnDamaged;
            _enemy.Died -= OnDied;
            _enemy.Revived -= OnRevived;
        }

        void OnDamaged(DamageInfo damage)
        {
            if (flash != null)
                flash.Flash(flashColor, flashDuration);

            if (visual == null || knockDistance <= 0f)
                return;

            float away = Mathf.Sign(transform.position.x - damage.Point.x);
            _knockTween?.Kill();
            visual.localPosition = _visualBasePosition;
            _knockTween = visual.DOPunchPosition(new Vector3(away * knockDistance, 0f, 0f), knockDuration, vibrato: 6, elasticity: 0.5f)
                .SetLink(gameObject);
        }

        void OnDied()
        {
            _deathTween?.Kill();
            float duration = Mathf.Max(0.01f, _enemy.DeathDuration);
            _deathTween = DOTween.Sequence()
                .Append(transform.DOScale(_baseScale * deathSwell, duration * swellPortion).SetEase(Ease.OutQuad))
                .Append(transform.DOScale(Vector3.zero, duration * (1f - swellPortion)).SetEase(Ease.InBack))
                .SetLink(gameObject);
        }

        void OnRevived()
        {
            _deathTween?.Kill();
            _knockTween?.Kill();
            transform.localScale = _baseScale;
            if (visual != null)
                visual.localPosition = _visualBasePosition;
            if (flash != null)
                flash.Clear();
        }
    }
}
