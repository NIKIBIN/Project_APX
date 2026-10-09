using APX.Core;
using UnityEngine;

namespace APX.Enemies
{
    /// <summary>Waits for the fire cooldown.</summary>
    sealed class ShooterIdleState : IState
    {
        readonly ShooterEnemy _owner;
        float _cooldown;

        public ShooterIdleState(ShooterEnemy owner) => _owner = owner;

        public bool IsReady => _cooldown <= 0f;

        public void Enter()
        {
            _cooldown = _owner.FireCooldown;
            _owner.SetTelegraph(0f);
        }

        public void Tick(float deltaTime) => _cooldown -= deltaTime;

        public void Exit() { }
    }

    /// <summary>Telegraphs the shot by blending the body colour towards the warning colour.</summary>
    sealed class ShooterWindupState : IState
    {
        readonly ShooterEnemy _owner;
        float _elapsed;

        public ShooterWindupState(ShooterEnemy owner) => _owner = owner;

        public bool IsComplete => _elapsed >= _owner.WindupDuration;

        public void Enter() => _elapsed = 0f;

        public void Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            float duration = _owner.WindupDuration;
            _owner.SetTelegraph(duration > 0f ? Mathf.Clamp01(_elapsed / duration) : 1f);
        }

        public void Exit() => _owner.SetTelegraph(0f);
    }

    /// <summary>Fires once on enter; the machine immediately returns to idle.</summary>
    sealed class ShooterFireState : IState
    {
        readonly ShooterEnemy _owner;

        public ShooterFireState(ShooterEnemy owner) => _owner = owner;

        public void Enter() => _owner.Fire();

        public void Tick(float deltaTime) { }

        public void Exit() { }
    }
}
