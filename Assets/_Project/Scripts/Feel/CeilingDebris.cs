using UnityEngine;

namespace APX.Feel
{
    /// <summary>
    /// Pebbles and dust raining from a ceiling while the room rumbles. Each child particle system emits from a
    /// box along the top edge of the given area; stopping lets what is already falling land and fade.
    /// </summary>
    public sealed class CeilingDebris : MonoBehaviour
    {
        [Tooltip("How far below the area's top edge the debris appears (e.g. the ceiling's thickness).")]
        [SerializeField, Min(0f)] float ceilingThickness = 1f;
        [Tooltip("Kept clear at each side, so nothing spawns inside the walls.")]
        [SerializeField, Min(0f)] float wallInset = 1.5f;

        ParticleSystem[] _systems;

        void Awake()
        {
            _systems = GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem system in _systems)
            {
                ParticleSystem.MainModule main = system.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = false;
            }
        }

        public void Begin(Rect area)
        {
            transform.position = new Vector3(area.center.x, area.yMax - ceilingThickness, transform.position.z);
            float width = Mathf.Max(1f, area.width - wallInset * 2f);
            foreach (ParticleSystem system in _systems)
            {
                ParticleSystem.ShapeModule shape = system.shape;
                shape.scale = new Vector3(width, shape.scale.y, shape.scale.z);
                system.Play(false);
            }
        }

        public void End()
        {
            foreach (ParticleSystem system in _systems)
                system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
