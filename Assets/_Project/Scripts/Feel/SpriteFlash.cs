using DG.Tweening;
using UnityEngine;

namespace APX.Feel
{
    /// <summary>
    /// Flashes sprites a solid colour (e.g. white on hit). The renderers switch to the flash material only
    /// while a flash plays, so they keep their normal (lit) material the rest of the time. Runs on real
    /// time, so a flash still fades during a hit-stop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpriteFlash : MonoBehaviour
    {
        static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

        [Tooltip("Material using the APX/Sprite Flash shader.")]
        [SerializeField] Material flashMaterial;
        [Tooltip("Renderers to flash. Empty = every SpriteRenderer on this object and its children.")]
        [SerializeField] SpriteRenderer[] renderers;

        Material[] _originalMaterials;
        MaterialPropertyBlock _block;
        Tween _tween;
        Color _color;
        float _amount;
        bool _flashing;

        void Awake()
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<SpriteRenderer>(true);

            _originalMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                _originalMaterials[i] = renderers[i].sharedMaterial;

            _block = new MaterialPropertyBlock();
        }

        void OnDisable() => Clear();

        public void Flash(Color color, float duration)
        {
            if (flashMaterial == null)
                return;

            _tween?.Kill();
            _color = color;
            SetFlashing(true);
            SetAmount(1f);
            _tween = DOTween.To(() => _amount, SetAmount, 0f, duration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .OnComplete(Clear)
                .SetLink(gameObject);
        }

        public void Clear()
        {
            _tween?.Kill();
            _tween = null;
            if (_flashing)
            {
                SetAmount(0f);
                SetFlashing(false);
            }
        }

        void SetFlashing(bool flashing)
        {
            _flashing = flashing;
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterial = flashing ? flashMaterial : _originalMaterials[i];
        }

        void SetAmount(float amount)
        {
            _amount = amount;
            foreach (SpriteRenderer spriteRenderer in renderers)
            {
                spriteRenderer.GetPropertyBlock(_block);
                _block.SetColor(FlashColorId, _color);
                _block.SetFloat(FlashAmountId, amount);
                spriteRenderer.SetPropertyBlock(_block);
            }
        }
    }
}
