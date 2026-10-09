using UnityEngine;

namespace APX.Core
{
    /// <summary>
    /// Hidden helper created by <see cref="GameTime"/> to count time pulses down on real time, so a
    /// hit-stop cannot freeze its own timer. Lives in its own file so Unity can resolve its script.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class GameTimeRunner : MonoBehaviour
    {
        void Update() => GameTime.Tick(Time.unscaledDeltaTime);
    }
}
