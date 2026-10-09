namespace APX.Core
{
    /// <summary>Exposes which way an actor is facing: +1 (right) or -1 (left).</summary>
    public interface IFacingProvider
    {
        int FacingDirection { get; }
    }
}
