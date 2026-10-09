using System.Collections.Generic;
using UnityEngine;

namespace APX.Core
{
    /// <summary>
    /// Single entry point for global time scale changes. A base scale (e.g. radial-menu slow motion) is
    /// combined with short-lived pulses (hit-stop, death slow-mo): the slowest active pulse wins and is
    /// multiplied with the base. Keeps the physics step proportional to the time scale so slow motion stays smooth.
    /// </summary>
    public static class GameTime
    {
        struct TimePulse
        {
            public float Scale;
            public float Remaining;
        }

        static readonly List<TimePulse> s_pulses = new();
        static float s_defaultFixedDeltaTime = -1f;
        static float s_baseScale = 1f;
        static GameTimeRunner s_runner;

        public static void SetTimeScale(float scale)
        {
            s_baseScale = Mathf.Max(0f, scale);
            Apply();
        }

        public static void ResetTimeScale() => SetTimeScale(1f);

        /// <summary>
        /// Scales time for <paramref name="duration"/> real-time seconds: ~0 is a hit-stop, higher values a
        /// slow-motion beat. Pulses overlap, so a freeze followed by slow-mo is two calls with nested durations.
        /// </summary>
        public static void Pulse(float scale, float duration)
        {
            if (duration <= 0f)
                return;

            s_pulses.Add(new TimePulse { Scale = Mathf.Clamp01(scale), Remaining = duration });
            EnsureRunner();
            Apply();
        }

        /// <summary>Counts pulses down on real time (driven by <see cref="GameTimeRunner"/>).</summary>
        internal static void Tick(float unscaledDeltaTime)
        {
            if (s_pulses.Count == 0)
                return;

            for (int i = s_pulses.Count - 1; i >= 0; i--)
            {
                TimePulse pulse = s_pulses[i];
                pulse.Remaining -= unscaledDeltaTime;
                if (pulse.Remaining <= 0f)
                    s_pulses.RemoveAt(i);
                else
                    s_pulses[i] = pulse;
            }

            Apply();
        }

        static void Apply()
        {
            if (s_defaultFixedDeltaTime < 0f)
                s_defaultFixedDeltaTime = Time.fixedDeltaTime;

            float pulseScale = 1f;
            foreach (TimePulse pulse in s_pulses)
                pulseScale = Mathf.Min(pulseScale, pulse.Scale);

            float scale = s_baseScale * pulseScale;
            Time.timeScale = scale;
            Time.fixedDeltaTime = s_defaultFixedDeltaTime * Mathf.Max(scale, 0.01f);
        }

        static void EnsureRunner()
        {
            if (s_runner != null)
                return;

            var host = new GameObject(nameof(GameTime)) { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(host);
            s_runner = host.AddComponent<GameTimeRunner>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RestoreOnPlayModeEnter()
        {
            // Without Domain Reload a session that ended mid slow-motion would leak into the next one.
            s_pulses.Clear();
            s_runner = null;
            if (s_defaultFixedDeltaTime > 0f)
                ResetTimeScale();
        }
    }
}
