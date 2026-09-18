using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace Bragi
{
    /// <summary>
    /// Metadata for a single song that can be played on a Bragi instrument.
    /// This is the JSON schema for files in BepInEx/config/Bragi/songs/*.json
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
        /// Filename of the audio clip inside the bragiassets AssetBundle (e.g. "odin_call.ogg").
        /// OR a full absolute path to a loose .ogg/.wav file in the songs folder.
        /// </summary>
        [JsonProperty("audioFile")]
        public string AudioFile { get; set; } = "";

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

        /// <summary>Loaded Unity AudioClip. Populated by SongLibrary at runtime.</summary>
        [JsonIgnore]
        public UnityEngine.AudioClip? Clip { get; set; }

        /// <summary>Full path to the JSON file this song was loaded from.</summary>
        [JsonIgnore]
        public string SourcePath { get; set; } = "";

        /// <summary>Returns true if this song is compatible with the given instrument type.</summary>
        public bool SupportsInstrument(InstrumentType type)
        {
            if (Instruments == null || Instruments.Count == 0) return true;
            return Instruments.Contains(type.ToString());
        }

        public override string ToString() => $"[{Id}] {Name} by {Author} ({Duration:0}s)";
    }
}
