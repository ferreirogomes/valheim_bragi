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
    /// Item stats mirror the vanilla Wishbone philosophy:
    ///   - Utility slot item (doesn't take weapon/armour slot)
    ///   - Crafted at the Workbench with foraged materials
    ///   - Zero stat bonuses; its value is the radar alone
    ///
    /// Crafting recipe (Workbench, level 1):
    ///   10× Raspberry + 10× Blueberry + 5× Mushroom + 2× Dandelion
    ///
    /// Activation strategy (no EquipItem/UnequipItem patches needed):
    ///   <see cref="ForagerRadar"/> is attached to the local player on spawn
    ///   via a patch on <see cref="Player.OnSpawned"/> (same approach as BardBuff).
    ///   The radar's own ScanLoop checks every pulse whether the utility slot
    ///   holds the Forager's Nose — if yes it scans, if no it clears pins and
    ///   waits. This avoids fragile method-name patches that break on updates.
    /// </summary>
    public static class ForagerItem
    {
        // Internal prefab name — must match localization tokens in English.json
        public const string ItemPrefabName = "ForagersNose";

        // ── Registration ─────────────────────────────────────────────────────

        public static void Register()
        {
            var itemConfig = new ItemConfig
            {
                Name        = "$item_foragersnose",
                Description = "$item_foragersnose_desc",
                CraftingStation = CraftingStations.Workbench,
                Requirements = new[]
                {
                    new RequirementConfig("Raspberry",   10),
                    new RequirementConfig("Blueberries", 10),
                    new RequirementConfig("Mushroom",    5),
                    new RequirementConfig("Dandelion",   2),
                },
            };

            // Clone the Wishbone so we inherit the correct equipment slot and icon
            var item = new CustomItem(ItemPrefabName, "Wishbone", itemConfig);
            ItemManager.Instance.AddItem(item);

            // Strip the vanilla SE_Finder from the clone (our MonoBehaviour handles it)
            ItemManager.OnItemsRegistered += TweakItemPrefab;

            BragiPlugin.Log.LogInfo("🌿 Forager's Nose item registered.");
        }

        private static void TweakItemPrefab()
        {
            var prefab = PrefabManager.Instance.GetPrefab(ItemPrefabName);
            if (prefab == null)
            {
                BragiPlugin.Log.LogWarning("🌿 ForagersNose prefab not found during TweakItemPrefab.");
                return;
            }

            var itemDrop = prefab.GetComponent<ItemDrop>();
            if (itemDrop == null) return;

            var shared = itemDrop.m_itemData.m_shared;
            shared.m_name        = "$item_foragersnose";
            shared.m_description = "$item_foragersnose_desc";

            // Remove the cloned Wishbone SE_Finder — ForagerRadar handles detection
            shared.m_equipStatusEffect = null;
            shared.m_setStatusEffect   = null;

            BragiPlugin.Log.LogInfo("🌿 Forager's Nose prefab tweaked.");
        }
    }

    // ── Attach ForagerRadar on spawn (same pattern as BardBuff) ─────────────

    /// <summary>
    /// Piggybacks on the existing PlayerSpawnPatch in BardBuff.cs is NOT used here
    /// to avoid double-patch conflicts. We declare our own postfix on OnSpawned.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    internal static class ForagerSpawnPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance)
        {
            if (!__instance.IsOwner()) return;

            // Attach once — the component persists across equip/unequip cycles.
            // ForagerRadar.ScanLoop checks the utility slot itself each pulse.
            if (__instance.GetComponent<ForagerRadar>() == null)
            {
                __instance.gameObject.AddComponent<ForagerRadar>();
                BragiPlugin.Log.LogInfo("🌿 ForagerRadar attached to local player.");
            }
        }
    }
}
