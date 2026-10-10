using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace APX.Hints
{
    /// <summary>
    /// Static hint text for a room. Add it next to the <see cref="Rooms.Room"/> component. With a companion spot, the
    /// companion flies there to give the hint (e.g. next to the thing it's about) instead of staying beside the player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomHint : MonoBehaviour, IHintProvider
    {
        [SerializeField, TextArea(2, 6)] string hint;
        [Tooltip("Optional: where the companion waits while giving this hint. Empty = beside the player. " +
                 "Right-click this component > Create Companion Spot to add one.")]
        [SerializeField] Transform companionSpot;

        public string GetHint() => hint;

        public bool TryGetCompanionSpot(out Vector3 position)
        {
            position = companionSpot != null ? companionSpot.position : default;
            return companionSpot != null;
        }

#if UNITY_EDITOR
        [ContextMenu("Create Companion Spot")]
        void CreateCompanionSpot()
        {
            if (companionSpot == null)
            {
                var spot = new GameObject("CompanionSpot").transform;
                Undo.RegisterCreatedObjectUndo(spot.gameObject, "Create Companion Spot");
                spot.SetParent(transform, false);
                Undo.RecordObject(this, "Create Companion Spot");
                companionSpot = spot;
            }

            Selection.activeTransform = companionSpot;
        }

        void OnDrawGizmos()
        {
            if (companionSpot == null)
                return;

            Gizmos.color = new Color(0.55f, 0.85f, 1f);
            Gizmos.DrawWireSphere(companionSpot.position, 0.35f);
            Handles.Label(companionSpot.position + Vector3.up * 0.6f, "Companion");
        }
#endif
    }
}
