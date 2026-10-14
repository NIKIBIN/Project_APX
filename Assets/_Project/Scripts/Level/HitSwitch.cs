using APX.Combat;
using APX.Core;
using UnityEngine;
using UnityEngine.Events;

namespace APX.Level
{
    /// <summary>
    /// Puzzle switch toggled by the player's attack. Wire its events to gates, platforms, etc.
    /// The indicator shows the state by brightness (an unlit stone when off, a lit lamp when on), never by hue
    /// alone: the world is seen through a red-green colour blindness filter until "Differentiate" is worn.
    /// </summary>
    public sealed class HitSwitch : MonoBehaviour, IDamageable, IResettable
    {
        [SerializeField] bool startsOn;
        [Tooltip("Once switched on, further hits are ignored.")]
        [SerializeField] bool oneShot = true;
        [SerializeField] SpriteRenderer indicator;
        [Tooltip("Keep off and on far apart in brightness: hue differences vanish under the colour blindness filter.")]
        [SerializeField] Color offColor = new(0.45f, 0.47f, 0.55f);
        [SerializeField] Color onColor = new(0.92f, 0.97f, 1f);
        [Tooltip("Indicator size when on, relative to its size when off (a second cue besides brightness).")]
        [SerializeField, Min(0.1f)] float onScale = 1.25f;

        Vector3 _indicatorOffScale;
        [SerializeField] UnityEvent onSwitchedOn = new();
        [SerializeField] UnityEvent onSwitchedOff = new();

        public bool IsOn { get; private set; }

        public Team Team => Team.Neutral;

        public UnityEvent OnSwitchedOn => onSwitchedOn;

        public UnityEvent OnSwitchedOff => onSwitchedOff;

        void Awake()
        {
            if (indicator != null)
                _indicatorOffScale = indicator.transform.localScale;
            SetState(startsOn, notify: false);
        }

        public bool TryReceiveDamage(in DamageInfo damage)
        {
            if (damage.SourceTeam != Team.Player || (oneShot && IsOn))
                return false;

            SetState(!IsOn, notify: true);
            return true;
        }

        /// <summary>Restores the initial state silently; listeners such as <see cref="Gate"/> reset themselves.</summary>
        public void ResetState() => SetState(startsOn, notify: false);

        /// <summary>Switches off (notifying listeners) so it can be hit again, e.g. when a timed door closes.</summary>
        public void TurnOff()
        {
            if (IsOn)
                SetState(false, notify: true);
        }

        void SetState(bool on, bool notify)
        {
            IsOn = on;

            if (indicator != null)
            {
                Color color = on ? onColor : offColor;
                color.a = indicator.color.a; // Alpha belongs to mask visibility.
                indicator.color = color;
                indicator.transform.localScale = on ? _indicatorOffScale * onScale : _indicatorOffScale;
            }

            if (!notify)
                return;

            if (on)
                onSwitchedOn.Invoke();
            else
                onSwitchedOff.Invoke();
        }
    }
}
