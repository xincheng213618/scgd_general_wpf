# Project Capability & Handoff Matrix

This page compares all customer project packages and integration demos under `Projects/`. It answers: what field problem the project solves, how outside systems trigger it, which code owns the workflow, and what output must be accepted during delivery. For concrete field issues around triggers, flow groups, templates, Recipe/Fix, export, or packaging, use [Project Package Runtime And Handoff Playbook](./project-package-playbook.md). For release, field replacement, and rollback evidence, use [Project Package Release Evidence Checklist](./project-release-evidence.md).

Read this page before the [Project Package Handoff Manual](./project-handoff.md) and before opening a specific project folder.

## Project Summary

| Project | Business role | External trigger | Main output | Start with code |
| --- | --- | --- | --- | --- |
| `ProjectARVR` | Early AR/VR optical test with fixed picture-switch order | JSON Socket: `ProjectARVRInit`, `SwitchPGCompleted` | `ObjectiveTestResult`, CSV, Socket `ProjectARVRResult` | `ARVRWindow.xaml.cs`, `Services/SocketControl.cs`, `ObjectiveTestResult.cs` |
| `ProjectARVRLite` | Lightweight AR/VR quick test with configurable enabled items | JSON Socket; init can auto-open the window | `ObjectiveTestResult`, SN/date CSV, Socket result | `ARVRWindow.xaml.cs`, `TestTypeConfig.cs`, `Services/SocketControl.cs` |
| `ProjectARVRPro` | Main professional AR/VR flow-group package | JSON Socket, `RunAll`, `SwitchGroup`, picture-switch serial, AOI relay | SQLite, CSV, Legacy CSV, custom XLSX, Socket result | `ARVRWindow.xaml.cs`, `Process/`, `Recipe/`, `Services/SocketControl.cs`, `SocketRelay/` |
| `ProjectARVRPro.IntegrationDemo` | External TCP/JSON sample for customers or automation controllers | Client connects to port `6666` and sends JSON events | Raw JSON, parsed result table, CSV | `Program.cs`, `MainWindow.xaml.cs`, `Contracts/` |
| `ProjectBlackMura` | Display panel Black Mura inspection | Serial PG/MES: `CON`, `CCPI`, `CSN`, `CGI` | Excel report, POI overlay, Mura result | `MainWindow.xaml.cs`, `HYMesManager.cs`, `ExcelReportGenerator.cs` |
| `ProjectHeyuan` | Heyuan four-point WBRO color/luminance test | STX/ETX serial: `CSN`, `CMI`, `CGI`, `CPT` | WBRO CSV, MES upload result | `ProjectHeyuanWindow.xaml.cs`, `HYMesManager.cs`, `TempResult.cs` |
| `ProjectKB` | Keyboard backlight luminance, uniformity, and autotune | Modbus TCP, MES DLL, optional TCP Socket | SQLite, text, summary, CSV, MES upload | `ProjectKBWindow.xaml.cs`, `Modbus/`, `MesDll.cs`, `BacklightAutotuneService.cs` |
| `ProjectLUX` | LUX optical automation for luminance, color, MTF, distortion, and optical center | Text Socket: `T00XX,SN;` | `ProjectLUX.db`, `C_*.csv`, `B_*.csv`, `D_*.csv`, PDF/CSV | `LUXWindow.xaml.cs`, `Process/`, `Recipe/`, `Fix/`, `Services/SocketControl.cs` |
| `ProjectShiyuan` | Shiyuan JND/POI export and fixed-image post-processing | Mainly manual window and Flow execution in current code | JND CSV, POI CSV, copied input images, pseudo-color images | `ShiyuanProjectWindow.xaml.cs`, `ShiyuanProjectExport.cs`, `ProjectShiYuanConfig.cs` |

## By Protocol and Trigger Mode

| Type | Projects | Entry | Handoff focus |
| --- | --- | --- | --- |
| JSON Socket | `ProjectARVR`, `ProjectARVRLite`, `ProjectARVRPro` | `Services/SocketControl.cs`, event handlers | `EventName`, SN initialization, picture-switch confirmation, final `ProjectARVRResult` |
| Text Socket | `ProjectLUX` | `Services/SocketControl.cs` | Mapping between `T00XX` and `ProcessMeta.SocketCode` |
| Serial/MES | `ProjectBlackMura`, `ProjectHeyuan` | `HYMesManager.cs`, `SerialMsg.cs` | STX/ETX, device id, action return code, NG pass handling |
| PLC/Modbus | `ProjectKB` | `Modbus/ModbusControl.cs` | holding register, trigger value `1`, completion writeback `0`, empty-SN policy |
| Customer demo | `ProjectARVRPro.IntegrationDemo` | `Contracts/`, `Program.cs` | Public JSON contract only; do not import internal ColorVision business logic |
| Manual/offline export | `ProjectShiyuan` | Main window buttons, Flow template selection | `DataPath`, fixed image paths, JND/POI output files |

