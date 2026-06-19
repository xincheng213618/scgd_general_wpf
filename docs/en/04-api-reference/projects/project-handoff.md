# Project Package Handoff Manual

Project packages are not ordinary tool plugins. They combine customer test order, FlowEngine templates, device actions, Recipe/Fix rules, Socket/MES protocols, and result export into a production workflow. During handoff, do not start from one `Process` class in isolation. First connect: who triggers the test, which flow runs, where the result is written, and how the outside system receives it.

If you do not yet know the protocol or output style of a project, first read the [Project Capability & Handoff Matrix](./project-capability-matrix.md). If you already have a concrete field issue, use [Project Package Runtime And Handoff Playbook](./project-package-playbook.md). For release, field replacement, and rollback evidence, fill in [Project Package Release Evidence Checklist](./project-release-evidence.md) before reading this execution chain.

## First Identify the Project Type

| Type | Typical projects | Handoff focus |
| --- | --- | --- |
| AR/VR flow-group projects | `ProjectARVRPro/`, `ProjectLUX/` | `ProcessGroup`, `ProcessMeta`, FlowTemplate, Recipe, Socket protocol |
| Lightweight or historical AR/VR projects | `ProjectARVR/`, `ProjectARVRLite/` | Compatibility, legacy events, fixed test type order, CSV fields |
| Business algorithm projects | `ProjectBlackMura/`, `ProjectKB/` | Algorithm parameters, result models, report/export fields |
| Customer-specific projects | `ProjectHeyuan/`, `ProjectShiyuan/` | Customer protocol, field config, menu entry, device dependency |
| Integration demos | `ProjectARVRPro.IntegrationDemo/` | How external systems send JSON and parse results |

## Common Execution Chain

| Step | Code entry | Confirm |
| --- | --- | --- |
| Plugin loading | `manifest.json`, `PluginConfig/` | `Id`, `dllpath`, menu name, window singleton, minimum host version |
| Initialization | Main window `InitTest()` | How SN is written and whether previous `ObjectiveTestResult` is reset |
| Flow selection | `ProcessManager`, `ProcessGroup` | Active group, enabled steps, order, legacy config migration |
| Template binding | `ProcessMeta.FlowTemplate` | Flow template name matches `TemplateFlow.Params` |
| Flow run | Main window `RunTemplate()` or `RunAllAsync()` | Batch creation, preprocessing, timeout, retry |
| Result parsing | `IProcess.Execute(ctx)` | Which Engine result is read and how Recipe/Fix participates |
| Result aggregation | `ObjectiveTestResult` | Which field or dynamic collection receives each test item |
| Save/export | `ViewResultManager`, CSV/XLSX/PDF exporter | SQLite path, export path, legacy switches, file naming |
| External response | `Services/SocketControl.cs` or handler | Text/JSON protocol, status code, final result event |

## Configuration Layers

| Layer | Purpose | Typical files or classes |
| --- | --- | --- |
| Project global config | SN, paths, retry count, output mode | `Project*Config`, `ViewResultManagerConfig` |
| Flow configuration | Which steps run for the current product and which template each step uses | `ProcessGroups.json`, older `ProcessMetas.json` |
| Judgment/correction config | Limits, correction factors, customer specifications | `RecipeManager`, `FixManager`, `RecipeBase`, `FixConfig` |

