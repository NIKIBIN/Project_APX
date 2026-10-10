using System;
using System.Collections;
using APX.Core;
using APX.Feel;
using APX.Hints;
using APX.Level;
using APX.Masks;
using APX.Player;
using APX.Rooms;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace APX.Cutscenes
{
    /// <summary>
    /// Opening cutscene:
    /// <list type="number">
    /// <item>Black screen; the respawn transition opens on the spirit (the hint companion) floating around the room.</item>
    /// <item>The room rumbles and debris falls from the ceiling; the spirit stops, puzzled ("?").</item>
    /// <item>Once it calms down, the camera pans to the ceiling opening and the player drops through it, knocked out.</item>
    /// <item>The spirit notices ("!"), flies over and nudges the player: once, then twice. The player trembles once,
    /// then twice, and hops to their feet.</item>
    /// <item>The spirit takes its place beside the player, the camera zooms out to the room and control returns.</item>
    /// </list>
    /// Without a companion the spirit beats are skipped; without a ceiling opening the player appears above their
    /// placed position instead. Ordinary game systems (gravity, landing dust, squash) play along.
    /// </summary>
    [DefaultExecutionOrder(-10)] // Moves the camera focus before the RoomCamera applies it.
    public sealed class IntroCutscene : MonoBehaviour
    {
        const float MaxBlackStep = 1f / 30f;
        const float CameraArrivedDistance = 0.05f;
        const float CameraTimeout = 3f;

        [Header("References")]
        [Tooltip("Defaults to the PlayerController in the scene.")]
        [SerializeField] PlayerController player;
        [Tooltip("Defaults to the RoomCamera in the scene.")]
        [SerializeField] RoomCamera roomCamera;
        [Tooltip("The spirit. Defaults to the HintCompanion in the scene; without one its beats are skipped.")]
        [SerializeField] HintCompanion companion;
        [Tooltip("Defaults to the MaskManager in the scene.")]
        [SerializeField] MaskManager maskManager;
        [SerializeField] bool playOnStart = true;

        [Header("Opening")]
        [Tooltip("Component implementing IRespawnTransition (e.g. the HUD's IrisTransitionView) that opens the " +
                 "scene. Found automatically when empty; without one the scene starts uncovered.")]
        [SerializeField] MonoBehaviour openingTransition;
        [Tooltip("Seconds the screen stays completely black before the opening transition starts.")]
        [SerializeField, Min(0f)] float blackScreenTime = 0.5f;
        [Tooltip("Camera zoom during the cutscene: 1 shows the whole room, 2.5 shows 40% of it.")]
        [SerializeField, Min(1f)] float introZoom = 2.5f;
        [Tooltip("Seconds the camera takes to settle when it moves to something new.")]
        [SerializeField, Min(0f)] float cameraMoveTime = 0.45f;

        [Header("Animator States")]
        [SerializeField] string fallState = "IntroFall";
        [SerializeField] string knockedOutState = "IntroKnockedOut";
        [SerializeField] string jumpState = "Jump";

        [Header("Spirit Floating Around")]
        [Tooltip("Centre of the spirit's floating. Defaults to the middle of the room.")]
        [SerializeField] Transform spiritAnchor;
        [Tooltip("Half-width and half-height of the figure-eight the spirit floats along.")]
        [SerializeField] Vector2 wanderSize = new(2.5f, 0.7f);
        [Tooltip("Seconds per figure-eight.")]
        [SerializeField, Min(0.1f)] float wanderPeriod = 5f;
        [Tooltip("Seconds of floating after the screen opens, before the rumble.")]
        [SerializeField, Min(0f)] float wanderTime = 1.5f;

        [Header("Rumble")]
        [SerializeField, Min(0f)] float rumbleDuration = 2.5f;
        [Tooltip("Seconds into the rumble before the spirit stops and the \"?\" appears.")]
        [SerializeField, Min(0f)] float puzzledDelay = 0.6f;
        [Tooltip("Seconds the spirit takes to slow down to a stop.")]
        [SerializeField, Min(0.01f)] float spiritStopTime = 0.4f;
        [Tooltip("Seconds between the puzzled spirit's glances left and right. 0 = it doesn't look around.")]
        [SerializeField, Min(0f)] float lookAroundInterval = 0.5f;
        [Tooltip("Seconds after the rumble stops (so the last debris lands) before the camera moves to the opening.")]
        [SerializeField, Min(0f)] float pauseAfterRumble = 1f;

        [Header("Fall")]
        [Tooltip("Optional hole in the ceiling the player drops through from off screen. Without one, they appear " +
                 "above their placed position.")]
        [SerializeField] Transform ceilingOpening;
        [Tooltip("How far above the top of the room the player starts falling through the ceiling opening.")]
        [SerializeField, Min(0f)] float offscreenMargin = 1.5f;
        [Tooltip("How far above the player's placed position the fall starts (kept inside the room). Without a ceiling opening only.")]
        [SerializeField, Min(0f)] float fallHeight = 6f;
        [Tooltip("Seconds the camera holds on the opening before the player falls.")]
        [SerializeField, Min(0f)] float holdBeforeFall = 0.6f;

        [Header("Waking Up")]
        [Tooltip("Seconds lying still after landing before the spirit notices (\"!\").")]
        [SerializeField, Min(0f)] float surpriseDelay = 0.6f;
        [Tooltip("Seconds the \"!\" shows before the spirit flies over.")]
        [SerializeField, Min(0f)] float surpriseTime = 0.7f;
        [Tooltip("Room the camera leaves around the spirit and the player while both are in view.")]
        [SerializeField] Vector2 twoShotMargin = new(3f, 2.5f);
        [Tooltip("Speed of the spirit's flight to the player, in units per second.")]
        [SerializeField, Min(0.1f)] float spiritSpeed = 7f;
        [Tooltip("Where the spirit hovers to nudge the player, from the player's centre (x towards their head).")]
        [SerializeField] Vector2 nudgeSpot = new(0.95f, 0f);
        [SerializeField, Min(0f)] float nudgeDistance = 0.3f;
        [SerializeField, Min(0.01f)] float nudgeInTime = 0.07f;
        [SerializeField, Min(0.01f)] float nudgeOutTime = 0.16f;
        [Tooltip("How far each nudge shoves the player's sprite.")]
        [SerializeField, Min(0f)] float nudgePush = 0.06f;
        [SerializeField, Min(0f)] float pauseAfterFirstNudge = 0.7f;
        [SerializeField, Min(0f)] float quickNudgeGap = 0.08f;
        [SerializeField, Min(0f)] float pauseBeforeTremble = 0.6f;
        [SerializeField, Min(0f)] float firstTrembleStrength = 0.04f;
        [SerializeField, Min(0f)] float firstTrembleDuration = 0.25f;
        [SerializeField, Min(0f)] float pauseBetweenTrembles = 0.5f;
        [SerializeField, Min(0f)] float quickTrembleStrength = 0.08f;
        [SerializeField, Min(0f)] float quickTrembleDuration = 0.18f;
        [SerializeField, Min(0f)] float quickTrembleGap = 0.08f;
        [SerializeField, Min(0f)] float pauseBeforeHop = 0.35f;
        [Tooltip("Upward speed of the hop to the feet.")]
        [SerializeField, Min(0f)] float hopSpeed = 9f;

        [Header("After the Intro")]
        [Tooltip("Seconds the spirit gets to settle beside the player before the camera zooms out.")]
        [SerializeField, Min(0f)] float pauseBeforeZoomOut = 0.4f;
        [SerializeField, Min(0f)] float zoomOutDuration = 1.2f;
        [SerializeField] Ease zoomOutEase = Ease.InOutSine;
        [Tooltip("Give the player control as soon as the zoom-out starts instead of when it ends.")]
        [SerializeField] bool controlDuringZoomOut;
        [Tooltip("Keep the spirit as the hint companion beside the player afterwards (through the MaskManager).")]
        [SerializeField] bool summonCompanion = true;
        [Tooltip("Seconds after control returns before On Finished fires.")]
        [SerializeField, Min(0f)] float finishedEventDelay = 0.6f;
        [Tooltip("Extra actions once the intro has played through.")]
        [SerializeField] UnityEvent onFinished = new();

        IRespawnTransition _transition;
        PlayerAttack _attack;
        Rigidbody2D _body;
        Animator _animator;
        Transform _sprite;
        SpriteRenderer _spriteRenderer;
        Vector3 _spriteRestPosition;
        Coroutine _routine;
        Coroutine _wanderRoutine;
        Tween _tween;
        Tween _spiritTween;
        bool _landed;
        bool _rumbling;

        // Camera direction: the RoomCamera follows a proxy focus that eases towards the current shot.
        Transform _cameraFocus;
        Func<Vector2> _shot;
        Func<float> _shotZoom;
        float _shotMoveTime;
        bool _directing;
        bool _snapCamera;
        Vector2 _viewCenter;
        Vector2 _viewVelocity;
        float _zoom = 1f;
        float _zoomVelocity;

        // Spirit floating.
        Vector2 _wanderCenter;
        float _wanderPhase;
        float _wanderSpeed;

        public bool IsPlaying { get; private set; }

        bool HasSpirit => companion != null;

        void Awake()
        {
            if (player == null)
                player = FindAnyObjectByType<PlayerController>();
            if (roomCamera == null)
                roomCamera = FindAnyObjectByType<RoomCamera>();
            if (companion == null)
                companion = FindAnyObjectByType<HintCompanion>();
            if (maskManager == null)
                maskManager = FindAnyObjectByType<MaskManager>();

            _transition = openingTransition as IRespawnTransition ?? FindTransition();
            _attack = player.GetComponent<PlayerAttack>();
            _body = player.GetComponent<Rigidbody2D>();
            _animator = player.GetComponentInChildren<Animator>();
            _sprite = _animator.transform;
            _spriteRenderer = _sprite.GetComponentInChildren<SpriteRenderer>();

            _cameraFocus = new GameObject("IntroCameraFocus").transform;
            _cameraFocus.SetParent(transform, false);
        }

        void OnValidate()
        {
            if (openingTransition != null && openingTransition is not IRespawnTransition)
            {
                Debug.LogWarning($"{openingTransition.name} does not implement {nameof(IRespawnTransition)}.", this);
                openingTransition = null;
            }
        }

        void OnEnable() => EventBus<PlayerDiedEvent>.Subscribe(OnPlayerDied);

        void OnDisable()
        {
            EventBus<PlayerDiedEvent>.Unsubscribe(OnPlayerDied);
            if (IsPlaying)
                Abort();
        }

        void Start()
        {
            if (playOnStart)
                Play();
        }

        void LateUpdate()
        {
            if (!_directing)
                return;

            // Eases the actual view centre (already kept inside the room), so moves stay smooth even where the
            // camera presses against the room's edges.
            float zoomTarget = _shotZoom?.Invoke() ?? introZoom;
            Vector2 target = roomCamera.GetViewCenter(_shot(), zoomTarget);
            if (_shotMoveTime <= 0f || _snapCamera)
            {
                _snapCamera = false;
                _viewCenter = target;
                _zoom = zoomTarget;
                _viewVelocity = Vector2.zero;
                _zoomVelocity = 0f;
            }
            else
            {
                _viewCenter = Vector2.SmoothDamp(_viewCenter, target, ref _viewVelocity, _shotMoveTime);
                _zoom = Mathf.SmoothDamp(_zoom, zoomTarget, ref _zoomVelocity, _shotMoveTime);
            }

            _cameraFocus.position = _viewCenter;
            roomCamera.SetZoom(_zoom, _cameraFocus);
        }

        public void Play()
        {
            if (IsPlaying)
                return;

            _routine = StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            IsPlaying = true;
            EventBus<CutsceneStartedEvent>.Raise(default);
            player.Landed += OnLanded;
            SetPlayerControl(false);
            _spriteRestPosition = _sprite.localPosition;

            // The player waits off screen (or hidden) until their fall.
            Vector2 fallStart = GetFallStart();
            _body.simulated = false;
            player.Teleport(fallStart);
            _animator.Play(fallState, 0, 0f);
            if (ceilingOpening == null)
                _spriteRenderer.enabled = false;

            if (HasSpirit)
            {
                _wanderCenter = spiritAnchor != null ? (Vector2)spiritAnchor.position : RoomCenter();
                _wanderPhase = 0f;
                _wanderSpeed = 1f;
                companion.BeginScripted(_wanderCenter);
                _wanderRoutine = StartCoroutine(Wander());
            }

            Transform spirit = HasSpirit ? companion.transform : null;
            Func<Vector2> openingFocus = HasSpirit ? () => spirit.position : (Func<Vector2>)(() => fallStart);
            StartDirecting(openingFocus, snap: true);
            yield return OpenScreen(openingFocus);

            if (HasSpirit)
            {
                yield return new WaitForSeconds(wanderTime);
                yield return Rumble();
            }

            // To the opening, then drop.
            Vector2 dropPoint = ceilingOpening != null ? (Vector2)ceilingOpening.position : fallStart;
            Shoot(() => dropPoint, cameraMoveTime);
            yield return WaitForCamera();
            yield return new WaitForSeconds(holdBeforeFall);
            yield return Fall();

            _animator.Play(knockedOutState, 0, 0f);
            yield return new WaitForSeconds(surpriseDelay);
            if (HasSpirit)
                yield return SpiritWakesPlayer();

            yield return Tremble(firstTrembleStrength, firstTrembleDuration);
            yield return new WaitForSeconds(pauseBetweenTrembles);
            yield return Tremble(quickTrembleStrength, quickTrembleDuration);
            yield return new WaitForSeconds(quickTrembleGap);
            yield return Tremble(quickTrembleStrength, quickTrembleDuration);
            yield return new WaitForSeconds(pauseBeforeHop);

            // Hop up; the regular jump/fall/land animations take it from here.
            _landed = false;
            player.Launch(new Vector2(0f, hopSpeed));
            _animator.Play(jumpState, 0, 0f);
            while (!_landed)
                yield return null;

            // The spirit takes its place beside the player.
            if (HasSpirit)
            {
                companion.SetFacing(player.FacingDirection);
                if (summonCompanion && maskManager != null)
                    maskManager.SummonCompanion();
                else
                    companion.EndScripted();
                yield return new WaitForSeconds(pauseBeforeZoomOut);
            }

            yield return ZoomOut();
            Finish();
        }

        IEnumerator OpenScreen(Func<Vector2> focus)
        {
            if (_transition == null)
                yield break;

            _transition.Cover();

            // Counted in capped steps: the first frames after loading can stall for most of a second and
            // would otherwise swallow the black screen.
            for (float black = 0f; black < blackScreenTime; black += Mathf.Min(Time.unscaledDeltaTime, MaxBlackStep))
                yield return null;

            yield return _transition.Open(focus);
        }

        IEnumerator Rumble()
        {
            Room room = RoomManager.CurrentRoom;
            _rumbling = true;
            EventBus<RumbleStartedEvent>.Raise(new RumbleStartedEvent(room != null ? room.Bounds : new Rect(_wanderCenter, Vector2.one)));

            yield return new WaitForSeconds(Mathf.Min(puzzledDelay, rumbleDuration));
            _tween = DOVirtual.Float(_wanderSpeed, 0f, spiritStopTime, speed => _wanderSpeed = speed).SetEase(Ease.OutQuad).SetLink(gameObject);
            companion.ShowEmote(Emote.Question);

            // Glances left and right while the room shakes.
            float remaining = rumbleDuration - puzzledDelay;
            int look = -1;
            while (remaining > 0f)
            {
                float step = lookAroundInterval > 0f ? Mathf.Min(lookAroundInterval, remaining) : remaining;
                yield return new WaitForSeconds(step);
                remaining -= step;
                if (lookAroundInterval > 0f && remaining > 0f)
                {
                    companion.SetFacing(look);
                    look = -look;
                }
            }

            StopRumble();
            yield return new WaitForSeconds(pauseAfterRumble);
            companion.HideEmote();
            StopWandering();
        }

        IEnumerator Fall()
        {
            _landed = false;
            _spriteRenderer.enabled = true;
            _body.simulated = true;

            // The camera holds on the opening until the player drops past it, then follows them down.
            if (ceilingOpening != null)
            {
                while (player.transform.position.y > ceilingOpening.position.y && !_landed)
                    yield return null;
            }

            Shoot(() => player.transform.position, 0f);
            while (!_landed)
                yield return null;
        }

        IEnumerator SpiritWakesPlayer()
        {
            Transform spirit = companion.transform;
            int head = player.FacingDirection;
            Func<Vector2> playerCenter = () => player.transform.position;

            // Both in view while the spirit notices and flies over; the "!" waits for the camera to get there.
            Shoot(() => (playerCenter() + (Vector2)spirit.position) * 0.5f, cameraMoveTime, () => FitZoom(spirit.position, playerCenter()));
            yield return WaitForCamera();
            companion.SetFacing(playerCenter().x < spirit.position.x ? -1 : 1);
            companion.ShowEmote(Emote.Exclamation);
            yield return new WaitForSeconds(surpriseTime);
            companion.HideEmote();

            Vector3 spot = player.transform.position + new Vector3(nudgeSpot.x * head, nudgeSpot.y, 0f);
            float flight = Mathf.Max(0.4f, Vector2.Distance(spirit.position, spot) / spiritSpeed);
            companion.SetFacing(spot.x < spirit.position.x ? -1 : 1);
            _spiritTween = spirit.DOMove(spot, flight).SetEase(Ease.InOutSine).SetLink(gameObject);
            yield return _spiritTween.WaitForCompletion();
            companion.SetFacing(-head);

            // Once, a pause, then twice in quick succession.
            yield return Nudge(-head);
            yield return new WaitForSeconds(pauseAfterFirstNudge);
            yield return Nudge(-head);
            yield return new WaitForSeconds(quickNudgeGap);
            yield return Nudge(-head);
            yield return new WaitForSeconds(pauseBeforeTremble);
        }

        /// <summary>The spirit bumps into the player (towards <paramref name="direction"/>) and backs off.</summary>
        IEnumerator Nudge(int direction)
        {
            Transform spirit = companion.transform;
            Vector3 rest = spirit.position;
            Vector3 bump = rest + new Vector3(nudgeDistance * direction, 0f, 0f);
            _spiritTween = DOTween.Sequence()
                .Append(spirit.DOMove(bump, nudgeInTime).SetEase(Ease.InQuad))
                .AppendCallback(() => PushPlayerSprite(direction))
                .Append(spirit.DOMove(rest, nudgeOutTime).SetEase(Ease.OutQuad))
                .SetLink(gameObject);
            yield return _spiritTween.WaitForCompletion();
        }

        void PushPlayerSprite(int direction)
        {
            _tween?.Kill();
            _sprite.localPosition = _spriteRestPosition;

            // The punch is in local space, which is mirrored while the player faces left.
            float mirror = _sprite.parent != null && _sprite.parent.lossyScale.x < 0f ? -1f : 1f;
            _tween = _sprite.DOPunchPosition(new Vector3(nudgePush * direction * mirror, 0f, 0f), 0.2f, vibrato: 4, elasticity: 0.5f)
                .OnComplete(() => _sprite.localPosition = _spriteRestPosition)
                .SetLink(gameObject);
        }

        IEnumerator Tremble(float strength, float duration)
        {
            if (strength <= 0f || duration <= 0f)
                yield break;

            _tween?.Kill();
            _sprite.localPosition = _spriteRestPosition;
            _tween = _sprite.DOShakePosition(duration, new Vector3(strength, 0f, 0f), vibrato: 25, randomness: 0f, snapping: false, fadeOut: true)
                .SetLink(gameObject);
            yield return _tween.WaitForCompletion();
            _sprite.localPosition = _spriteRestPosition;
        }

        IEnumerator ZoomOut()
        {
            // From wherever the camera is now out to the whole room.
            _directing = false;
            _cameraFocus.position = _viewCenter;
            if (controlDuringZoomOut)
                SetPlayerControl(true);

            _tween = DOVirtual.Float(_zoom, 1f, zoomOutDuration, zoom => roomCamera.SetZoom(zoom, _cameraFocus))
                .SetEase(zoomOutEase)
                .SetLink(gameObject);
            yield return _tween.WaitForCompletion();
        }

        IEnumerator Wander()
        {
            while (true)
            {
                float delta = Time.deltaTime * _wanderSpeed * 2f * Mathf.PI / wanderPeriod;
                _wanderPhase += delta;
                companion.transform.position = _wanderCenter + new Vector2(
                    Mathf.Sin(_wanderPhase) * wanderSize.x,
                    Mathf.Sin(_wanderPhase * 2f) * wanderSize.y);

                if (_wanderSpeed > 0.2f)
                    companion.SetFacing(Mathf.Cos(_wanderPhase) >= 0f ? 1 : -1);
                yield return null;
            }
        }

        void StopWandering()
        {
            if (_wanderRoutine != null)
                StopCoroutine(_wanderRoutine);
            _wanderRoutine = null;
        }

        void StopRumble()
        {
            if (!_rumbling)
                return;

            _rumbling = false;
            EventBus<RumbleStoppedEvent>.Raise(default);
        }

        void StartDirecting(Func<Vector2> shot, bool snap)
        {
            _directing = true;
            Shoot(shot, cameraMoveTime);
            if (!snap)
                return;

            // The room camera may not know its room yet, so the first directed frame snaps again.
            _snapCamera = true;
            _zoom = introZoom;
            _viewCenter = roomCamera.GetViewCenter(shot(), introZoom);
            _viewVelocity = Vector2.zero;
            _zoomVelocity = 0f;
            _cameraFocus.position = _viewCenter;
            roomCamera.SetZoom(_zoom, _cameraFocus);
        }

        void Shoot(Func<Vector2> shot, float moveTime, Func<float> zoom = null)
        {
            _shot = shot;
            _shotMoveTime = moveTime;
            _shotZoom = zoom;
        }

        IEnumerator WaitForCamera()
        {
            for (float waited = 0f; waited < CameraTimeout; waited += Time.deltaTime)
            {
                float zoomTarget = _shotZoom?.Invoke() ?? introZoom;
                Vector2 target = roomCamera.GetViewCenter(_shot(), zoomTarget);
                if (Vector2.Distance(_viewCenter, target) < CameraArrivedDistance && Mathf.Abs(_zoom - zoomTarget) < 0.01f)
                    yield break;
                yield return null;
            }
        }

        /// <summary>Zoom that keeps both points in view with some margin, never wider than the room.</summary>
        float FitZoom(Vector2 a, Vector2 b)
        {
            Room room = RoomManager.CurrentRoom;
            if (room == null)
                return introZoom;

            Vector2 span = new(Mathf.Abs(a.x - b.x) + twoShotMargin.x * 2f, Mathf.Abs(a.y - b.y) + twoShotMargin.y * 2f);
            float zoom = Mathf.Min(room.Size.x / span.x, room.Size.y / span.y);
            return Mathf.Clamp(zoom, 1f, introZoom);
        }

        static Vector2 RoomCenter() => RoomManager.CurrentRoom != null ? RoomManager.CurrentRoom.Center : Vector2.zero;

        /// <summary>
        /// Start of the fall: just above the top of the room over the ceiling opening (off screen); or, without
        /// one, above the placed position but inside the room.
        /// </summary>
        Vector2 GetFallStart()
        {
            Room room = RoomManager.CurrentRoom;
            if (ceilingOpening != null)
            {
                // The camera never shows past the room's top, so above it is off screen.
                float top = room != null ? Mathf.Max(room.Bounds.yMax, ceilingOpening.position.y) : ceilingOpening.position.y;
                return new Vector2(ceilingOpening.position.x, top + offscreenMargin);
            }

            Vector2 start = (Vector2)player.transform.position + Vector2.up * fallHeight;
            if (room != null)
                start.y = Mathf.Min(start.y, room.Bounds.yMax - 1.5f);
            return start;
        }

        void OnLanded(float impactSpeed) => _landed = true;

        void Finish()
        {
            Cleanup();
            SetPlayerControl(true);
            StartCoroutine(InvokeFinishedAfterDelay());
        }

        IEnumerator InvokeFinishedAfterDelay()
        {
            if (finishedEventDelay > 0f)
                yield return new WaitForSeconds(finishedEventDelay);

            onFinished.Invoke();
        }

        /// <summary>Stops early, e.g. if the player died mid-cutscene (the respawn restores control).</summary>
        void Abort()
        {
            if (_routine != null)
                StopCoroutine(_routine);

            // Also runs while the scene unloads, when the player may already be gone.
            if (_body != null)
                _body.simulated = true;
            if (_spriteRenderer != null)
                _spriteRenderer.enabled = true;
            if (_sprite != null)
                _sprite.localPosition = _spriteRestPosition;
            if (companion != null)
                companion.EndScripted();
            Cleanup();
        }

        void Cleanup()
        {
            _routine = null;
            StopWandering();
            StopRumble();
            _tween?.Kill();
            _tween = null;
            _spiritTween?.Kill();
            _spiritTween = null;
            _directing = false;
            if (player != null)
                player.Landed -= OnLanded;
            if (roomCamera != null)
                roomCamera.SetZoom(1f);
            IsPlaying = false;
            EventBus<CutsceneEndedEvent>.Raise(default);
        }

        void SetPlayerControl(bool hasControl)
        {
            player.SetControl(hasControl);
            if (_attack != null)
                _attack.enabled = hasControl;
        }

        void OnPlayerDied(PlayerDiedEvent evt)
        {
            if (IsPlaying)
                Abort();
        }

        static IRespawnTransition FindTransition()
        {
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude))
            {
                if (behaviour is IRespawnTransition transition)
                    return transition;
            }

            return null;
        }
    }
}
