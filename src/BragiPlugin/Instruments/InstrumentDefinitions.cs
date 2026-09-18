namespace Bragi
{
    /// <summary>
    /// Defines which items are Bragi instruments and what songs each can play.
    /// This is the source-of-truth for instrument→song compatibility.
    /// </summary>
    public static class InstrumentDefinitions
    {
        /// <summary>Map of item name → InstrumentType enum for quick lookup.</summary>
        public static readonly System.Collections.Generic.Dictionary<string, InstrumentType> ItemNameToType =
            new System.Collections.Generic.Dictionary<string, InstrumentType>
            {
                { "BragiLyre",      InstrumentType.Lyre      },
                { "BragiBoneFlute", InstrumentType.BoneFlute },
                { "BragiJawHarp",   InstrumentType.JawHarp   },
            };

        /// <summary>Returns true if the given item name is a Bragi instrument.</summary>
        public static bool IsInstrument(string itemName) => ItemNameToType.ContainsKey(itemName);

        /// <summary>Returns the instrument type for the given item name, or null.</summary>
        public static InstrumentType? GetType(string itemName) =>
            ItemNameToType.TryGetValue(itemName, out var t) ? t : (InstrumentType?)null;
    }

    public enum InstrumentType
    {
        Lyre,
        BoneFlute,
        JawHarp,
    }
}
