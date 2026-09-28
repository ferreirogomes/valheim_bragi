using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bragi
{
    /// <summary>
    /// ForagerRadar is a MonoBehaviour permanently attached to the local player.
    ///
    /// Every PulseInterval seconds it:
    ///   1. Self-gates: skips if Forager's Nose isn't equipped.
    ///   2. Physics.OverlapSphere → walks up to ZNetView root → checks prefab name.
    ///   3. Updates minimap pins for every live (unpicked) harvestable food resource in range.
    ///   4. Plays a proximity-scaled ping SFX borrowed from the Wishbone.
    /// </summary>
    public class ForagerRadar : MonoBehaviour
    {
        // ── Prefab name sets for harvestable food resources ──────────────────

        private static readonly HashSet<string> BerryPrefabs = new HashSet<string>
        {
            "BlueberryBush", "RaspberryBush", "CloudberryBush",
        };

        private static readonly HashSet<string> MushroomPrefabs = new HashSet<string>
        {
            "Pickable_Mushroom",
            "Pickable_Mushroom_blue",
            "Pickable_Mushroom_yellow",
            "Pickable_Mushroom_JotunPuffs",
            "Pickable_Mushroom_Magecap",
        };

        // Cache for dynamic prefab classification (null = not a food resource)
        private static readonly Dictionary<string, string?> FoodCategoryCache = new Dictionary<string, string?>();

        // ZDO key the Pickable class uses for harvested state
        private static readonly int s_pickedKey = "picked".GetHashCode();

        // Minimap pin type — PinType.None = small dot
        private const Minimap.PinType PIN_TYPE = Minimap.PinType.None;

        // ── Runtime state ─────────────────────────────────────────────────────

        private readonly Dictionary<int, Minimap.PinData> _pins = new Dictionary<int, Minimap.PinData>();
        private Coroutine? _scanLoop;
        private static AudioClip? _pingSfx;
        private readonly Collider[] _colliderBuffer = new Collider[256];
        private bool _sfxAttempted;  // guard so we only try loading the SFX once

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void OnEnable()
        {
            _scanLoop = StartCoroutine(ScanLoop());
            BragiPlugin.Log.LogInfo("ForagerRadar started.");
        }

        private void OnDisable()
        {
            if (_scanLoop != null) StopCoroutine(_scanLoop);
            _scanLoop = null;
            ClearAllPins();
            BragiPlugin.Log.LogInfo("ForagerRadar stopped.");
        }

        // ── Scan loop ─────────────────────────────────────────────────────────

        private IEnumerator ScanLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(ForagerConfig.PulseInterval.Value);

                var player = Player.m_localPlayer;
                if (player == null) continue;

                if (!IsForagersNoseEquipped(player))
                {
                    if (_pins.Count > 0) ClearAllPins();
                    continue;
                }

                var results = FindNearbyPickables(player.transform.position);

                BragiPlugin.Log.LogDebug(
                    $"Radar scan: found {results.Count} harvestable food resource(s) within {ForagerConfig.ScanRadius.Value}m.");

                RefreshPins(results);
                if (results.Count > 0)
                    PlayPingSfx(player, results);
            }
        }

        private static bool IsForagersNoseEquipped(Player player)
        {
            var equipped = player.GetInventory()?.GetEquippedItems();
            if (equipped == null) return false;
            foreach (var item in equipped)
            {
                if (item?.m_shared?.m_name == "$item_foragersnose")
                    return true;
            }
            return false;
        }

        // ── Detection ─────────────────────────────────────────────────────────

        private List<(GameObject go, string category)> FindNearbyPickables(Vector3 origin)
        {
            var found    = new List<(GameObject, string)>();
            float radius = ForagerConfig.ScanRadius.Value;
            int maxPins  = ForagerConfig.MaxPins.Value;

            // Include trigger colliders — berry bushes often use trigger volumes
            int count = Physics.OverlapSphereNonAlloc(
                origin, radius, _colliderBuffer,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            var seenIds = new HashSet<int>();

            for (int i = 0; i < count && found.Count < maxPins; i++)
            {
                var col = _colliderBuffer[i];
                if (col == null) continue;

                // Walk UP to the ZNetView root (collider may be a child like "_TriggerHit")
                var znv = col.gameObject.GetComponentInParent<ZNetView>();
                if (znv == null) continue;

                var rootGo = znv.gameObject;

                // Deduplicate by root instance ID early
                int id = rootGo.GetInstanceID();
                if (seenIds.Contains(id)) continue;

                // Strip "(Clone)" if present
                string rawName = rootGo.name;
                int cloneIdx   = rawName.IndexOf('(');
                string prefabName = cloneIdx >= 0
                    ? rawName.Substring(0, cloneIdx).TrimEnd()
                    : rawName;

                string? category = GetCategory(rootGo, prefabName);
                if (category == null) continue;

                // Skip already-picked objects
                var pickable = rootGo.GetComponentInChildren<Pickable>();
                if (pickable != null && (pickable.GetPicked() || !pickable.CanBePicked())) continue;

                if (znv.IsValid() && znv.GetZDO().GetBool(s_pickedKey, false)) continue;

                seenIds.Add(id);
                found.Add((rootGo, category));
            }

            return found;
        }

        private string? GetCategory(GameObject rootGo, string prefabName)
        {
            if (!FoodCategoryCache.TryGetValue(prefabName, out var category))
            {
                if (BerryPrefabs.Contains(prefabName))
                {
                    category = "$forager_berries";
                }
                else if (MushroomPrefabs.Contains(prefabName))
                {
                    category = "$forager_mushrooms";
                }
                else
                {
                    // Dynamic check: verify if the object is a Pickable that yields edible food
                    var pickable = rootGo.GetComponentInChildren<Pickable>();
                    if (pickable != null && pickable.m_itemPrefab != null)
                    {
                        var itemDrop = pickable.m_itemPrefab.GetComponent<ItemDrop>();
                        var shared = itemDrop?.m_itemData?.m_shared;
                        if (shared != null && (shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f ||
                            (shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable && !shared.m_isDrink)))
                        {
                            category = prefabName.ToLowerInvariant().Contains("mushroom")
                                ? "$forager_mushrooms"
                                : "$forager_berries";
                        }
                    }
                }

                FoodCategoryCache[prefabName] = category;
            }

            if (category == null) return null;
            if (category == "$forager_berries" && !ForagerConfig.DetectBerries.Value) return null;
            if (category == "$forager_mushrooms" && !ForagerConfig.DetectMushrooms.Value) return null;

            return category;
        }

        // ── Minimap pins ──────────────────────────────────────────────────────

        private void RefreshPins(List<(GameObject go, string category)> current)
        {
            if (Minimap.instance == null) return;

            var currentIds = new HashSet<int>();
            foreach (var (go, _) in current)
                currentIds.Add(go.GetInstanceID());

            // Remove stale pins
            var toRemove = new List<int>();
            foreach (var id in _pins.Keys)
                if (!currentIds.Contains(id)) toRemove.Add(id);
            foreach (var id in toRemove)
            {
                Minimap.instance.RemovePin(_pins[id]);
                _pins.Remove(id);
            }

            // Add new pins
            foreach (var (go, category) in current)
            {
                int id = go.GetInstanceID();
                if (_pins.ContainsKey(id)) continue;

                var pin = Minimap.instance.AddPin(
                    go.transform.position,
                    PIN_TYPE,
                    category,   // raw "$forager_berries" token — Jotunn/Localization resolves it
                    false,      // save = not a permanent player pin
                    false);     // isChecked

                _pins[id] = pin;
                BragiPlugin.Log.LogDebug($"Pin added at {go.transform.position} [{category}]");
            }
        }

        private void ClearAllPins()
        {
            if (Minimap.instance == null) { _pins.Clear(); return; }
            foreach (var pin in _pins.Values)
                Minimap.instance.RemovePin(pin);
            _pins.Clear();
        }

        // ── Audio ─────────────────────────────────────────────────────────────

        private void PlayPingSfx(Player player, List<(GameObject go, string category)> results)
        {
            float nearest = float.MaxValue;
            foreach (var (go, _) in results)
            {
                float d = Vector3.Distance(player.transform.position, go.transform.position);
                if (d < nearest) nearest = d;
            }

            float radius = ForagerConfig.ScanRadius.Value;
            float t      = 1f - Mathf.Clamp01(nearest / radius);

            if (_pingSfx == null && !_sfxAttempted)
            {
                _sfxAttempted = true;
                _pingSfx = LoadWishbonePingClip();
                BragiPlugin.Log.LogInfo(_pingSfx != null
                    ? "Wishbone SFX loaded successfully."
                    : "Wishbone SFX not found — continuing without audio.");
            }

            if (_pingSfx == null) return;

            float vol   = Mathf.Lerp(0.15f, 0.70f, t);
            float pitch = Mathf.Lerp(0.80f, 1.40f, t);
            PlaySfxAtPoint(_pingSfx, player.transform.position, vol, pitch);
        }

        private static void PlaySfxAtPoint(AudioClip clip, Vector3 position, float volume, float pitch)
        {
            var go  = new GameObject("_ForagerPing");
            go.transform.position = position;
            var src = go.AddComponent<AudioSource>();
            src.clip         = clip;
            src.volume       = volume;
            src.pitch        = pitch;
            src.spatialBlend = 1f;
            src.maxDistance  = 25f;
            src.rolloffMode  = AudioRolloffMode.Linear;
            src.Play();
            Destroy(go, clip.length + 0.5f);
        }

        private static AudioClip? LoadWishbonePingClip()
        {
            if (ObjectDB.instance == null)
            {
                BragiPlugin.Log.LogInfo("LoadWishbonePingClip: ObjectDB not ready.");
                return null;
            }

            // SE_Finder is the StatusEffect class used by the Wishbone.
            // ObjectDB hashes are computed with the stable (djb2) hash, NOT GetHashCode().
            SE_Finder? se = null;
            foreach (var effect in ObjectDB.instance.m_StatusEffects)
            {
                if (effect is SE_Finder finder && effect.name == "Wishbone")
                {
                    se = finder;
                    break;
                }
            }

            if (se == null)
            {
                BragiPlugin.Log.LogInfo("LoadWishbonePingClip: SE_Finder 'Wishbone' not found in ObjectDB.");
                return null;
            }

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