If a project supports several entry paths, first confirm what the customer site actually uses. Do not merge Modbus, MES, Socket, and manual flows into one mental model.

## By Result Delivery

| Output type | Projects | Key files/config | Acceptance point |
| --- | --- | --- | --- |
| Product-level CSV | `ProjectARVR`, `ProjectARVRLite`, `ProjectARVRPro`, `ProjectLUX` | `ObjectiveTestResult`, CSV exporter, `ViewResultManager.Config` | File name, directory, field order, PASS/FAIL, legacy compatibility |
| SQLite/local result | `ProjectARVRPro`, `ProjectLUX`, `ProjectKB` | `ViewResultManager`, `Project*Reuslt`, `KBItemMaster` | Query by SN/time and match batch plus Flow template |
| Excel/XLSX | `ProjectBlackMura`, `ProjectARVRPro` | `ExcelReportGenerator`, `CustomTestResultExportService` | Customer title, template fields, output path, dependency library |
| MES/serial upload | `ProjectBlackMura`, `ProjectHeyuan`, `ProjectKB` | `HYMesManager`, `MesDll.cs`, `Summary` | Return-code convention, device id, station/line/operator/machine fields |
| Socket response | `ProjectARVR*`, `ProjectLUX` | Socket handlers, `ObjectiveTestResult`, Legacy converter | `Data` shape, status code, timeout/failure response |
| Images and pseudo-color | `ProjectBlackMura`, `ProjectShiyuan` | `ImageView`, `OpenCVMediaHelper`, fixed input image path | Image exists, overlay coordinate, pseudo-color naming |
| Summary/text | `ProjectKB` | `Summary.cs`, `ViewResultManager.Config` | Model folders, yield summary, failure appendix |

When changing result fields, check every output channel. ARVRPro commonly requires standard CSV, Legacy CSV, Socket `Data`, and custom XLSX checks. KB commonly requires CSV, summary, MES `Collect_test`, and database checks.

## Flow Organization

| Project | Flow organization | Config/object | Risk |
| --- | --- | --- | --- |
| `ProjectARVR` | Fixed enum order until `OpticCenter` | `StepIndex`, `ARVRTestType`, template keywords | Later enum values do not prove implemented automation |
| `ProjectARVRLite` | Enabled test type config controls order | `ProjectARVRLiteTestTypeConfig.json`, `ARVR1TestType` | Enabling unimplemented branches breaks automation |
| `ProjectARVRPro` | `ProcessGroup` + `ProcessMeta` | `ProcessGroups.json`, `PictureSwitchConfig` | Flow group, picture switching, Recipe, and output format migrate together |
| `ProjectLUX` | `ProcessGroup` + `SocketCode` | `ProcessGroups.json`, `ProcessMeta.SocketCode` | Renaming flow without SocketCode update breaks external commands |
| `ProjectBlackMura` | Fixed five-color flow | `StepIndex`, template keyword | PG switch response and template keyword must match |
| `ProjectHeyuan` | Fixed four-point result order | `White/Blue/Red/Orange`, `TempResult` | Fewer than four POI results is a business error |
| `ProjectKB` | Current Flow template plus Recipe | `RecipeManager`, `KBRecipeConfig`, Modbus config | POI name/width must match KB template |
| `ProjectShiyuan` | Manual Flow template selection | `TemplateSelectedIndex`, `DataPath` | Serial config is present but automatic upload is not complete in the current main flow |

## Minimum Smoke Acceptance

