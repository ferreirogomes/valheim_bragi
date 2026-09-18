# CHANGELOG

## [0.1.0] — 2026-09-18

### Added
- **Lyre** — craftable stringed instrument (Workbench: Fine Wood x8, Deer Hide x2, Resin x4)
- **Bone Flute** — craftable wind instrument (Workbench: Bone Fragments x4, Feathers x2)
- **Jaw Harp** — craftable iron instrument (Forge: Iron x2, Leather Scraps x1)
- **Song Selection UI** — press G while instrument equipped to open song menu
- **6 default songs**: Odin's Call, Jorvik Reel, Skald's Lament, Munnharpe Drone, Feast of Valhalla, Winter Night
- **Multiplayer sync** — nearby players hear music via ZRoutedRpc (configurable range)
- **Skald's Blessing** — bard buff: +15% stamina regen for nearby allies while playing
- **Extensible song system** — drop JSON + OGG into BepInEx/config/Bragi/songs/ to add songs
- **Full BepInEx config** — volume, range, buff strength all configurable

### Known Issues
- Placeholder meshes used (Club model) — proper instrument models coming in v0.2
- Audio clips not bundled (placeholder JSONs only) — add your own OGG files
