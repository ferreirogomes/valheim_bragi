using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Bragi
{
    /// <summary>
    /// Scans the BepInEx config/Bragi/songs/ directory for *.json song definition files,
    /// deserializes them into SongData objects, and loads matching audio clips from either:
    ///   1. The embedded bragiassets AssetBundle (for bundled songs)
    ///   2. Loose .ogg / .wav files alongside the JSON (for user-added songs)
    ///
    /// Other mods can drop JSON + audio files into the songs folder to add new songs
    /// without touching Bragi's code. This is the extensibility hook.
    /// </summary>
    public static class SongLibrary
    {
        private static readonly List<SongData> _songs = new List<SongData>();
        public static IReadOnlyList<SongData> AllSongs => _songs;

        /// <summary>Directory where song JSON files live.</summary>
        public static string SongsDirectory => Path.Combine(
            BepInEx.Paths.ConfigPath, "Bragi", "songs");

        public static void Init()
        {
            // Ensure the songs directory exists (users/other mods may add songs here)
            Directory.CreateDirectory(SongsDirectory);

            // Copy default bundled songs on first install
            ExtractDefaultSongs();

            // Load all .json files from the directory
            LoadSongs();
        }

        // ── Default Song Extraction ───────────────────────────────────────────

        private static void ExtractDefaultSongs()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            // Embedded resource names follow: Bragi.songs.filename.json
            var resourceNames = assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith("Bragi.songs.") && n.EndsWith(".json"));

            foreach (var resourceName in resourceNames)
            {
                var fileName = resourceName.Replace("Bragi.songs.", "");
                var dest = Path.Combine(SongsDirectory, fileName);
                if (File.Exists(dest)) continue; // Don't overwrite user-edited songs

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null) continue;
                using var reader = new StreamReader(stream);
                File.WriteAllText(dest, reader.ReadToEnd());
                BragiPlugin.Log.LogInfo($"📄 Extracted default song: {fileName}");
            }
        }

        // ── JSON Loading ──────────────────────────────────────────────────────

        private static void LoadSongs()
        {
            _songs.Clear();
            var jsonFiles = Directory.GetFiles(SongsDirectory, "*.json");

            foreach (var file in jsonFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var song = JsonConvert.DeserializeObject<SongData>(json);
                    if (song == null) continue;

                    song.SourcePath = file;
                    _songs.Add(song);
                    BragiPlugin.Log.LogInfo($"🎵 Loaded song: {song}");
                }
                catch (Exception ex)
                {
                    BragiPlugin.Log.LogError($"Failed to load song JSON {file}: {ex.Message}");
                }
            }

            BragiPlugin.Log.LogInfo($"🎵 SongLibrary: {_songs.Count} songs loaded.");
        }

        // ── Audio Clip Loading ────────────────────────────────────────────────

        /// <summary>
        /// Loads the AudioClip for a song asynchronously using UnityWebRequest.
        /// Supports .ogg and .wav files located in the same folder as the JSON.
        /// </summary>
        public static IEnumerator LoadClip(SongData song, Action<AudioClip?> onLoaded)
        {
            if (song.Clip != null)
            {
                onLoaded(song.Clip);
                yield break;
            }

            // Try to find the audio file next to the JSON
            var dir = Path.GetDirectoryName(song.SourcePath) ?? SongsDirectory;
            var audioPath = Path.Combine(dir, song.AudioFile);

            if (!File.Exists(audioPath))
            {
                BragiPlugin.Log.LogWarning($"Audio file not found: {audioPath}");
                onLoaded(null);
                yield break;
            }

            var audioType = audioPath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
                ? AudioType.OGGVORBIS
                : AudioType.WAV;

            using var request = UnityWebRequestMultimedia.GetAudioClip(
                "file://" + audioPath.Replace('\\', '/'), audioType);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                BragiPlugin.Log.LogError($"Failed to load audio '{audioPath}': {request.error}");
                onLoaded(null);
                yield break;
            }

            var clip = DownloadHandlerAudioClip.GetContent(request);
            clip.name = song.Id;
            song.Clip = clip;
            onLoaded(clip);
        }

        // ── Filtering ─────────────────────────────────────────────────────────

        /// <summary>Returns songs compatible with a specific instrument.</summary>
        public static List<SongData> GetSongsForInstrument(InstrumentType type) =>
            _songs.Where(s => s.SupportsInstrument(type)).ToList();

        /// <summary>Looks up a song by its unique ID (used for network sync).</summary>
        public static SongData? GetById(string id) =>
            _songs.FirstOrDefault(s => s.Id == id);
    }
}
