using UnityEngine;

namespace APX.Hints
{
    /// <summary>Static hint text for a room. Add it next to the <see cref="Rooms.Room"/> component.</summary>
    [DisallowMultipleComponent]
    public sealed class RoomHint : MonoBehaviour, IHintProvider
    {
        [SerializeField, TextArea(2, 6)] string hint;

        public string GetHint() => hint;
    }
}
