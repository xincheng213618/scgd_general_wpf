# ProjectARVR

`Projects/ProjectARVR/` is an early AR/VR optical-test project package loaded at runtime as `ProjectARVR.dll`. It combines fixed PG picture switching, FlowEngine template execution, `ObjectiveTestResult` aggregation, CSV export, and Socket result return.

## Runtime Identity

| Field | Value |
| --- | --- |
| `Id` | `ProjectARVR` |
| `version` | `1.0` |
| `dllpath` | `ProjectARVR.dll` |
| `requires` | `1.3.9.10` |

## Business Scope

ProjectARVR targets AR/VR display optical tests: white-screen FOV, luminance/color uniformity, center CCT/luminance, black-screen contrast, chessboard contrast, horizontal/vertical MTF, distortion, and optical-axis angle.

The current automation chain runs in a fixed order and effectively completes at `OpticCenter`:

```text
White2 -> White -> White1 -> Black -> Chessboard -> MTFH -> MTFV -> Distortion -> OpticCenter -> ProjectARVRResult
```

Later enum values such as `Ghost`, `DotMatrix`, and screen-defect items exist in code, but the current `SwitchPGCompleted()` chain does not execute templates for them.

## Main Code Entry Points

| File | Responsibility |
| --- | --- |
| `ARVRWindow.xaml(.cs)` | Main window, picture-switch state machine, Flow execution, result parsing, Socket return |
| `ProjectARVRConfig.cs` | Project config and template editing |
| `ProjectARVRReuslt.cs` | Per-flow result entity |
| `ObjectiveTestResult.cs` | Product-level result DTO and CSV export |
| `ARVRRecipeConfig.cs` | Limits for white/black/chessboard/MTF/distortion/optical axis |
| `ObjectiveTestResultFix.cs` | Result correction coefficients |
| `ViewResultManager.cs` | Result list, persistence, CSV path config |
| `Services/SocketControl.cs` | Socket event handling |
| `PluginConfig/ProjectARVRMenu.cs` | Tool menu entry |

## Automation Chain

1. External client sends `ProjectARVRInit`.
2. `FlowInit.Handle()` records the `NetworkStream`; the ARVR window must already be open.
3. `InitTest(SN)` resets `StepIndex`, `ObjectiveTestResult`, and `CurrentTestType`.
4. The project returns `SwitchPG` for the first picture.
5. External client switches the picture and sends `SwitchPGCompleted`.
6. The project selects the next `ARVRTestType`, finds a Flow template by keyword, and runs it.
7. Flow completion reads algorithm results from the batch and updates the current test result.
8. At `OpticCenter`, `TestCompleted()` builds the product result, writes CSV, and returns `ProjectARVRResult`.

## Socket Events

| Event | Direction | Behavior |
| --- | --- | --- |
| `ProjectARVRInit` | External -> project | Initialize SN and state; fails if the window is not open |
| `SwitchPG` | Project -> external | Ask external system to switch to an `ARVRTestType` picture |
| `SwitchPGCompleted` | External -> project | Confirm picture switch and start the matching Flow |
| `ProjectARVRResult` | Project -> external | Return final, timeout, or failure result |
| `ProjectARVR` | External -> project | Checks `request.Params` against `TemplateFlow.Params`, then calls current window `RunTemplate()` |

The `ProjectARVR` event currently validates `request.Params` but does not switch `FlowTemplate.SelectedValue` to that requested template. Do not document it as "run arbitrary requested Flow" unless the code is changed.

## Results and Delivery

Each Flow saves a `ProjectARVRReuslt`; final completion aggregates into `ObjectiveTestResult`. CSV files are named:

```text
ObjectiveTestResults_{yyyyMMdd_HHmmss}.csv
```

The CSV directory comes from `ViewResultManager.Config.CsvSavePath`. Current `TestCompleted()` requires a connected Socket stream; without it, CSV and Socket return may be skipped.

## Build

```powershell
dotnet build Projects/ProjectARVR/ProjectARVR.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectARVR --no-upload
```

## Handoff Acceptance

| Item | Action | Pass criteria |
| --- | --- | --- |
| Project loading | Check `manifest.json`, `ProjectARVR.dll`, and the menu entry | Host discovers `ProjectARVR`, and `ARVRWindow` opens |
| Init precondition | Open the window first, then send `ProjectARVRInit` | No `Code=-3`; SN is recorded and the first `SwitchPG` is returned |
| Fixed picture order | Send `SwitchPGCompleted` step by step | The chain advances through `White2 -> White -> White1 -> Black -> Chessboard -> MTFH -> MTFV -> Distortion -> OpticCenter` |
| Template matching | Check Flow template names for each step | Names contain expected keywords such as `WhiteFOV`, `MTF_H`, and `OpticCenter` |
| Flow execution | Confirm each picture switch starts Flow | Batch creation succeeds, and Flow completion enters `Processing()` |
| Result aggregation | Run the full product chain | `ObjectiveTestResult` contains item values, limits, and PASS/FAIL |
| CSV output | Inspect `ViewResultManager.Config.CsvSavePath` | `ObjectiveTestResults_{yyyyMMdd_HHmmss}.csv` is created |
| Socket return | Complete at `OpticCenter` | External client receives `ProjectARVRResult` for success, failure, or timeout |
| Failure policy | Test with `AllowTestFailures=true/false` | true continues where possible; false returns failure early |

## First Checks

| Symptom | Check first |
| --- | --- |
| `ProjectARVRInit` says the window is not open | Whether `ARVRWindow` is already open and `FlowInit.Handle()` has `WindowInstance` |
| No `SwitchPG` after init | SN reset, `StepIndex`, and `CurrentTestType` starting at `White2` |
| `SwitchPGCompleted` does not run Flow | Current test type, template keyword matching, selected Flow template, and window instance |
| Behavior around `Ghost` is unexpected | The current automation is delivered only through `OpticCenter`; later enum values are not automated here |
| Flow succeeds but product result is empty | Batch algorithm JSON parsing and `Flow*TestReslut` flags |
| CSV is missing | Valid Socket stream in `TestCompleted()` and writable `CsvSavePath` |
| Socket result is missing | `SocketControl.Current.Stream`, client connection, and `ProjectARVRResult` serialization |
| `ProjectARVR` event does not run requested template | Current code validates `request.Params` but does not switch to that template |

## Handoff Notes

- `ProjectARVRInit` requires the window to already be open; Lite differs by auto-opening the window.
- Template matching depends on keywords such as `White255`, `MTF_H`, and `OpticCenter`.
- Do not treat later enum values as implemented automation.
- For new AR/VR delivery, evaluate ProjectARVRPro or ProjectARVRLite first.
