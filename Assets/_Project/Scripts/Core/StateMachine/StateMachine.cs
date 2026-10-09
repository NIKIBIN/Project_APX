using System;
using System.Collections.Generic;

namespace APX.Core
{
    public interface IState
    {
        void Enter();
        void Tick(float deltaTime);
        void Exit();
    }

    /// <summary>
    /// Minimal predicate-driven finite state machine. States hold behaviour; transitions are declared
    /// by the owner, so the same state classes can be rewired for different actors.
    /// </summary>
    public sealed class StateMachine
    {
        readonly struct Transition
        {
            public readonly IState To;
            public readonly Func<bool> Condition;

            public Transition(IState to, Func<bool> condition)
            {
                To = to;
                Condition = condition;
            }
        }

        static readonly List<Transition> s_noTransitions = new();

        readonly Dictionary<IState, List<Transition>> _transitions = new();
        readonly List<Transition> _anyTransitions = new();
        List<Transition> _currentTransitions = s_noTransitions;

        public IState CurrentState { get; private set; }

        public event Action<IState> StateChanged;

        public void AddTransition(IState from, IState to, Func<bool> condition)
        {
            if (!_transitions.TryGetValue(from, out List<Transition> transitions))
            {
                transitions = new List<Transition>();
                _transitions.Add(from, transitions);
            }

            transitions.Add(new Transition(to, condition));
        }

        /// <summary>Transition evaluated from every state (e.g. "stunned", "dead").</summary>
        public void AddAnyTransition(IState to, Func<bool> condition) =>
            _anyTransitions.Add(new Transition(to, condition));

        /// <summary>Switches state; <paramref name="force"/> re-enters the state even if it is already current.</summary>
        public void SetState(IState state, bool force = false)
        {
            if (state == CurrentState && !force)
                return;

            CurrentState?.Exit();
            CurrentState = state;
            _currentTransitions = state != null && _transitions.TryGetValue(state, out List<Transition> transitions)
                ? transitions
                : s_noTransitions;
            CurrentState?.Enter();
            StateChanged?.Invoke(CurrentState);
        }

        public void Tick(float deltaTime)
        {
            if (TryGetTransition(out IState next))
                SetState(next);

            CurrentState?.Tick(deltaTime);
        }

        bool TryGetTransition(out IState next)
        {
            foreach (Transition transition in _anyTransitions)
            {
                if (transition.To != CurrentState && transition.Condition())
                {
                    next = transition.To;
                    return true;
                }
            }

            foreach (Transition transition in _currentTransitions)
            {
                if (transition.Condition())
                {
                    next = transition.To;
                    return true;
                }
            }

            next = null;
            return false;
        }
    }
}
