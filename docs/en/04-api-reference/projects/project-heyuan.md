# ProjectHeyuan

`Projects/ProjectHeyuan/` is a Heyuan Jingdian customer-specific project package loaded as `ProjectHeyuan.dll`.

## Runtime Identity

| Field | Value |
| --- | --- |
| `Id` | `ProjectHeyuan` |
| `version` | `1.0` |
| `dllpath` | `ProjectHeyuan.dll` |
| `requires` | `1.3.15.10` |

## Business Scope

ProjectHeyuan owns a four-point color/luminance test and customer serial pass-through. It combines FlowEngine templates, POI/Compliance results, CSV trace, serial upload, and NG pass confirmation in one window.

The fixed point order is:

```text
White, Blue, Red, Orange
```

These points are collected into `TempResult` and used for PASS/FAIL, CSV, and MES upload.

## Main Code Entry Points

| File | Responsibility |
| --- | --- |
| `ProjectHeyuanWindow.xaml(.cs)` | Main window |
| `MenuItemHeyuan.cs` | Launcher and Tool menu |
| `HYMesManager.cs` | MES and serial manager |
| `SerialMsg.cs` | Serial message model |
| `TempResult.cs` | Temporary four-point result |
| `NumSet.cs` | Numeric limits/settings |
| `manifest.json` | Runtime loading manifest |

## Workflow

1. Tool menu opens the Heyuan project window.
2. `HYMesManager.OpenPort()` connects to the selected serial port at 38400 baud.
3. Operator selects a Flow template from `TemplateFlow.Params`.
4. Before execution, SN is checked and optionally uploaded.
5. `FlowControl.Start(sn)` runs the selected Flow and creates a `MeasureBatchModel`.
6. On completion, the project reads `POI_XYZ` and `Compliance_Math_CIE_XYZ` results.
7. Exactly four POI results must be resolved, otherwise the project reports result-data error.
8. Values are ordered as White -> Blue -> Red -> Orange, judged, written to CSV, and uploaded.
9. PASS calls `UploadMes()`; FAIL prompts for NG pass and calls `UploadNG()`.

## Serial Protocol

All outgoing messages are wrapped as:

```text
0x02 + ASCII text + 0x03
```

| Command | Purpose |
| --- | --- |
| `CSN,C,{DeviceId},{SN}` | Upload product SN |
| `CMI,C,{DeviceId},{TestName},White,...,Blue,...,Red,...,Orange,...` | Upload four-point measurement |
| `CGI,C,{DeviceId},Default,{Msg}` | Upload NG information |
| `CPT,C,{DeviceId}` | Post/pass command |

Received `CSN,S`, `CPT,S`, `CGI,S`, and `CMI,S` are treated as successful when the final field contains `0`.

## Result Output

CSV is saved to `HYMesConfig.DataPath`:

```text
yyyy-MM-dd_{TestName}_{MachineName}.csv
```

Each point includes `x`, `y`, `Lv`, `Dw`, and `Result`.

## Build

```powershell
dotnet build Projects/ProjectHeyuan/ProjectHeyuan.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectHeyuan --no-upload
```

## Handoff Acceptance

| Check | Action | Pass criteria |
| --- | --- | --- |
| Serial connection | Select the site serial port and open it | `IsConnect=true`; receive buffer can parse STX/ETX packets |
| SN upload | Enter SN and trigger `UploadSN()` manually or automatically | Sends `CSN,C,{DeviceId},{SN}` and receives successful `CSN,S` |
| Flow execution | Select a template with four-point output and run | `FlowCompleted` resolves White, Blue, Red, and Orange `TempResult` items |
| PASS upload | All four points pass | Sends `CMI,C,...`; successful `CMI,S` is followed by `CPT,C,{DeviceId}` |
| NG upload | Any point fails or flow errors | Sends `CGI,C,{DeviceId},Default,{Msg}` according to site policy |
| CSV trace | Inspect `DataPath` after run | Generates `yyyy-MM-dd_{TestName}_{MachineName}.csv` with customer header order |
| Data order | Compare CSV and upload message | Docs, CSV, and `UploadMes()` all interpret White, Blue, Red, Orange the same way |

## First Checks

| Symptom | First check | Handling |
| --- | --- | --- |
| Port opens but no response | 38400 baud, STX/ETX framing, `DeviceId` | Capture raw serial log before changing Flow |
| SN upload fails | Whether final `CSN,S` field contains `0` | Do not treat later results as bound to SN |
| Too few results | Whether both `POI_XYZ` and `Compliance_Math_CIE_XYZ` exist | Template must output four color-matched points |
| PASS does not pass station | `CMI,S` success and whether `SendPost()` sent `CPT` | Separate result upload failure from post/pass failure |
| CSV and MES differ | `CsvHandler` header and `UploadMes()` order | Color order changes must update both |

## Handoff Notes

- Serial/MES protocol fields are customer-site boundaries.
- Flow output must contain four POI values; partial output is a business error.
- Current `Button_Click` calls `flowControl.Start(sn)` twice; investigate here if duplicate batches appear.
- Changing color order, `TestName`, or field format requires CSV and protocol updates.
