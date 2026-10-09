namespace APX.Core
{
    /// <summary>
    /// Local time multiplier for world objects. Lets the world slow down or speed up independently
    /// of <see cref="UnityEngine.Time.timeScale"/>, so the player can keep acting in real time.
    /// </summary>
    public interface ITimeScaleSource
    {
        float TimeScale { get; }
    }

    public static class TimeScaleSourceExtensions
    {
        /// <summary>Time scale of <paramref name="source"/>, or 1 when there is none.</summary>
        public static float ScaleOrDefault(this ITimeScaleSource source) => source?.TimeScale ?? 1f;
    }
}
