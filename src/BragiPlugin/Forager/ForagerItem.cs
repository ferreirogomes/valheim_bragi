using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Managers;
using Jotunn.Entities;
using UnityEngine;

namespace Bragi
{
    /// <summary>
    /// Registers the "Forager's Nose" craftable item and manages attaching the
    /// <see cref="ForagerRadar"/> component to the local player when the item
    /// is equipped in the utility slot.
    ///
    /// Item stats mirror the vanilla Wishbone philosophy:
    ///   - Utility slot item (doesn't take weapon/armour slot)
    ///   - Crafted at the Workbench with foraged materials
    ///   - Zero stat bonuses; its value is the radar alone
    ///
    /// Crafting recipe (Workbench, level 1):
    ///   10× Raspberry + 10× Blueberry + 5× Mushroom + 2× Dandelion
    ///
    /// Harmony patches two Player methods:
    ///   • EquipItem  — enable radar when the charm is put on
    ///   • UnequipItem — disable radar when the charm is taken off
    /// </summary>
    public static class ForagerItem
    {
        // Internal prefab name — must match any localization tokens in English.json
        public const string ItemPrefabName = "ForagersNose";

        // ── Registration ─────────────────────────────────────────────────────

        public static void Register()
        {
            // CustomItem without an asset bundle: we build the item from scratch
            // using Jotunn's ItemConfig DSL.
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

            // Use the vanilla Wishbone as the item base — same slot, same
            // equipType (utility), correct inventory icon fallback.
            // Jotunn's MockManager will resolve the vanilla prefab at runtime.
            var item = new CustomItem(ItemPrefabName, "Wishbone", itemConfig);

            // Rename it so it has a distinct identity
            // (Jotunn clones the Wishbone prefab; we customise the clone below)
            ItemManager.Instance.AddItem(item);

            // Subscribe to Jotunn's OnObjectDBReady to tweak the cloned prefab
            ItemManager.OnItemsRegistered += TweakItemPrefab;

            BragiPlugin.Log.LogInfo("🌿 Forager's Nose item registered.");
        }

        /// <summary>
        /// Runs after Jotunn finishes copying the Wishbone prefab so we can
        /// override name/description tokens and remove the SE_Finder status effect
        /// (we implement our own radar logic via ForagerRadar, not through SE_Finder,
        /// so the item doesn't double-ping with the vanilla silver detector).
        /// </summary>
        private static void TweakItemPrefab()
        {
            var prefab = PrefabManager.Instance.GetPrefab(ItemPrefabName);
            if (prefab == null)
            {
                BragiPlugin.Log.LogWarning(
                    "🌿 ForagersNose prefab not found during TweakItemPrefab.");
                return;
            }

            var itemDrop = prefab.GetComponent<ItemDrop>();
            if (itemDrop == null) return;

            var shared = itemDrop.m_itemData.m_shared;

            // Point to our localisation tokens
            shared.m_name        = "$item_foragersnose";
            shared.m_description = "$item_foragersnose_desc";

            // Remove the vanilla Wishbone SE_Finder — our MonoBehaviour handles detection
            shared.m_equipStatusEffect   = null;
            shared.m_setStatusEffect      = null;

            BragiPlugin.Log.LogInfo("🌿 Forager's Nose prefab tweaked.");
        }
    }

    // ── Harmony: equip / unequip hooks ────────────────────────────────────────

    [HarmonyPatch(typeof(Player), nameof(Player.EquipItem))]
    internal static class ForagerEquipPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance, ItemDrop.ItemData item, bool __result)
        {
            if (!__result) return;
            if (!__instance.IsOwner()) return;

            if (item?.m_shared?.m_name == "$item_foragersnose")
            {
                ForagerItemHelpers.EnableRadar(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.UnequipItem))]
    internal static class ForagerUnequipPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance, ItemDrop.ItemData item)
        {
            if (!__instance.IsOwner()) return;

            if (item?.m_shared?.m_name == "$item_foragersnose")
            {
                ForagerItemHelpers.DisableRadar(__instance);
            }
        }
    }

    /// <summary>
    /// Also disable the radar on player death so stale pins don't persist.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
    internal static class ForagerDeathPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance)
        {
            if (!__instance.IsOwner()) return;
            ForagerItemHelpers.DisableRadar(__instance);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    internal static class ForagerItemHelpers
    {
        internal static void EnableRadar(Player player)
        {
            var radar = player.GetComponent<ForagerRadar>();
            if (radar == null)
                radar = player.gameObject.AddComponent<ForagerRadar>();

            // OnEnable fires automatically when component is added or re-enabled
            radar.enabled = true;
            BragiPlugin.Log.LogDebug("🌿 Forager radar ON.");
        }

        internal static void DisableRadar(Player player)
        {
            var radar = player.GetComponent<ForagerRadar>();
            if (radar != null)
            {
                radar.enabled = false;
                BragiPlugin.Log.LogDebug("🌿 Forager radar OFF.");
            }
        }
    }
}
