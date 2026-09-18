using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Bragi
{
    /// <summary>
    /// Handles multiplayer synchronization of music playback using Valheim's ZRoutedRpc system.
    ///
    /// When the local player starts/stops a song:
    ///   - An RPC is broadcast to all clients in the current zone
    ///   - Remote clients find the corresponding player character and attach
    ///     a RemoteAudioSource to their position, playing the matching audio clip
    ///
    /// Network data sent: song ID (string) — lightweight. Audio clips are loaded
    /// client-side from their local song library, so no audio data crosses the wire.
    /// </summary>
    public static class MusicSync
    {
        private const string RPC_PLAY = "Bragi_PlaySong";
        private const string RPC_STOP = "Bragi_StopSong";

        public static void Init()
        {
            ZRoutedRpc.instance.Register<string>(RPC_PLAY, OnRemotePlay);
            ZRoutedRpc.instance.Register(RPC_STOP,         OnRemoteStop);
            BragiPlugin.Log.LogInfo("🎵 MusicSync RPCs registered.");
        }

        // ── Send ──────────────────────────────────────────────────────────────

        /// <summary>Broadcast to all clients that the local player started playing a song.</summary>
        public static void SendPlayToAll(string songId)
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody, RPC_PLAY, songId);
        }

        /// <summary>Broadcast to all clients that the local player stopped playing.</summary>
        public static void SendStopToAll()
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody, RPC_STOP);
        }

        // ── Receive ───────────────────────────────────────────────────────────

        private static void OnRemotePlay(long senderPeerId, string songId)
        {
            // Ignore if it's our own RPC echo
            if (senderPeerId == ZDOMan.GetSessionID()) return;

            var song = SongLibrary.GetById(songId);
            if (song == null)
            {
                BragiPlugin.Log.LogWarning($"Received RPC for unknown song ID: {songId}");
                return;
            }

            // Find the sending player's character in the world
            var senderPlayer = GetPlayerByPeerId(senderPeerId);
            if (senderPlayer == null) return;

            // Attach a RemoteAudioSource to that player's position
            RemoteAudioSource.PlayAt(senderPlayer, song);
        }

        private static void OnRemoteStop(long senderPeerId)
        {
            if (senderPeerId == ZDOMan.GetSessionID()) return;

            var senderPlayer = GetPlayerByPeerId(senderPeerId);
            if (senderPlayer == null) return;

            RemoteAudioSource.StopAt(senderPlayer);
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

    /// <summary>
    /// Attached to remote player GameObjects to play their music locally in 3D space.
    /// Self-destructs when StopAt is called or when the song ends.
    /// </summary>
    public class RemoteAudioSource : MonoBehaviour
    {
        private AudioSource? _source;
        private static System.Collections.Generic.Dictionary<Player, RemoteAudioSource> _active =
            new System.Collections.Generic.Dictionary<Player, RemoteAudioSource>();

        public static void PlayAt(Player player, SongData song)
        {
            // Remove any existing remote source on this player
            StopAt(player);

            var go  = new GameObject("BragiRemoteAudio");
            go.transform.SetParent(player.transform, worldPositionStays: false);
            go.transform.localPosition = new Vector3(0, 1.5f, 0); // head height

            var rpc = go.AddComponent<RemoteAudioSource>();
            rpc.StartCoroutine(rpc.LoadAndPlay(song));
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

        private IEnumerator LoadAndPlay(SongData song)
        {
            AudioClip? clip = null;
            yield return SongLibrary.LoadClip(song, c => clip = c);

            if (clip == null || this == null) yield break;

            _source = gameObject.AddComponent<AudioSource>();
            _source.clip          = clip;
            _source.volume        = BragiConfig.MasterVolume.Value;
            _source.spatialBlend  = 1f;
            _source.rolloffMode   = AudioRolloffMode.Linear;
            _source.minDistance   = 2f;
            _source.maxDistance   = BragiConfig.MusicRange.Value;
            _source.dopplerLevel  = 0f;
            _source.loop          = false;
            _source.Play();

            yield return new WaitForSeconds(clip.length);
            if (this != null) Destroy(gameObject);
        }
    }
}
