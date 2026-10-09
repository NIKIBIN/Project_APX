using APX.Combat;
using APX.Core;
using UnityEngine;
using UnityEngine.Events;

namespace APX.Level
{
    /// <summary>Puzzle switch toggled by the player's attack. Wire its events to gates, platforms, etc.</summary>
    public sealed class HitSwitch : MonoBehaviour, IDamageable, IResettable
    {
        [SerializeField] bool startsOn;
        [Tooltip("Once switched on, further hits are ignored.")]
        [SerializeField] bool oneShot = true;
        [SerializeField] SpriteRenderer indicator;
        [SerializeField] Color offColor = new(0.85f, 0.3f, 0.3f);
        [SerializeField] Color onColor = new(0.35f, 0.9f, 0.45f);
        [SerializeField] UnityEvent onSwitchedOn = new();
        [SerializeField] UnityEvent onSwitchedOff = new();

        public bool IsOn { get; private set; }

        public Team Team => Team.Neutral;

        public UnityEvent OnSwitchedOn => onSwitchedOn;

        public UnityEvent OnSwitchedOff => onSwitchedOff;

        void Awake() => SetState(startsOn, notify: false);

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
