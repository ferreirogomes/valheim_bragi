using BepInEx.Configuration;

namespace Bragi
{
    /// <summary>
    /// Configuration entries for the Forager's Nose radar item.
    ///
    /// All settings live in the same BepInEx config file as the rest of Bragi
    /// (com.bragi.valheim.cfg) under the [Forager] section.
    /// </summary>
    public static class ForagerConfig
    {
        // ── Detection radius ──────────────────────────────────────────────────
        public static ConfigEntry<float> ScanRadius = null!;

        // ── Pulse timing ─────────────────────────────────────────────────────
        public static ConfigEntry<float> PulseInterval = null!;

        // ── Category toggles ─────────────────────────────────────────────────
        public static ConfigEntry<bool> DetectBerries    = null!;
        public static ConfigEntry<bool> DetectMushrooms  = null!;

        /// <summary>Maximum number of minimap pins shown at once (perf guard).</summary>
        public static ConfigEntry<int> MaxPins = null!;

        public static void Init(ConfigFile cfg)
        {
            ScanRadius = cfg.Bind(
                "Forager", "ScanRadius", 40f,
                new ConfigDescription(
                    "Radius in metres that the Forager's Nose scans for pickables.",
                    new AcceptableValueRange<float>(10f, 200f)));

            PulseInterval = cfg.Bind(
                "Forager", "PulseInterval", 3f,
                new ConfigDescription(
                    "Seconds between each scan pulse (lower = more frequent, heavier on CPU).",
                    new AcceptableValueRange<float>(1f, 30f)));

            DetectBerries = cfg.Bind(
                "Forager", "DetectBerries", true,
                "Detect blueberry, raspberry and cloudberry bushes.");

            DetectMushrooms = cfg.Bind(
                "Forager", "DetectMushrooms", true,
                "Detect wild mushrooms (red, yellow, blue, jotun puffs, magecap).");

            MaxPins = cfg.Bind(
                "Forager", "MaxPins", 20,
                new ConfigDescription(
                    "Maximum number of minimap pins shown by the radar at once.",
                    new AcceptableValueRange<int>(1, 100)));
        }
    }
}
