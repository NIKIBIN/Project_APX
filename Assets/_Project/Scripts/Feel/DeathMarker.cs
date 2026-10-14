using System.Collections.Generic;
using APX.Core;
using APX.Player;
using APX.Rooms;
using DG.Tweening;
using UnityEngine;

namespace APX.Feel
{
    /// <summary>
    /// Small crosses where the player died in the current room, so each retry can be read against the earlier ones.
    /// Every death in the room adds a cross; each time the room opens up after a respawn they all pop up (oldest
    /// first, so the newest lands last), stay for a few seconds and fade out. Leaving the room clears them.
    /// </summary>
    public sealed class DeathMarker : MonoBehaviour
    {
        [Tooltip("Sprite for the two bars of each cross (a plain white sprite).")]
        [SerializeField] Sprite barSprite;
        [Tooltip("Unlit, so the crosses read the same in light and shadow.")]
        [SerializeField] Material material;
        [SerializeField] Color color = new(1f, 1f, 1f, 0.5f);
        [Tooltip("Length of each bar, in world units.")]
        [SerializeField, Min(0.05f)] float size = 0.4f;
        [SerializeField, Min(0.01f)] float thickness = 0.08f;
        [Tooltip("Below the player (and most of the level), above the background.")]
        [SerializeField] int sortingOrder = 1;
        [Tooltip("Past this many deaths in a room, the oldest cross is dropped.")]
        [SerializeField, Min(1)] int maxMarkers = 30;

        [Header("Pop-up")]
        [SerializeField, Min(0f)] float popDuration = 0.35f;
        [SerializeField] Ease popEase = Ease.OutBack;
        [Tooltip("Delay between one cross popping up and the next, oldest first.")]
        [SerializeField, Min(0f)] float popStagger = 0.05f;
        [Tooltip("Upper bound for the whole stagger, so many crosses still pop up quickly.")]
        [SerializeField, Min(0f)] float maxStaggerTotal = 0.5f;
        [Tooltip("How long the crosses stay after popping up, before they fade out.")]
        [SerializeField, Min(0f)] float visibleDuration = 2.5f;
        [SerializeField, Min(0f)] float fadeOutDuration = 0.5f;

        sealed class Marker
        {
            public Transform Root;
            public SpriteRenderer[] Bars;
            public Sequence Animation;
        }

        readonly List<Vector2> _deaths = new();
        readonly List<Marker> _markers = new();
        Transform _container;
        Room _deathRoom;

        void Awake() => _container = new GameObject("DeathMarkers").transform;

        void OnDestroy()
        {
            HideAll();
            if (_container != null)
                Destroy(_container.gameObject);
        }

        void OnEnable()
        {
            EventBus<PlayerDiedEvent>.Subscribe(OnPlayerDied);
            EventBus<RoomRevealedEvent>.Subscribe(OnRoomRevealed);
            EventBus<RoomChangedEvent>.Subscribe(OnRoomChanged);
        }

        void OnDisable()
        {
            EventBus<PlayerDiedEvent>.Unsubscribe(OnPlayerDied);
            EventBus<RoomRevealedEvent>.Unsubscribe(OnRoomRevealed);
            EventBus<RoomChangedEvent>.Unsubscribe(OnRoomChanged);
        }

        void OnPlayerDied(PlayerDiedEvent evt)
        {
            if (RoomManager.CurrentRoom != _deathRoom)
            {
                _deaths.Clear();
                _deathRoom = RoomManager.CurrentRoom;
            }

            _deaths.Add(evt.Position);
            if (_deaths.Count > maxMarkers)
                _deaths.RemoveAt(0);
        }

        void OnRoomRevealed(RoomRevealedEvent evt)
        {
            if (evt.Room == _deathRoom)
                PopUp();
        }

        void OnRoomChanged(RoomChangedEvent evt)
        {
            _deathRoom = null;
            _deaths.Clear();
            HideAll();
        }

        void PopUp()
        {
            HideAll();
            float stagger = _deaths.Count > 1 ? Mathf.Min(popStagger, maxStaggerTotal / (_deaths.Count - 1)) : 0f;
            for (int i = 0; i < _deaths.Count; i++)
            {
                Marker marker = GetMarker(i);
                marker.Root.position = _deaths[i];
                marker.Root.localScale = Vector3.zero;
                SetAlpha(marker, color.a);
                marker.Root.gameObject.SetActive(true);

                GameObject target = marker.Root.gameObject;
                marker.Animation = DOTween.Sequence()
                    .AppendInterval(i * stagger)
                    .Append(marker.Root.DOScale(1f, popDuration).SetEase(popEase))
                    .AppendInterval(visibleDuration)
                    .Append(DOVirtual.Float(color.a, 0f, fadeOutDuration, alpha => SetAlpha(marker, alpha)))
                    .OnComplete(() => target.SetActive(false))
                    .SetUpdate(true)
                    .SetLink(target);
            }
        }

        void HideAll()
        {
            foreach (Marker marker in _markers)
            {
                marker.Animation?.Kill();
                marker.Animation = null;
                if (marker.Root != null)
                    marker.Root.gameObject.SetActive(false);
            }
        }

        Marker GetMarker(int index)
        {
            while (_markers.Count <= index)
                _markers.Add(CreateMarker());
            return _markers[index];
        }

        Marker CreateMarker()
        {
            var root = new GameObject("DeathMarker").transform;
            root.SetParent(_container, false);
            root.gameObject.SetActive(false);
            return new Marker { Root = root, Bars = new[] { CreateBar(root, 45f), CreateBar(root, -45f) } };
        }

        void SetAlpha(Marker marker, float alpha)
        {
            Color tint = color;
            tint.a = alpha;
            foreach (SpriteRenderer bar in marker.Bars)
                bar.color = tint;
        }

        SpriteRenderer CreateBar(Transform parent, float angle)
        {
            var bar = new GameObject("Bar").AddComponent<SpriteRenderer>();
            bar.transform.SetParent(parent, false);
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            bar.sprite = barSprite;
            if (material != null)
                bar.sharedMaterial = material;
            bar.sortingOrder = sortingOrder;

            Vector2 spriteSize = barSprite != null ? (Vector2)barSprite.bounds.size : Vector2.one;
            bar.transform.localScale = new Vector3(size / spriteSize.x, thickness / spriteSize.y, 1f);
            return bar;
        }
    }
}
