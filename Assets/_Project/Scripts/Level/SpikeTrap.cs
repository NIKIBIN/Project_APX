using APX.Core;
using APX.Rooms;
using UnityEngine;

namespace APX.Level
{
    /// <summary>
    /// Spikes hidden under the floor that shoot up when their <see cref="SpikeTrapGroup"/> strikes, then sink back.
    /// Place the transform on the floor surface, centred on the trap. A sprite mask clips the spikes at the surface,
    /// and the hurtbox only hurts while they are out. In edit mode the spikes are drawn up so the trap can be seen.
    /// </summary>
    public sealed class SpikeTrap : MonoBehaviour, IResettable
    {
        [Tooltip("Width in world units (1 = one tile).")]
        [SerializeField, Min(0.25f)] float width = 2f;
        [Tooltip("Share of the spikes that must be out before they hurt.")]
        [SerializeField, Range(0f, 1f)] float hurtThreshold = 0.35f;
        [Tooltip("While sinking back, the spikes stop hurting once they drop below this share. Higher = safe sooner, " +
                 "giving the player some breathing room before the spikes are fully down.")]
        [SerializeField, Range(0f, 1f)] float retractHurtThreshold = 0.85f;

        [Header("Parts")]
        [Tooltip("Tiled sprite renderer, visible inside the mask only.")]
        [SerializeField] SpriteRenderer spikes;
        [Tooltip("Covers the space above the surface; the spikes are invisible below it.")]
        [SerializeField] SpriteMask clip;
        [Tooltip("Trigger with a DamageOnContact, enabled while the spikes are out.")]
        [SerializeField] BoxCollider2D hurtbox;

        SpikeTrapGroup _group;
        float _shownExtension = -1f;

        public float Width => width;

        internal ITimeScaleSource TimeSource { get; private set; }

        void Awake()
        {
            TimeSource = GetComponentInParent<ITimeScaleSource>(true);
            _group = GetComponentInParent<SpikeTrapGroup>(true);
            if (_group == null)
            {
                Room room = GetComponentInParent<Room>(true);
                GameObject host = room != null ? room.gameObject : transform.parent != null ? transform.parent.gameObject : gameObject;
                _group = host.AddComponent<SpikeTrapGroup>();
            }

            Layout();
            ShowExtension(0f);
        }

        void OnEnable() => _group.Register(this);

        void OnDisable() => _group.Unregister(this);

        void LateUpdate() => ShowExtension(_group.Extension);

        public void ResetState()
        {
            if (_group == null)
                return;

            _group.ResetState();
            ShowExtension(0f);
        }

        internal void OnStrike()
        {
            EventBus<SpikeTrapPoppedEvent>.Raise(new SpikeTrapPoppedEvent(transform.position, width));
        }

        internal bool IsUnderFeet(float feetLeft, float feetRight, float feetY, float tolerance)
        {
            Vector2 surface = transform.position;
            float halfWidth = width * 0.5f;
            return Mathf.Abs(feetY - surface.y) <= tolerance && feetRight >= surface.x - halfWidth && feetLeft <= surface.x + halfWidth;
        }

        void ShowExtension(float extension)
        {
            if (spikes == null || Mathf.Approximately(extension, _shownExtension))
                return;

            _shownExtension = extension;
            spikes.transform.localPosition = new Vector3(0f, Mathf.LerpUnclamped(HiddenY, UpY, extension), 0f);
            if (hurtbox != null)
                hurtbox.enabled = extension >= (_group != null && _group.IsRetracting ? retractHurtThreshold : hurtThreshold);
        }

        // Positions of the spikes' pivot with their top just under the surface, and with their base on it.
        float HiddenY => -SpriteBounds.max.y - 0.02f;

        float UpY => -SpriteBounds.min.y;

        Bounds SpriteBounds => spikes.sprite != null ? spikes.sprite.bounds : new Bounds(Vector3.zero, Vector3.one);

        float SpikeHeight => SpriteBounds.size.y;

        void Layout()
        {
            if (spikes == null)
                return;

            spikes.drawMode = SpriteDrawMode.Tiled;
            spikes.size = new Vector2(width, SpikeHeight);
            spikes.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            if (clip != null && clip.sprite != null)
            {
                // Room above the full height for the pop's overshoot.
                Vector2 clipSize = new(width + 0.1f, SpikeHeight * 1.5f);
                Vector2 spriteSize = clip.sprite.bounds.size;
                clip.transform.localPosition = new Vector3(0f, clipSize.y * 0.5f, 0f);
                clip.transform.localScale = new Vector3(clipSize.x / spriteSize.x, clipSize.y / spriteSize.y, 1f);
            }

            if (hurtbox != null)
            {
                hurtbox.isTrigger = true;
                hurtbox.offset = new Vector2(0f, SpikeHeight * 0.4f);
                hurtbox.size = new Vector2(Mathf.Max(0.1f, width - 0.1f), SpikeHeight * 0.8f);
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // Resizing renderers and colliders is not allowed during OnValidate itself.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || Application.isPlaying)
                    return;

                Layout();
                _shownExtension = -1f;
                ShowExtension(1f);
            };
        }

        void OnDrawGizmos()
        {
            Vector3 surface = transform.position;
            Gizmos.color = new Color(1f, 0.35f, 0.3f, 0.9f);
            Gizmos.DrawLine(surface + Vector3.left * (width * 0.5f), surface + Vector3.right * (width * 0.5f));
        }
#endif
    }
}
