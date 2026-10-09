using System;
using APX.Core;
using APX.Masks;
using APX.Player;
using APX.Rooms;
using DG.Tweening;
using UnityEngine;

namespace APX.Hints
{
    /// <summary>
    /// Helper that appears only when the player picks "no mask" in the radial menu, flies next to the
    /// player and exposes the current room's hint (read from the room's <see cref="IHintProvider"/>).
    /// </summary>
    public sealed class HintCompanion : MonoBehaviour
    {
        [SerializeField] Transform followTarget;
        [Tooltip("Child holding the visuals; toggled on summon/dismiss while this component keeps listening.")]
        [SerializeField] GameObject visualRoot;
        [Tooltip("Offset from the target when it faces right (mirrored when it faces left, so the companion trails behind).")]
        [SerializeField] Vector2 followOffset = new(-1.1f, 1.5f);
        [SerializeField, Min(0.01f)] float followSmoothTime = 0.18f;
        [SerializeField, Min(0f)] float bobAmplitude = 0.12f;
        [SerializeField, Min(0f)] float bobFrequency = 1.2f;
        [Tooltip("Seconds to pop in (scale up with a little overshoot) when summoned. 0 = appear instantly.")]
        [SerializeField, Min(0f)] float appearDuration = 0.35f;
        [SerializeField, TextArea(2, 4)] string fallbackHint = "Hmm... não tenho nenhuma dica para esta sala.";

        IFacingProvider _facing;
        Vector3 _velocity;
        Vector3 _visualRestPosition;
        Vector3 _visualRestScale;
        Tween _visualTween;
        float _bobTime;

        public bool IsSummoned { get; private set; }

        public string CurrentHint { get; private set; } = string.Empty;

        public event Action<bool> SummonStateChanged;
        public event Action<string> HintChanged;

        void Awake()
        {
            if (followTarget == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                    followTarget = player.transform;
            }

            if (followTarget != null)
                _facing = followTarget.GetComponent<IFacingProvider>();

            if (visualRoot != null)
            {
                _visualRestPosition = visualRoot.transform.localPosition;
                _visualRestScale = visualRoot.transform.localScale;
                visualRoot.SetActive(false);
            }
        }

        void OnEnable()
        {
            EventBus<CompanionSummonChangedEvent>.Subscribe(OnSummonChanged);
            EventBus<RoomChangedEvent>.Subscribe(OnRoomChanged);
            EventBus<PlayerRespawnedEvent>.Subscribe(OnPlayerRespawned);
        }

        void OnDisable()
        {
            EventBus<CompanionSummonChangedEvent>.Unsubscribe(OnSummonChanged);
            EventBus<RoomChangedEvent>.Unsubscribe(OnRoomChanged);
            EventBus<PlayerRespawnedEvent>.Unsubscribe(OnPlayerRespawned);
        }

        void Start() => SetSummoned(MaskManager.IsCompanionSummoned);

        void LateUpdate()
        {
            if (!IsSummoned || followTarget == null)
                return;

            transform.position = Vector3.SmoothDamp(transform.position, GetFollowPosition(), ref _velocity, followSmoothTime);

            if (visualRoot != null)
            {
                _bobTime += Time.deltaTime;
                float bob = Mathf.Sin(_bobTime * bobFrequency * 2f * Mathf.PI) * bobAmplitude;
                visualRoot.transform.localPosition = _visualRestPosition + Vector3.up * bob;
            }
        }

        /// <summary>
        /// Flies in a tightening spiral into <paramref name="target"/> (re-read every frame, so it may move) and
        /// vanishes at the end, e.g. when the player puts on a mask. Returns null when not summoned.
        /// </summary>
        public Tween SpiralInto(Func<Vector3> target, float duration, float turns)
        {
            if (!IsSummoned || visualRoot == null)
                return null;

            // Gone at once as far as everyone else is concerned (the hint bubble hides); the visuals finish the flight.
            IsSummoned = false;
            SummonStateChanged?.Invoke(false);

            Transform visual = visualRoot.transform;
            visual.localPosition = _visualRestPosition;
            Vector3 offset = transform.position - target();
            float startRadius = ((Vector2)offset).magnitude;
            float startAngle = Mathf.Atan2(offset.y, offset.x);

            _visualTween?.Kill();
            _visualTween = DOVirtual.Float(0f, 1f, duration, t =>
                {
                    float angle = startAngle + turns * 2f * Mathf.PI * t;
                    transform.position = target() + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (startRadius * (1f - t));
                    visual.localScale = _visualRestScale * Mathf.Lerp(1f, 0.3f, t);
                })
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    visualRoot.SetActive(false);
                    visual.localScale = _visualRestScale;
                })
                .SetLink(gameObject);
            return _visualTween;
        }

        /// <summary>Matches the <see cref="MaskManager"/> summon state again, e.g. after an interrupted spiral.</summary>
        public void SyncSummonState() => SetSummoned(MaskManager.IsCompanionSummoned);

        void SetSummoned(bool summoned)
        {
            if (summoned == IsSummoned)
                return;

            IsSummoned = summoned;
            if (visualRoot != null)
            {
                visualRoot.SetActive(summoned);
                PlayAppear(summoned);
            }

            if (summoned)
            {
                SnapToTarget();
                RefreshHint(RoomManager.CurrentRoom);
            }

            SummonStateChanged?.Invoke(summoned);
        }

        void PlayAppear(bool summoned)
        {
            _visualTween?.Kill();
            Transform visual = visualRoot.transform;
            if (!summoned || appearDuration <= 0f)
            {
                visual.localScale = _visualRestScale;
                return;
            }

            visual.localScale = Vector3.zero;
            _visualTween = visual.DOScale(_visualRestScale, appearDuration).SetEase(Ease.OutBack).SetLink(gameObject);
        }

        void RefreshHint(Room room)
        {
            string hint = room != null && room.TryGetComponent(out IHintProvider provider) ? provider.GetHint() : null;
            CurrentHint = string.IsNullOrWhiteSpace(hint) ? fallbackHint : hint;
            HintChanged?.Invoke(CurrentHint);
        }

        void SnapToTarget()
        {
            if (followTarget == null)
                return;

            transform.position = GetFollowPosition();
            _velocity = Vector3.zero;
        }

        Vector3 GetFollowPosition()
        {
            int facing = _facing?.FacingDirection ?? 1;
            return followTarget.position + new Vector3(followOffset.x * facing, followOffset.y, 0f);
        }

        void OnSummonChanged(CompanionSummonChangedEvent evt) => SetSummoned(evt.IsSummoned);

        void OnRoomChanged(RoomChangedEvent evt)
        {
            if (!IsSummoned)
                return;

            // The camera cuts between rooms, so the companion cuts with it.
            SnapToTarget();
            RefreshHint(evt.Current);
        }

        void OnPlayerRespawned(PlayerRespawnedEvent evt)
        {
            if (IsSummoned)
                SnapToTarget();
        }
    }
}
