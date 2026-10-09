using System.Collections;
using APX.Core;
using APX.Hints;
using APX.Masks;
using DG.Tweening;
using UnityEngine;

namespace APX.Player
{
    /// <summary>
    /// The mask on the player's face and its transitions (played by <see cref="MaskManager"/> before a change
    /// takes effect):
    /// <list type="bullet">
    /// <item>No mask to a mask: the hint companion spirals into the face, bursts into magic and becomes the mask.</item>
    /// <item>Mask to mask: the worn mask glows brighter until it bursts and becomes the new one.</item>
    /// <item>Mask to no mask: the mask glows briefly and bursts away.</item>
    /// </list>
    /// Lives on a child of the player sprite placed over the face, so it follows facing and squash.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerMaskPresenter : MonoBehaviour, IMaskTransition
    {
        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

        [Tooltip("Defaults to the MaskManager in the scene (for each mask's colour).")]
        [SerializeField] MaskManager maskManager;
        [Tooltip("Defaults to the HintCompanion in the scene.")]
        [SerializeField] HintCompanion companion;

        [Header("Companion Spiral (no mask to mask)")]
        [SerializeField, Min(0.01f)] float spiralDuration = 0.45f;
        [SerializeField, Min(0f)] float spiralTurns = 1.25f;

        [Header("Charge (mask to mask, mask to none)")]
        [Tooltip("Seconds the worn mask glows brighter before bursting.")]
        [SerializeField, Min(0.01f)] float chargeDuration = 0.3f;
        [Tooltip("Shorter charge when the mask just comes off.")]
        [SerializeField, Min(0.01f)] float removeChargeDuration = 0.15f;
        [SerializeField, Min(1f)] float chargeScale = 1.3f;
        [SerializeField, Min(0f)] float chargeShake = 0.03f;

        [Header("Appear / Vanish")]
        [SerializeField, Min(0.01f)] float appearDuration = 0.18f;
        [SerializeField, Min(0.01f)] float vanishDuration = 0.1f;

        SpriteRenderer _renderer;
        MaterialPropertyBlock _block;
        Vector3 _restScale;
        Vector3 _restPosition;
        Sequence _sequence;
        bool _isPlaying;
        bool _isPlayerDead;

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _block = new MaterialPropertyBlock();
            _restScale = transform.localScale;
            _restPosition = transform.localPosition;
            if (maskManager == null)
                maskManager = FindAnyObjectByType<MaskManager>();
            if (companion == null)
                companion = FindAnyObjectByType<HintCompanion>();
        }

        void OnEnable()
        {
            EventBus<MaskChangedEvent>.Subscribe(OnMaskChanged);
            EventBus<PlayerDiedEvent>.Subscribe(OnPlayerDied);
            EventBus<PlayerRespawnedEvent>.Subscribe(OnPlayerRespawned);
        }

        void OnDisable()
        {
            EventBus<MaskChangedEvent>.Unsubscribe(OnMaskChanged);
            EventBus<PlayerDiedEvent>.Unsubscribe(OnPlayerDied);
            EventBus<PlayerRespawnedEvent>.Unsubscribe(OnPlayerRespawned);
            _sequence?.Kill();
        }

        void Start() => ShowMask(MaskManager.CurrentMask);

        public IEnumerator Play(MaskType from, MaskType to)
        {
            _isPlaying = true;
            ResetPose();

            if (from == MaskType.None)
            {
                SetVisible(false);
                Tween spiral = companion != null ? companion.SpiralInto(() => transform.position, spiralDuration, spiralTurns) : null;
                if (spiral != null)
                    yield return spiral.WaitForCompletion();

                Burst(ColorOf(to));
                yield return Appear(to, fromFlash: 0f);
            }
            else
            {
                SetMask(from);
                yield return Charge(to == MaskType.None ? removeChargeDuration : chargeDuration);
                Burst(ColorOf(to == MaskType.None ? from : to));

                if (to == MaskType.None)
                    yield return Vanish();
                else
                    yield return Appear(to, fromFlash: 1f);
            }

            _isPlaying = false;
        }

        /// <summary>Interrupted transition: snap to <paramref name="mask"/>.</summary>
        public void Show(MaskType mask)
        {
            ShowMask(mask);

            // An interrupted spiral may have taken the companion away.
            if (companion != null)
                companion.SyncSummonState();
        }

        void ShowMask(MaskType mask)
        {
            _isPlaying = false;
            ResetPose();
            if (mask == MaskType.None)
                SetVisible(false);
            else
                SetMask(mask);
        }

        /// <summary>Glows towards white while swelling and trembling.</summary>
        IEnumerator Charge(float duration)
        {
            _sequence = DOTween.Sequence()
                .Join(DOVirtual.Float(0f, 1f, duration, SetFlash).SetEase(Ease.InQuad))
                .Join(transform.DOScale(_restScale * chargeScale, duration).SetEase(Ease.InQuad))
                .Join(transform.DOShakePosition(duration, new Vector3(chargeShake, chargeShake, 0f), vibrato: 40, randomness: 90f, snapping: false, fadeOut: false))
                .SetLink(gameObject);
            yield return _sequence.WaitForCompletion();
            transform.localPosition = _restPosition;
        }

        /// <summary>Pops the mask in from nothing, fading out any leftover glow.</summary>
        IEnumerator Appear(MaskType mask, float fromFlash)
        {
            SetMask(mask);
            SetFlash(fromFlash);
            transform.localScale = Vector3.zero;
            _sequence = DOTween.Sequence()
                .Join(transform.DOScale(_restScale, appearDuration).SetEase(Ease.OutBack))
                .Join(DOVirtual.Float(fromFlash, 0f, appearDuration, SetFlash))
                .SetLink(gameObject);
            yield return _sequence.WaitForCompletion();
        }

        IEnumerator Vanish()
        {
            _sequence = DOTween.Sequence()
                .Join(transform.DOScale(Vector3.zero, vanishDuration).SetEase(Ease.InBack))
                .SetLink(gameObject);
            yield return _sequence.WaitForCompletion();
            SetVisible(false);
            ResetPose();
        }

        void Burst(Color color) => EventBus<MaskBurstEvent>.Raise(new MaskBurstEvent(transform.position, color));

        void SetMask(MaskType mask)
        {
            _renderer.color = ColorOf(mask);
            SetVisible(!_isPlayerDead);
        }

        void SetVisible(bool visible) => _renderer.enabled = visible;

        void SetFlash(float amount)
        {
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(FlashColorId, Color.white);
            _block.SetFloat(FlashAmountId, amount);
            _renderer.SetPropertyBlock(_block);
        }

        void ResetPose()
        {
            _sequence?.Kill();
            transform.localScale = _restScale;
            transform.localPosition = _restPosition;
            SetFlash(0f);
        }

        Color ColorOf(MaskType mask) =>
            maskManager != null && maskManager.TryGetDefinition(mask, out MaskDefinition definition) ? definition.Color : Color.white;

        // Changes that skip the transition (e.g. the starting mask) still need to show up.
        void OnMaskChanged(MaskChangedEvent evt)
        {
            if (!_isPlaying)
                ShowMask(evt.Current);
        }

        // The death poses lie down, so a floating mask would look wrong; it comes back on respawn.
        void OnPlayerDied(PlayerDiedEvent evt)
        {
            _isPlayerDead = true;
            SetVisible(false);
        }

        void OnPlayerRespawned(PlayerRespawnedEvent evt)
        {
            _isPlayerDead = false;
            if (!_isPlaying)
                ShowMask(MaskManager.CurrentMask);
        }
    }
}
