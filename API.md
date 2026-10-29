# AstronomicalEventApi - controlling the event from your own menu

The plugin (`local.astronomical.event`, `AstronomicalEventMod.dll`) exposes one public static class,
`AstronomicalEvent.AstronomicalEventApi`. Everything is primitives/strings/enums, main-thread only,
and purely local (no networking). Version: `AstronomicalEventApi.ApiVersion` ("1.0").

## Option A - reference the DLL (recommended)
In your menu plugin's `.csproj` add a reference to `...\BepInEx\plugins\AstronomicalEventMod\AstronomicalEventMod.dll`
(`<Private>false</Private>`), then:

```csharp
[BepInPlugin("you.menu", "My Menu", "1.0")]
[BepInDependency("local.astronomical.event")]          // load order: event mod first
public class MyMenu : BaseUnityPlugin
{
    void Awake()
    {
        AstronomicalEventApi.BuiltInMenuEnabled = false;   // hide the P-key menu so only yours shows
        AstronomicalEventApi.StateChanged    += s => Logger.LogInfo("event state " + s);
        AstronomicalEventApi.SequenceChanged += i => Logger.LogInfo("sequence " + AstronomicalEventApi.GetSequenceName(i));
    }
    void OnGUI()   // or your VR panel buttons
    {
        if (GUILayout.Button("Start"))  AstronomicalEventApi.Start();          // -1 = preferred start sequence
        if (GUILayout.Button("Pause"))  AstronomicalEventApi.TogglePause();
        if (GUILayout.Button("Stop"))   AstronomicalEventApi.Stop();
        AstronomicalEventApi.Volume = GUILayout.HorizontalSlider(AstronomicalEventApi.Volume, 0, 3);
        GUILayout.Label(AstronomicalEventApi.State + "  " + AstronomicalEventApi.Time.ToString("0") + "/" + AstronomicalEventApi.TotalTime.ToString("0") + "s  " + AstronomicalEventApi.CurrentSequenceName);
    }
}
```

## Option B - no reference (reflection)
```csharp
var api = Type.GetType("AstronomicalEvent.AstronomicalEventApi, AstronomicalEventMod");
api.GetMethod("Start").Invoke(null, new object[] { -1 });
float t = (float)api.GetProperty("Time").GetValue(null);
api.GetProperty("Volume").SetValue(null, 2f);
```
Or find the assembly via `BepInEx.Bootstrap.Chainloader.PluginInfos["local.astronomical.event"].Instance.GetType().Assembly`.

## Members
| Area | Members |
|---|---|
| Readiness | `IsAvailable`, `AssetsReady`, `AssetStatus`, `Message`, `ClearMessage()`, `InForest` |
| Control | `Start(int fromSequence = -1)`, `Pause()`, `Resume()`, `TogglePause()`, `Restart()`, `Stop()`, `SeekToSequence(i)` |
| Status | `State` (`Idle/Running/Paused/Finished`), `IsRunning`, `IsPaused`, `Time`, `TotalTime`, `CurrentSequence`, `CurrentSequenceName`, `CurrentMove` (name of the choreography move Travis is performing right now) |
| Sequences | `SequenceCount`, `GetSequenceName(i)`, `GetSequenceStartTime(i)`, `GetSequenceDuration(i)` |
| Events | `StateChanged(AstronomicalState)`, `SequenceChanged(int)` |
| Stage location | `HasSavedStage`, `UseSavedStage`, `SaveStageHere()`, `ClearSavedStage()`, `SavedStagePosition`, `SavedStageYaw`, `TeleportToStage()` |
| Settings | `Volume` (0-3), `EffectsIntensity`, `LightingIntensity`, `ParticleDensity`, `PlaybackSpeed`, `StageScale`, `PerformerHeight`, `CameraEffects`, `TreeDestruction`, `CleanupOnFinish`, `DebugLogging`, `PreferredStartSequence` |
| Built-in menu | `BuiltInMenuEnabled` (also config `[Menu] BuiltInMenu`), `IsBuiltInMenuOpen`, `OpenBuiltInMenu()`, `CloseBuiltInMenu()` |

Notes
- `Start()` does nothing (and sets `Message`) until `AssetsReady` and while the zone is not Forest; it never starts twice.
- Settings are saved by BepInEx immediately. Particle density and tree destruction take effect on the next Start.
- Nothing teleports the player except an explicit `TeleportToStage()` call from your menu.
- Dropping the event: `Stop()` restores lighting/fog and removes every spawned object.
