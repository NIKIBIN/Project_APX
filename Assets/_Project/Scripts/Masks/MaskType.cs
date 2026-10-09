namespace APX.Masks
{
    /// <summary>The masks the player can wear. Each one nullifies one of the three curses.</summary>
    public enum MaskType
    {
        /// <summary>No mask: every curse is active.</summary>
        None = 0,

        /// <summary>"Devagar devagarinho": nullifies the accelerated-world curse.</summary>
        NiceAndSlow = 1,

        /// <summary>"Segundo canal": nullifies the lack-of-perception curse.</summary>
        SecondChannel = 2,

        /// <summary>"Diferenciar isso daquilo": nullifies the colour-blindness curse.</summary>
        Differentiate = 3,
    }
}
