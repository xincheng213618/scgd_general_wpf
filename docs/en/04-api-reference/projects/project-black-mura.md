# ProjectBlackMura

`Projects/ProjectBlackMura/` is a display-panel Black Mura inspection project package loaded as `ProjectBlackMura.dll`.

## Runtime Identity

| Field | Value |
| --- | --- |
| `Id` | `ProjectBlackMura` |
| `version` | `1.0` |
| `dllpath` | `ProjectBlackMura.dll` |
| `requires` | `1.3.15.10` |

## Business Scope

ProjectBlackMura is not a single algorithm wrapper. It combines PG power control, PG picture switching, five picture flows, Engine result parsing, POI overlay, Excel report generation, and MES/serial state into a field workflow.

The current picture order is:

```text
None -> White -> Black -> Red -> Green -> Blue
```

`White` and `Black` drive luminance uniformity, gradient, and contrast judgment. `Red`, `Green`, and `Blue` feed color and wavelength result summaries.

## Main Code Entry Points

| File | Responsibility |
| --- | --- |
| `MainWindow.xaml(.cs)` | Main window and flow control |
| `ProjectBlackMuraConfig.cs` | Project config |
| `PluginConfig/BlackMuraProject.cs` | Launcher |
| `PluginConfig/BlackMuraMenu.cs` | Tool menu entry |
| `ExcelReportGenerator.cs` | Excel report generation |
| `HYMesManager.cs` | MES and PG serial control |
| `Config/EditARVRConfig.xaml(.cs)` | Config window |
| `manifest.json` | Runtime loading manifest |

## Workflow

1. Operator enters SN and starts the test.
2. The project powers on PG through `HYMesManager.PGPowerOn()`.
3. Serial responses such as `CON,S` and `CCPI,S` advance the workflow.
4. Each `CCPI,S` moves to the next `BlackMuraTestType`.
5. The project selects Flow templates by keywords `White`, `Black`, `Red`, `Green`, and `Blue`.
6. After `Blue`, it uploads SN, generates Excel, and powers off PG.

## Serial/MES Protocol

Messages are wrapped with `0x02` and `0x03`. Common commands include:

| Command | Purpose |
| --- | --- |
| `CON,C,{DeviceId}` | PG power on |
| `COFF,C,{DeviceId}` | PG power off |
| `CCPI,C,{DeviceId},{id}` | Switch PG picture |
| `CSN,C,{DeviceId},{sn}` | Upload product SN |
| `CGI,C,{DeviceId},Default,{Msg}` | Upload NG information |

If the workflow stops, first check whether `CCPICompleted` fired, whether complete STX/ETX serial frames were received, and whether `HYMesConfig.DeviceId` matches the site device.

## Report Output

`ExcelReportGenerator.GenerateExcel()` writes `<SN>.xlsx` under `ProjectBlackMuraConfig.ResultSavePath`. The report includes SN/model/time, white/black/red/green/blue measurements, CIE values, gradient, contrast, border size, and Mura coordinates.

The window also opens the result image and draws POI overlays in `ImageView`.

## Build

```powershell
dotnet build Projects/ProjectBlackMura/ProjectBlackMura.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectBlackMura --no-upload
```

## Handoff Acceptance

| Check | Action | Pass criteria |
| --- | --- | --- |
| Manual flow | Select a Flow template containing a color keyword and run once | `CurrentTestType` is detected; result image and POI overlay are normal |
| PG power on | Start the automatic test and send `CON,C,{DeviceId}` | Successful `CON,S` sets `StepIndex=1` and advances to White |
| Five picture switching | Advance after each `CCPI,S` | `StepIndex` goes from 1 to 5; template keyword and PG id match |
| Retry behavior | Force a Flow failure | Retries while `TryCount < TryCountMax`, then fails cleanly |
| Result parsing | Complete White/Black/RGB flows | `BlackMudraResult` receives luminance, uniformity, CIE, wavelength, and Mura fields |
| Excel report | Inspect output after Blue completes | `ResultSavePath\<SN>.xlsx` exists and matches window results |
| PG power off | Finish full workflow | Sends `COFF,C,{DeviceId}` and resets window state |

## First Checks

| Symptom | Check first |
| --- | --- |
| PG does not power on after starting test | Serial port/baud rate, `HYMesConfig.DeviceId`, whether `CON,C,{DeviceId}` was sent, and complete STX/ETX frames |
| `CON,S` arrives but White does not start | `CONCompleted` event, `StepIndex=1`, and `HYMesConfig.IsSingleMes` mode |
| `CCPI,S` does not advance the flow | `CCPICompleted`, current `BlackMuraTestType`, picture id, and `StepIndex` |
| Flow template cannot be found | Template name contains `White`, `Black`, `Red`, `Green`, or `Blue` keyword |
| Flow succeeds but report fields are empty | `AlgResultMasterDao`, `PoiPointResultDao`, Black Mura JSON, and `BlackMudraResult` mapping |
| Excel is missing | `ResultSavePath` permission, SN after `SNMax` truncation, EPPlus dependency, and `ExcelReportGenerator` exception |
| POI overlay is misplaced | Result image path, `ViewImageReadDelay`, POI coordinate system, and ImageEditor overlay type |
| Retry does not happen | `TryCount`, `TryCountMax`, Flow completion status, and early failure path |
| NG info is not uploaded | Whether `CGI,C,{DeviceId},Default,{Msg}` is sent and fail fields are written to `BlackMudraResult` |
| PG does not power off | Blue completion chain, `PGPowerOff()` call, and `COFF,S` response |

## Change Impact

| Change | Must check |
| --- | --- |
| Color order or new picture | `BlackMuraTestType`, `MainWindow_CCPICompleted`, PG picture id, Excel fields |
| Template naming | Keyword matching for `White`, `Black`, `Red`, `Green`, and `Blue` |
| Judgment fields | `BlackMudraResult`, `ExcelReportGenerator`, window result text, MES NG message |
| Serial protocol | `HYMesManager` send fields, response parsing, site `DeviceId` |
| Result image loading | `ViewImageReadDelay`, file existence, ImageEditor overlay coordinates |

## Handoff Notes

- EPPlus is part of the report path; check license and output compatibility before upgrades.
- Serial PG/MES protocol is customer-site logic and should not move into Engine.
- Template name changes must update the keyword matching in the main window.
- If result images fail to open, check file write delay before changing algorithm code.
