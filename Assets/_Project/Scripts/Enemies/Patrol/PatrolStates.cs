using APX.Core;

namespace APX.Enemies
{
    /// <summary>Walks forward until the path is blocked.</summary>
    sealed class PatrolWalkState : IState
    {
        readonly PatrolEnemy _owner;

        public PatrolWalkState(PatrolEnemy owner) => _owner = owner;

        public bool IsBlocked { get; private set; }

        public void Enter() => IsBlocked = false;

        public void Tick(float deltaTime)
        {
            IsBlocked = _owner.IsPathBlocked();
            if (IsBlocked)
                _owner.Stop();
            else
                _owner.Walk();
        }

        public void Exit() => _owner.Stop();
    }

    /// <summary>Waits briefly, then turns around on exit.</summary>
    sealed class PatrolTurnState : IState
    {
        readonly PatrolEnemy _owner;
        float _remaining;

        public PatrolTurnState(PatrolEnemy owner) => _owner = owner;

        public bool IsComplete => _remaining <= 0f;

        public void Enter()
        {
            _remaining = _owner.TurnPause;
            _owner.Stop();
        }

        public void Tick(float deltaTime) => _remaining -= deltaTime;

        public void Exit() => _owner.TurnAround();
    }
}
