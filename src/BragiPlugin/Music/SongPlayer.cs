using System.Collections;
using UnityEngine;

namespace Bragi
{
    /// <summary>
    /// Manages local audio playback for the local player's instrument.
    /// Attached as a MonoBehaviour to the local player GameObject while playing.
    ///
    /// Responsibilities:
    ///   - Detect the equipped instrument type and load its specific stem audio
    ///   - Play the stem seeked to the correct offset (either 0 for a new session,
    ///     or elapsed time when joining an existing ensemble session)
    ///   - Route AudioSource to Valheim's AudioMixerGroup (respects game volume sliders)
    ///   - Duck background game music while playing; restore it on stop
    ///   - Broadcast Start/Join/Stop RPC via MusicSync for ensemble sync
    ///   - Apply/remove the Bard buff via BardBuff
    /// </summary>
    public class SongPlayer : MonoBehaviour
    {
        public static SongPlayer? Instance { get; private set; }

        public bool      IsPlaying    { get; private set; }
        public SongData? CurrentSong  { get; private set; }

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

        /// <summary>
        /// Starts a brand-new ensemble session — the local player chose a song and
        /// is starting it fresh. Other players can join later.
        /// </summary>
        public void StartNewSession(SongData song)
        {
            if (IsPlaying) StopPlaying();

            var instrType = GetEquippedInstrumentType();
            CurrentSong   = song;
            _loadCoroutine = StartCoroutine(LoadAndPlay(song, instrType, seekSeconds: 0f, isNewSession: true));
        }

        /// <summary>
        /// Joins an existing ensemble session (called from UI's Join button).
        /// Seeks to the session's current elapsed position to sync perfectly.
        /// The player does NOT choose a song — it's taken from ActiveSession.
        /// </summary>
        public void JoinActiveSession()
        {
            var session = MusicSync.ActiveSession;
            if (session == null)
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center,
                    "⚠ No active ensemble session nearby.");
                return;
            }

            if (IsPlaying) StopPlaying();

            var instrType = GetEquippedInstrumentType();
            float elapsed = ZNet.instance != null
                ? (float)(ZNet.instance.GetTimeSeconds() - session.StartNetworkTime)
                : 0f;

            CurrentSong    = session.Song;
            _loadCoroutine = StartCoroutine(LoadAndPlay(session.Song, instrType,
                seekSeconds: elapsed, isNewSession: false));
        }

        /// <summary>Stops the current song, restores game music, and cleans up.</summary>
        public void StopPlaying()
        {
            if (_loadCoroutine != null) { StopCoroutine(_loadCoroutine); _loadCoroutine = null; }
            if (_audioSource   != null) { _audioSource.Stop(); }

            if (IsPlaying)
            {
                IsPlaying = false;
                MusicSync.SendStopSession();
                AudioRouting.RestoreGameMusic();
                BardBuff.EndBuff();
                BragiPlugin.Log.LogInfo("🎵 Stopped playing.");
            }

            CurrentSong = null;
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private IEnumerator LoadAndPlay(SongData song, InstrumentType? instrType,
            float seekSeconds, bool isNewSession)
        {
            BragiPlugin.Log.LogInfo($"🎵 Loading song: {song.Name}" +
                (instrType.HasValue ? $" [{instrType.Value} stem]" : " [full mix]"));

            AudioClip? clip = null;
            yield return SongLibrary.LoadClip(song, instrType, c => clip = c);

            if (clip == null)
            {
                BragiPlugin.Log.LogError($"Could not load audio for '{song.Name}'. Aborting.");
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "⚠ Could not load audio file.");
                yield break;
            }

            _audioSource!.clip       = clip;
            _audioSource.volume      = BragiConfig.MasterVolume.Value;
            _audioSource.maxDistance = BragiConfig.MusicRange.Value;

            // Route through Valheim's mixer so game volume sliders work
            AudioRouting.ApplyMixerGroup(_audioSource);

            // Seek to correct position before playing
            float safeSeek = Mathf.Repeat(seekSeconds, clip.length);
            _audioSource.time = safeSeek;
            _audioSource.Play();

            IsPlaying = true;

            // Duck background game music so it doesn't clash
            AudioRouting.DuckGameMusic();

            // Notify other clients
            if (isNewSession)
                MusicSync.SendStartSession(song.Id, instrType?.ToString() ?? "");
            else
                MusicSync.SendJoinSession(song.Id, instrType?.ToString() ?? "");

            // Start bard buff
            if (BragiConfig.BuffEnabled.Value)
                BardBuff.StartBuff();

            BragiPlugin.Log.LogInfo($"🎵 Now playing: {song.Name}" +
                (safeSeek > 0f ? $" (offset {safeSeek:0.0}s)" : ""));

            // Auto-stop when the remaining clip time elapses
            float remaining = clip.length - safeSeek;
            yield return new WaitForSeconds(remaining);
            StopPlaying();
        }

        // ── Instrument Detection ──────────────────────────────────────────────

        private static InstrumentType? GetEquippedInstrumentType()
        {
            var player = Player.m_localPlayer;
            if (player == null) return null;
            var items  = player.GetInventory().GetEquippedItems();
            var item   = items.Find(i => InstrumentDefinitions.IsInstrument(i.m_dropPrefab?.name ?? ""));
            if (item == null) return null;
            return InstrumentDefinitions.GetType(item.m_dropPrefab?.name ?? "");
        }

        // ── Audio Configuration ───────────────────────────────────────────────

        private static void Configure3DAudio(AudioSource src)
        {
            src.spatialBlend = 1f;
            src.rolloffMode  = AudioRolloffMode.Linear;
            src.minDistance  = 2f;
            src.maxDistance  = BragiConfig.MusicRange.Value;
            src.dopplerLevel = 0f;
            src.spread       = 60f;
            src.loop         = false;
            src.playOnAwake  = false;
        }
    }
}
