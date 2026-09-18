using HarmonyLib;
using UnityEngine;

namespace Bragi
{
    /// <summary>
    /// Implements the "Skald's Blessing" status effect — a bard buff applied to
    /// all players within BragiConfig.BuffRadius metres while music is playing.
    ///
    /// Effect: +15% stamina regeneration (configurable), lasting BragiConfig.BuffDuration
    /// seconds after the music stops (like a campfire warmth bonus).
    ///
    /// Implementation approach: We apply a custom StatusEffect (SE_Rested-style)
    /// to each nearby Player's SEMan using Harmony to hook into the existing
    /// status effect pipeline — no new classes required for v1.
    /// </summary>
    public static class BardBuff
    {
        private const string BUFF_NAME = "SE_SkaldsBlessing";
        private static Coroutine? _buffLoop;

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

            // Let the existing SE expire naturally (it has a TTL)
            // Players keep the buff for BuffDuration seconds after music stops.
        }

        // ── Buff Loop ─────────────────────────────────────────────────────────

        /// <summary>
        /// Every 5 seconds while playing, apply/refresh the buff to nearby players.
        /// Using a refresh loop (rather than one-shot) ensures new players who
        /// walk into range pick it up without any additional logic.
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

                ApplyBuff(player);
            }
        }

        private static void ApplyBuff(Player player)
        {
            var seman = player.GetSEMan();
            if (seman == null) return;

            // Use SE_Rested as a base since it already handles comfort/regen bonuses.
            var buffHash = BUFF_NAME.GetHashCode();
            var se = ObjectDB.instance?.GetStatusEffect(buffHash);
            if (se == null)
            {
                // First time: create and register the status effect
                se = CreateSkaldsBlessing();
                if (se == null) return;
                ObjectDB.instance?.m_StatusEffects.Add(se);
            }

            // Refresh or apply
            if (seman.HaveStatusEffect(buffHash))
            {
                seman.GetStatusEffect(buffHash)?.ResetTime();
            }
            else
            {
                seman.AddStatusEffect(se);
            }
        }

        // ── Status Effect Creation ────────────────────────────────────────────

        private static StatusEffect? CreateSkaldsBlessing()
        {
            // SE_Rested is a SE_Stats subclass which has stamina regen multiplier.
            // We find it, clone it, then override the multiplier value.
            var restedSE = ObjectDB.instance?.GetStatusEffect("Rested".GetHashCode()) as SE_Stats;
            if (restedSE == null)
            {
                BragiPlugin.Log.LogWarning("Could not find SE_Rested (SE_Stats) to clone Skald's Blessing from.");
                return null;
            }

            var se = UnityEngine.Object.Instantiate(restedSE);
            se.name                    = BUFF_NAME;
            se.m_name                  = "$se_skaldsblessing_name";
            se.m_tooltip               = "$se_skaldsblessing_tooltip";
            se.m_ttl                   = BragiConfig.BuffDuration.Value;
            // SE_Stats exposes stamina regen as m_staminaRegenMultiplier
            se.m_staminaRegenMultiplier = 1f + BragiConfig.StaminaRegenBonus.Value;

            return se;
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
