namespace APX.Hints
{
    /// <summary>
    /// Supplies the companion's hint for a room. Implement it on the Room GameObject to source hints
    /// from anywhere (static text, localisation tables, puzzle progress...).
    /// </summary>
    public interface IHintProvider
    {
        string GetHint();
    }
}
