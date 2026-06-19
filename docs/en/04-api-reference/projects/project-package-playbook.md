# Project Package Runtime And Handoff Playbook

This page is for maintainers who take over customer project packages, troubleshoot field protocol/result/workflow issues, or publish project `.cvxp` packages. It does not replace individual project pages. It connects trigger, workflow, template, Recipe/Fix, result export, external response, and packaging into executable scenarios.

If you do not yet know what each project owns, start with [Project Capability & Handoff Matrix](./project-capability-matrix.md). For release, field replacement, and rollback evidence, fill in [Project Package Release Evidence Checklist](./project-release-evidence.md). For the common execution chain and `ProcessGroup` / `IProcess` model, read [Project Package Handoff Manual](./project-handoff.md).

## How To Use This Page

1. Use [Scenario Entry Points](#scenario-entry-points) to decide whether the issue belongs to loading, external trigger, flow/template, judgment config, result output, device/MES, package delivery, or integration demo validation.
2. Follow the scenario to inspect project folder, config, main window, `Process/`, `Recipe/`, `Fix/`, `Services/`, and exporters.
3. Fill in [Project Package Handoff Record](#project-package-handoff-record) with protocol, output, config path, package version, and acceptance result.

## Scenario Entry Points

| Problem | Start here | Typical projects |
| --- | --- | --- |
| Menu or project window does not open after installation | [Scenario A](#scenario-a-project-entry-or-window-does-not-open) | All project packages |
| Outside system sends a command but test does not start | [Scenario B](#scenario-b-external-trigger-does-not-start-test) | ARVR, ARVRLite, ARVRPro, LUX, KB, BlackMura, Heyuan |
| Workflow starts but runs wrong step or cannot find template | [Scenario C](#scenario-c-flow-group-or-template-binding-is-wrong) | ARVRPro, LUX, KB, Shiyuan |
| Algorithm has result but PASS/FAIL or customer field is wrong | [Scenario D](#scenario-d-judgment-config-or-result-field-is-wrong) | ARVRPro, LUX, KB, BlackMura, Heyuan |
| CSV/XLSX/PDF/SQLite/MES output misses fields | [Scenario E](#scenario-e-result-export-or-customer-response-is-wrong) | ARVR series, LUX, KB, BlackMura, Heyuan, Shiyuan |
| Serial, Modbus, MES, PG, or picture switching fails | [Scenario F](#scenario-f-device-mes-or-picture-switching-fails) | BlackMura, Heyuan, KB, ARVRPro |
| Need to publish a project `.cvxp` | [Scenario G](#scenario-g-package-and-deliver-project-package) | All project packages except IntegrationDemo |
| Customer only needs integration demo or protocol validation | [Scenario H](#scenario-h-customer-integration-demo-validation) | ProjectARVRPro.IntegrationDemo |

## Runtime Model

Project packages usually enter the host under `Plugins/<Name>/`, but they are different from general plugins. Plugins provide reusable tools. Project packages deliver customer production workflows.

Common chain:

```text
manifest / PluginConfig
  -> project window
  -> external command or manual start
  -> active flow group / fixed workflow
  -> FlowTemplate
  -> Engine Flow execution
  -> IProcess reads result and applies Recipe/Fix
  -> ObjectiveTestResult aggregation
  -> SQLite / CSV / XLSX / PDF / MES / Socket response
```

Do not inspect only the exporter or CSV. Many project failures happen earlier: command mismatch, wrong active flow group, changed `FlowTemplate` name, Recipe/Fix not loaded, or `IProcess.Execute()` not called.

## Scenario A: Project Entry Or Window Does Not Open

Steps:

1. Confirm the project folder is under host output `Plugins/<ProjectName>/`.
2. Check `manifest.json` `id`, `dllpath`, and `requires`.
3. Check `PluginConfig/` or root `Menu*.cs` for menu registration and window singleton.
4. Read host logs for plugin loading, dependency DLL, manifest, or window-constructor errors.
5. Open the project page and confirm entry menu name and whether administrator/device environment is required.

Current entry focus:

| Project | Entry focus |
| --- | --- |
| ARVR series | `PluginConfig/ProjectARVRMenu.cs`, window singleton |
| LUX | `PluginConfig/ProjectLUXMenu.cs`, `LUXWindow` |
| KB | `PluginConfig/KBMenu.cs`, `ProjectKBWindow` |
| BlackMura | `PluginConfig/BlackMuraMenu.cs`, `MainWindow` |
| Heyuan | `MenuItemHeyuan.cs`, `ProjectHeyuanWindow` |
| Shiyuan | Project window and fixed export config |

## Scenario B: External Trigger Does Not Start Test

First identify trigger type:

| Type | Project | Key entry | Confirm first |
| --- | --- | --- | --- |
| JSON Socket | ProjectARVR, ProjectARVRLite, ProjectARVRPro | `Services/SocketControl.cs`, handlers | `EventName`, SN, whether window exists, picture-switch confirmation |
| Text Socket | ProjectLUX | `Services/SocketControl.cs` | Whether `XX` in `T00XX,SN;` matches `ProcessMeta.SocketCode` |
| Modbus TCP | ProjectKB | `Modbus/ModbusControl.cs` | holding register, trigger value `1`, completion writeback `0`, SN source |
| Serial/MES | ProjectBlackMura, ProjectHeyuan | `HYMesManager.cs`, `SerialMsg.cs` | STX/ETX, device id, action code, return code |
| Manual/offline | ProjectShiyuan | Main window buttons and Flow selection | `DataPath`, template selection, fixed image path |

Steps:

1. Confirm host Socket or serial/Modbus service is enabled.
2. Confirm the project window is open, or that source supports automatic window creation.
3. Compare external command fields with the current project page.
4. Confirm SN, flow group, template, and current state allow start.
5. If initialization works but execution does not continue, check whether the project is waiting for `SwitchPGCompleted`, PG response, Modbus state, or MES approval.

## Scenario C: Flow Group Or Template Binding Is Wrong

Applies to projects that select Flow templates or flow groups, such as ARVRPro, LUX, KB, and Shiyuan.

Steps:

1. Identify the workflow model: `ProcessGroup`, fixed enum, enabled-item config, or manual template selection.
2. Check current config files, such as `ProcessGroups.json`, `ProjectARVRLiteTestTypeConfig.json`, KB Recipe config, or Shiyuan `TemplateSelectedIndex`.
3. Confirm the `FlowTemplate` string can be found in Engine `TemplateFlow.Params`.
4. If class or namespace changed, confirm `ProcessTypeFullName` can still deserialize.
5. If the customer says the wrong item ran, check active group, enabled steps, and `SocketCode` before changing algorithm logic.

High-risk fields:

| Field | Impact |
| --- | --- |
| `ProcessMeta.FlowTemplate` | Name mismatch prevents Flow start |
| `ProcessMeta.ProcessTypeFullName` | Class rename can break old config loading |
| `ProcessMeta.IsEnabled` | Affects automation and final result completeness |
| `ProcessMeta.SocketCode` | Determines whether ProjectLUX external command finds the step |
| `PictureSwitchConfig` | ARVRPro picture-switch serial command, response, and delay |

## Scenario D: Judgment Config Or Result Field Is Wrong

Project judgment usually happens in `IProcess.Execute(ctx)`, not in generic Engine templates.

Steps:

1. Confirm the Flow completed and produced Engine results.
2. Confirm the correct `IProcess` implementation was matched.
3. Inspect `Recipe/` limits, customer specs, and enabled items.
4. Inspect `Fix/` correction factors, calibration parameters, or customer compensation.
5. Confirm `ObjectiveTestResult` writes the correct fields.
6. For customer-specific output, inspect exporters or legacy converters for old fields.

Project focus:

| Project | Judgment focus |
| --- | --- |
| ARVR/ARVRLite | Fixed test types, `ObjectiveTestResult`, CSV switches |
| ARVRPro | `Process/`, `Recipe/`, Legacy CSV, custom XLSX |
| LUX | `Process/`, `Recipe/`, `Fix/`, `SocketCode` |
| BlackMura | Five-color flow, Mura result, Excel report |
| Heyuan | WBRO four-point order, `TempResult`, MES return |
| KB | POI name/width, backlight autotune, MES DLL fields |
| Shiyuan | JND/POI CSV, fixed images and pseudo-color output |

## Scenario E: Result Export Or Customer Response Is Wrong

Result fields often have multiple exits and must be verified together:

| Exit | Typical projects | Acceptance |
| --- | --- | --- |
| SQLite | ARVRPro, LUX, KB | Query by SN, time, flow, or model |
| CSV | ARVR series, LUX, KB, Heyuan, Shiyuan | File name, folder, field order, PASS/FAIL |
| XLSX/Excel | BlackMura, ARVRPro | Template fields, customer title, images/POI |
| PDF | LUX | Output path, image resources, customer fields |
| MES/serial upload | BlackMura, Heyuan, KB | Return code, device id, line/station/operator |
| Socket response | ARVR series, LUX | `Code`, `Msg`, `Data` shape and timeout |

Steps:

1. Confirm local result aggregation reached `ObjectiveTestResult` or a project result model.
2. Check whether the project has legacy or customer-specific output switches.
3. Verify file output, Socket/MES response, and local database together.
4. If only CSV changed, explicitly document whether other exits keep the old format.

## Scenario F: Device, MES, Or Picture Switching Fails

| Project | External boundary | Check first |
| --- | --- | --- |
| ARVRPro | Picture-switch serial, AOI Relay, Socket JSON | `PictureSwitchConfig`, `SocketRelay/`, switch-complete event |
| BlackMura | PG serial, MES, five-color images | `HYMesManager.cs`, `SerialMsg.cs`, PG response |
| Heyuan | STX/ETX serial, WBRO upload | `HYMesManager.cs`, `TempResult.cs` |
| KB | Modbus TCP, MES DLL, FunTestDll | `Modbus/`, `MesDll.cs`, `FunTestDllConfig.INI` |
| LUX | Text Socket, flow-group command code | `SocketCode`, `ProcessGroup`, output folder |

Principles:

- Confirm project window state, device connection, and external service before changing business code.
- Serial/MES issues must record raw command, return code, timeout, and device id.
- Modbus issues must record address, trigger value, completion writeback, and SN source.
- Picture-switch issues must record sent command, expected response, actual response, and delay.

## Scenario G: Package And Deliver Project Package

Common command:

```powershell
Scripts\package_project.bat ProjectLUX --no-upload
```

`package_project.bat` calls `Scripts/package_cvxp.py`, the same package flow used by plugins: build project, collect output, remove host-shared files, copy root `README.md`, `CHANGELOG.md`, `manifest.json`, and `PackageIcon.png`, then use main DLL `FileVersion` to create `.cvxp`.

Pre-delivery checks:

| Item | Check |
| --- | --- |
| manifest | `id`, `dllpath`, `version`, `requires` |
| README/CHANGELOG | Match the current customer version |
| config | Whether flow group, Recipe, Fix, Socket/MES, and path config travel with package or are imported on site |
| native/external DLL | `FunTestDll.dll`, MES DLL, serial/device SDK documented |
| output path | CSV/XLSX/PDF/SQLite/MES folders writable |
| host dependency | Host `ColorVision.*.dll` satisfies project `.deps.json` |
| rollback package | Previous `.cvxp`, config backup, field project folder |

IntegrationDemo is not a project plugin package. Publish it with:

```powershell
dotnet publish Projects/ProjectARVRPro.IntegrationDemo/ProjectARVRPro.IntegrationDemo.csproj -f net48 -c Release -p:Platform=x64 -o artifacts/ProjectARVRPro.IntegrationDemo
```

## Scenario H: Customer Integration Demo Validation

ProjectARVRPro.IntegrationDemo is a sample for customers or host systems to validate ARVRPro JSON/TCP protocol. It should not import internal ColorVision business logic.

Acceptance:

1. Reads sample JSON, such as `Samples/project-arvr-result.json`.
2. Connects to a test server or field ARVRPro Socket.
3. Sends `ProjectARVRInit`, `SwitchPGCompleted`, `RunAll`, or agreed events.
4. Handles partial/sticky TCP packets and displays a full response.
5. Exports customer-readable CSV or result table.

If the demo and project package protocol diverge, update the demo, ARVRPro project page, protocol manual, and customer delivery notes together.

## Project Package Handoff Record

Every project handoff or release should record:

| Item | Content |
| --- | --- |
| Project | Name, source directory, manifest id |
| Customer/scenario | Customer name, product model, field trigger mode |
| Versions | `manifest.version`, `.csproj VersionPrefix`, output DLL `FileVersion`, `.cvxp` file name |
| Build commands | `dotnet build`, `Scripts\package_project.bat ... --no-upload`, or demo publish |
| Protocol | Socket/MES/serial/Modbus event, command, return code, timeout |
| Workflow | Active flow group, enabled steps, template name, `SocketCode` or fixed order |
| Config | Recipe/Fix, path, SN, output mode, device config |
| Output | SQLite, CSV, XLSX, PDF, MES, Socket response fields |
| Acceptance | Minimum smoke result, customer-field check, field dependencies |
| Rollback | Previous package, config backup, database backup, customer protocol version |
| Known limits | Untested device, legacy compatibility, permissions, manual steps |

## Continue Reading

- [Project Capability & Handoff Matrix](./project-capability-matrix.md)
- [Project Package Release Evidence Checklist](./project-release-evidence.md)
- [Project Package Handoff Manual](./project-handoff.md)
- [ProjectARVR](./project-arvr.md)
- [ProjectARVRLite](./project-arvr-lite.md)
- [ProjectARVRPro](./project-arvr-pro.md)
- [ProjectARVRPro.IntegrationDemo](./project-arvr-pro-integration-demo.md)
- [ProjectBlackMura](./project-black-mura.md)
- [ProjectHeyuan](./project-heyuan.md)
- [ProjectKB](./project-kb.md)
- [ProjectLUX](./project-lux.md)
- [ProjectShiyuan](./project-shiyuan.md)
