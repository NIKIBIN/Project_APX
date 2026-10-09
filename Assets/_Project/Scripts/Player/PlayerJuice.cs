using APX.Feel;
using DG.Tweening;
using UnityEngine;

namespace APX.Player
{
    /// <summary>
    /// Purely visual feedback on the player sprite: squash and stretch on jumps and landings, a small
    /// forward lunge on attacks and a flash on death. Lives on the sprite (not the facing root, whose
    /// X scale is flipped), and scales around the sprite's pivot at the feet.
    /// </summary>
    public sealed class PlayerJuice : MonoBehaviour
    {
        [Tooltip("Default to components on a parent.")]
        [SerializeField] PlayerController controller;
        [SerializeField] PlayerAttack attack;
        [SerializeField] PlayerLife life;
        [Tooltip("Defaults to a SpriteFlash on this object.")]
        [SerializeField] SpriteFlash flash;

        [Header("Squash & Stretch")]
        [SerializeField] Vector2 jumpStretch = new(0.82f, 1.22f);
        [Tooltip("Squash for the hardest landing; softer landings squash proportionally less.")]
        [SerializeField] Vector2 landSquash = new(1.3f, 0.72f);
        [Tooltip("Landings slower than this don't squash.")]
        [SerializeField, Min(0f)] float minLandingSpeed = 4f;
        [Tooltip("Landings at this speed or faster get the full squash.")]
        [SerializeField, Min(0.1f)] float fullSquashSpeed = 20f;
        [SerializeField, Min(0.01f)] float squashDuration = 0.22f;

        [Header("Attack")]
        [Tooltip("How far the sprite lunges forward on a swing, in world units.")]
        [SerializeField, Min(0f)] float attackLunge = 0.15f;
        [SerializeField, Min(0.01f)] float lungeDuration = 0.14f;
        [SerializeField] Vector2 attackSquash = new(1.1f, 0.93f);

        [Header("Death")]
        [SerializeField] Color deathFlashColor = Color.white;
        [SerializeField, Min(0f)] float deathFlashDuration = 0.35f;

        Vector3 _baseScale;
        Vector3 _basePosition;
        Sequence _squashTween;
        Tween _lungeTween;

        void Awake()
        {
            if (controller == null)
                controller = GetComponentInParent<PlayerController>();
            if (attack == null)
                attack = GetComponentInParent<PlayerAttack>();
            if (life == null)
                life = GetComponentInParent<PlayerLife>();
            if (flash == null)
                flash = GetComponent<SpriteFlash>();

            _baseScale = transform.localScale;
            _basePosition = transform.localPosition;
        }

        void OnEnable()
        {
            controller.Jumped += OnJumped;
            controller.Landed += OnLanded;
            if (attack != null)
                attack.Swung += OnSwung;
            if (life != null)
            {
                life.Died += OnDied;
                life.Respawned += ResetVisuals;
            }
        }

        void OnDisable()
        {
            controller.Jumped -= OnJumped;
            controller.Landed -= OnLanded;
            if (attack != null)
                attack.Swung -= OnSwung;
            if (life != null)
            {
                life.Died -= OnDied;
                life.Respawned -= ResetVisuals;
            }

            ResetVisuals();
        }

        void OnJumped() => Squash(jumpStretch);

        void OnLanded(float impactSpeed)
        {
            float strength = Mathf.InverseLerp(minLandingSpeed, fullSquashSpeed, impactSpeed);
            if (strength > 0f)
                Squash(Vector2.Lerp(Vector2.one, landSquash, strength));
        }

        void OnSwung()
        {
            Squash(attackSquash);
            if (attackLunge <= 0f)
                return;

            // Local +X is "forward": the facing root above mirrors it.
            _lungeTween?.Kill();
            transform.localPosition = _basePosition;
            _lungeTween = transform.DOLocalMoveX(_basePosition.x + attackLunge, lungeDuration * 0.5f)
                .SetEase(Ease.OutQuad)
                .SetLoops(2, LoopType.Yoyo)
                .SetLink(gameObject);
        }

        void OnDied()
        {
            ResetVisuals();
            if (flash != null)
                flash.Flash(deathFlashColor, deathFlashDuration);
        }

        /// <summary>Snaps to <paramref name="factor"/> (relative to the rest scale) and springs back.</summary>
        void Squash(Vector2 factor)
        {
            _squashTween?.Kill();
            Vector3 target = Vector3.Scale(_baseScale, new Vector3(factor.x, factor.y, 1f));
            _squashTween = DOTween.Sequence()
                .Append(transform.DOScale(target, squashDuration * 0.25f).SetEase(Ease.OutQuad))
                .Append(transform.DOScale(_baseScale, squashDuration * 0.75f).SetEase(Ease.OutBack))
                .SetLink(gameObject);
        }

        void ResetVisuals()
        {
            _squashTween?.Kill();
            _lungeTween?.Kill();
            transform.localScale = _baseScale;
            transform.localPosition = _basePosition;
            if (flash != null)
                flash.Clear();
        }
    }
}
