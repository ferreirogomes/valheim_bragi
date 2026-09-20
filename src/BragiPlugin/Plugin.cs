using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using System.IO;
using System.Reflection;

namespace Bragi
{
    /// <summary>
    /// Bragi — Viking Music Mod for Valheim
    /// Named after Bragi, the Norse god of poetry and music.
    /// 
    /// This BepInEx plugin adds craftable Viking-era instruments (Lyre, Bone Flute, Jaw Harp)
    /// that players can use to play pre-composed Norse songs. Music is spatially synced
    /// over the network so nearby players hear it in multiplayer.
    /// </summary>
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid, BepInDependency.DependencyFlags.HardDependency)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class BragiPlugin : BaseUnityPlugin
    {
        public const string PluginGUID    = "com.bragi.valheim";
        public const string PluginName    = "Bragi";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log = null!;
        private static Harmony _harmony = null!;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo($"🎵 Bragi {PluginVersion} loading...");

            // Load configuration entries first
            BragiConfig.Init(Config);

            // Patch game methods via Harmony
            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            // Register custom items and recipes via Jotunn
            InstrumentRegistry.Register();

            // Initialize the song library (scans config folder)
            SongLibrary.Init();

            // Register network RPCs for multiplayer sync
            MusicSync.Init();

            // Register localisation tokens
            RegisterLocalization();

            Log.LogInfo("🎵 Bragi loaded successfully!");
        }

        private static void RegisterLocalization()
        {
            // Use the Jotunn-managed localization instance (GetLocalization is the preferred API)
            var localization = LocalizationManager.Instance.GetLocalization();

            // Try loading from the assembly's embedded resource first
            var assembly = Assembly.GetExecutingAssembly();
            const string resourceName = "Bragi.Localization.English.json";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                localization.AddJsonFile("English", reader.ReadToEnd());
                Log.LogInfo("🎵 Localization loaded from embedded resource.");
            }
            else
            {
                // Fallback: load from BepInEx config folder (loose file)
                var path = Path.Combine(BepInEx.Paths.ConfigPath, "Bragi", "Localization", "English.json");
                if (File.Exists(path))
                {
                    localization.AddJsonFile("English", File.ReadAllText(path));
                    Log.LogInfo("🎵 Localization loaded from config folder.");
                }
                else
                {
                    Log.LogWarning("⚠ Localization file not found — item names may appear as raw tokens.");
                }
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
