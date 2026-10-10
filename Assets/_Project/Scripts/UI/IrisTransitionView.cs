using System;
using System.Collections;
using APX.Rooms;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

namespace APX.UI
{
    /// <summary>
    /// Respawn transition: a black iris closes in on the dead player, then reopens a little around the
    /// respawned player (following them) before opening completely. Runs on unscaled time.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class IrisTransitionView : MonoBehaviour, IRespawnTransition
    {
        [Tooltip("Defaults to Camera.main.")]
        [SerializeField] Camera worldCamera;

        [Header("Close")]
        [SerializeField, Min(0f)] float closeDuration = 0.7f;
        [SerializeField] Ease closeEase = Ease.InOutQuad;
        [Tooltip("Seconds the screen stays black. The room restarts at the end of this pause.")]
        [SerializeField, Min(0f)] float blackDuration = 0.25f;

        [Header("Open")]
        [Tooltip("Radius of the first, small opening around the player, in world units.")]
        [SerializeField, Min(0f)] float peekRadius = 2.5f;
        [SerializeField, Min(0f)] float peekOpenDuration = 0.35f;
        [SerializeField] Ease peekOpenEase = Ease.OutBack;
        [Tooltip("Seconds the small opening follows the player before opening completely.")]
        [SerializeField, Min(0f)] float peekHoldDuration = 0.6f;
        [SerializeField, Min(0f)] float fullOpenDuration = 0.6f;
        [SerializeField] Ease fullOpenEase = Ease.InQuad;

        [Tooltip("Keeps the iris centre this far inside the screen edges (in panel pixels), e.g. after a fall off-screen.")]
        [SerializeField, Min(0f)] float screenMargin = 80f;

        IrisMask _iris;

        void OnEnable()
        {
            _iris = GetComponent<UIDocument>().rootVisualElement.Q<IrisMask>("iris");
            if (_iris == null)
                Debug.LogError($"{name}: the UI document has no {nameof(IrisMask)} named \"iris\".", this);
            if (worldCamera == null)
                worldCamera = Camera.main;
        }

        void OnDisable() => _iris?.SetHole(Vector2.zero, float.PositiveInfinity);

        public IEnumerator Close(Func<Vector2> worldFocus)
        {
            if (_iris == null)
                yield break;

            // Starts from wherever the iris is, in case a new death interrupted the last opening.
            float from = Mathf.Min(_iris.Radius, _iris.GetOpenRadius(ToLocal(worldFocus())));
            yield return Animate(worldFocus, closeDuration, closeEase, (t, center) => Mathf.LerpUnclamped(from, 0f, t));
            yield return Animate(worldFocus, blackDuration, Ease.Linear, (t, center) => 0f);
        }

        public IEnumerator Open(Func<Vector2> worldFocus)
        {
            if (_iris == null)
                yield break;

            yield return Animate(worldFocus, peekOpenDuration, peekOpenEase,
                (t, center) => Mathf.LerpUnclamped(0f, GetPeekRadius(worldFocus()), t));
            yield return Animate(worldFocus, peekHoldDuration, Ease.Linear,
                (t, center) => GetPeekRadius(worldFocus()));
            yield return Animate(worldFocus, fullOpenDuration, fullOpenEase,
                (t, center) => Mathf.LerpUnclamped(GetPeekRadius(worldFocus()), _iris.GetOpenRadius(center), t));

            _iris.SetHole(Vector2.zero, float.PositiveInfinity);
        }

        public void Cover() => _iris?.SetHole(_iris.contentRect.center, 0f);

        /// <summary>Re-centres on the focus every frame and sets the radius from the eased progress.</summary>
        IEnumerator Animate(Func<Vector2> worldFocus, float duration, Ease ease, Func<float, Vector2, float> radius)
        {
            for (float elapsed = 0f; ; elapsed += Time.unscaledDeltaTime)
            {
                float progress = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                Vector2 center = ToLocal(worldFocus());
                _iris.SetHole(center, radius(DOVirtual.EasedValue(0f, 1f, progress, ease), center));
                if (progress >= 1f)
                    yield break;

                yield return null;
            }
        }

        float GetPeekRadius(Vector2 worldFocus)
        {
            Vector2 center = ToLocal(worldFocus, clampToScreen: false);
            Vector2 edge = ToLocal(worldFocus + Vector2.right * peekRadius, clampToScreen: false);
            return Vector2.Distance(center, edge);
        }

        Vector2 ToLocal(Vector2 worldPosition, bool clampToScreen = true)
        {
            if (worldCamera == null || _iris.panel == null)
                return _iris.contentRect.center;

            Vector2 panelPosition = RuntimePanelUtils.CameraTransformWorldToPanel(_iris.panel, worldPosition, worldCamera);
            Vector2 local = _iris.WorldToLocal(panelPosition);
            if (!clampToScreen)
                return local;

            Rect rect = _iris.contentRect;
            float marginX = Mathf.Min(screenMargin, rect.width * 0.5f);
            float marginY = Mathf.Min(screenMargin, rect.height * 0.5f);
            return new Vector2(
                Mathf.Clamp(local.x, rect.xMin + marginX, rect.xMax - marginX),
                Mathf.Clamp(local.y, rect.yMin + marginY, rect.yMax - marginY));
        }
    }
}
