using System;
using APX.Core;
using APX.Masks;
using UnityEngine;

namespace APX.Player
{
    /// <summary>
    /// "Segundo canal" spider sense for dangers the player can't see (<see cref="DangerSources"/>, e.g. spike traps).
    /// While the mask is worn and the player is in danger, an orb glows behind their head: small and white while
    /// in danger, bigger and baby blue when the danger is almost ready to strike, big and blue right before it does.
    /// Lives on a child of the player sprite behind the head, so it follows facing and squash. Raises
    /// <see cref="DangerSenseChangedEvent"/> on every level change (for the sounds).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerDangerSense : MaskResponder
    {
        [Serializable]
        struct Look
        {
            public Color color;
            [Tooltip("Diameter in the parent's units, glow included (the solid core is about 60% of it).")]
            [Min(0f)] public float size;
            [Tooltip("Pulses per second.")]
            [Min(0f)] public float pulseRate;
        }

        [Header("Levels (real seconds before the strike)")]
        [Tooltip("At this many seconds or fewer the orb turns baby blue.")]
        [SerializeField, Min(0f)] float almostReadyTime = 1f;
        [Tooltip("At this many seconds or fewer the orb turns blue.")]
        [SerializeField, Min(0f)] float aboutToStrikeTime = 0.4f;

        [Header("Looks")]
        [SerializeField] Look low = new() { color = Color.white, size = 0.55f, pulseRate = 1.5f };
        [SerializeField] Look medium = new() { color = new Color(0.54f, 0.81f, 0.94f), size = 0.8f, pulseRate = 3.5f };
        [SerializeField] Look high = new() { color = new Color(0.12f, 0.4f, 1f), size = 1.05f, pulseRate = 8f };
        [Tooltip("Size change of each pulse, as a share of the size.")]
        [SerializeField, Range(0f, 0.5f)] float pulseAmount = 0.1f;
        [Tooltip("Extra size for a moment when the danger grows, as a share of the size.")]
        [SerializeField, Range(0f, 1f)] float growKick = 0.35f;
        [Tooltip("Seconds to blend size and colour between levels.")]
        [SerializeField, Min(0.01f)] float blendTime = 0.06f;

        SpriteRenderer _renderer;
        DangerLevel _level;
        float _size;
        Color _color = Color.white;
        float _pulsePhase;
        float _kick;
        bool _isPlayerDead;

        public DangerLevel Level => _level;

        void Reset() => SetDefaultMask(MaskType.SecondChannel);

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _renderer.enabled = false;
            transform.localScale = Vector3.zero;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EventBus<PlayerDiedEvent>.Subscribe(OnPlayerDied);
            EventBus<PlayerRespawnedEvent>.Subscribe(OnPlayerRespawned);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            EventBus<PlayerDiedEvent>.Unsubscribe(OnPlayerDied);
            EventBus<PlayerRespawnedEvent>.Unsubscribe(OnPlayerRespawned);
            SetLevel(DangerLevel.None);
        }

        // The level is polled every frame; the mask condition is read from IsConditionMet.
        protected override void Apply(bool conditionMet)
        {
        }

        void Update()
        {
            SetLevel(SenseLevel());

            // Unscaled, so the orb keeps breathing through the radial menu's slow motion.
            float deltaTime = Time.unscaledDeltaTime;
            float blend = 1f - Mathf.Exp(-deltaTime / blendTime);
            Look look = LookFor(_level);
            float targetSize = _level == DangerLevel.None ? 0f : look.size;
            _size = Mathf.Lerp(_size, targetSize, blend);
            _color = Color.Lerp(_color, look.color, blend);
            _kick = Mathf.MoveTowards(_kick, 0f, deltaTime * 4f);
            _pulsePhase = Mathf.Repeat(_pulsePhase + deltaTime * look.pulseRate, 1f);

            float pulse = 1f + pulseAmount * Mathf.Sin(_pulsePhase * Mathf.PI * 2f) + _kick;
            float scale = _size * pulse;
            bool visible = scale > 0.005f;
            _renderer.enabled = visible;
            if (!visible)
                return;

            transform.localScale = new Vector3(scale, scale, 1f);
            _renderer.color = _color;
        }

        DangerLevel SenseLevel()
        {
            if (!IsConditionMet || _isPlayerDead || !DangerSources.TryGetMostUrgent(out float seconds))
                return DangerLevel.None;

            if (seconds <= aboutToStrikeTime)
                return DangerLevel.High;
            return seconds <= almostReadyTime ? DangerLevel.Medium : DangerLevel.Low;
        }

        void SetLevel(DangerLevel level)
        {
            if (level == _level)
                return;

            DangerLevel previous = _level;
            _level = level;
            if (level > previous)
                _kick = growKick;
            if (previous == DangerLevel.None)
                _color = LookFor(level).color; // Appear in the right colour instead of fading from the last one.

            EventBus<DangerSenseChangedEvent>.Raise(new DangerSenseChangedEvent(previous, level));
        }

        Look LookFor(DangerLevel level) => level switch
        {
            DangerLevel.High => high,
            DangerLevel.Medium => medium,
            DangerLevel.Low => low,
            _ => new Look { color = _color, size = 0f, pulseRate = low.pulseRate },
        };

        void OnPlayerDied(PlayerDiedEvent evt) => _isPlayerDead = true;

        void OnPlayerRespawned(PlayerRespawnedEvent evt) => _isPlayerDead = false;
    }
}
