# Bragi — Viking Music Mod for Valheim

> *"Bragi is best known for his wisdom and is most eloquent, and has the gift of eloquence to an unusual degree."*
> — Prose Edda, Snorri Sturluson

**Bragi** adds historically-grounded Viking-era musical instruments to Valheim. Craft a Lyre, Bone Flute, or Jaw Harp, equip it, and perform Norse songs around the longhouse fire. Nearby players in multiplayer will hear your music in 3D spatial audio. Bards are rewarded with the **Skald's Blessing** — a stamina regen bonus for the whole party.

---

## Features

- 🎻 **3 Craftable Instruments** — Lyre, Bone Flute, Jaw Harp (more coming in v1.1)
- 🎵 **6 Default Songs** — Curated Viking-era inspired tracks
- 🌐 **Multiplayer Sync** — Other players hear your music within configurable range
- ✨ **Skald's Blessing** — Bard buff: +15% stamina regen for nearby allies while playing
- 📂 **Extensible Song System** — Drop any `.ogg` + `.json` pair into the songs folder to add custom music

## Requirements

- [BepInExPack Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/) `5.4.2202+`
- [Jötunn](https://valheim.thunderstore.io/package/ValheimModding/Jotunn/) `2.21.0+`

## Installation (r2modman — Recommended)

1. Install [r2modman](https://r2modman.com/download-latest/)
2. Search for **Bragi** in the Thunderstore tab
3. Click Install — dependencies are handled automatically

## Manual Installation

1. Install BepInExPack_Valheim
2. Install Jötunn
3. Extract `Bragi.zip` contents into your Valheim game folder

## Instruments & Recipes

| Instrument | Materials | Station | Era |
|---|---|---|---|
| **Bone Flute** | Bone Fragments ×4, Feathers ×2 | Workbench | Early game |
| **Lyre** | Fine Wood ×8, Deer Hide ×2, Resin ×4 | Workbench | Black Forest |
| **Jaw Harp** | Iron ×2, Leather Scraps ×1 | Forge | Iron Age |

## How to Play

1. Craft an instrument and **equip** it
2. Press **G** (configurable) to open the Song Selection menu
3. Choose a song and press **Play** (or Enter)
4. Press **G** again or **Stop** to end the song

## Adding Custom Songs

1. Find a royalty-free `.ogg` or `.wav` file
2. Create a JSON file in `BepInEx/config/Bragi/songs/`:

```json
{
  "id": "my_custom_song",
  "name": "My Viking Song",
  "author": "Me",
  "description": "A custom song I added!",
  "audioFile": "my_song.ogg",
  "duration": 120,
  "instruments": [],
  "mood": "Joyful"
}
```

3. Place the `.ogg` next to the `.json` file
4. Restart the game — your song will appear in the menu!

## Configuration

Edit `BepInEx/config/com.bragi.valheim.cfg`:

| Setting | Default | Description |
|---|---|---|
| `MasterVolume` | `1.0` | Global volume multiplier |
| `MusicRange` | `30` | Metres other players hear you |
| `BuffEnabled` | `true` | Toggle Skald's Blessing buff |
| `BuffRadius` | `15` | Buff range in metres |
| `BuffDuration` | `60` | Seconds buff lasts after music stops |
| `StaminaRegenBonus` | `0.15` | +% stamina regen bonus |
| `OpenMenuKey` | `G` | Key to open song menu |

## Changelog

### v0.1.0
- Initial release
- Lyre, Bone Flute, Jaw Harp
- 6 default songs
- Song selection UI
- Multiplayer sync via ZRoutedRpc
- Skald's Blessing bard buff

## Credits

- Mod by: nimbc
- Norse music research: archaeomusicology sources
- Audio clips: Royalty-free / CC0 (see ATTRIBUTIONS.md)
- Built with [Jötunn](https://valheim-modding.github.io/Jotunn/)
