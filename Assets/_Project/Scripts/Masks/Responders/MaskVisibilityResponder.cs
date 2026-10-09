using UnityEngine;
using UnityEngine.Tilemaps;

namespace APX.Masks
{
    /// <summary>
    /// Shows the renderers in this object (sprites, tilemaps...) only while the mask condition is met.
    /// Visibility only: hidden objects keep their colliders, so a hidden enemy is still dangerous.
    /// </summary>
    public sealed class MaskVisibilityResponder : MaskResponder
    {
        [Tooltip("0 = fully hidden. Above 0, sprites and tilemaps stay faintly visible as a 'ghost'.")]
        [SerializeField, Range(0f, 1f)] float hiddenAlpha;

        Renderer[] _renderers;
        float[] _visibleAlphas;

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _visibleAlphas = new float[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _visibleAlphas[i] = TryGetAlpha(_renderers[i], out float alpha) ? alpha : 1f;
        }

        protected override void Apply(bool conditionMet)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer target = _renderers[i];
                bool ghosted = hiddenAlpha > 0f && TrySetAlpha(target, conditionMet ? _visibleAlphas[i] : _visibleAlphas[i] * hiddenAlpha);
                target.enabled = conditionMet || ghosted;
            }
        }

        static bool TryGetAlpha(Renderer target, out float alpha)
        {
            switch (target)
            {
                case SpriteRenderer sprite:
                    alpha = sprite.color.a;
                    return true;
                case TilemapRenderer when target.TryGetComponent(out Tilemap tilemap):
                    alpha = tilemap.color.a;
                    return true;
                default:
                    alpha = 1f;
                    return false;
            }
        }

        static bool TrySetAlpha(Renderer target, float alpha)
        {
            switch (target)
            {
                case SpriteRenderer sprite:
                    Color spriteColor = sprite.color;
                    spriteColor.a = alpha;
                    sprite.color = spriteColor;
                    return true;
                case TilemapRenderer when target.TryGetComponent(out Tilemap tilemap):
                    Color tilemapColor = tilemap.color;
                    tilemapColor.a = alpha;
                    tilemap.color = tilemapColor;
                    return true;
                default:
                    return false;
            }
        }
    }
}
