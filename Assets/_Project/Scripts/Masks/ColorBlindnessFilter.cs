using APX.Core;
using DG.Tweening;
using UnityEngine;

namespace APX.Masks
{
    /// <summary>
    /// Drives the full-screen red-green colour blindness filter (the "APX/Color Blindness" shader in the 2D
    /// renderer's Full Screen Pass feature). The world is seen colour blind until the player wears the mask that
    /// clears it ("Differentiate"); the change follows <see cref="MaskChangedEvent"/>, so it lands at the end of
    /// the mask animation. Screen-space UI is drawn afterwards and is not affected.
    /// </summary>
    public sealed class ColorBlindnessFilter : MonoBehaviour
    {
        public enum Deficiency
        {
            [Tooltip("Green cones missing: the most common red-green colour blindness.")]
            Deuteranopia,
            [Tooltip("Red cones missing: reds also look darker.")]
            Protanopia,
        }

        static readonly int StrengthId = Shader.PropertyToID("_APX_ColorBlindStrength");
        static readonly int ProtanId = Shader.PropertyToID("_APX_ColorBlindProtan");

        [SerializeField] Deficiency deficiency = Deficiency.Deuteranopia;
        [Tooltip("1 = full simulation, lower values blend back towards normal colour.")]
        [SerializeField, Range(0f, 1f)] float strength = 1f;
        [Tooltip("The mask that lets the player see colours normally.")]
        [SerializeField] MaskType clearingMask = MaskType.Differentiate;
        [Tooltip("Seconds to fade the filter in or out when the mask changes.")]
        [SerializeField, Min(0f)] float fadeDuration = 0.35f;

        float _current;
        Tween _fade;

        void OnEnable()
        {
            EventBus<MaskChangedEvent>.Subscribe(OnMaskChanged);
            Shader.SetGlobalFloat(ProtanId, deficiency == Deficiency.Protanopia ? 1f : 0f);
            SetStrength(TargetFor(MaskManager.CurrentMask));
        }

        void OnDisable()
        {
            EventBus<MaskChangedEvent>.Unsubscribe(OnMaskChanged);
            _fade?.Kill();
            // Leave the editor (and anything without this component) with normal colours.
            SetStrength(0f);
        }

        void OnValidate()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
                return;

            Shader.SetGlobalFloat(ProtanId, deficiency == Deficiency.Protanopia ? 1f : 0f);
            SetStrength(TargetFor(MaskManager.CurrentMask));
        }

        void OnMaskChanged(MaskChangedEvent evt)
        {
            _fade?.Kill();
            float target = TargetFor(evt.Current);
            if (fadeDuration <= 0f)
            {
                SetStrength(target);
                return;
            }

            _fade = DOTween.To(() => _current, SetStrength, target, fadeDuration)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        float TargetFor(MaskType mask) => mask == clearingMask ? 0f : strength;

        void SetStrength(float value)
        {
            _current = value;
            Shader.SetGlobalFloat(StrengthId, value);
        }
    }
}
