using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;

namespace Bragi
{
    /// <summary>
    /// Registers all three v1 instruments as custom items using Jötunn's ItemManager.
    /// Each instrument is an equippable off-hand item that opens the song selection
    /// menu when the player presses the configured key.
    /// </summary>
    public static class InstrumentRegistry
    {
        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += AddInstruments;
        }

        private static void AddInstruments()
        {
            RegisterLyre();
            RegisterBoneFlute();
            RegisterJawHarp();

            BragiPlugin.Log.LogInfo("🎵 Bragi instruments registered.");

            // Un-hook — only needs to run once on world load
            PrefabManager.OnVanillaPrefabsAvailable -= AddInstruments;
        }

        // ── Lyre ──────────────────────────────────────────────────────────────
        /// <summary>
        /// The Viking Lyre (Lyra): a small plucked string instrument used by skalds
        /// to accompany poetry and sagas. Based on the Ribe lyre fragments (8th century).
        /// Crafted at the Workbench from Fine Wood, Deer Hide, and Resin.
        /// </summary>
        private static void RegisterLyre()
        {
            var config = new ItemConfig
            {
                Name        = "$item_bragi_lyre",
                Description = "$item_bragi_lyre_desc",
                CraftingStation = CraftingStations.Workbench,
                MinStationLevel = 1,
                Requirements = new[]
                {
                    new RequirementConfig { Item = "FineWood",    Amount = 8 },
                    new RequirementConfig { Item = "DeerHide",    Amount = 2 },
                    new RequirementConfig { Item = "Resin",       Amount = 4 },
                }
            };
            ItemManager.Instance.AddItem(new CustomItem("BragiLyre", "Club", config));
        }

        // ── Bone Flute ────────────────────────────────────────────────────────
        /// <summary>
        /// The Bone Flute: carved from animal leg bones, among the most common
        /// archaeological finds in Viking-Age Scandinavia. Simple to craft early game.
        /// </summary>
        private static void RegisterBoneFlute()
        {
            var config = new ItemConfig
            {
                Name        = "$item_bragi_boneflute",
                Description = "$item_bragi_boneflute_desc",
                CraftingStation = CraftingStations.Workbench,
                MinStationLevel = 1,
                Requirements = new[]
                {
                    new RequirementConfig { Item = "BoneFragments", Amount = 4 },
                    new RequirementConfig { Item = "Feathers",      Amount = 2 },
                }
            };
            ItemManager.Instance.AddItem(new CustomItem("BragiBoneFlute", "Club", config));
        }

        // ── Jaw Harp ──────────────────────────────────────────────────────────
        /// <summary>
        /// The Jaw Harp (Munnharpe): a small iron instrument held against the teeth.
        /// Produces a distinctive buzzing drone. Requires the Forge — an iron-age instrument.
        /// </summary>
        private static void RegisterJawHarp()
        {
            var config = new ItemConfig
            {
                Name        = "$item_bragi_jawharp",
                Description = "$item_bragi_jawharp_desc",
                CraftingStation = CraftingStations.Forge,
                MinStationLevel = 1,
                Requirements = new[]
                {
                    new RequirementConfig { Item = "Iron",          Amount = 2 },
                    new RequirementConfig { Item = "LeatherScraps", Amount = 1 },
                }
            };
            ItemManager.Instance.AddItem(new CustomItem("BragiJawHarp", "Club", config));
        }
    }
}
