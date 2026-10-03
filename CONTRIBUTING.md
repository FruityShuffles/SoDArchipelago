# Contributing

## Repository

| Path | What |
|---|---|
| `apworld/shape_of_dreams/` | The Archipelago world (Python) that generates seeds |
| `mod/SoDArchipelago/` | The client mod (C#), loaded by the game's official mod loader |
| `examples/` | The example player YAML |
| `tools/` | Game data extraction, apworld packaging and test helpers |
| [`DESIGN.md`](DESIGN.md) | How the randomizer works, in detail |
| [`TESTING.md`](TESTING.md) | The in-game test checklist |

## Building

- **Mod:** .NET SDK 8 and the game installed. Set `SOD_GAME_DIR` if the game isn't in the default Steam folder (or
  copy `mod/GamePath.user.props.example` to `mod/GamePath.user.props`). `dotnet build mod/SoDArchipelago.sln` builds the
  mod and copies it into the game's `Mods` folder (`-p:DeployToGame=false` skips that).
- **apworld:** `python tools/build_apworld.py` writes `dist/shape_of_dreams.apworld`. For tests, copy
  `apworld/shape_of_dreams` into an Archipelago checkout's `worlds/` and run `python -m pytest worlds/shape_of_dreams/test`.

The mod and the apworld from the same release belong together: the mod refuses seeds generated from different game data.
