using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace Bragi
{
    /// <summary>
    /// Metadata for a single song that can be played on a Bragi instrument.
    /// This is the JSON schema for files in BepInEx/config/Bragi/songs/*.json
    ///
    /// Stem / Multi-Track Support:
    ///   Each song can define instrument-specific audio stems via the "tracks" object.
    ///   When a player plays with an instrument that has a matching stem, only that
    ///   stem is played (e.g. the flute player hears/broadcasts only the flute part).
    ///   If no stem is defined for a given instrument, "audioFile" is used as fallback
    ///   so legacy single-file songs continue to work.
    ///
    ///   Example JSON:
    ///   {
    ///     "id": "feast_of_valhalla",
    ///     "audioFile": "feast_of_valhalla.ogg",
    ///     "tracks": {
    ///       "BoneFlute": "feast_of_valhalla_flute.ogg",
    ///       "Lyre":      "feast_of_valhalla_lyre.ogg",
    ///       "JawHarp":   "feast_of_valhalla_jawharp.ogg"
    ///     }
    ///   }
    /// </summary>
    [Serializable]
    public class SongData
    {
        /// <summary>Unique internal identifier (used for RPC sync and deduplication).</summary>
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        /// <summary>Display name shown in the song selection UI.</summary>
        [JsonProperty("name")]
        public string Name { get; set; } = "Unknown Song";

        /// <summary>Historical or artistic attribution ("Traditional Norse", etc.).</summary>
        [JsonProperty("author")]
        public string Author { get; set; } = "Traditional";

        /// <summary>Short description shown under the song title in the UI.</summary>
        [JsonProperty("description")]
        public string Description { get; set; } = "";

        /// <summary>
        /// Filename of the full-mix / fallback audio clip (e.g. "odin_call.ogg").
        /// Used when no instrument-specific stem is available in Tracks.
        /// </summary>
        [JsonProperty("audioFile")]
        public string AudioFile { get; set; } = "";

        /// <summary>
        /// Optional per-instrument stem files. Keys must match InstrumentType enum names:
        /// "Lyre", "BoneFlute", "JawHarp".
        /// If empty or the instrument has no entry, AudioFile is used as fallback.
        /// </summary>
        [JsonProperty("tracks")]
        public Dictionary<string, string> Tracks { get; set; } = new Dictionary<string, string>();

        /// <summary>Approximate duration in seconds (used for UI display only).</summary>
        [JsonProperty("duration")]
        public float Duration { get; set; } = 60f;

        /// <summary>
        /// Which instruments can play this song.
        /// Values must match InstrumentType enum names: "Lyre", "BoneFlute", "JawHarp".
        /// An empty list means ALL instruments can play it.
        /// </summary>
        [JsonProperty("instruments")]
        public List<string> Instruments { get; set; } = new List<string>();

        /// <summary>Musical mood tag for UI filtering ("Solemn", "Joyful", "Battle", etc.).</summary>
        [JsonProperty("mood")]
        public string Mood { get; set; } = "";

        // ── Runtime (not serialized) ──────────────────────────────────────────

        /// <summary>Cached loaded stem AudioClips keyed by audio file name. Populated by SongLibrary.</summary>
        [JsonIgnore]
        public Dictionary<string, UnityEngine.AudioClip> StemClips { get; } =
            new Dictionary<string, UnityEngine.AudioClip>();

        /// <summary>Full path to the JSON file this song was loaded from.</summary>
        [JsonIgnore]
        public string SourcePath { get; set; } = "";

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Returns true if this song is compatible with the given instrument type.</summary>
        public bool SupportsInstrument(InstrumentType type)
        {
            if (Instruments == null || Instruments.Count == 0) return true;
            return Instruments.Contains(type.ToString());
        }

        /// <summary>
        /// Returns the audio filename to load for the given instrument type.
        /// Checks Tracks first; falls back to AudioFile if no stem is defined.
        /// </summary>
        public string GetAudioFileForInstrument(InstrumentType? type)
        {
            if (type.HasValue && Tracks != null && Tracks.TryGetValue(type.Value.ToString(), out var stem))
                return stem;
            return AudioFile;
        }

        /// <summary>
        /// Returns true if this song has a dedicated stem for the given instrument type.
        /// </summary>
        public bool HasStemFor(InstrumentType type) =>
            Tracks != null && Tracks.ContainsKey(type.ToString());

        public override string ToString() => $"[{Id}] {Name} by {Author} ({Duration:0}s)";
    }
}
