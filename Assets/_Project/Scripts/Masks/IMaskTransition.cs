using System.Collections;

namespace APX.Masks
{
    /// <summary>
    /// Presentation played by <see cref="MaskManager"/> before a mask change takes effect: the new mask (and
    /// its gameplay effects) only applies once <see cref="Play"/> finishes.
    /// </summary>
    public interface IMaskTransition
    {
        IEnumerator Play(MaskType from, MaskType to);

        /// <summary>Shows <paramref name="mask"/> at once, e.g. after a transition was interrupted.</summary>
        void Show(MaskType mask);
    }
}
