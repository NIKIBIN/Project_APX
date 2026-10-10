using System;
using APX.Core;
using APX.Feel;
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
    /// If the room has a companion spot it flies there instead and waits, until the player leaves the room.
    /// Cutscenes can take it over with <see cref="BeginScripted"/>: it shows up and stays wherever it is
    /// moved, without following anyone or offering hints, until <see cref="EndScripted"/> or a summon.
    /// </summary>
    public sealed class HintCompanion : MonoBehaviour
    {
        [SerializeField] Transform followTarget;
        [Tooltip("Child holding the visuals; toggled on summon/dismiss while this component keeps listening.")]
        [SerializeField] GameObject visualRoot;
        [Tooltip("Optional reaction bubble over the companion's head, used by cutscenes.")]
        [SerializeField] EmotePopup emote;
        [Tooltip("Offset from the target when it faces right (mirrored when it faces left, so the companion trails behind).")]
        [SerializeField] Vector2 followOffset = new(-1.1f, 1.5f);
        [SerializeField, Min(0.01f)] float followSmoothTime = 0.18f;
        [Tooltip("Seconds to ease into a room's companion spot (see RoomHint).")]
        [SerializeField, Min(0.01f)] float spotSmoothTime = 0.45f;
        [Tooltip("Top speed when flying to a room's companion spot, in units per second.")]
        [SerializeField, Min(0.1f)] float spotFlySpeed = 14f;
        [SerializeField, Min(0f)] float bobAmplitude = 0.12f;
        [SerializeField, Min(0f)] float bobFrequency = 1.2f;
        [Tooltip("Seconds to pop in (scale up with a little overshoot) when summoned. 0 = appear instantly.")]
        [SerializeField, Min(0f)] float appearDuration = 0.35f;
        [Tooltip("How close to its place (beside the player or the room's spot) the companion must be to count as settled (see IsSettled).")]
        [SerializeField, Min(0.01f)] float settleDistance = 0.4f;
        [SerializeField, TextArea(2, 4)] string fallbackHint = "Hmm... não tenho nenhuma dica para esta sala.";

        IFacingProvider _facing;
        IHintProvider _hintProvider;
        Vector3 _velocity;
        Vector3 _visualRestPosition;
        Vector3 _visualRestScale;
        Tween _visualTween;
        float _bobTime;
        bool _isAppearing;

        public bool IsSummoned { get; private set; }

        /// <summary>Summoned, fully popped in and at its place, beside the player or at the room's companion spot
        /// (e.g. ready to show its hint).</summary>
        public bool IsSettled =>
            IsSummoned && !IsScripted && !_isAppearing && followTarget != null &&
            ((Vector2)(transform.position - GetTargetPosition(out _))).sqrMagnitude <= settleDistance * settleDistance;

        /// <summary>A cutscene is moving the companion (see <see cref="BeginScripted"/>).</summary>
        public bool IsScripted { get; private set; }

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
            if (IsScripted)
            {
                Bob();
                return;
            }

            if (!IsSummoned || followTarget == null)
                return;

            Vector3 target = GetTargetPosition(out bool atSpot);
            transform.position = atSpot
                ? Vector3.SmoothDamp(transform.position, target, ref _velocity, spotSmoothTime, spotFlySpeed)
                : Vector3.SmoothDamp(transform.position, target, ref _velocity, followSmoothTime);
            Bob();
        }

        /// <summary>
        /// Cutscene control: shows the companion at <paramref name="position"/> at once and stops it following the
        /// player; move its transform freely until <see cref="EndScripted"/>, or until a summon, which makes it glide
        /// from wherever it is to its place beside the player.
        /// </summary>
        public void BeginScripted(Vector3 position)
        {
            IsScripted = true;
            _visualTween?.Kill();
            transform.position = position;
            _velocity = Vector3.zero;
            if (visualRoot != null)
            {
                visualRoot.SetActive(true);
                visualRoot.transform.localScale = _visualRestScale;
            }
        }

        /// <summary>Ends cutscene control: back to following the player when summoned, otherwise hidden.</summary>
        public void EndScripted()
        {
            if (!IsScripted)
                return;

            IsScripted = false;
            _velocity = Vector3.zero;
            SetFacing(1);
            if (emote != null)
                emote.Hide();
            if (!IsSummoned && visualRoot != null)
                visualRoot.SetActive(false);
        }

        /// <summary>Turns the companion to look right (1) or left (-1).</summary>
        public void SetFacing(int direction)
        {
            if (visualRoot == null)
                return;

            Vector3 scale = _visualRestScale;
            scale.x = Mathf.Abs(scale.x) * (direction < 0 ? -1f : 1f);
            visualRoot.transform.localScale = scale;
        }

        public void ShowEmote(Emote reaction)
        {
            if (emote != null)
                emote.Show(reaction);
        }

        public void HideEmote()
        {
            if (emote != null)
                emote.Hide();
        }

        void Bob()
        {
            if (visualRoot == null)
                return;

            _bobTime += Time.deltaTime;
            float bob = Mathf.Sin(_bobTime * bobFrequency * 2f * Mathf.PI) * bobAmplitude;
            visualRoot.transform.localPosition = _visualRestPosition + Vector3.up * bob;
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

            // Already on screen in a cutscene: no pop-in or jump, it just glides over to the player.
            bool wasScripted = IsScripted;
            if (wasScripted)
            {
                IsScripted = false;
                _velocity = Vector3.zero;
                HideEmote();
            }

            if (visualRoot != null && !(summoned && wasScripted))
            {
                visualRoot.SetActive(summoned);
                PlayAppear(summoned);
            }

            if (summoned)
            {
                // Pops in beside the player, then flies off to the room's spot if it has one.
                if (!wasScripted)
                    SnapBesidePlayer();
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
            _isAppearing = true;
            _visualTween = visual.DOScale(_visualRestScale, appearDuration)
                .SetEase(Ease.OutBack)
                .OnKill(() => _isAppearing = false)
                .SetLink(gameObject);
        }

        void RefreshHint(Room room)
        {
            _hintProvider = room != null && room.TryGetComponent(out IHintProvider provider) ? provider : null;
            string hint = _hintProvider != null ? _hintProvider.GetHint() : null;
            CurrentHint = string.IsNullOrWhiteSpace(hint) ? fallbackHint : hint;
            HintChanged?.Invoke(CurrentHint);
        }

        void SnapBesidePlayer()
        {
            if (followTarget == null)
                return;

            transform.position = GetFollowPosition();
            _velocity = Vector3.zero;
        }

        /// <summary>The room's companion spot if it has one (re-read every frame, so it may move), otherwise beside the player.</summary>
        Vector3 GetTargetPosition(out bool atSpot)
        {
            atSpot = TryGetSpot(out Vector3 spot);
            return atSpot ? spot : GetFollowPosition();
        }

        bool TryGetSpot(out Vector3 spot)
        {
            spot = default;
            if (_hintProvider == null || !_hintProvider.TryGetCompanionSpot(out spot))
                return false;

            if (followTarget != null)
                spot.z = followTarget.position.z;
            return true;
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

            // The camera cuts between rooms, so the companion cuts with it; then it flies off to the room's spot if it has one.
            SnapBesidePlayer();
            RefreshHint(evt.Current);
        }

        void OnPlayerRespawned(PlayerRespawnedEvent evt)
        {
            // At a room's spot it stays put; otherwise it keeps up with the player.
            if (IsSummoned && !TryGetSpot(out _))
                SnapBesidePlayer();
        }
    }
}
