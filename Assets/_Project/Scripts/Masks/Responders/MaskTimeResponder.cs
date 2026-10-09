using APX.Core;
using UnityEngine;

namespace APX.Masks
{
    /// <summary>
    /// Local time scale for world objects (enemies, projectiles, moving hazards), consumed through
    /// <see cref="ITimeScaleSource"/>. By default: slow while "Nice and Slow" is worn, accelerated by
    /// the curse otherwise. Put it on a single object or on a parent to drive a whole group.
    /// The player never has one, so it always acts in real time.
    /// </summary>
    public sealed class MaskTimeResponder : MaskResponder, ITimeScaleSource
    {
        [Tooltip("Time scale while the condition is met (mask worn).")]
        [SerializeField, Min(0f)] float scaleWhenMet = 0.5f;
        [Tooltip("Time scale otherwise (accelerated-world curse active).")]
        [SerializeField, Min(0f)] float scaleWhenNotMet = 1.5f;
        [SerializeField] bool affectAnimators = true;
        [SerializeField] bool affectParticles = true;

        Animator[] _animators;
        ParticleSystem[] _particles;

        public float TimeScale { get; private set; } = 1f;

        void Reset() => SetDefaultMask(MaskType.NiceAndSlow);

        void Awake()
        {
            _animators = affectAnimators ? GetComponentsInChildren<Animator>(true) : System.Array.Empty<Animator>();
            _particles = affectParticles ? GetComponentsInChildren<ParticleSystem>(true) : System.Array.Empty<ParticleSystem>();
        }

        protected override void Apply(bool conditionMet)
        {
            TimeScale = conditionMet ? scaleWhenMet : scaleWhenNotMet;

            foreach (Animator animator in _animators)
                animator.speed = TimeScale;

            foreach (ParticleSystem particles in _particles)
            {
                ParticleSystem.MainModule main = particles.main;
                main.simulationSpeed = TimeScale;
            }
        }
    }
}
