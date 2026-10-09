namespace APX.Core
{
    /// <summary>
    /// Anything inside a room that must return to its initial state when the room restarts
    /// (player death) or is re-entered: enemies, switches, gates, moving hazards...
    /// </summary>
    public interface IResettable
    {
        void ResetState();
    }
}