`ProcessGroups.json` is the newer flow-group model. It is usually saved under `%APPDATA%\ColorVision\Config\`. Older projects may still have `ProcessMetas.json`; confirm migration logic before upgrading a field site.

## ProcessGroup and ProcessMeta

`ProcessGroup` is a product or scenario workflow. It contains an ordered list of `ProcessMeta` objects. A `ProcessMeta` describes one test step; it is not the algorithm result itself.

| Field | Meaning | Maintenance risk |
| --- | --- | --- |
| `Name` | Operator-facing step name | Renaming can confuse operators |
| `FlowTemplate` | Bound FlowEngine template name | Name mismatch prevents the step from starting |
| `ProcessTypeFullName` | Bound `IProcess` implementation type | Class or namespace changes can break old configs |
| `IsEnabled` | Whether the step participates in automation | Affects final result completeness |
| `ConfigJson` | Step-private behavior config | Field renames can break old configs |
| `SocketCode` | `ProjectLUX` text protocol step code | Must match customer command `T00XX` |
| `PictureSwitchConfig` | `ProjectARVRPro` picture-switch config | Serial command, response, timeout, and delay affect cycle time |

When adding a step, copy a similar `Process` folder and verify: the `IProcess` type is discoverable, `FlowTemplate` matches a real template, Recipe/Fix can be edited, and the result is exported.

## IProcess Extension Pattern

`IProcess` is the business core of many project packages. Engine runs devices and algorithms; the project `Process` maps algorithm results into customer judgment.

| Method | Purpose |
| --- | --- |
| `Execute(IProcessExecutionContext ctx)` | Main business entry: read batch result, apply Recipe/Fix, write `ObjectiveTestResult` |
| `Render(ctx)` | Optional result visualization |
| `GenText(ctx)` | Optional text summary |
| `GetRecipeConfig()` | Return limit configuration |
| `GetFixConfig()` | Return correction configuration |
| `GetProcessConfig()` / `SetProcessConfig()` | Save and restore step-private config |
| `CreateInstance()` | Create a same-type process instance when loading or copying steps |

Do not put customer judgment rules back into generic Engine handlers. Engine templates and device results should remain reusable.

## Socket and Protocol Styles

| Project | Protocol style | Entry | Note |
| --- | --- | --- | --- |
| ProjectLUX | Text command, such as `T00XX,SN;` | `Services/SocketControl.cs` | `XX` maps to `ProcessMeta.SocketCode` |
| ProjectARVRPro | JSON events | `Services/SocketControl.cs`, `RunAllSocket.cs`, `SwitchGroupSocket.cs` | Events include `ProjectARVRInit`, `SwitchPGCompleted`, `SwitchGroup`, `RunAll` |
| ProjectARVRPro AOI | JSON plus relay server | `SocketRelay/` | Flow and external client communicate through relay port 9200 |
| IntegrationDemo | Client sample | `ProjectARVRPro.IntegrationDemo/` | Demonstrates the external side of the ARVRPro protocol |

Protocol changes must update the project docs page, project README or protocol manual, and customer-facing integration material.

## Result Delivery Chain

| Result type | Purpose | Typical location |
| --- | --- | --- |
| Process result | One Flow execution status, batch id, template name, elapsed time | `Project*Reuslt`, SQLite |
| Customer result | Aggregated judgment and exported fields for one product test | `ObjectiveTestResult`, CSV/XLSX/PDF, Socket `Data` |

When results look wrong, first confirm the Flow completed, then confirm the matching `IProcess.Execute()` ran, then inspect the exporter and legacy-output switches.

## Handoff Checklist

| Check | Pass condition |
| --- | --- |
| manifest | `Id`, `dllpath`, `requires`, and package name match |
| menu entry | Host can open the project window from Tools or project menu |
| flow group | At least one valid group exists and key steps are enabled |
| template binding | Every `FlowTemplate` resolves to a real template |
| Recipe/Fix | Editors open, save, and survive restart |
| Socket/MES/serial | External command can initialize, confirm switching, run, and receive result |
| result output | SQLite, CSV/XLSX/PDF, or customer upload path is writable |
| compatibility | Old config, old export format, or old protocol switches are documented |

## Recommended Reading Order

1. [Project Capability & Handoff Matrix](./project-capability-matrix.md).
2. Current project docs page or source README.
3. `PluginConfig/`.
4. Main window `.xaml.cs`, especially `InitTest`, `RunTemplate`, `Processing`, or `RunAllAsync`.
5. `Process/ProcessManager.cs`, `ProcessMeta.cs`, `IProcess.cs`.
6. Specific `Process/<TestName>/` folder.
7. `Recipe/`, `Fix/`, `ObjectiveTestResult.cs`, `ViewResultManager.cs`.
8. `Services/` or `SocketRelay/`.
