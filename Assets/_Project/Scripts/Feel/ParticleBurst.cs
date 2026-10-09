using UnityEngine;

namespace APX.Feel
{
    /// <summary>
    /// One shared particle system per effect, moved to wherever a burst is needed. Particles live in world
    /// space, so earlier bursts stay put; no instantiation or pooling needed.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class ParticleBurst : MonoBehaviour
    {
        ParticleSystem _system;
        Vector3 _baseShapeScale;

        void Awake()
        {
            _system = GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = _system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            _baseShapeScale = _system.shape.scale;
        }

        /// <summary>Burst tinted with <paramref name="color"/> instead of the authored start colour.</summary>
        public void Play(Vector2 position, int count, Color color)
        {
            if (count <= 0)
                return;

            MoveTo(position, 0f);
            _system.Emit(new ParticleSystem.EmitParams { startColor = color }, count);
        }

        /// <param name="width">Spreads the emitter horizontally (e.g. a door's width); 0 keeps the authored shape.</param>
        public void Play(Vector2 position, int count, float width = 0f)
        {
            if (count <= 0)
                return;

            MoveTo(position, width);
            _system.Emit(count);
        }

        void MoveTo(Vector2 position, float width)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            ParticleSystem.ShapeModule shape = _system.shape;
            shape.scale = width > 0f ? new Vector3(width, _baseShapeScale.y, _baseShapeScale.z) : _baseShapeScale;
        }
    }
}
