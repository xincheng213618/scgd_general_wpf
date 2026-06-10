# ProjectARVRLite

`Projects/ProjectARVRLite/` is a lightweight AR/VR quick-test package loaded as `ProjectARVRLite.dll`. It keeps the AR/VR picture-switch and Socket workflow, but emphasizes configurable enabled test types, preprocessing, and simpler delivery.

## Runtime Identity

| Field | Value |
| --- | --- |
| `Id` | `ProjectARVRLite` |
| `version` | `1.0` |
| `dllpath` | `ProjectARVRLite.dll` |
| `requires` | `1.3.15.6` |

## Business Scope

ProjectARVRLite uses `ProjectARVRLiteTestTypeConfig.json` to decide which test types are enabled, then coordinates `SwitchPG` / `SwitchPGCompleted`, Flow templates, result parsing, CSV, and Socket result return.

Currently implemented template branches cover:

```text
W51, White, W25, Chessboard, MTFHV, Distortion, Ghost, OpticCenter
```

`DotMatrix`, white-screen defect detection, and black-screen defect detection exist as enum/config options, but do not have implemented automation branches in the current `SwitchPGCompleted()` chain. Disable them before field delivery unless code support is added.

## Main Code Entry Points

| File | Responsibility |
| --- | --- |
| `ARVRWindow.xaml(.cs)` | Main window, enabled-test state machine, Flow execution, preprocessing, result return |
| `ProjectARVRLiteConfig.cs` | Project config and template editing |
| `TestTypeConfig.cs` | Enabled/disabled test type config saved under AppData |
| `ProjectARVRReuslt.cs` | Per-flow result entity |
| `ObjectiveTestResult.cs` | Product-level result DTO and CSV export |
| `ARVRRecipeConfig.cs` | Limits for W51, W255, W25, MTFHV, distortion, Ghost, optical axis |
| `ObjectiveTestResultFix.cs` | Result correction coefficients |
| `Services/SocketControl.cs` | Socket event handling |
| `EditTestTypeConfigWindow.xaml(.cs)` | Enabled-test configuration window |

## Automation Chain

1. External client sends `ProjectARVRInit`.
2. Lite auto-creates and shows `ARVRWindow` if it is not open.
3. `InitTest(SN)` resets the step state and product result.
4. The project reads the first enabled test type and returns `SwitchPG`.
5. After `SwitchPGCompleted`, it selects the next enabled test type.
6. `RunTemplate()` executes `PreProcessManager.ExecuteAsync(...)` before starting Flow.
7. Flow completion parses batch results and continues to the next enabled test.
8. `TestCompleted()` aggregates `ObjectiveTestResult`, writes CSV if enabled, and returns `ProjectARVRResult`.

## Socket Events

| Event | Direction | Behavior |
| --- | --- | --- |
| `ProjectARVRInit` | External -> project | Auto-open window, initialize SN, return first `SwitchPG` |
| `SwitchPG` | Project -> external | Ask for a specific `ARVR1TestType` picture |
| `SwitchPGCompleted` | External -> project | Run the next enabled test |
| `ProjectARVRResult` | Project -> external | Return final, failure, or timeout result |
| blank event name | External -> project | Legacy direct-run path; avoid extending it for new protocols |

## Result Output

CSV output is controlled by `ViewResultManager.Config.IsSaveCsv` and uses:

```text
TestResults_{SN}_{yyyyMMdd_HHmmss}_.csv
```

When `SaveByDate` is enabled, a date folder is created first. Result-image output is affected by `SaveImageReusltDelay`, `CodeUseSN`, and `CodeDateFormat`.

## Build

```powershell
dotnet build Projects/ProjectARVRLite/ProjectARVRLite.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectARVRLite --no-upload
```

## Handoff Acceptance

| Item | Action | Pass criteria |
| --- | --- | --- |
| Project loading | Check `manifest.json`, `ProjectARVRLite.dll`, and the menu entry | Host discovers the package, and `ARVRWindow` opens |
| Auto-open on init | Send `ProjectARVRInit` while the window is closed | Lite creates the window, locks SN, and returns the first `SwitchPG` |
| Test-type config | Inspect `ProjectARVRLiteTestTypeConfig.json` or the config window | Unsupported `DotMatrix` and screen-defect items stay disabled unless implemented |
| Picture switching | Send `SwitchPGCompleted` for enabled items | Each enabled item is requested once, and the chain reaches the final enabled test |
| Preprocessing | Run a Flow that requires preprocessing | `PreProcessManager.ExecuteAsync(...)` succeeds before Flow starts; failures return a clear failed result |
| Template matching | Check templates for enabled items | Names contain keywords such as `White51`, `White255_Ghost_Test`, and `MTF_HV` |
| Result aggregation | Run the full enabled set | W51/W255/W25/MTF/Distortion/Ghost/OpticCenter fields are populated |
| CSV/images | Enable `IsSaveCsv` and `SaveByDate`, then run | `TestResults_{SN}_{yyyyMMdd_HHmmss}_.csv` and date folders follow config |
| Failure policy | Simulate Flow or preprocessing failure | `AllowTestFailures` and `TryCountMax` match delivery expectations |

## First Checks

| Symptom | Check first |
| --- | --- |
| `ProjectARVRInit` does not auto-open the window | Plugin loading, `FlowInit.Handle()` window creation, and window singleton state |
| No first `SwitchPG` | At least one enabled test type and valid `ProjectARVRLiteTestTypeConfig.json` |
| Same picture repeats | Unsupported enabled test type or `GetNextEnabledTestType()` cannot find the next valid item |
| Preprocessing fails | `PreProcessManager`, current Flow name, `CVBaseServerNode`, and preprocessing service response |
| Flow template is missing | Template keyword, `TemplateFlow.Params`, and current `TemplateSelectedIndex` |
| Ghost does not affect total result | There is no separate `FlowGhostTestReslut` aggregate flag in current code |
| CSV is missing | `IsSaveCsv`, `CsvSavePath`, `SaveByDate`, and directory permission |
| SN cannot be changed | `SNlocked`, init flow, and empty-SN auto-generation logic |

## Handoff Notes

- Check `%AppData%\ColorVision\Config\ProjectARVRLiteTestTypeConfig.json` before delivery.
- Preprocessing failure stops the Flow before algorithm execution.
- Ghost has parsing logic but no separate `FlowGhostTestReslut` flag in final aggregate checks.
- Template names must include current keywords such as `White51`, `White255_Ghost_Test`, and `MTF_HV`.
