using APX.Core;

namespace APX.Cutscenes
{
    /// <summary>A cutscene took over: player-driven systems (e.g. the mask menu) should stay idle.</summary>
    public readonly struct CutsceneStartedEvent : IEvent
    {
    }

    public readonly struct CutsceneEndedEvent : IEvent
    {
    }
}
