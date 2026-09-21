using BepInEx.Configuration;

namespace Bragi
{
    /// <summary>
    /// All configurable values for the Bragi mod.
    /// Settings are exposed in BepInEx/config/com.bragi.valheim.cfg
    /// and can be edited live or via a BepInEx ConfigManager UI.
    /// </summary>
    public static class BragiConfig
    {
        // ── Audio ─────────────────────────────────────────────────────────────
        public static ConfigEntry<float> MasterVolume = null!;
        public static ConfigEntry<float> MusicRange   = null!;

        /// <summary>When true, Valheim's background ambient/music is ducked while instruments play.</summary>
        public static ConfigEntry<bool> DuckGameMusic = null!;

        // ── Bard Buff ─────────────────────────────────────────────────────────
        public static ConfigEntry<bool>  BuffEnabled       = null!;
        public static ConfigEntry<float> BuffRadius        = null!;
        public static ConfigEntry<float> BuffDuration      = null!;

        /// <summary>
        /// Bonus stamina regen multiplier added to the vanilla SE_Rested effect when listening.
        /// (0.15 = +15% on top of whatever vanilla Rested gives.)
        /// </summary>
        public static ConfigEntry<float> StaminaRegenBonus = null!;

        // ── UI ────────────────────────────────────────────────────────────────
        public static ConfigEntry<string> OpenMenuKey = null!;

        public static void Init(ConfigFile cfg)
        {
            MasterVolume = cfg.Bind(
                "Audio", "MasterVolume", 1.0f,
                new ConfigDescription("Global volume multiplier for instrument audio (0.0 – 1.0).",
                    new AcceptableValueRange<float>(0f, 1f)));

            MusicRange = cfg.Bind(
                "Audio", "MusicRange", 30f,
                new ConfigDescription("Radius in metres within which other players hear your music.",
                    new AcceptableValueRange<float>(5f, 100f)));

            DuckGameMusic = cfg.Bind(
                "Audio", "DuckGameMusic", true,
                "Reduce Valheim's background ambient music volume while instruments are playing, " +
                "so it doesn't clash with the performed song.");

            BuffEnabled = cfg.Bind(
                "BardBuff", "Enabled", true,
                "Enable the bard buff: playing music refreshes the vanilla Rested status effect " +
                "for nearby players.");

            BuffRadius = cfg.Bind(
                "BardBuff", "Radius", 15f,
                new ConfigDescription("Radius in metres of the bard Rested-extension buff.",
                    new AcceptableValueRange<float>(2f, 50f)));

            BuffDuration = cfg.Bind(
                "BardBuff", "Duration", 60f,
                new ConfigDescription("How long (seconds) the Rested buff persists naturally after being granted.",
                    new AcceptableValueRange<float>(10f, 300f)));

            StaminaRegenBonus = cfg.Bind(
                "BardBuff", "StaminaRegenBonus", 0.15f,
                new ConfigDescription("Extra stamina regen percentage on top of the vanilla Rested buff (0.15 = +15%).",
                    new AcceptableValueRange<float>(0f, 1f)));

            OpenMenuKey = cfg.Bind(
                "UI", "OpenMenuKey", "G",
                "Keyboard key to open the song selection menu while holding an instrument.");
        }
    }
}
