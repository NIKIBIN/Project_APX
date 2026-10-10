using System.Collections.Generic;
using UnityEngine;

namespace APX.Core
{
    /// <summary>How urgent a hidden danger feels to the player's danger sense.</summary>
    public enum DangerLevel
    {
        None = 0,
        /// <summary>In danger, but it is not about to strike.</summary>
        Low = 1,
        /// <summary>The danger is almost ready to strike.</summary>
        Medium = 2,
        /// <summary>The danger is about to strike.</summary>
        High = 3,
    }

    /// <summary>A hidden danger the player can sense (e.g. a group of spike traps the player stepped on).</summary>
    public interface IDangerSource
    {
        /// <summary>True while the player is in danger from this source.</summary>
        /// <param name="secondsUntilStrike">Real seconds until it strikes; infinity when it is not counting down.</param>
        bool TryGetThreat(out float secondsUntilStrike);
    }

    /// <summary>Every active <see cref="IDangerSource"/>; sources register themselves while enabled.</summary>
    public static class DangerSources
    {
        static readonly List<IDangerSource> s_sources = new();

        public static void Register(IDangerSource source)
        {
            if (!s_sources.Contains(source))
                s_sources.Add(source);
        }

        public static void Unregister(IDangerSource source) => s_sources.Remove(source);

        /// <summary>The most urgent threat among all sources, if the player is in any danger.</summary>
        public static bool TryGetMostUrgent(out float secondsUntilStrike)
        {
            bool inDanger = false;
            secondsUntilStrike = float.PositiveInfinity;
            foreach (IDangerSource source in s_sources)
            {
                if (!source.TryGetThreat(out float seconds))
                    continue;

                inDanger = true;
                secondsUntilStrike = Mathf.Min(secondsUntilStrike, seconds);
            }

            return inDanger;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_sources.Clear();
    }
}
