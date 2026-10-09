using APX.Combat;
using APX.Core;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace APX.Level
{
    /// <summary>
    /// Heavy stone door opened by a <see cref="HitSwitch"/>: it rumbles, slowly grinds open, stays open for a
    /// while, then drops shut and switches its trigger off so the player has to hit it again. Hitting the switch
    /// while the door is dropping sends it back up. The cycle runs on local time, so "Nice and Slow" stretches it.
    /// The pivot is the bottom cell, so the GameObject Brush paints it standing on the floor.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class StoneDoor : MonoBehaviour, IResettable
    {
        enum DoorState
        {
            Closed,
            Opening,
            Open,
            Closing,
        }

        const float LeadingFaceTolerance = 0.05f;
        const float PinCheckDistance = 0.1f;

        [Header("Trigger")]
        [Tooltip("Switch that opens this door. It is switched off when the door starts closing. " +
                 "Leave empty to drive the door from any UnityEvent through Open().")]
        [SerializeField] HitSwitch trigger;

        [Header("Motion")]
        [Tooltip("Where the door travels when open, relative to its closed position: (0, 4) slides up, (0, -4) sinks into the floor.")]
        [SerializeField] Vector2 openOffset = new(0f, 4f);
        [SerializeField, Min(0f)] float openDuration = 2.5f;
        [SerializeField] Ease openEase = Ease.InOutSine;
        [Tooltip("Seconds the door stays fully open before it starts closing.")]
        [SerializeField, Min(0f)] float stayOpenDuration = 3f;
        [SerializeField, Min(0f)] float closeDuration = 0.6f;
        [SerializeField] Ease closeEase = Ease.InQuad;

        [Header("Shake")]
        [Tooltip("Child that shakes. Only the visuals shake; the collider moves smoothly.")]
        [SerializeField] Transform visual;
        [Tooltip("Rumble before the door starts moving, and again right before it drops.")]
        [SerializeField, Min(0f)] float rumbleDuration = 0.5f;
        [SerializeField, Min(0f)] float rumbleStrength = 0.08f;
        [Tooltip("Lighter shake while the stone grinds open.")]
        [SerializeField, Min(0f)] float grindStrength = 0.03f;
        [SerializeField, Min(0f)] float impactDuration = 0.25f;
        [SerializeField, Min(0f)] float impactStrength = 0.15f;
        [SerializeField, Min(1)] int shakeVibrato = 30;

        [Header("Crushing")]
        [Tooltip("A closing door kills the player when it pins them against the level.")]
        [SerializeField] bool crushesPlayer = true;
        [SerializeField] LayerMask crushAgainst = 1;

        [Header("Events")]
        [SerializeField] UnityEvent onStartOpening = new();
        [SerializeField] UnityEvent onStartClosing = new();
        [SerializeField] UnityEvent onClosed = new();

        readonly RaycastHit2D[] _pinHits = new RaycastHit2D[1];
        Rigidbody2D _body;
        BoxCollider2D _collider;
        ITimeScaleSource _timeSource;
        ContactFilter2D _pinFilter;
        Vector2 _closedPosition;
        Vector3 _visualRestPosition;
        float _openAmount;
        DoorState _state;
        Sequence _cycle;

        public bool IsClosed => _state == DoorState.Closed;

        void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Kinematic;
            _collider = GetComponent<BoxCollider2D>();
            _timeSource = GetComponentInParent<ITimeScaleSource>(true);
            _closedPosition = transform.position;
            if (visual != null)
                _visualRestPosition = visual.localPosition;

            _pinFilter = new ContactFilter2D { useTriggers = false };
            _pinFilter.SetLayerMask(crushAgainst);
        }

        void OnEnable()
        {
            if (trigger != null)
                trigger.OnSwitchedOn.AddListener(Open);
        }

        void OnDisable()
        {
            if (trigger != null)
                trigger.OnSwitchedOn.RemoveListener(Open);
            StopCycle();
        }

        void Update()
        {
            if (_cycle != null && _cycle.IsActive())
                _cycle.timeScale = _timeSource.ScaleOrDefault();
        }

        void FixedUpdate() => _body.MovePosition(_closedPosition + openOffset * _openAmount);

        /// <summary>Starts the open cycle. Ignored while opening or open; reverses a door that is closing.</summary>
        public void Open()
        {
            if (_state == DoorState.Opening || _state == DoorState.Open)
                return;

            StopCycle();
            _state = DoorState.Opening;
            onStartOpening.Invoke();
            EventBus<DoorRumbledEvent>.Raise(new DoorRumbledEvent(GetContactPoint(), _collider.bounds.size.x));

            // Reopening mid-drop only travels the remaining distance, at the same speed.
            float travelDuration = openDuration * (1f - _openAmount);
            _cycle = DOTween.Sequence()
                .Append(Shake(rumbleDuration, rumbleStrength))
                .Append(TweenOpenAmount(1f, travelDuration, openEase))
                .Join(Shake(travelDuration, grindStrength))
                .AppendCallback(() => _state = DoorState.Open)
                .AppendInterval(stayOpenDuration)
                .AppendCallback(BeginClosing)
                .Append(Shake(rumbleDuration, rumbleStrength))
                .Append(TweenOpenAmount(0f, closeDuration, closeEase))
                .AppendCallback(FinishClosing)
                .Append(Shake(impactDuration, impactStrength))
                .SetLink(gameObject);
            _cycle.timeScale = _timeSource.ScaleOrDefault();
        }

        public void ResetState()
        {
            // Not initialised yet: it was disabled in the scene on purpose, leave it alone.
            if (_body == null)
                return;

            StopCycle();
            _state = DoorState.Closed;
            _openAmount = 0f;
            transform.position = _closedPosition;
            _body.position = _closedPosition;
        }

        void BeginClosing()
        {
            _state = DoorState.Closing;
            if (trigger != null)
                trigger.TurnOff();
            onStartClosing.Invoke();
            EventBus<DoorRumbledEvent>.Raise(new DoorRumbledEvent(GetContactPoint(), _collider.bounds.size.x));
        }

        void FinishClosing()
        {
            _state = DoorState.Closed;
            onClosed.Invoke();
            EventBus<DoorSlammedEvent>.Raise(new DoorSlammedEvent(GetContactPoint(atClosedPosition: true), _collider.bounds.size.x));
        }

        /// <summary>Centre of the face that meets the floor (or ceiling) when closed: the leading face when closing.</summary>
        /// <param name="atClosedPosition">Measure at the closed pose (the body may still be catching up with the tween).</param>
        Vector2 GetContactPoint(bool atClosedPosition = false)
        {
            Bounds bounds = _collider.bounds;
            Vector2 center = bounds.center;
            if (atClosedPosition)
                center += _closedPosition - _body.position;

            Vector2 closeDirection = -openOffset.normalized;
            return center + Vector2.Scale(closeDirection, bounds.extents);
        }

        void StopCycle()
        {
            _cycle?.Kill();
            _cycle = null;
            // A shake killed halfway would leave the visuals offset.
            if (visual != null)
                visual.localPosition = _visualRestPosition;
        }

        Tween TweenOpenAmount(float target, float duration, Ease ease) =>
            DOTween.To(() => _openAmount, value => _openAmount = value, target, duration).SetEase(ease);

        Tween Shake(float duration, float strength)
        {
            if (visual == null || duration <= 0f || strength <= 0f)
                return DOTween.Sequence().AppendInterval(duration);

            // Mostly sideways: stone grinding in its frame.
            var strength3 = new Vector3(strength, strength * 0.35f, 0f);
            return visual.DOShakePosition(duration, strength3, shakeVibrato, randomness: 90f, snapping: false, fadeOut: true);
        }

        void OnCollisionEnter2D(Collision2D collision) => TryCrush(collision);

        void OnCollisionStay2D(Collision2D collision) => TryCrush(collision);

        void TryCrush(Collision2D collision)
        {
            if (!crushesPlayer || _state != DoorState.Closing || collision.rigidbody == null)
                return;

            IDamageable target = collision.collider.GetComponentInParent<IDamageable>();
            if (target == null || target.Team != Team.Player)
                return;

            // Only the face leading the closing motion crushes, and only if the player has nowhere to go.
            Vector2 closeDirection = -openOffset.normalized;
            if (!IsInFrontOfLeadingFace(collision.collider.bounds.center, closeDirection)
                || collision.rigidbody.Cast(closeDirection, _pinFilter, _pinHits, PinCheckDistance) == 0)
                return;

            target.TryReceiveDamage(new DamageInfo(Team.Environment, gameObject, collision.GetContact(0).point));
        }

        bool IsInFrontOfLeadingFace(Vector2 point, Vector2 direction)
        {
            Bounds bounds = _collider.bounds;
            float distanceAlong = Vector2.Dot(point - (Vector2)bounds.center, direction);
            float halfDepth = Mathf.Abs(Vector2.Dot(bounds.extents, direction));
            return distanceAlong >= halfDepth - LeadingFaceTolerance;
        }

        void OnDrawGizmosSelected()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null)
                return;

            Vector2 closed = Application.isPlaying ? _closedPosition : (Vector2)transform.position;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireCube(closed + openOffset + box.offset, box.size);
        }
    }
}
