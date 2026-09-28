using System.Collections;
using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace Bragi
{
    /// <summary>
    /// ForagerRadar is a MonoBehaviour that runs on the local player while the
    /// "Forager's Nose" item is equipped in the utility slot.
    ///
    /// Every <see cref="ForagerConfig.PulseInterval"/> seconds it:
    ///   1. Uses Physics.OverlapSphere to find all Pickable/Bush objects in range.
    ///   2. Plays the vanilla Wishbone ping SFX scaled by proximity.
    ///   3. Refreshes minimap pins for every detected pickable.
    ///   4. Clears stale pins when items are harvested or out of range.
    ///
    /// Detection:
    ///   Physics.OverlapSphereNonAlloc is used to avoid GC pressure.
    ///   The name is obtained from gameObject.name (Unity strips "(Clone)" for
    ///   scene-placed objects; for dynamic spawns we strip it manually).
    ///
    /// Picked state:
    ///   Checked via the ZNetView ZDO "picked" bool key — the standard networked
    ///   approach since the Pickable class stores state in the ZDO.
    /// </summary>
    public class ForagerRadar : MonoBehaviour
    {
        // ── Prefab name lists (vanilla Valheim) ───────────────────────────────

        private static readonly HashSet<string> BerryPrefabs = new HashSet<string>
        {
            "BlueberryBush",
            "RaspberryBush",
            "CloudberryBush",
        };

        private static readonly HashSet<string> MushroomPrefabs = new HashSet<string>
        {
            "Pickable_Mushroom",
            "Pickable_Mushroom_blue",
            "Pickable_Mushroom_yellow",
        };

        private static readonly HashSet<string> ThistlePrefabs = new HashSet<string>
        {
            "Pickable_Thistle",
            "Pickable_Dandelion",
        };

        private static readonly HashSet<string> FlintStonePrefabs = new HashSet<string>
        {
            "Pickable_Flint",
            "Pickable_Stone",
        };

        // ZDO key used by the Pickable class to store harvested state
        private static readonly int s_pickedKey = "picked".GetHashCode();

        // ── Minimap pin type ──────────────────────────────────────────────────
        // PinType.None shows as a small dot — clean for foraging markers.
        private const Minimap.PinType PIN_TYPE = Minimap.PinType.None;

        // ── Runtime state ─────────────────────────────────────────────────────

        /// <summary>Active minimap pins, keyed by instance ID of the root GO.</summary>
        private readonly Dictionary<int, Minimap.PinData> _pins = new Dictionary<int, Minimap.PinData>();

        /// <summary>Coroutine handle for clean teardown on Disable.</summary>
        private Coroutine? _scanLoop;

        // ── Wishbone ping SFX ─────────────────────────────────────────────────
        private static AudioClip? _pingSfx;

        // ── Collider scratch buffer (avoid per-scan GC alloc) ─────────────────
        private readonly Collider[] _colliderBuffer = new Collider[256];

        // ─────────────────────────────────────────────────────────────────────
        // Unity lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            _scanLoop = StartCoroutine(ScanLoop());
            BragiPlugin.Log.LogDebug("🌿 ForagerRadar enabled.");
        }

        private void OnDisable()
        {
            if (_scanLoop != null)
                StopCoroutine(_scanLoop);
            _scanLoop = null;

            ClearAllPins();
            BragiPlugin.Log.LogDebug("🌿 ForagerRadar disabled.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Scan loop
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator ScanLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(ForagerConfig.PulseInterval.Value);

                var player = Player.m_localPlayer;
                if (player == null) continue;

                // Self-gate: only scan while the Forager's Nose is in the utility slot.
                // This avoids needing fragile EquipItem/UnequipItem patches.
                if (!IsForagersNoseEquipped(player))
                {
                    // Clear any stale pins when the item is unequipped / on death
                    if (_pins.Count > 0)
                        ClearAllPins();
                    continue;
                }

                var results = FindNearbyPickables(player.transform.position);
                RefreshPins(results);
                if (results.Count > 0)
                    PlayPingSfx(player, results);
            }
        }

        /// <summary>
        /// Returns true if the local player currently has the Forager's Nose
        /// equipped in their utility slot.
        /// </summary>
        private static bool IsForagersNoseEquipped(Player player)
        {
            var utilityItem = player.GetInventory()?.GetEquippedItems();
            if (utilityItem == null) return false;
            foreach (var item in utilityItem)
            {
                if (item?.m_shared?.m_name == "$item_foragersnose")
                    return true;
            }
            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Detection
        // ─────────────────────────────────────────────────────────────────────

        private List<(GameObject go, string category)> FindNearbyPickables(Vector3 origin)
        {
            var found   = new List<(GameObject, string)>();
            float radius = ForagerConfig.ScanRadius.Value;
            int   maxPins = ForagerConfig.MaxPins.Value;

            // We rely on the default layer mask — all relevant pickables have colliders
            int count = Physics.OverlapSphereNonAlloc(origin, radius, _colliderBuffer);

            var seenIds = new HashSet<int>(); // dedup by root GO instance ID

            for (int i = 0; i < count && found.Count < maxPins; i++)
            {
                var col = _colliderBuffer[i];
                if (col == null) continue;

                var go = col.gameObject;
                if (go == null) continue;

                // Strip "(Clone)" suffix that Unity appends to instantiated prefabs
                string prefabName = go.name;
                int cloneIdx = prefabName.IndexOf('(');
                if (cloneIdx >= 0)
                    prefabName = prefabName.Substring(0, cloneIdx).TrimEnd();

                string? category = GetCategory(prefabName);
                if (category == null) continue;

                // Check harvested state via ZDO (network-synced, authoritative)
                var znv = go.GetComponentInParent<ZNetView>();
                if (znv != null && znv.IsValid())
                {
                    bool picked = znv.GetZDO().GetBool(s_pickedKey, false);
                    if (picked) continue;
                }

                // Deduplicate by root GO instance ID (multiple colliders per bush)
                var rootGo = znv != null ? znv.gameObject : go;
                int id = rootGo.GetInstanceID();
                if (!seenIds.Add(id)) continue;

                found.Add((rootGo, category));
            }

            return found;
        }

        private string? GetCategory(string prefabName)
        {
            if (ForagerConfig.DetectBerries.Value && BerryPrefabs.Contains(prefabName))
                return "$forager_berries";
            if (ForagerConfig.DetectMushrooms.Value && MushroomPrefabs.Contains(prefabName))
                return "$forager_mushrooms";
            if (ForagerConfig.DetectThistle.Value && ThistlePrefabs.Contains(prefabName))
                return "$forager_plants";
            if (ForagerConfig.DetectFlintStone.Value && FlintStonePrefabs.Contains(prefabName))
                return "$forager_minerals";
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Minimap pins
        // ─────────────────────────────────────────────────────────────────────

        private void RefreshPins(List<(GameObject go, string category)> current)
        {
            if (Minimap.instance == null) return;

            // Build set of current IDs for diff
            var currentIds = new HashSet<int>();
            foreach (var (go, _) in current)
                currentIds.Add(go.GetInstanceID());

            // Remove stale pins for objects that left the range or were picked
            var toRemove = new List<int>();
            foreach (var id in _pins.Keys)
            {
                if (!currentIds.Contains(id))
                    toRemove.Add(id);
            }
            foreach (var id in toRemove)
            {
                Minimap.instance.RemovePin(_pins[id]);
                _pins.Remove(id);
            }

            // Add pins for newly detected objects
            foreach (var (go, category) in current)
            {
                int id = go.GetInstanceID();
                if (_pins.ContainsKey(id)) continue;

                // Use the raw token — Jotunn registers all tokens with the vanilla
                // Localization singleton, so the minimap will resolve "$forager_berries"
                // etc. automatically when the pin label is rendered.
                string label = category;

                var pin = Minimap.instance.AddPin(
                    go.transform.position,
                    PIN_TYPE,
                    label,
                    false,   // save = false (temporary, not stored in player file)
                    false);  // isChecked

                _pins[id] = pin;
            }
        }

        private void ClearAllPins()
        {
            if (Minimap.instance == null)
            {
                _pins.Clear();
                return;
            }
            foreach (var pin in _pins.Values)
                Minimap.instance.RemovePin(pin);
            _pins.Clear();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Audio feedback
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Plays a proximity-scaled ping based on the nearest detected pickable.
        /// </summary>
        private void PlayPingSfx(Player player, List<(GameObject go, string category)> results)
        {
            float nearest = float.MaxValue;
            foreach (var (go, _) in results)
            {
                float d = Vector3.Distance(player.transform.position, go.transform.position);
                if (d < nearest) nearest = d;
            }

            float radius = ForagerConfig.ScanRadius.Value;
            float t = 1f - Mathf.Clamp01(nearest / radius);

            if (_pingSfx == null)
                _pingSfx = LoadWishbonePingClip();

            if (_pingSfx == null) return;

            float vol   = Mathf.Lerp(0.15f, 0.70f, t);
            float pitch = Mathf.Lerp(0.80f, 1.40f, t);

            PlaySfxAtPoint(_pingSfx, player.transform.position, vol, pitch);
        }

        /// <summary>
        /// Spawns a temporary AudioSource at position to play the clip with
        /// custom volume and pitch, then destroys itself.
        /// </summary>
        private static void PlaySfxAtPoint(AudioClip clip, Vector3 position, float volume, float pitch)
        {
            var go  = new GameObject("_ForagerPing");
            go.transform.position = position;
            var src = go.AddComponent<AudioSource>();
            src.clip         = clip;
            src.volume       = volume;
            src.pitch        = pitch;
            src.spatialBlend = 1f;  // 3D sound
            src.maxDistance  = 25f;
            src.rolloffMode  = AudioRolloffMode.Linear;
            src.Play();
            Destroy(go, clip.length + 0.5f);
        }

        /// <summary>
        /// Grabs the audio clip from the vanilla Wishbone's SE_Finder effect so
        /// the "ding" sounds familiar to players who know the Wishbone.
        /// Falls back gracefully to silence if the prefab isn't loaded yet.
        /// </summary>
        private static AudioClip? LoadWishbonePingClip()
        {
            if (ObjectDB.instance == null) return null;

            // ObjectDB stores status effects by hash; Wishbone's SE is "Wishbone"
            var se = ObjectDB.instance.GetStatusEffect("Wishbone".GetHashCode()) as SE_Finder;
            if (se == null) return null;

            var effects = se.m_pingEffectNear?.m_effectPrefabs;
            if (effects == null) return null;

            foreach (var ep in effects)
            {
                if (ep.m_prefab == null) continue;
                var src = ep.m_prefab.GetComponentInChildren<AudioSource>();
                if (src?.clip != null) return src.clip;
            }
            return null;
        }
    }
}
