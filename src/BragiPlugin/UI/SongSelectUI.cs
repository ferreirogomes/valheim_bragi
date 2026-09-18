using UnityEngine;
using UnityEngine.UI;

namespace Bragi
{
    /// <summary>
    /// Song Selection UI — opens when the player presses the configured key (default: G)
    /// while an instrument is equipped. Displays a scrollable list of compatible songs
    /// styled to match Valheim's native dark-wood UI aesthetic.
    ///
    /// Built with Valheim's IMGUI / Unity UI stack to avoid external dependencies.
    /// </summary>
    public class SongSelectUI : MonoBehaviour
    {
        public static SongSelectUI? Instance { get; private set; }

        private bool _isOpen;
        private System.Collections.Generic.List<SongData> _availableSongs =
            new System.Collections.Generic.List<SongData>();

        // IMGUI layout constants
        private const int PanelW = 420;
        private const int PanelH = 520;
        private Rect _panelRect;
        private Vector2 _scrollPos;
        private int _selectedIndex = -1;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            _panelRect = new Rect(
                (Screen.width  - PanelW) / 2f,
                (Screen.height - PanelH) / 2f,
                PanelW, PanelH);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Open / close on configured key while instrument equipped
            if (Input.GetKeyDown(BragiConfig.OpenMenuKey.Value) && IsInstrumentEquipped())
            {
                Toggle();
            }

            // Close on Escape
            if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }

            // Press Enter to play selected
            if (_isOpen && _selectedIndex >= 0 && Input.GetKeyDown(KeyCode.Return))
            {
                PlaySelected();
            }
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            // Block game input while UI is open
            GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Keyboard);

            GUI.skin = GUISkin.CreateInstance<GUISkin>(); // use default skin (replaced with Jotunn skin later)

            // Dark semi-transparent background overlay
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Main panel
            GUI.Box(_panelRect, "");
            GUILayout.BeginArea(_panelRect);

            DrawHeader();
            DrawSongList();
            DrawFooter();

            GUILayout.EndArea();
        }

        // ── Drawing ───────────────────────────────────────────────────────────

        private void DrawHeader()
        {
            GUILayout.Space(8);
            GUILayout.Label($"♪  Choose a Song  ♪", LargeLabel());
            GUILayout.Label(GetInstrumentDisplayName(), SmallLabel());
            GUILayout.Space(4);
            DrawHorizontalLine();
            GUILayout.Space(4);
        }

        private void DrawSongList()
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos,
                GUILayout.Width(PanelW - 16), GUILayout.Height(PanelH - 120));

            for (int i = 0; i < _availableSongs.Count; i++)
            {
                var song  = _availableSongs[i];
                bool selected = (i == _selectedIndex);

                GUI.backgroundColor = selected
                    ? new Color(0.6f, 0.45f, 0.1f, 0.9f)  // Warm amber (selected)
                    : new Color(0.2f, 0.15f, 0.08f, 0.8f); // Dark wood (normal)

                if (GUILayout.Button("", GUILayout.Height(52)))
                    _selectedIndex = i;

                // Draw song info overlaid on button
                var btnRect = GUILayoutUtility.GetLastRect();
                GUI.Label(new Rect(btnRect.x + 8, btnRect.y + 4, btnRect.width - 80, 20),
                    song.Name, SongNameStyle());
                GUI.Label(new Rect(btnRect.x + 8, btnRect.y + 24, btnRect.width - 80, 18),
                    $"{song.Author}  ·  {song.Mood}", SubtitleStyle());
                GUI.Label(new Rect(btnRect.xMax - 70, btnRect.y + 16, 64, 20),
                    FormatDuration(song.Duration), DurationStyle());

                GUI.backgroundColor = Color.white;
                GUILayout.Space(2);
            }

            if (_availableSongs.Count == 0)
            {
                GUILayout.Label("No songs found.\nAdd .json + .ogg files to:\nBepInEx/config/Bragi/songs/", SmallLabel());
            }

            GUILayout.EndScrollView();
        }

        private void DrawFooter()
        {
            DrawHorizontalLine();
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();

            GUI.enabled = _selectedIndex >= 0;
            if (GUILayout.Button("▶  Play", GUILayout.Height(32), GUILayout.Width(140)))
                PlaySelected();
            GUI.enabled = true;

            if (SongPlayer.Instance?.IsPlaying == true)
            {
                if (GUILayout.Button("■  Stop", GUILayout.Height(32), GUILayout.Width(100)))
                    SongPlayer.Instance.StopPlaying();
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", GUILayout.Height(32), GUILayout.Width(36)))
                Close();

            GUILayout.EndHorizontal();
            GUILayout.Space(6);
        }

        // ── Actions ───────────────────────────────────────────────────────────

        public void Open(InstrumentType instrumentType)
        {
            _availableSongs  = SongLibrary.GetSongsForInstrument(instrumentType);
            _selectedIndex   = _availableSongs.Count > 0 ? 0 : -1;
            _scrollPos       = Vector2.zero;
            _isOpen          = true;
        }

        public void Close()
        {
            _isOpen = false;
        }

        public void Toggle()
        {
            if (_isOpen) Close();
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

            player.Play(song);
            Close();
        }

        // ── Instrument detection ──────────────────────────────────────────────

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
            var items = player.GetInventory().GetEquippedItems();
            var item = items.Find(i => InstrumentDefinitions.IsInstrument(i.m_dropPrefab?.name ?? ""));
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
            normal = { textColor = new Color(0.95f, 0.8f, 0.4f) }
        };

        private static GUIStyle SmallLabel() => new GUIStyle(GUI.skin.label)
        {
            fontSize = 11, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.75f, 0.68f, 0.55f) }
        };

        private static GUIStyle SongNameStyle() => new GUIStyle(GUI.skin.label)
        {
            fontSize = 13, fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.95f, 0.9f, 0.8f) }
        };

        private static GUIStyle SubtitleStyle() => new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            normal = { textColor = new Color(0.7f, 0.65f, 0.5f) }
        };

        private static GUIStyle DurationStyle() => new GUIStyle(GUI.skin.label)
        {
            fontSize = 11, alignment = TextAnchor.MiddleRight,
            normal = { textColor = new Color(0.6f, 0.8f, 0.6f) }
        };
    }
}
