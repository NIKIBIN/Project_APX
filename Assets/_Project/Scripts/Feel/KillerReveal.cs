using System.Collections.Generic;
using APX.Core;
using APX.Masks;
using APX.Player;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace APX.Feel
{
    /// <summary>
    /// When something hidden kills the player, it shows itself until the respawn, with a ping in the colour of the
    /// mask that reveals it: a hidden enemy (or any hidden sprite) appears where it is, and hidden tiles show the
    /// cells that touched the player. Turns "what killed me?" into "that's what this mask is for".
    /// </summary>
    [RequireComponent(typeof(SpriteGhosts))]
    public sealed class KillerReveal : MonoBehaviour
    {
        [Tooltip("Defaults to the MaskManager in the scene (for each mask's colour).")]
        [SerializeField] MaskManager maskManager;
        [Tooltip("How far around the player's body hidden tiles count as the killer, in world units.")]
        [SerializeField, Min(0f)] float tileReach = 0.5f;
        [Tooltip("Tint of the revealed tiles, on top of the mask's colour.")]
        [SerializeField, Range(0f, 1f)] float tileAlpha = 0.9f;
        [SerializeField, Min(0.01f)] float pingDuration = 0.5f;
        [SerializeField, Min(1f)] float pingScale = 1.4f;

        readonly List<MaskVisibilityResponder> _revealed = new();
        readonly List<SpriteGhosts.Ghost> _heldTiles = new();
        SpriteGhosts _ghosts;

        void Awake()
        {
            _ghosts = GetComponent<SpriteGhosts>();
            if (maskManager == null)
                maskManager = FindAnyObjectByType<MaskManager>();
        }

        void OnEnable()
        {
            EventBus<PlayerDiedEvent>.Subscribe(OnPlayerDied);
            EventBus<PlayerRespawnedEvent>.Subscribe(OnPlayerRespawned);
        }

        void OnDisable()
        {
            EventBus<PlayerDiedEvent>.Unsubscribe(OnPlayerDied);
            EventBus<PlayerRespawnedEvent>.Unsubscribe(OnPlayerRespawned);
            Hide();
        }

        void OnPlayerDied(PlayerDiedEvent evt)
        {
            if (evt.Killer == null)
                return;

            foreach (MaskVisibilityResponder hider in evt.Killer.GetComponentsInParent<MaskVisibilityResponder>())
            {
                if (hider.IsConditionMet)
                    continue;

                Color color = ColorOf(hider.Mask);
                var tilemap = hider.GetComponentInChildren<Tilemap>();
                if (tilemap != null && evt.Player.TryGetComponent(out Collider2D body))
                    RevealTiles(tilemap, body.bounds, color);
                else
                    RevealSprites(hider, color);
            }
        }

        void OnPlayerRespawned(PlayerRespawnedEvent evt) => Hide();

        void RevealSprites(MaskVisibilityResponder hider, Color color)
        {
            hider.SetRevealed(true);
            _revealed.Add(hider);
            foreach (Renderer target in hider.Renderers)
            {
                if (target is SpriteRenderer sprite)
                    _ghosts.Spawn(sprite, color, pingDuration, pingScale, orderOffset: 1);
            }
        }

        // Only the cells around the body: showing the whole hidden layer would give the room away.
        void RevealTiles(Tilemap tilemap, Bounds body, Color color)
        {
            body.Expand(tileReach * 2f);
            // Measured on the tilemap's plane: a 2D collider's bounds can sit a hair off it in depth (another cell z).
            float depth = tilemap.transform.position.z;
            Vector3Int min = tilemap.WorldToCell(new Vector3(body.min.x, body.min.y, depth));
            Vector3Int max = tilemap.WorldToCell(new Vector3(body.max.x, body.max.y, depth));
            Color held = color;
            held.a = tileAlpha;
            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    var cell = new Vector3Int(x, y, min.z);
                    if (!tilemap.HasTile(cell))
                        continue;

                    _heldTiles.Add(_ghosts.SpawnTile(tilemap, cell, held, duration: 0f, orderOffset: 1));
                    _ghosts.SpawnTile(tilemap, cell, color, pingDuration, pingScale, orderOffset: 2);
                }
            }
        }

        void Hide()
        {
            foreach (MaskVisibilityResponder hider in _revealed)
            {
                if (hider != null)
                    hider.SetRevealed(false);
            }

            foreach (SpriteGhosts.Ghost ghost in _heldTiles)
                _ghosts.Release(ghost);

            _revealed.Clear();
            _heldTiles.Clear();
        }

        Color ColorOf(MaskType mask) =>
            maskManager != null && maskManager.TryGetDefinition(mask, out MaskDefinition definition) ? definition.Color : Color.white;
    }
}
