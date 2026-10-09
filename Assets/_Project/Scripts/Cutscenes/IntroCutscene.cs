using System.Collections;
using APX.Core;
using APX.Masks;
using APX.Player;
using APX.Rooms;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace APX.Cutscenes
{
    /// <summary>
    /// Opening cutscene: the camera starts zoomed in on the player, who falls in knocked out, lies still for
    /// a moment, shivers twice and hops to their feet. Then the camera zooms out to the whole room and
    /// control is handed over; shortly after, the hint companion appears and On Finished fires.
    /// Ordinary game systems (gravity, landing dust, squash) play along.
    /// </summary>
    public sealed class IntroCutscene : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Defaults to the PlayerController in the scene.")]
        [SerializeField] PlayerController player;
        [Tooltip("Defaults to the RoomCamera in the scene.")]
        [SerializeField] RoomCamera roomCamera;
        [SerializeField] bool playOnStart = true;

        [Header("Animator States")]
        [SerializeField] string fallState = "IntroFall";
        [SerializeField] string knockedOutState = "IntroKnockedOut";
        [SerializeField] string jumpState = "Jump";

        [Header("Camera")]
        [Tooltip("Starting zoom: 1 shows the whole room, 2.5 shows 40% of it.")]
        [SerializeField, Min(1f)] float introZoom = 2.5f;
        [SerializeField, Min(0f)] float zoomOutDuration = 1.2f;
        [SerializeField] Ease zoomOutEase = Ease.InOutSine;
        [Tooltip("Give the player control as soon as the zoom-out starts instead of when it ends.")]
        [SerializeField] bool controlDuringZoomOut;

        [Header("Fall")]
        [Tooltip("How far above the player's placed position the fall starts (kept inside the room).")]
        [SerializeField, Min(0f)] float fallHeight = 6f;
        [Tooltip("Seconds hanging in the air before the fall starts.")]
        [SerializeField, Min(0f)] float startDelay = 0.4f;

        [Header("Waking Up")]
        [Tooltip("Seconds lying still after landing.")]
        [SerializeField, Min(0f)] float knockedOutTime = 1f;
        [SerializeField, Min(0f)] float firstShakeStrength = 0.04f;
        [SerializeField, Min(0f)] float firstShakeDuration = 0.25f;
        [SerializeField, Min(0f)] float pauseBetweenShakes = 0.5f;
        [SerializeField, Min(0f)] float secondShakeStrength = 0.09f;
        [SerializeField, Min(0f)] float secondShakeDuration = 0.4f;
        [SerializeField, Min(0f)] float pauseBeforeHop = 0.35f;
        [Tooltip("Upward speed of the hop to the feet.")]
        [SerializeField, Min(0f)] float hopSpeed = 9f;

        [Header("After the Intro")]
        [Tooltip("Seconds after the cutscene ends (and control returns) before the companion appears and On Finished fires.")]
        [SerializeField, Min(0f)] float finishedEventDelay = 0.6f;
        [Tooltip("Summon the hint companion once the intro has played (through the scene's MaskManager).")]
        [SerializeField] bool summonCompanion = true;
        [Tooltip("Defaults to the MaskManager in the scene.")]
        [SerializeField] MaskManager maskManager;
        [Tooltip("Extra actions once the intro has played through.")]
        [SerializeField] UnityEvent onFinished = new();

        PlayerAttack _attack;
        Rigidbody2D _body;
        Animator _animator;
        Transform _sprite;
        Vector3 _spriteRestPosition;
        Coroutine _routine;
        Tween _tween;
        bool _landed;

        public bool IsPlaying { get; private set; }

        void Awake()
        {
            if (player == null)
                player = FindAnyObjectByType<PlayerController>();
            if (roomCamera == null)
                roomCamera = FindAnyObjectByType<RoomCamera>();

            _attack = player.GetComponent<PlayerAttack>();
            _body = player.GetComponent<Rigidbody2D>();
            _animator = player.GetComponentInChildren<Animator>();
            _sprite = _animator.transform;
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
            roomCamera.SetZoom(introZoom, player.transform);

            // Hang in the air, knocked out, then drop.
            player.Teleport(GetFallStart());
            _animator.Play(fallState, 0, 0f);
            _body.simulated = false;
            yield return new WaitForSeconds(startDelay);
            _body.simulated = true;
            yield return WaitForLanding();

            _animator.Play(knockedOutState, 0, 0f);
            yield return new WaitForSeconds(knockedOutTime);
            yield return Shake(firstShakeStrength, firstShakeDuration);
            yield return new WaitForSeconds(pauseBetweenShakes);
            yield return Shake(secondShakeStrength, secondShakeDuration);
            yield return new WaitForSeconds(pauseBeforeHop);

            // Hop up; the regular jump/fall/land animations take it from here.
            player.Launch(new Vector2(0f, hopSpeed));
            _animator.Play(jumpState, 0, 0f);
            yield return WaitForLanding();

            if (controlDuringZoomOut)
                SetPlayerControl(true);

            _tween = DOVirtual.Float(introZoom, 1f, zoomOutDuration, zoom => roomCamera.SetZoom(zoom, player.transform))
                .SetEase(zoomOutEase)
                .SetLink(gameObject);
            yield return _tween.WaitForCompletion();

            Finish();
        }

        /// <summary>Start of the fall: above the placed position, but inside the room so the camera can follow.</summary>
        Vector2 GetFallStart()
        {
            Vector2 start = (Vector2)player.transform.position + Vector2.up * fallHeight;
            Room room = RoomManager.CurrentRoom;
            if (room != null)
                start.y = Mathf.Min(start.y, room.Bounds.yMax - 1.5f);
            return start;
        }

        IEnumerator WaitForLanding()
        {
            _landed = false;
            while (!_landed)
                yield return null;
        }

        IEnumerator Shake(float strength, float duration)
        {
            if (strength <= 0f || duration <= 0f)
                yield break;

            _tween = _sprite.DOShakePosition(duration, new Vector3(strength, 0f, 0f), vibrato: 25, randomness: 0f, snapping: false, fadeOut: true)
                .SetLink(gameObject);
            yield return _tween.WaitForCompletion();
            _sprite.localPosition = _spriteRestPosition;
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

            if (summonCompanion)
            {
                if (maskManager == null)
                    maskManager = FindAnyObjectByType<MaskManager>();
                if (maskManager != null)
                    maskManager.SummonCompanion();
                else
                    Debug.LogWarning($"{name}: no {nameof(MaskManager)} in the scene, so the companion can't be summoned.", this);
            }

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
            if (_sprite != null)
                _sprite.localPosition = _spriteRestPosition;
            Cleanup();
        }

        void Cleanup()
        {
            _routine = null;
            _tween?.Kill();
            _tween = null;
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
    }
}
