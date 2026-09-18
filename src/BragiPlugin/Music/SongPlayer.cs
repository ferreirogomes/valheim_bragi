using System.Collections;
using UnityEngine;

namespace Bragi
{
    /// <summary>
    /// Manages the local playback of a song for a given player.
    /// Attached as a MonoBehaviour to the local player GameObject while playing.
    ///
    /// Responsibilities:
    ///   - Create and manage an AudioSource with 3D spatial audio settings
    ///   - Load the AudioClip via SongLibrary.LoadClip (coroutine)
    ///   - Apply Bragi volume config
    ///   - Notify MusicSync on start / stop so remote players are informed
    ///   - Apply/remove the Bard buff via BardBuff
    /// </summary>
    public class SongPlayer : MonoBehaviour
    {
        public static SongPlayer? Instance { get; private set; }

        public bool  IsPlaying   { get; private set; }
        public SongData? CurrentSong { get; private set; }

        private AudioSource? _audioSource;
        private Coroutine?   _loadCoroutine;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            _audioSource = gameObject.AddComponent<AudioSource>();
            Configure3DAudio(_audioSource);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            StopPlaying();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Loads and plays a song. Shows loading indicator while audio is being fetched.</summary>
        public void Play(SongData song)
        {
            if (IsPlaying) StopPlaying();

            CurrentSong = song;
            _loadCoroutine = StartCoroutine(LoadAndPlay(song));
        }

        /// <summary>Stops the current song and cleans up.</summary>
        public void StopPlaying()
        {
            if (_loadCoroutine != null) { StopCoroutine(_loadCoroutine); _loadCoroutine = null; }
            if (_audioSource != null)  { _audioSource.Stop(); }

            if (IsPlaying)
            {
                IsPlaying = false;
                MusicSync.SendStopToAll();
                BardBuff.EndBuff();
                BragiPlugin.Log.LogInfo("🎵 Stopped playing.");
            }

            CurrentSong = null;
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private IEnumerator LoadAndPlay(SongData song)
        {
            BragiPlugin.Log.LogInfo($"🎵 Loading song: {song.Name}");

            AudioClip? clip = null;
            yield return SongLibrary.LoadClip(song, c => clip = c);

            if (clip == null)
            {
                BragiPlugin.Log.LogError($"Could not load audio for '{song.Name}'. Aborting playback.");
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "⚠ Could not load audio file.");
                yield break;
            }

            _audioSource!.clip   = clip;
            _audioSource.volume  = BragiConfig.MasterVolume.Value;
            _audioSource.maxDistance = BragiConfig.MusicRange.Value;
            _audioSource.Play();
            IsPlaying = true;

            // Notify other clients
            MusicSync.SendPlayToAll(song.Id);

            // Start bard buff
            if (BragiConfig.BuffEnabled.Value)
                BardBuff.StartBuff();

            BragiPlugin.Log.LogInfo($"🎵 Now playing: {song.Name}");

            // Auto-stop when clip ends
            yield return new WaitForSeconds(clip.length);
            StopPlaying();
        }

        private static void Configure3DAudio(AudioSource src)
        {
            src.spatialBlend    = 1f;          // Full 3D
            src.rolloffMode     = AudioRolloffMode.Linear;
            src.minDistance     = 2f;
            src.maxDistance     = BragiConfig.MusicRange.Value;
            src.dopplerLevel    = 0f;          // No doppler — we're not a moving train
            src.spread          = 60f;
            src.loop            = false;
            src.playOnAwake     = false;
        }
    }
}
