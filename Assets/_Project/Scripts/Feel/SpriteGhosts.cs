using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace APX.Feel
{
    /// <summary>
    /// Pool of sprite copies ("ghosts") for afterimages, pings and reveals. A ghost copies the look and place of a
    /// sprite or a tile, takes a tint, then fades out on real time (optionally growing), or stays until released.
    /// </summary>
    public sealed class SpriteGhosts : MonoBehaviour
    {
        /// <summary>A ghost on screen; keep it to <see cref="Release"/> one that was spawned to stay.</summary>
        public sealed class Ghost
        {
            internal SpriteRenderer Renderer;
            internal float Age;
            internal float Duration;
            internal Color Color;
            internal Vector3 StartScale;
            internal float EndScale;
        }

        [Tooltip("Material for every ghost. Unlit, so ghosts read the same in light and shadow.")]
        [SerializeField] Material material;

        readonly List<Ghost> _active = new();
        readonly Stack<Ghost> _free = new();
        Transform _container;

        void Awake()
        {
            // Ghosts live in world space, outside this object, so its transform never moves them.
            _container = new GameObject("SpriteGhosts").transform;
        }

        void OnDestroy()
        {
            if (_container != null)
                Destroy(_container.gameObject);
        }

        void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Ghost ghost = _active[i];
                if (ghost.Duration <= 0f)
                    continue;

                ghost.Age += deltaTime;
                float t = Mathf.Clamp01(ghost.Age / ghost.Duration);
                float remaining = 1f - t;
                Color color = ghost.Color;
                color.a *= remaining * remaining;
                ghost.Renderer.color = color;
                ghost.Renderer.transform.localScale = ghost.StartScale * Mathf.Lerp(1f, ghost.EndScale, 1f - remaining * remaining);

                if (t >= 1f)
                    Recycle(i);
            }
        }

        /// <summary>A copy of <paramref name="source"/> as it is drawn right now.</summary>
        /// <param name="duration">Real seconds to fade out; 0 keeps it until <see cref="Release"/>.</param>
        /// <param name="endScale">Size at the end of the fade, relative to the start (above 1 pops outwards).</param>
        /// <param name="orderOffset">Sorting order relative to the source (negative draws behind it).</param>
        public Ghost Spawn(SpriteRenderer source, Color color, float duration, float endScale = 1f, int orderOffset = 0)
        {
            if (source == null || source.sprite == null)
                return null;

            Transform from = source.transform;
            Ghost ghost = Take(source.sprite, from.position, from.rotation, from.lossyScale, color, duration, endScale);
            SpriteRenderer target = ghost.Renderer;
            target.flipX = source.flipX;
            target.flipY = source.flipY;
            target.drawMode = source.drawMode;
            if (source.drawMode != SpriteDrawMode.Simple)
            {
                target.size = source.size;
                target.tileMode = source.tileMode;
            }

            target.maskInteraction = source.maskInteraction;
            target.sortingLayerID = source.sortingLayerID;
            target.sortingOrder = source.sortingOrder + orderOffset;
            return ghost;
        }

        /// <summary>A copy of the tile at <paramref name="cell"/>; null when the cell is empty.</summary>
        public Ghost SpawnTile(Tilemap tilemap, Vector3Int cell, Color color, float duration, float endScale = 1f, int orderOffset = 0)
        {
            Sprite sprite = tilemap.GetSprite(cell);
            if (sprite == null)
                return null;

            Matrix4x4 tileMatrix = tilemap.GetTransformMatrix(cell);
            Vector3 scale = Vector3.Scale(tilemap.transform.lossyScale, tileMatrix.lossyScale);
            Ghost ghost = Take(sprite, tilemap.GetCellCenterWorld(cell), tilemap.transform.rotation * tileMatrix.rotation, scale,
                color, duration, endScale);
            SpriteRenderer target = ghost.Renderer;
            target.flipX = false;
            target.flipY = false;
            target.drawMode = SpriteDrawMode.Simple;
            target.maskInteraction = SpriteMaskInteraction.None;
            if (tilemap.TryGetComponent(out TilemapRenderer tilemapRenderer))
            {
                target.sortingLayerID = tilemapRenderer.sortingLayerID;
                target.sortingOrder = tilemapRenderer.sortingOrder + orderOffset;
            }

            return ghost;
        }

        /// <summary>Takes a ghost away at once (e.g. one spawned to stay).</summary>
        public void Release(Ghost ghost)
        {
            int index = ghost != null ? _active.IndexOf(ghost) : -1;
            if (index >= 0)
                Recycle(index);
        }

        Ghost Take(Sprite sprite, Vector3 position, Quaternion rotation, Vector3 scale, Color color, float duration, float endScale)
        {
            if (!_free.TryPop(out Ghost ghost))
            {
                var renderer = new GameObject("Ghost").AddComponent<SpriteRenderer>();
                renderer.transform.SetParent(_container, false);
                if (material != null)
                    renderer.sharedMaterial = material;
                ghost = new Ghost { Renderer = renderer };
            }

            ghost.Age = 0f;
            ghost.Duration = duration;
            ghost.Color = color;
            ghost.StartScale = scale;
            ghost.EndScale = endScale;

            SpriteRenderer target = ghost.Renderer;
            target.sprite = sprite;
            target.color = color;
            target.transform.SetPositionAndRotation(position, rotation);
            target.transform.localScale = scale;
            target.gameObject.SetActive(true);
            _active.Add(ghost);
            return ghost;
        }

        void Recycle(int index)
        {
            Ghost ghost = _active[index];
            _active.RemoveAt(index);
            ghost.Renderer.gameObject.SetActive(false);
            _free.Push(ghost);
        }
    }
}
