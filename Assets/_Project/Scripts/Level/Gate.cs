using APX.Core;
using UnityEngine;

namespace APX.Level
{
    /// <summary>Blocking door, opened by switches or any UnityEvent. Restores itself on room reset.</summary>
    public sealed class Gate : MonoBehaviour, IResettable
    {
        [SerializeField] bool startsOpen;

        Collider2D[] _colliders;
        Renderer[] _renderers;

        public bool IsOpen { get; private set; }

        void Awake()
        {
            _colliders = GetComponentsInChildren<Collider2D>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);
            SetOpen(startsOpen);
        }

        public void Open() => SetOpen(true);

        public void Close() => SetOpen(false);

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            IsOpen = open;

            foreach (Collider2D target in _colliders)
                target.enabled = !open;

            foreach (Renderer target in _renderers)
                target.enabled = !open;
        }

        public void ResetState() => SetOpen(startsOpen);
    }
}
