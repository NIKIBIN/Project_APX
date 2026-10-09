using System;
using UnityEngine;

namespace APX.Rooms
{
    /// <summary>
    /// Layered background for one room. The camera only cuts between rooms, so the parallax follows a "virtual
    /// camera" that leans a little towards the player's position in the room, mostly sideways. Deeper layers
    /// follow it more, which against the still foreground reads as depth. Real camera movement (e.g. a cutscene
    /// zoom) is deliberately ignored, so the background never slides around under a moving camera. Only the
    /// current room draws and updates its background: the layers are wider than the room (to leave room to
    /// drift), so a neighbour's would otherwise poke in at the screen edges.
    /// </summary>
    public sealed class RoomParallax : MonoBehaviour
    {
        [Serializable]
        public struct Layer
        {
            public SpriteRenderer renderer;
            [Tooltip("0 = moves with the foreground, 1 = infinitely far (moves fully with the virtual camera).")]
            [Range(0f, 1f)] public float depth;
        }

        [Tooltip("Far to near.")]
        [SerializeField] Layer[] layers = Array.Empty<Layer>();
        [Tooltip("How far the virtual camera leans towards the player on each axis (share of the player's distance " +
                 "from the room centre). Keep Y small: vertical drift is distracting.")]
        [SerializeField] Vector2 influence = new(0.08f, 0.02f);
        [Tooltip("Seconds to catch up with the player; higher feels floatier.")]
        [SerializeField, Min(0f)] float smoothTime = 0.3f;
        [Tooltip("On start, scale each layer so it covers the whole room even at its largest drift.")]
        [SerializeField] bool fitToRoom = true;

        Room _room;
        Transform _player;
        Vector2 _focus;
        Vector2 _focusVelocity;
        bool _wasCurrent;

        void Awake()
        {
            _room = GetComponentInParent<Room>();
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                _player = player.transform;

            if (fitToRoom && _room != null)
                FitLayers();

            SetVisible(false);
        }

        void LateUpdate()
        {
            if (_room == null)
                return;

            bool isCurrent = _room.IsCurrent;
            if (!isCurrent)
            {
                if (_wasCurrent)
                    SetVisible(false);
                _wasCurrent = false;
                return;
            }

            // Entering the room: start at rest instead of sliding in from wherever the focus was.
            Vector2 target = GetFocusOffset();
            if (!_wasCurrent)
            {
                _focus = target;
                _focusVelocity = Vector2.zero;
                _wasCurrent = true;
                SetVisible(true);
            }
            else
            {
                _focus = Vector2.SmoothDamp(_focus, target, ref _focusVelocity, smoothTime);
            }

            Vector2 center = _room.Center;
            foreach (Layer layer in layers)
            {
                if (layer.renderer == null)
                    continue;

                Transform layerTransform = layer.renderer.transform;
                Vector2 position = center + _focus * layer.depth;
                layerTransform.position = new Vector3(position.x, position.y, layerTransform.position.z);
            }
        }

        void SetVisible(bool visible)
        {
            foreach (Layer layer in layers)
            {
                if (layer.renderer != null)
                    layer.renderer.enabled = visible;
            }
        }

        /// <summary>Virtual camera offset from the room centre.</summary>
        Vector2 GetFocusOffset()
        {
            if (_player == null)
                return Vector2.zero;

            // Clamped to the room, so e.g. a fall from above the screen doesn't push the layers out of range.
            Vector2 halfSize = _room.Size * 0.5f;
            Vector2 offset = (Vector2)_player.position - _room.Center;
            offset = new Vector2(Mathf.Clamp(offset.x, -halfSize.x, halfSize.x), Mathf.Clamp(offset.y, -halfSize.y, halfSize.y));
            return Vector2.Scale(offset, influence);
        }

        /// <summary>Uniformly scales each layer to cover the room plus the most it can drift.</summary>
        void FitLayers()
        {
            Vector2 maxFocusOffset = Vector2.Scale(_room.Size * 0.5f, influence);
            foreach (Layer layer in layers)
            {
                if (layer.renderer == null || layer.renderer.sprite == null)
                    continue;

                Vector2 needed = _room.Size + maxFocusOffset * (2f * layer.depth);
                Vector2 spriteSize = layer.renderer.sprite.bounds.size;
                float scale = Mathf.Max(needed.x / spriteSize.x, needed.y / spriteSize.y);

                Transform layerTransform = layer.renderer.transform;
                Vector3 parentScale = layerTransform.parent != null ? layerTransform.parent.lossyScale : Vector3.one;
                layerTransform.localScale = new Vector3(scale / parentScale.x, scale / parentScale.y, 1f);
            }
        }
    }
}
