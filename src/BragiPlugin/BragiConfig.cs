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

        // ── Bard Buff ─────────────────────────────────────────────────────────
        public static ConfigEntry<bool>  BuffEnabled       = null!;
        public static ConfigEntry<float> BuffRadius        = null!;
        public static ConfigEntry<float> BuffDuration      = null!;
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

            BuffEnabled = cfg.Bind(
                "BardBuff", "Enabled", true,
                "Enable the Skald's Blessing stamina/comfort buff for nearby players.");

            BuffRadius = cfg.Bind(
                "BardBuff", "Radius", 15f,
                new ConfigDescription("Radius in metres of the Skald's Blessing buff.",
                    new AcceptableValueRange<float>(2f, 50f)));

            BuffDuration = cfg.Bind(
                "BardBuff", "Duration", 60f,
                new ConfigDescription("How long (seconds) Skald's Blessing persists after music stops.",
                    new AcceptableValueRange<float>(10f, 300f)));

            StaminaRegenBonus = cfg.Bind(
                "BardBuff", "StaminaRegenBonus", 0.15f,
                new ConfigDescription("Stamina regen percentage bonus from Skald's Blessing (0.15 = +15%).",
                    new AcceptableValueRange<float>(0f, 1f)));

            OpenMenuKey = cfg.Bind(
                "UI", "OpenMenuKey", "G",
                "Keyboard key to open the song selection menu while holding an instrument.");
        }
    }
}
