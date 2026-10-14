using APX.Core;
using APX.Masks;
using APX.Player;
using UnityEngine;

namespace APX.Level
{
    /// <summary>
    /// A mask resting on an altar. Touching it raises <see cref="MaskPickedUpEvent"/>: the <see cref="MaskManager"/>
    /// unlocks the mask in the radial menu (it keeps what the player owns) and the mask leaves the altar. A room
    /// reset only brings back masks the player doesn't own yet.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class MaskPickup : MonoBehaviour, IResettable
    {
        [Tooltip("The mask found here. Its colour tints the floating mask.")]
        [SerializeField] MaskDefinition mask;
        [Tooltip("The floating mask, hidden once picked up. The rest (e.g. the altar) stays.")]
        [SerializeField] SpriteRenderer maskVisual;
        [SerializeField, Min(0f)] float bobAmplitude = 0.12f;
        [Tooltip("Bobs per second.")]
        [SerializeField, Min(0f)] float bobFrequency = 0.6f;

        Collider2D _trigger;
        Vector3 _visualRestPosition;

        bool IsAvailable => mask != null && !MaskManager.IsUnlocked(mask.Type);

        void Awake()
        {
            _trigger = GetComponent<Collider2D>();
            _trigger.isTrigger = true;
            if (maskVisual != null)
            {
                _visualRestPosition = maskVisual.transform.localPosition;
                if (mask != null)
                    maskVisual.color = mask.Color;
            }
        }

        void OnEnable()
        {
            EventBus<MaskUnlockedEvent>.Subscribe(OnMaskUnlocked);
            Refresh();
        }

        void OnDisable() => EventBus<MaskUnlockedEvent>.Unsubscribe(OnMaskUnlocked);

        void OnValidate()
        {
            if (mask != null && mask.Type == MaskType.None)
            {
                Debug.LogWarning($"{name}: \"no mask\" is always available and can't be picked up.", this);
                mask = null;
            }
        }

        void Update()
        {
            if (maskVisual == null || !maskVisual.enabled || bobAmplitude <= 0f)
                return;

            float bob = Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
            maskVisual.transform.localPosition = _visualRestPosition + Vector3.up * bob;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsAvailable)
                return;

            PlayerLife player = other.GetComponentInParent<PlayerLife>();
            if (player == null || player.IsDead)
                return;

            Vector2 position = maskVisual != null ? maskVisual.transform.position : transform.position;
            EventBus<MaskPickedUpEvent>.Raise(new MaskPickedUpEvent(mask.Type, position, mask.Color));
            Refresh();
        }

        public void ResetState() => Refresh();

        void OnMaskUnlocked(MaskUnlockedEvent evt) => Refresh();

        void Refresh()
        {
            bool available = IsAvailable;
            if (_trigger != null)
                _trigger.enabled = available;
            if (maskVisual != null)
                maskVisual.enabled = available;
        }

        void OnDrawGizmos()
        {
            if (mask == null)
                return;

            Gizmos.color = mask.Color;
            Gizmos.DrawWireSphere(maskVisual != null ? maskVisual.transform.position : transform.position, 0.45f);
        }
    }
}
