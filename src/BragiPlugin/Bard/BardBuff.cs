using HarmonyLib;
using UnityEngine;

namespace Bragi
{
    /// <summary>
    /// Implements the "Skald's Blessing" effect by augmenting Valheim's native
    /// SE_Rested status effect — no extra icon, no clutter.
    ///
    /// While music is playing, nearby players who already have the Rested buff
    /// get their remaining time extended every pulse. Players without the buff
    /// receive the vanilla Rested status directly from ObjectDB.
    ///
    /// This matches Enshrouded's approach: music refreshes and extends the
    /// existing rest bonus rather than stacking a separate icon.
    /// </summary>
    public static class BardBuff
    {
        private static Coroutine? _buffLoop;

        // Bonus seconds to add to Rested TTL on each 5-second buff pulse
        private const float RESTED_EXTENSION_PER_PULSE = 10f;
        private const float RESTED_MAX_EXTENSION       = 1200f; // 20 minutes cap (vanilla max)

        // ── Public API ────────────────────────────────────────────────────────

        public static void StartBuff()
        {
            if (!BragiConfig.BuffEnabled.Value) return;
            _buffLoop = Player.m_localPlayer?.StartCoroutine(BuffLoop());
        }

        public static void EndBuff()
        {
            if (_buffLoop != null && Player.m_localPlayer != null)
                Player.m_localPlayer.StopCoroutine(_buffLoop);
            _buffLoop = null;
        }

        // ── Buff Loop ─────────────────────────────────────────────────────────

        /// <summary>
        /// Every 5 seconds while music plays, refresh or extend the vanilla Rested
        /// buff on all players within buff radius.
        /// </summary>
        private static System.Collections.IEnumerator BuffLoop()
        {
            while (true)
            {
                ApplyToNearbyPlayers();
                yield return new WaitForSeconds(5f);
            }
        }

        private static void ApplyToNearbyPlayers()
        {
            var origin = Player.m_localPlayer;
            if (origin == null) return;

            float radius = BragiConfig.BuffRadius.Value;
            foreach (var player in Player.GetAllPlayers())
            {
                if (Vector3.Distance(player.transform.position, origin.transform.position) > radius)
                    continue;
                ExtendRestBuff(player);
            }
        }

        // ── Rested Extension ──────────────────────────────────────────────────

        private static void ExtendRestBuff(Player player)
        {
            var seman = player.GetSEMan();
            if (seman == null) return;

            int restedHash = "Rested".GetHashCode();

            if (seman.HaveStatusEffect(restedHash))
            {
                // Player already has Rested — extend its remaining duration
                var se = seman.GetStatusEffect(restedHash);
                if (se != null)
                {
                    // Get current remaining time, add our extension, clamp to max
                    float remaining = se.GetRemaningTime(); // Valheim's typo: "Remaning"
                    float newTtl    = Mathf.Min(RESTED_MAX_EXTENSION,
                        remaining + RESTED_EXTENSION_PER_PULSE
                        + BragiConfig.StaminaRegenBonus.Value * 60f);
                    se.m_ttl = newTtl;
                    // Reset the internal timer so the SE starts counting down from the new TTL
                    se.ResetTime();
                }
            }
            else
            {
                // Player doesn't have Rested yet — grant the vanilla Rested effect
                var restedSE = ObjectDB.instance?.GetStatusEffect(restedHash);
                if (restedSE != null)
                {
                    seman.AddStatusEffect(restedSE);
                    player.Message(MessageHud.MessageType.TopLeft,
                        "$se_rested_start"); // Uses vanilla Rested start message
                }
            }
        }
    }

    // ── Harmony patch to attach SongPlayer + SongSelectUI to local player ─────

    [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
    internal static class PlayerSpawnPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance)
        {
            if (!__instance.IsOwner()) return;

            // Attach song player and UI to the local player GameObject
            if (__instance.GetComponent<SongPlayer>() == null)
                __instance.gameObject.AddComponent<SongPlayer>();
            if (__instance.GetComponent<SongSelectUI>() == null)
                __instance.gameObject.AddComponent<SongSelectUI>();

            BragiPlugin.Log.LogInfo("🎵 Bragi components attached to local player.");
        }
    }
}
