using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Managers;
using Jotunn.Entities;
using UnityEngine;

namespace Bragi
{
    /// <summary>
    /// Registers the "Forager's Nose" craftable item.
    ///
    /// Timing note:
    ///   Jotunn's CustomItem clone-from-base requires the vanilla prefab
    ///   (Wishbone) to already exist in ZNetScene. That only happens AFTER
    ///   the game finishes loading its asset bundles, NOT during plugin Awake().
    ///   We therefore defer AddItem into PrefabManager.OnVanillaPrefabsAvailable,
    ///   which is the canonical Jotunn hook for exactly this case.
    ///
    /// Activation strategy:
    ///   ForagerSpawnPatch attaches ForagerRadar to the local player on spawn.
    ///   The radar's ScanLoop self-gates by checking the utility slot each pulse.
    /// </summary>
    public static class ForagerItem
    {
        public const string ItemPrefabName = "ForagersNose";

        // ── Registration ─────────────────────────────────────────────────────

        public static void Register()
        {
            // Defer item creation until vanilla prefabs (incl. Wishbone) are loaded
            PrefabManager.OnVanillaPrefabsAvailable += CreateItem;
            BragiPlugin.Log.LogInfo("🌿 Forager's Nose queued for registration.");
        }

        private static void CreateItem()
        {
            // Unsubscribe immediately — this must only run once per session
            PrefabManager.OnVanillaPrefabsAvailable -= CreateItem;

            var itemConfig = new ItemConfig
            {
                Name        = "$item_foragersnose",
                Description = "$item_foragersnose_desc",
                CraftingStation = CraftingStations.Workbench,
                Requirements = new[]
                {
                    new RequirementConfig("Raspberry",   10),
                    new RequirementConfig("Blueberries", 10),
                    new RequirementConfig("Mushroom",     5),
                    new RequirementConfig("Dandelion",    2),
                },
            };

            // Clone the Wishbone — vanilla prefab is now guaranteed to exist
            var item = new CustomItem(ItemPrefabName, "Wishbone", itemConfig);

            // Strip the SE_Finder before Jotunn registers it
            TweakSharedData(item);

            ItemManager.Instance.AddItem(item);
            BragiPlugin.Log.LogInfo("🌿 Forager's Nose item registered.");
        }

        /// <summary>
        /// Overrides name/description tokens on the cloned ItemDrop shared data
        /// and removes the vanilla Wishbone SE_Finder so our MonoBehaviour
        /// handles detection instead (no double-pinging silver veins).
        /// </summary>
        private static void TweakSharedData(CustomItem item)
        {
            var shared = item.ItemPrefab?.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
            if (shared == null)
            {
                BragiPlugin.Log.LogWarning("🌿 Could not tweak Forager's Nose shared data — ItemDrop missing.");
                return;
            }

            shared.m_name        = "$item_foragersnose";
            shared.m_description = "$item_foragersnose_desc";
            shared.m_equipStatusEffect = null;
            shared.m_setStatusEffect   = null;

            BragiPlugin.Log.LogInfo("🌿 Forager's Nose shared data tweaked.");
        }
    }

    // ── Attach ForagerRadar on spawn ──────────────────────────────────────────

    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    internal static class ForagerSpawnPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance)
        {
            if (!__instance.IsOwner()) return;

            if (__instance.GetComponent<ForagerRadar>() == null)
            {
                __instance.gameObject.AddComponent<ForagerRadar>();
                BragiPlugin.Log.LogInfo("🌿 ForagerRadar attached to local player.");
            }
        }
    }
}
