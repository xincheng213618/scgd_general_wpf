# ProjectARVRPro

`Projects/ProjectARVRPro/` is the main professional AR/VR project package, loaded as `ProjectARVRPro.dll`. It is the primary folder to read when taking over modern AR/VR customer workflows.

## Runtime Identity

| Field | Value |
| --- | --- |
| `Id` | `ProjectARVRPro` |
| `version` | `1.1.7.7` |
| `dllpath` | `ProjectARVRPro.dll` |
| `requires` | `1.3.15.15` |

## Business Scope

ProjectARVRPro covers luminance, uniformity, color, FOFO contrast, chessboard, MTF, distortion, optical center, and OLED AOI. Its core model is `ProcessGroup` plus `ProcessMeta`, which makes it suitable for multiple products, process groups, customers, and output formats.

Unlike ProjectLUX, ARVRPro uses JSON `EventName` dispatch and can coordinate picture switching through `ProjectARVRInit` -> `SwitchPGCompleted` -> `ProjectARVRResult`. Each step can also include `PictureSwitchConfig` for serial picture switching before Flow execution.

## Main Code Entry Points

| File or directory | Responsibility |
| --- | --- |
| `ARVRWindow.xaml(.cs)` | Main test window |
| `ProjectARVRProConfig.cs` | Global config |
| `PluginConfig/` | Menu, launcher, window singleton |
| `Process/` | Test-step framework and all process implementations |
| `Recipe/` | Limits and linear correction configuration |
| `Services/SocketControl.cs` | TCP JSON event dispatch |
| `Services/RunAllSocket.cs` | Run-all handler |
| `Services/SwitchGroupSocket.cs` | External flow-group switching |
| `SocketRelay/` | AOI relay server |
| `ObjectiveTestResult.cs` | Aggregated result model |
| `ViewResultManager.cs` | Result query and persistence |

## Runtime Chain

1. External client sends `ProjectARVRInit`, or an operator enters SN in the window.
2. The project selects the current `ProcessGroup`.
3. The first enabled `ProcessMeta` is selected.
4. If `PictureSwitchConfig` is enabled, serial picture switching runs before Flow.
5. The bound FlowEngine template runs.
6. The matching `IProcess.Execute()` reads Engine algorithm results.
7. Recipe correction and limit judgment are applied.
8. `ObjectiveTestResult` is updated.
9. SQLite, CSV, legacy CSV, custom XLSX, and Socket output are generated according to config.

## ProcessGroup and ProcessMeta

| Object | Field | Meaning |
| --- | --- | --- |
| `ProcessGroup` | `Name` | Product, model, customer plan, or debug scenario |
| `ProcessGroup` | `ProcessMetas` | Ordered test steps |
| `ProcessMeta` | `FlowTemplate` | Bound FlowEngine template |
| `ProcessMeta` | `ProcessTypeFullName` | Result parsing strategy |
| `ProcessMeta` | `IsEnabled` | Whether the step participates in automation |
| `ProcessMeta` | `ConfigJson` | Step-private config |
| `ProcessMeta` | `PictureSwitchConfig` | Picture switch before execution |

Flow groups are persisted in `ProcessGroups.json` under the config directory. Older deployments may still have `ProcessMetas.json`; verify migration before field replacement.

## Socket Events

| EventName | Purpose |
| --- | --- |
| `ProjectARVRInit` | Initialize a test and return the first picture-switch request |
| `SwitchPGCompleted` | External system confirms picture switch and triggers the next step |
| `SwitchGroup` | Switch the current process group |
| `RunAll` | Run all enabled steps |
| `AOITestSwitchImageComplete` | Relay AOI image-switch completion back to Flow |

`SocketRelay/` is a separate ARVRPro communication layer, defaulting to `127.0.0.1:9200`, used by AOI flows to bridge Flow and the external client. A main Socket connection does not prove the relay path works.

## Output Paths

`ARVRWindow.TestCompleted()` uses `ViewResultManager.Config` to decide output behavior:

| Config | Meaning |
| --- | --- |
| `IsSaveCsv` | Save standard CSV |
| `CsvSavePath` | CSV output directory |
| `SaveByDate` | Create date subfolders |
| `UseLegacyARVROutput` | Use old flat CSV and Socket result shape |
| `IsSaveCustomXlsx` | Save custom customer XLSX |
| `CustomOutputProfile` | Field template for custom XLSX |

When changing fields, check standard CSV, legacy CSV, Socket `Data`, and custom XLSX together.

## Build

```powershell
dotnet build Projects/ProjectARVRPro/ProjectARVRPro.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectARVRPro --no-upload
```

## Handoff Acceptance

| Item | Action | Pass criteria |
| --- | --- | --- |
| Project loading | Check `manifest.json`, `ProjectARVRPro.dll`, and the menu entry | Host discovers the package, and `ARVRWindow` opens |
| Flow-group persistence | Create or switch a `ProcessGroup`, then restart | Current group, step order, enabled states, and `ProcessGroups.json` recover |
| Init chain | Send `ProjectARVRInit` with SN | The first enabled step returns `SwitchPG`, and aggregate result is reset |
| Picture confirmation | Send `SwitchPGCompleted` | The current step runs its Flow and matching `IProcess.Execute()` |
| RunAll | Execute `RunAll` on the current group | All enabled steps run in order and follow `AllowTestFailures` |
| PictureSwitch | Enable Thunderbird switching for one step | Command is sent, expected response is received, and the step waits before Flow |
| Recipe/correction | Change a step limit or `K/B` correction | Result value, PASS/FAIL, and display update together |
| Output shape | Toggle `UseLegacyARVROutput` and `IsSaveCustomXlsx` | Standard CSV, legacy CSV/Socket `Data`, and custom XLSX match config |
| SocketRelay/AOI | Run an AOI step with relay enabled | Relay receives Flow request, and `AOITestSwitchImageComplete` resumes the flow |

## First Checks

| Symptom | Check first |
| --- | --- |
| Init returns no next step | Current group has enabled steps, and legacy mode is not skipping the first step |
| `SwitchPGCompleted` does nothing | Window instance, `CurrentTestType`, and handler state |
| Picture switching fails | Thunderbird serial connection, `SendCommand`, `ExpectedResponse`, and timeout |
| RunAll stops midway | `AllowTestFailures`, Flow timeout, preprocessing failure, and template matching |
| CSV fields differ from customer expectation | `UseLegacyARVROutput`, standard exporter, legacy converter, and custom XLSX profile |
| AOI is stuck | Main Socket 6666, Relay 9200, and whether the client replies `AOITestSwitchImageComplete` |
| `IProcess` does not execute | `ProcessMeta.ProcessTypeFullName` resolves to an implementation in the loaded assembly |
| Flow groups disappear after restart | `%APPDATA%\ColorVision\Config\ProcessGroups.json` path and JSON compatibility |
| `SwitchGroup` has no effect | `request.Params` exactly matches the process group name |

## Handoff Notes

- `ProcessGroup` is the product/scenario-level workflow unit.
- `ProcessMeta` controls Flow template, enabled state, picture switching, and private config.
- Customer judgment belongs in project `IProcess`, not in generic Engine templates.
- `UseLegacyARVROutput` affects both CSV and Socket output shape.
- `PictureSwitchConfig` is step-level; copy flow groups carefully.