| Project | Smoke check |
| --- | --- |
| `ProjectARVR` | Open window, send `ProjectARVRInit`, receive first `SwitchPG`, complete to `OpticCenter`, generate CSV and `ProjectARVRResult` |
| `ProjectARVRLite` | Check enabled item config, run enabled tests, confirm preprocessing, CSV, and Socket result |
| `ProjectARVRPro` | Switch one flow group, run `RunAll` or full `ProjectARVRInit` chain, confirm picture switching, Recipe, CSV/Legacy/Socket output |
| `ProjectARVRPro.IntegrationDemo` | Parse sample JSON, send `ProjectARVRInit` or `RunAll` online, confirm partial/sticky-packet reader returns a full result |
| `ProjectBlackMura` | Power on and switch five colors through serial PG, finish `<SN>.xlsx`, show POI overlay |
| `ProjectHeyuan` | Serial connect, Flow outputs four POI values, generate WBRO CSV, PASS triggers `CMI`/`CPT` chain |
| `ProjectKB` | Modbus write `1` triggers run, completion writes back `0`, CSV/summary/MES match config |
| `ProjectLUX` | Send one `T00XX,SN;`, match active-group `SocketCode`, generate CSV/SQLite result |
| `ProjectShiyuan` | Manually run Flow, generate JND/POI CSV, copy fixed-path images and pseudo-color images when present |

## Build and Delivery Matrix

| Project | Build command | Package command | Extra delivery checks |
| --- | --- | --- | --- |
| `ProjectARVR` | `dotnet build Projects/ProjectARVR/ProjectARVR.csproj -c Release -p:Platform=x64` | `Scripts\package_project.bat ProjectARVR --no-upload` | Socket protocol, template keywords, CSV path |
| `ProjectARVRLite` | `dotnet build Projects/ProjectARVRLite/ProjectARVRLite.csproj -c Release -p:Platform=x64` | `Scripts\package_project.bat ProjectARVRLite --no-upload` | Test-type config, preprocessing, CSV switch |
| `ProjectARVRPro` | `dotnet build Projects/ProjectARVRPro/ProjectARVRPro.csproj -c Release -p:Platform=x64` | `Scripts\package_project.bat ProjectARVRPro --no-upload` | Legacy output, custom XLSX, SocketRelay, picture-switch serial |
| `ProjectBlackMura` | `dotnet build Projects/ProjectBlackMura/ProjectBlackMura.csproj -c Release -p:Platform=x64` | `Scripts\package_project.bat ProjectBlackMura --no-upload` | EPPlus, serial device id, Excel output path |
| `ProjectHeyuan` | `dotnet build Projects/ProjectHeyuan/ProjectHeyuan.csproj -c Release -p:Platform=x64` | `Scripts\package_project.bat ProjectHeyuan --no-upload` | Serial port, DeviceId, CSV directory |
| `ProjectKB` | `dotnet build Projects/ProjectKB/ProjectKB.csproj -c Release -p:Platform=x64` | `Scripts\package_project.bat ProjectKB --no-upload` | `FunTestDll.dll`, `FunTestDllConfig.INI`, Modbus address |
| `ProjectLUX` | `dotnet build Projects/ProjectLUX/ProjectLUX.csproj -c Release -p:Platform=x64` | `Scripts\package_project.bat ProjectLUX --no-upload` | SocketCode, Recipe/Fix, output directory |
| `ProjectShiyuan` | `dotnet build Projects/ProjectShiyuan/ProjectShiyuan.csproj -c Release -p:Platform=x64` | `Scripts\package_project.bat ProjectShiyuan --no-upload` | `DataPath`, fixed image path, whether serial chain remains disabled |

`ProjectARVRPro.IntegrationDemo` is not a plugin package:

```powershell
dotnet publish Projects/ProjectARVRPro.IntegrationDemo/ProjectARVRPro.IntegrationDemo.csproj -f net48 -c Release -p:Platform=x64 -o artifacts/ProjectARVRPro.IntegrationDemo
```

## Change Ownership

| Change | Primary location | Update docs |
| --- | --- | --- |
| Add a customer test item | Project `Process/`, `Recipe/`, `Fix/`, result model | Specific project page and this matrix |
| Add an external event or command | Project `Services/SocketControl.cs`, `HYMesManager`, `ModbusControl` | Protocol classification and project page |
| Change result fields | `ObjectiveTestResult`, exporter, Socket response, SQLite model | Result delivery matrix and project page |
| Change flow group or template names | `ProcessGroup` / `ProcessMeta` / main window keyword matching | Project handoff manual and project page |
| Change package contents | `manifest.json`, `.csproj`, package script, README/CHANGELOG | Project overview and build scripts docs |
| Generalize shared capability | Engine, UI, or general plugin | Engine matrix, UI DLL docs, plugin matrix |

Customer-specific logic belongs in the project package by default.

## Continue Reading

- [Project Package Runtime And Handoff Playbook](./project-package-playbook.md)
- [Project Package Release Evidence Checklist](./project-release-evidence.md)
- [Project Package Handoff Manual](./project-handoff.md)
