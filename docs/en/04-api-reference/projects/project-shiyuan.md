# ProjectShiyuan

`Projects/ProjectShiyuan/` is a Shiyuan customer-specific project package loaded as `ProjectShiyuan.dll`.

## Runtime Identity

| Field | Value |
| --- | --- |
| `Id` | `ProjectShiyuan` |
| `name` | `视源项目` |
| `version` | `1.0` |
| `dllpath` | `ProjectShiyuan.dll` |
| `requires` | `1.3.15.10` |

## Business Scope

ProjectShiyuan currently focuses on FlowEngine template execution, JND/POI result extraction, customer data-directory output, and pseudo-color image saving. Unlike Heyuan or BlackMura, the current main flow does not complete a full serial/MES upload chain; it is closer to "run Flow -> summarize algorithm results -> copy or generate customer files."

## Main Code Entry Points

| File | Responsibility |
| --- | --- |
| `ShiyuanProjectWindow.xaml(.cs)` | Main window |
| `ShiyuanProjectExport.cs` | Launcher and Tool menu |
| `ProjectShiYuanConfig.cs` | Project config |
| `TempResult.cs`, `NumSet.cs` | Temporary judgment and numeric ranges |
| `SerialMsg.cs` | Serial message model retained in project |
| `manifest.json` | Runtime loading manifest |
| `README.md`, `CHANGELOG.md` | Runtime help and version notes |

## Workflow

1. Tool menu opens `ProjectShiyuan`.
2. The window initializes Flow engine and node editor.
3. Operator selects a Flow template from `TemplateFlow.Params`.
4. Run creates a batch code and calls `FlowControl.Start(sn)`.
5. On Flow completion, the project reads `AlgResultMasterDao` by batch.
6. JND, POI, and `OLED_JND_CalVas` branches extract and display results.
7. CSV files and copied images are saved to `DataPath`.
8. JND validation drives final `OK` / `NG`.

## Outputs

| Result type | Output |
| --- | --- |
| `Compliance_Math_JND` | JND list view; all `Validate` true keeps OK |
| `POI_XYZ` | POI CSV and result list |
| `OLED_JND_CalVas` | `{timestamp}_{SN}_JND.csv` plus input image copy |
| Flow `TPAlgorithmNode.ImgFileName` | Copied to `DataPath` with timestamp |
| Fixed images | `h_gap.tif`, `v_gap.tif`, `luminance.tif` copied from `C:\Windows\System32\pic\`; h/v gap also get pseudo-color images |

## Current Boundaries

- `UploadSN` handler is currently empty and should not be documented as implemented automatic SN upload.
- `SerialMsg.cs` indicates retained serial message structures, not a complete current MES chain.
- Fixed paths under `C:\Windows\System32\pic\` are customer-site coupling points.
- POI CSV output does not by itself mean PASS; JND validation is the main OK/NG boundary.

## Build

```powershell
dotnet build Projects/ProjectShiyuan/ProjectShiyuan.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectShiyuan --no-upload
```

## Handoff Acceptance

| Check | Action | Pass criteria |
| --- | --- | --- |
| Menu entry | Open `ProjectShiyuan` from the Tool menu | Reuses one project window instance instead of opening many duplicates |
| Template loading | Select a Flow template from `TemplateFlow.Params` | `FlowEngineControl` loads `DataBase64`; node editor can open |
| Batch execution | Enter SN and run | `BeginNewBatch()` creates a batch and `FlowCompleted` resolves algorithm results |
| JND judgment | Run a flow containing `Compliance_Math_JND` | JND list is shown; any `Validate=false` turns final result to NG |
| POI export | Run a flow containing `POI_XYZ` | `{timestamp}_{SN}_POI.csv` is generated with POI result fields |
| OLED JND export | Run a flow producing `OLED_JND_CalVas` | `{timestamp}_{SN}_JND.csv` and input image copy are generated |
| Fixed images | Prepare `C:\Windows\System32\pic\h_gap.tif`, `v_gap.tif`, and `luminance.tif` | h/v gap get original and pseudo-color files; luminance is copied |
| Output directory | Change `DataPath` and run | CSV, copied images, and pseudo-color images land in the new folder |

## First Checks

| Symptom | First check | Handling |
| --- | --- | --- |
| Run produces no result | Valid `TemplateSelectedIndex`, whether `FlowCompleted` fired | First run the Flow template in generic FlowEngine |
| JND is NG | `Validate` values from `ComplianceJNDDao` | CSV generation is not PASS; JND validation controls OK/NG |
| POI CSV is empty | Batch id and whether `POI_XYZ` exists in `AlgResultMasterDao` | Confirm SN, batch, and template output type |
| Fixed images are missing | `C:\Windows\System32\pic\*.tif` and `DataPath` permissions | This is a site coupling point, not a generic Engine failure |
| Serial upload is expected | `UploadSN` is currently empty | Implement code before documenting it as a capability |

## Handoff Notes

- Update JND CSV, POI CSV, image copy, and pseudo-color docs together when `DataPath` rules change.
- If serial/MES upload is required, implement `UploadSN`, connection, protocol response, and failure handling first.
- Keep Shiyuan-specific file handling inside the project package, not in Engine.
