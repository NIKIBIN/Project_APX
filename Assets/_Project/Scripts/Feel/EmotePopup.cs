using DG.Tweening;
using UnityEngine;

namespace APX.Feel
{
    /// <summary>Reaction symbols that pop up over a character's head ("?", "!"...).</summary>
    public enum Emote
    {
        Question,
        Exclamation,
    }

    /// <summary>
    /// Pops an <see cref="Emote"/> sprite in and out above its parent (e.g. the hint companion's head).
    /// Hidden until shown.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class EmotePopup : MonoBehaviour
    {
        [SerializeField] Sprite question;
        [SerializeField] Sprite exclamation;
        [SerializeField, Min(0.01f)] float popInDuration = 0.25f;
        [SerializeField, Min(0.01f)] float popOutDuration = 0.15f;
        [Tooltip("Degrees of the little wobble when it pops in.")]
        [SerializeField, Min(0f)] float wobble = 12f;

        SpriteRenderer _renderer;
        Vector3 _restScale;
        Sequence _sequence;

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _restScale = transform.localScale;
            _renderer.enabled = false;
        }

        void OnDisable()
        {
            _sequence?.Kill();
            _renderer.enabled = false;
        }

        // Stays readable when the character it sits on turns around (mirrored through a negative scale).
        void LateUpdate() => _renderer.flipX = transform.lossyScale.x < 0f;

        public void Show(Emote emote)
        {
            _sequence?.Kill();
            _renderer.sprite = emote == Emote.Question ? question : exclamation;
            _renderer.enabled = true;
            transform.localScale = Vector3.zero;
            transform.localRotation = Quaternion.Euler(0f, 0f, wobble);
            _sequence = DOTween.Sequence()
                .Join(transform.DOScale(_restScale, popInDuration).SetEase(Ease.OutBack))
                .Join(transform.DOLocalRotate(Vector3.zero, popInDuration * 1.6f).SetEase(Ease.OutElastic))
                .SetLink(gameObject);
        }

        public void Hide()
        {
            // Never shown (its object may not even have woken up yet).
            if (_renderer == null || !_renderer.enabled)
                return;

            _sequence?.Kill();
            _sequence = DOTween.Sequence()
                .Join(transform.DOScale(Vector3.zero, popOutDuration).SetEase(Ease.InBack))
                .OnComplete(() => _renderer.enabled = false)
                .SetLink(gameObject);
        }
    }
}
