# J's Live Events Mod

BepInEx plugin for Gorilla Tag that recreates Fortnite live events client-side (Collision, Butterfly, New Year, Ice Storm, ...).
Events are data folders (`event.json` + glb models + music) under `BepInEx/plugins/JsLiveEventsMod/AstronomicalAssets/events/<id>/`; the scenes are coded in the `Astronomical*.cs` files.

- `tools/` - Blender / Python generators for the models and animations (not included: the large glb / mp3 assets and `tools/cache`).
- `AstronomicalDebug.cs` - in-game test console (write commands to `BepInEx/eventtest.txt`; see the file header). `tools/gt_restart.sh` installs and relaunches the game.
- `PROJECT_STATUS.md` - running log of every change.
- `tools/compile_check/check.sh` - compiles the event mod and J CLIENT without the game (stubs + Unity reference assemblies; see its README).

## Everything needed to continue
- `AstronomicalAssets/` - the installed event folders (copy to `BepInEx/plugins/JsLiveEventsMod/`), `tools/cache/collision` - built Collision models, `sources/` - the original supplied models + music.
- Build: `dotnet build -c Release`; install: `python tools/build_collision.py install`; test in-game: `tools/gt_restart.sh` + `BepInEx/eventtest.txt` commands (see `AstronomicalDebug.cs`). Blender 5.2 + Python with numpy are used by the tools.

## J CLIENT
`JClient/` is the source of the J CLIENT menu mod (with `EventMods.cs` that drives this plugin) plus its built `JClient.dll`.
## Install events from the repo
Press P in-game (built-in menu) -> "INSTALL FROM REPO" -> Load event list -> Install; or put `repo` / `install <id>` in `BepInEx/eventtest.txt`. Files that already match are skipped (it also updates).
