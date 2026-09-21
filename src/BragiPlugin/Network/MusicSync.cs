using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Bragi
{
    /// <summary>
    /// Handles multiplayer synchronization of music playback using Valheim's ZRoutedRpc system.
    ///
    /// Ensemble Session Model (Enshrouded-style):
    ///   - One player starts a session: broadcasts Bragi_StartSession with songId, networkTime,
    ///     and their instrument type. All nearby clients record the session.
    ///   - A second player (equipped with any instrument) can JOIN the active session:
    ///     they receive the songId + network start time, and begin playing their instrument's
    ///     stem at the correct elapsed offset — instant synchronized ensemble.
    ///   - When the host stops, Bragi_StopSession is broadcast and all remote stems stop.
    ///
    /// Network data sent: song ID (string), ZNet timestamp (double), instrument (string).
    /// Audio clips are loaded client-side from local files — no audio crosses the wire.
    ///
    /// RPC registration is deferred to ZNet.Awake via Harmony to guarantee
    /// the instance is initialised before we try to use it.
    /// </summary>
    public static class MusicSync
    {
        internal const string RPC_START_SESSION = "Bragi_StartSession";
        internal const string RPC_JOIN_SESSION  = "Bragi_JoinSession";
        internal const string RPC_STOP_SESSION  = "Bragi_StopSession";

        // ── Active Session State ──────────────────────────────────────────────

        /// <summary>Describes a song currently being performed by one or more players.</summary>
        public class EnsembleSession
        {
            /// <summary>Song being played in this session.</summary>
            public SongData Song = null!;
            /// <summary>ZNet time at which the session started (used for join-at-offset sync).</summary>
            public double StartNetworkTime;
            /// <summary>Peer ID of the player who started the session.</summary>
            public long HostPeerId;
        }

        /// <summary>The currently known active ensemble session, if any.</summary>
        public static EnsembleSession? ActiveSession { get; private set; }

        /// <summary>Called from Plugin.Awake — nothing to do; RPCs register in ZNetAwakePatch.</summary>
        public static void Init()
        {
            BragiPlugin.Log.LogInfo("🎵 MusicSync ready (RPCs will register on ZNet.Awake).");
        }

        // ── Send ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Broadcast that the local player started a new session (first player in a song).
        /// songId + current network time + instrument name are sent so others can sync.
        /// </summary>
        public static void SendStartSession(string songId, string instrumentType)
        {
            double netTime = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0.0;
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody, RPC_START_SESSION, songId, netTime, instrumentType);
        }

        /// <summary>
        /// Broadcast that the local player is joining the active session with a specific instrument.
        /// Used when a player joins mid-stream — their stem will be seeked to elapsed time.
        /// </summary>
        public static void SendJoinSession(string songId, string instrumentType)
        {
            double netTime = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0.0;
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody, RPC_JOIN_SESSION, songId, netTime, instrumentType);
        }

        /// <summary>Broadcast that the local player stopped playing (session ended or they left).</summary>
        public static void SendStopSession()
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RPC_STOP_SESSION);
            // Clear our own reference if we were the host
            if (ActiveSession?.HostPeerId == ZDOMan.GetSessionID())
                ActiveSession = null;
        }

        // ── Receive ───────────────────────────────────────────────────────────

        internal static void OnRemoteStartSession(long senderPeerId, string songId,
            double startNetTime, string instrumentType)
        {
            if (senderPeerId == ZDOMan.GetSessionID()) return;

            var song = SongLibrary.GetById(songId);
            if (song == null)
            {
                BragiPlugin.Log.LogWarning($"Received StartSession for unknown song: {songId}");
                return;
            }

            // Record the active session so local players can join it via the UI
            ActiveSession = new EnsembleSession
            {
                Song = song,
                StartNetworkTime = startNetTime,
                HostPeerId = senderPeerId,
            };

            // Play remote player's stem at correct position
            var senderPlayer = GetPlayerByPeerId(senderPeerId);
            if (senderPlayer == null) return;

            double now     = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : startNetTime;
            double elapsed = now - startNetTime;
            RemoteAudioSource.PlayAt(senderPlayer, song, instrumentType, startNetTime, elapsed);

            BragiPlugin.Log.LogInfo($"🎵 Remote session started: {song.Name} by {senderPlayer.GetPlayerName()}");
        }

        internal static void OnRemoteJoinSession(long senderPeerId, string songId,
            double joinNetTime, string instrumentType)
        {
            if (senderPeerId == ZDOMan.GetSessionID()) return;

            var song = SongLibrary.GetById(songId);
            if (song == null) return;

            // Update session record if not already set
            if (ActiveSession == null)
            {
                ActiveSession = new EnsembleSession
                {
                    Song = song,
                    StartNetworkTime = joinNetTime,
                    HostPeerId = senderPeerId,
                };
            }

            // Play this new player's instrument stem, seeked to session elapsed time
            var senderPlayer = GetPlayerByPeerId(senderPeerId);
            if (senderPlayer == null) return;

            double now     = ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : joinNetTime;
            double elapsed = now - ActiveSession.StartNetworkTime;
            RemoteAudioSource.PlayAt(senderPlayer, song, instrumentType, ActiveSession.StartNetworkTime, elapsed);

            BragiPlugin.Log.LogInfo($"🎵 Remote player joined session: {senderPlayer.GetPlayerName()} ({instrumentType})");
        }

        internal static void OnRemoteStopSession(long senderPeerId)
        {
            if (senderPeerId == ZDOMan.GetSessionID()) return;

            var senderPlayer = GetPlayerByPeerId(senderPeerId);
            if (senderPlayer != null)
                RemoteAudioSource.StopAt(senderPlayer);

            // If host stopped, clear the session
            if (ActiveSession?.HostPeerId == senderPeerId)
            {
                ActiveSession = null;
                BragiPlugin.Log.LogInfo("🎵 Remote ensemble session ended.");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Player? GetPlayerByPeerId(long peerId)
        {
            foreach (var player in Player.GetAllPlayers())
            {
                var znet = player.GetComponent<ZNetView>();
                if (znet != null && znet.GetZDO()?.GetOwner() == peerId)
                    return player;
            }
            return null;
        }
    }

    // ── Harmony patch: register RPCs after ZNet.Awake creates ZRoutedRpc ────────

    [HarmonyLib.HarmonyPatch(typeof(ZNet), "Awake")]
    internal static class ZNetAwakePatch
    {
        [HarmonyLib.HarmonyPostfix]
        private static void Postfix()
        {
            if (ZRoutedRpc.instance == null) return;

            ZRoutedRpc.instance.Register<string, double, string>(
                MusicSync.RPC_START_SESSION, MusicSync.OnRemoteStartSession);
            ZRoutedRpc.instance.Register<string, double, string>(
                MusicSync.RPC_JOIN_SESSION, MusicSync.OnRemoteJoinSession);
            ZRoutedRpc.instance.Register(
                MusicSync.RPC_STOP_SESSION, MusicSync.OnRemoteStopSession);

            BragiPlugin.Log.LogInfo("🎵 MusicSync RPCs registered.");
        }
    }

    /// <summary>
    /// Attached to remote player GameObjects to play their instrument stem locally in 3D space.
    /// Automatically seeks to the correct elapsed position in the song for perfect sync.
    /// Self-destructs when StopAt is called or when the stem ends.
    /// </summary>
    public class RemoteAudioSource : MonoBehaviour
    {
        private AudioSource? _source;
        private static readonly Dictionary<Player, RemoteAudioSource> _active =
            new Dictionary<Player, RemoteAudioSource>();

        public static void PlayAt(Player player, SongData song, string instrumentType,
            double sessionStartTime, double elapsedSeconds)
        {
            // Replace any existing remote source on this player
            StopAt(player);

            var go = new GameObject("BragiRemoteAudio");
            go.transform.SetParent(player.transform, worldPositionStays: false);
            go.transform.localPosition = new Vector3(0, 1.5f, 0);

            var rpc = go.AddComponent<RemoteAudioSource>();
            rpc.StartCoroutine(rpc.LoadAndPlay(song, instrumentType, (float)elapsedSeconds));
            _active[player] = rpc;
        }

        public static void StopAt(Player player)
        {
            if (_active.TryGetValue(player, out var rpc))
            {
                if (rpc != null) Destroy(rpc.gameObject);
                _active.Remove(player);
            }
        }

        private IEnumerator LoadAndPlay(SongData song, string instrumentType, float seekSeconds)
        {
            // Parse instrument type string → enum (null = fallback to full mix)
            InstrumentType? instrEnum = System.Enum.TryParse<InstrumentType>(
                instrumentType, out var parsed) ? parsed : (InstrumentType?)null;

            AudioClip? clip = null;
            yield return SongLibrary.LoadClip(song, instrEnum, c => clip = c);

            if (clip == null || this == null) yield break;

            _source = gameObject.AddComponent<AudioSource>();
            _source.clip         = clip;
            _source.volume       = BragiConfig.MasterVolume.Value;
            _source.spatialBlend = 1f;
            _source.rolloffMode  = AudioRolloffMode.Linear;
            _source.minDistance  = 2f;
            _source.maxDistance  = BragiConfig.MusicRange.Value;
            _source.dopplerLevel = 0f;
            _source.loop         = false;

            // Route through Valheim's audio mixer if available (respects game volume sliders)
            AudioRouting.ApplyMixerGroup(_source);

            // Seek to sync with the session
            float safeSeek = Mathf.Repeat(seekSeconds, clip.length);
            _source.time = safeSeek;
            _source.Play();

            float remaining = clip.length - safeSeek;
            yield return new WaitForSeconds(remaining);
            if (this != null) Destroy(gameObject);
        }
    }

    /// <summary>
    /// Helper that routes an AudioSource into Valheim's master AudioMixer so that
    /// in-game music/SFX volume sliders affect instrument sounds.
    /// </summary>
    public static class AudioRouting
    {
        private static UnityEngine.Audio.AudioMixerGroup? _cachedMixerGroup;

        /// <summary>
        /// Assigns the game's music or SFX mixer group to the given source.
        /// Falls back gracefully if AudioMan is not available.
        /// </summary>
        public static void ApplyMixerGroup(AudioSource source)
        {
            if (_cachedMixerGroup == null)
                _cachedMixerGroup = ResolveMixerGroup();

            if (_cachedMixerGroup != null)
                source.outputAudioMixerGroup = _cachedMixerGroup;
        }

        private static UnityEngine.Audio.AudioMixerGroup? ResolveMixerGroup()
        {
            try
            {
                // Try the ambient/music mixer from AudioMan
                if (AudioMan.instance != null)
                    return AudioMan.instance.m_ambientMixer;
            }
            catch { /* AudioMan may not be ready */ }
            return null;
        }

        /// <summary>
        /// Temporarily sets the game music volume to 0 to duck background music.
        /// Call RestoreGameMusic() when playing stops.
        /// </summary>
        public static void DuckGameMusic()
        {
            if (!BragiConfig.DuckGameMusic.Value) return;
            try
            {
                AudioMan.instance?.m_masterMixer?.SetFloat("MusicVolume", -80f);
            }
            catch { }
        }

        /// <summary>Restores game music volume after ducking.</summary>
        public static void RestoreGameMusic()
        {
            try
            {
                // Use the game's saved setting; 0dB = full volume in Unity mixer
                AudioMan.instance?.m_masterMixer?.SetFloat("MusicVolume", 0f);
            }
            catch { }
        }
    }
}
