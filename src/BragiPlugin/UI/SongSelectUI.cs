using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Bragi
{
    /// <summary>
    /// Song Selection UI — opens when the player presses the configured key (default: G)
    /// while an instrument is equipped. Displays a scrollable list of compatible songs
    /// styled to match Valheim's native dark-wood UI aesthetic.
    ///
    /// Key improvements over v1:
    ///   - Fixed: GUIUtility.hotControl no longer blocks button clicks (all songs selectable)
    ///   - Fixed: Cursor is properly unlocked when the menu opens, locked again when closed
    ///   - Fixed: Player.TakeInput is blocked via Harmony while the UI is open (no accidental
    ///     weapon swings or camera spins)
    ///   - New: Ensemble "Join Session" banner when another player is already performing nearby
    ///     — players can 1-click join without having to choose a song
    ///   - New: Stem indicator dots showing which instruments have dedicated stems per song
    /// </summary>
    public class SongSelectUI : MonoBehaviour
    {
        public static SongSelectUI? Instance      { get; private set; }
        public static bool          IsOpen        { get; private set; }

        private System.Collections.Generic.List<SongData> _availableSongs =
            new System.Collections.Generic.List<SongData>();

        // IMGUI layout constants
        private const int PanelW = 460;
        private const int PanelH = 560;
        private Rect    _panelRect;
        private Vector2 _scrollPos;
        private int     _selectedIndex = -1;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            CenterPanel();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ForceClose(); // ensure cursor is restored if destroyed while open
        }

        private void CenterPanel()
        {
            _panelRect = new Rect(
                (Screen.width  - PanelW) / 2f,
                (Screen.height - PanelH) / 2f,
                PanelW, PanelH);
        }

        // ── Key caching ───────────────────────────────────────────────────────

        private KeyCode _cachedMenuKey    = KeyCode.G;
        private string  _cachedMenuKeyStr = "G";

        private KeyCode GetMenuKey()
        {
            var raw = BragiConfig.OpenMenuKey.Value;
            if (raw != _cachedMenuKeyStr)
            {
                _cachedMenuKeyStr = raw;
                _cachedMenuKey = System.Enum.TryParse<KeyCode>(raw, ignoreCase: true, out var k)
                    ? k : KeyCode.G;
            }
            return _cachedMenuKey;
        }

        // ── Update ────────────────────────────────────────────────────────────

        private void Update()
        {
            if (Input.GetKeyDown(GetMenuKey()) && IsInstrumentEquipped())
                Toggle();

            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
                Close();

            if (IsOpen && _selectedIndex >= 0 && Input.GetKeyDown(KeyCode.Return))
                PlaySelected();
        }

        // ── IMGUI ─────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!IsOpen) return;

            // Re-centre if resolution changed
            if (Event.current.type == EventType.Layout)
                CenterPanel();

            // Dark overlay — no hotControl manipulation (that was the v1 click-blocking bug!)
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Panel background
            GUI.Box(_panelRect, "");
            GUILayout.BeginArea(_panelRect);

            DrawHeader();
            DrawEnsembleBanner();
            DrawSongList();
            DrawFooter();

            GUILayout.EndArea();
        }

        // ── Drawing ───────────────────────────────────────────────────────────

        private void DrawHeader()
        {
            GUILayout.Space(10);
            GUILayout.Label("♪  Choose a Song  ♪", LargeLabel());
            GUILayout.Label(GetInstrumentDisplayName(), SmallLabel());
            GUILayout.Space(6);
            DrawHorizontalLine();
            GUILayout.Space(4);
        }

        /// <summary>
        /// Shows a prominent "Join Session" banner when another player is already playing nearby.
        /// Clicking it immediately joins — no song selection needed.
        /// </summary>
        private void DrawEnsembleBanner()
        {
            var session = MusicSync.ActiveSession;
            if (session == null) return;

            // Find host player name
            string hostName = "someone";
            foreach (var p in Player.GetAllPlayers())
            {
                var znet = p.GetComponent<ZNetView>();
                if (znet != null && znet.GetZDO()?.GetOwner() == session.HostPeerId)
                {
                    hostName = p.GetPlayerName();
                    break;
                }
            }

            GUILayout.Space(4);
            GUI.backgroundColor = new Color(0.15f, 0.45f, 0.15f, 0.95f);
            if (GUILayout.Button(
                $"🎵  Join  {hostName}'s  \"{session.Song.Name}\"  →",
                JoinBannerStyle(), GUILayout.Height(40)))
            {
                JoinSession();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.Space(6);
            DrawHorizontalLine();
            GUILayout.Space(4);
        }

        private void DrawSongList()
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos,
                GUILayout.Width(PanelW - 16), GUILayout.Height(PanelH - 160));

            for (int i = 0; i < _availableSongs.Count; i++)
            {
                var  song     = _availableSongs[i];
                bool selected = (i == _selectedIndex);

                GUI.backgroundColor = selected
                    ? new Color(0.65f, 0.48f, 0.10f, 0.95f)   // warm amber
                    : new Color(0.18f, 0.14f, 0.07f, 0.85f);  // dark wood

                if (GUILayout.Button("", GUILayout.Height(54)))
                    _selectedIndex = i;

                // Overlay text on the button
                var btn = GUILayoutUtility.GetLastRect();
                GUI.Label(new Rect(btn.x + 10, btn.y + 5,  btn.width - 100, 22), song.Name,   SongNameStyle());
                GUI.Label(new Rect(btn.x + 10, btn.y + 28, btn.width - 100, 17),
                    $"{song.Author}  ·  {song.Mood}", SubtitleStyle());
                GUI.Label(new Rect(btn.xMax - 90, btn.y + 18, 80, 18),
                    FormatDuration(song.Duration), DurationStyle());

                // Stem indicator: small dots per instrument that has a dedicated stem
                DrawStemDots(song, btn);

                GUI.backgroundColor = Color.white;
                GUILayout.Space(2);
            }

            if (_availableSongs.Count == 0)
            {
                GUILayout.Label(
                    "No songs found.\nAdd .json + .ogg files to:\n" +
                    "BepInEx/config/Bragi/songs/",
                    SmallLabel());
            }

            GUILayout.EndScrollView();
        }

        /// <summary>Draws colored dots showing which instruments have dedicated stems for this song.</summary>
        private static void DrawStemDots(SongData song, Rect btnRect)
        {
            if (song.Tracks == null || song.Tracks.Count == 0) return;

            float dotX = btnRect.x + 10;
            float dotY = btnRect.yMax - 12;

            foreach (InstrumentType instr in System.Enum.GetValues(typeof(InstrumentType)))
            {
                if (!song.HasStemFor(instr)) continue;

                GUI.color = instr switch
                {
                    InstrumentType.BoneFlute => new Color(0.6f, 0.9f, 0.6f),
                    InstrumentType.Lyre      => new Color(0.9f, 0.85f, 0.4f),
                    InstrumentType.JawHarp   => new Color(0.7f, 0.5f, 0.9f),
                    _                        => Color.white,
                };
                GUI.DrawTexture(new Rect(dotX, dotY, 8, 8), Texture2D.whiteTexture);
                GUI.color = Color.white;
                dotX += 12;
            }
        }

        private void DrawFooter()
        {
            DrawHorizontalLine();
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();

            // Play button — enabled only when a song is selected
            GUI.enabled = (_selectedIndex >= 0);
            if (GUILayout.Button("▶  Play", GUILayout.Height(34), GUILayout.Width(130)))
                PlaySelected();
            GUI.enabled = true;

            // Stop button — visible while playing
            if (SongPlayer.Instance?.IsPlaying == true)
            {
                if (GUILayout.Button("■  Stop", GUILayout.Height(34), GUILayout.Width(90)))
                    SongPlayer.Instance.StopPlaying();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("✕", GUILayout.Height(34), GUILayout.Width(34)))
                Close();

            GUILayout.EndHorizontal();
            GUILayout.Space(8);
        }

        // ── Actions ───────────────────────────────────────────────────────────

        public void Open(InstrumentType instrumentType)
        {
            _availableSongs = SongLibrary.GetSongsForInstrument(instrumentType);
            _selectedIndex  = _availableSongs.Count > 0 ? 0 : -1;
            _scrollPos      = Vector2.zero;
            IsOpen          = true;

            // Unlock cursor so the player can click
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        public void Close()
        {
            IsOpen = false;

            // Re-lock cursor for normal play
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        /// <summary>Close without toggling — used in OnDestroy to ensure cleanup.</summary>
        private void ForceClose()
        {
            if (IsOpen) Close();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else
            {
                var type = GetEquippedInstrumentType();
                if (type.HasValue) Open(type.Value);
            }
        }

        private void PlaySelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _availableSongs.Count) return;
            var song   = _availableSongs[_selectedIndex];
            var player = SongPlayer.Instance;
            if (player == null) return;

            player.StartNewSession(song);
            Close();
        }

        private void JoinSession()
        {
            var player = SongPlayer.Instance;
            if (player == null) return;

            player.JoinActiveSession();
            Close();
        }

        // ── Instrument Detection ──────────────────────────────────────────────

        private static bool IsInstrumentEquipped()
        {
            var player = Player.m_localPlayer;
            if (player == null) return false;
            var items = player.GetInventory().GetEquippedItems();
            return items.Exists(i => InstrumentDefinitions.IsInstrument(i.m_dropPrefab?.name ?? ""));
        }

        private static InstrumentType? GetEquippedInstrumentType()
        {
            var player = Player.m_localPlayer;
            if (player == null) return null;
            var items  = player.GetInventory().GetEquippedItems();
            var item   = items.Find(i => InstrumentDefinitions.IsInstrument(i.m_dropPrefab?.name ?? ""));
            if (item == null) return null;
            return InstrumentDefinitions.GetType(item.m_dropPrefab?.name ?? "");
        }

        private static string GetInstrumentDisplayName()
        {
            var type = GetEquippedInstrumentType();
            return type switch
            {
                InstrumentType.Lyre      => "Lyre",
                InstrumentType.BoneFlute => "Bone Flute",
                InstrumentType.JawHarp   => "Jaw Harp",
                _                        => "Instrument",
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string FormatDuration(float seconds)
        {
            var t = System.TimeSpan.FromSeconds(seconds);
            return t.Minutes > 0 ? $"{t.Minutes}:{t.Seconds:00}" : $"0:{t.Seconds:00}";
        }

        private static void DrawHorizontalLine()
        {
            var rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            GUI.color = new Color(0.6f, 0.45f, 0.1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // ── GUI Styles ────────────────────────────────────────────────────────

        private static GUIStyle LargeLabel() => new GUIStyle(GUI.skin.label)
        {
            fontSize = 18, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.95f, 0.80f, 0.40f) }
        };

        private static GUIStyle SmallLabel() => new GUIStyle(GUI.skin.label)
        {
            fontSize = 11, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.75f, 0.68f, 0.55f) }
        };

        private static GUIStyle JoinBannerStyle() => new GUIStyle(GUI.skin.button)
        {
            fontSize = 13, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal  = { textColor = new Color(0.85f, 1.0f, 0.85f) },
            hover   = { textColor = Color.white },
        };

        private static GUIStyle SongNameStyle() => new GUIStyle(GUI.skin.label)
        {
            fontSize = 13, fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.95f, 0.90f, 0.80f) }
        };

        private static GUIStyle SubtitleStyle() => new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            normal = { textColor = new Color(0.70f, 0.65f, 0.50f) }
        };

        private static GUIStyle DurationStyle() => new GUIStyle(GUI.skin.label)
        {
            fontSize = 11, alignment = TextAnchor.MiddleRight,
            normal = { textColor = new Color(0.60f, 0.80f, 0.60f) }
        };
    }

    // ── Harmony: block player input while song selection UI is open ────────────

    /// <summary>
    /// Hooks PlayerController.InInventoryEtc() — the game checks this method to determine
    /// whether the player is currently in a menu (inventory, map, chat, etc.).
    /// Returning true here prevents weapon swings, jumping, and camera panning while
    /// the Song Selection UI is open, matching the behaviour of any native Valheim menu.
    /// </summary>
    [HarmonyPatch(typeof(PlayerController), "InInventoryEtc")]
    internal static class BlockInputWhileUIOpenPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref bool __result)
        {
            if (SongSelectUI.IsOpen)
                __result = true;
        }
    }
}
