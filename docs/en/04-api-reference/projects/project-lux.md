# ProjectLUX

`Projects/ProjectLUX/` is an optical automation project package loaded as `ProjectLUX.dll`. It covers luminance, color, contrast, MTF, distortion, optical center, VID, and luminous flux workflows.

## Runtime Identity

| Field | Value |
| --- | --- |
| `Id` | `ProjectLUX` |
| `version` | `1.0` |
| `dllpath` | `ProjectLUX.dll` |
| `requires` | `1.3.15.10` |

## Business Scope

ProjectLUX differs from ARVRPro mainly by protocol style. LUX uses text commands such as:

```text
T00XX,SN;
```

`XX` maps to `ProcessMeta.SocketCode` in the active process group. LUX handoff must therefore check Flow template, active group, SocketCode, Recipe, Fix, and customer return codes together.

## Main Code Entry Points

| File or directory | Responsibility |
| --- | --- |
| `LUXWindow.xaml(.cs)` | Main test window |
| `ProjectLUXConfig.cs` | Project config |
| `PluginConfig/` | Launcher, menu, window singleton |
| `Process/` | Test framework and test items |
| `Recipe/` | Limit config |
| `Fix/` | Correction factor config |
| `Services/SocketControl.cs` | TCP text command dispatch |
| `ObjectiveTestResult.cs` | Aggregated result model |
| `ViewResultManager.cs` | SQLite result management |

## Runtime Chain

1. Operator enters SN or Socket receives `T00XX,SN;`.
2. The project initializes output directory and `ObjectiveTestResult`.
3. Active `ProcessGroup` and `ProcessMeta` are selected.
4. The bound Flow template runs.
5. The matching `IProcess.Execute()` reads Engine batch results.
6. Fix correction and Recipe limits are applied.
7. `ObjectiveTestResult` and SQLite records are written.
8. CSV/PDF and Socket response are generated.

## Process Model

`ProcessManager` scans loaded assemblies for `IProcess` implementations and stores process groups in `ProcessGroups.json`.

| Object | Field | Meaning |
| --- | --- | --- |
| `ProcessGroup` | `Name` | Product/model/scenario name |
| `ProcessMeta` | `FlowTemplate` | FlowEngine template |
| `ProcessMeta` | `ProcessTypeFullName` | Result parsing strategy |
| `ProcessMeta` | `SocketCode` | `XX` in `T00XX` |
| `ProcessMeta` | `ConfigJson` | Step-private config |

## Socket Commands

| Command | Purpose |
| --- | --- |
| `T0000` | Handshake/init |
| `T0001` | VID |
| `T0002` | Optical center |
| `T0031` | Luminous flux |
| `T00XX` | Run active-group step whose `SocketCode == XX` |

Special outputs include `B_<SN>.csv` for VID and `D_<SN>.csv` for luminous flux. Normal process execution exports `C_<SN>.csv`.

## Recipe and Fix

| Config | Purpose |
| --- | --- |
| `RecipeBase` | Min/max limits |
| `FixConfig` | Calibration or correction coefficients |
| `ProcessConfig` | Per-step behavior stored in `ProcessMeta.ConfigJson` |

Changing judgment rules should usually update Recipe. Changing calibration should update Fix. Changing how data is parsed belongs in Process code/config.

## Build

```powershell
dotnet build Projects/ProjectLUX/ProjectLUX.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectLUX --no-upload
```

## Handoff Acceptance

| Item | Action | Pass criteria |
| --- | --- | --- |
| Project loading | Check `manifest.json`, `ProjectLUX.dll`, and the menu entry | Host discovers the package, and `LUXWindow` opens |
| Flow-group persistence | Create or switch a group, then restart | Group, steps, `SocketCode`, Recipe, and Fix recover |
| Text Socket handshake | Send `T0000,SN;` or the site handshake command | Socket returns a readable response without running a normal process by accident |
| SocketCode execution | Send `T00XX,SN;` | Active group finds `SocketCode == XX` and runs the matching Flow |
| VID command | Send `T0001,SN;` | Camera/autofocus chain runs and exports `B_<SN>.csv` |
| Luminous flux command | Send `T0031,SN;` | Spectrometer chain runs and exports `D_<SN>.csv` |
| Flow result processing | Run a normal step manually or by Socket | `IProcess.Execute()` writes `ObjectiveTestResult` and `ProjectLUX.db` |
| Recipe/Fix | Change limits and correction coefficients, then retest | Final value, PASS/FAIL, CSV, and display match |
| CSV/PDF output | Inspect `ResultSavePath` | Normal process creates `C_<SN>.csv`; required customer files are traceable |

## First Checks

| Symptom | Check first |
| --- | --- |
| Socket command does not start test | Active group, `ProcessMeta.SocketCode`, and `ProjectWindowInstance.WindowInstance` |
| Flow template cannot be found | `ProcessMeta.FlowTemplate` matches `TemplateFlow.Params` |
| CSV is missing | `ProjectLUXConfig.Instance.ResultSavePath` exists and is writable |
| All results fail | Recipe limits, Fix coefficients, and algorithm fields read by `Process.Execute()` |
| Spectrum or VID does not respond | `DeviceSpectrum` / `DeviceCamera` service state and template index |
| Flow group disappears after restart | `%APPDATA%\ColorVision\Config\ProcessGroups.json` saved successfully |
| Customer return code is wrong | `Services/SocketControl.cs` `ReturnCode`, command `lastTwo`, and customer protocol fields |
| Wrong step runs | Active group and whether multiple steps reuse the same `SocketCode` |
| Database has no record | `ViewResultManager`, `ProjectLUX.db` path, and whether `Processing()` ran after Flow completion |

## Handoff Notes

- Socket is text-based, unlike ARVRPro JSON.
- Do not rename Flow templates without checking `SocketCode`.
- `FixConfig` affects final values, so field calibration issues should not be treated as algorithm failures too early.
- Confirm `%APPDATA%\ColorVision\Config\ProcessGroups.json` persists after changes.
