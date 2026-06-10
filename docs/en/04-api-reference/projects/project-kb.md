# ProjectKB

`Projects/ProjectKB/` is a keyboard backlight inspection project package loaded as `ProjectKB.dll`. It combines FlowEngine capture, KB templates, POI luminance results, Recipe judgment, backlight autotune, PLC/Modbus trigger, MES DLL upload, and CSV/summary trace.

## Runtime Identity

| Field | Value |
| --- | --- |
| `Id` | `ProjectKB` |
| `version` | `1.0` |
| `dllpath` | `ProjectKB.dll` |
| `requires` | `1.3.15.10` |

## Entry Modes

| Entry | Meaning |
| --- | --- |
| Manual run | Operator enters SN, selects Flow template, and runs in the window |
| Modbus trigger | PLC writes holding register value `1`; project runs and writes back `0` |
| MES/SN upload | `Summary.AutoUploadSN` calls `FunTestDll.dll` `CheckWIP`; completion calls `Collect_test` |

## Main Code Entry Points

| File | Responsibility |
| --- | --- |
| `ProjectKBWindow.xaml(.cs)` | Main window, Flow execution, result parsing, CSV/MES/Modbus writeback |
| `ProjectKBConfig.cs` | Project config and Modbus/Socket config entry |
| `KBRecipeConfig.cs` | Luminance, uniformity, local contrast, autotune limits |
| `BacklightAutotuneService.cs` | Q1/Q3 and sigmoid-based correction |
| `KBItemMaster.cs` | Main result entity |
| `KBItem.cs` | Per-key result |
| `Summary.cs` | Yield, MES station/line/operator/device config |
| `Modbus/ModbusControl.cs` | Modbus TCP connect, polling, register writeback |
| `MesDll.cs` | P/Invoke wrapper for `FunTestDll.dll` |
| `Recipes/RecipeManager.cs` | Switch Recipe by Flow template name |

## Workflow

1. Window initializes Flow engine and loads templates.
2. Selecting a Flow template also switches the current Recipe by template name.
3. If `AutoModbusConnect` is enabled, Modbus connects on window startup.
4. When the configured register becomes `1`, `RunTemplate()` starts unless empty-SN policy blocks it.
5. Flow completion reads `KB` / `KB_Raw` and `POI_Y` / `POI_Y_V2` results.
6. Each key luminance is calculated and local contrast is computed.
7. Per-key limits and aggregate limits produce PASS/FAIL.
8. Optional backlight autotune stores raw values and adjusted values.
9. Database, text, summary, CSV, Modbus writeback, and MES upload are performed as configured.

## External Integration

| Integration | Key facts |
| --- | --- |
| Modbus | Default host `127.0.0.1`, port `502`, trigger value `1`, completion writeback `0` |
| MES DLL | Loaded from `Plugins/ProjectKB/FunTestDll.dll`; `CheckWIP` returning `"N"` is considered pass in current code |
| TCP Socket | Optional simple listener; messages must contain `#` and `*`; do not confuse it with Modbus |

## Outputs

`KBItemMaster` includes model, SN, batch, KB template, per-key values, average/min/max luminance, uniformity, brightest/darkest key, failure count, autotune values, result image, and final PASS/FAIL.

Output switches and paths live under `ViewResultManager.Config`: text, summary, CSV, and failure appendix.

## Build

```powershell
dotnet build Projects/ProjectKB/ProjectKB.csproj -c Release -p:Platform=x64
Scripts\package_project.bat ProjectKB --no-upload
```

## Handoff Acceptance

| Item | Action | Pass criteria |
| --- | --- | --- |
| Project loading | Check `manifest.json`, `ProjectKB.dll`, and the menu entry | Host discovers the package, and `ProjectKBWindow` opens |
| Flow/Recipe alignment | Select a KB Flow template | `RecipeManager.SetCurrentTemplate(Name)` selects the matching Recipe |
| Manual run | Enter SN and run current template | Flow creates `KBItemMaster`, and the result list shows PASS/FAIL |
| Modbus trigger | Connect PLC and write register value `1` | Test starts automatically and writes back `0` at completion |
| Empty-SN policy | Enable `IgnoreAutoRunWhenSnEmpty` and trigger with empty SN | Auto trigger is ignored and register still resets |
| MES CheckWIP | Enable `Summary.AutoUploadSN` and enter SN | `FunTestDll.dll` is called; `"N"` locks SN successfully |
| Result parsing | Run a Flow with `KB` / `POI_Y` results | Per-key luminance, chromaticity, local contrast, and fail count are populated |
| Backlight autotune | Enable autotune in Recipe | Raw and adjusted values are kept, and final judgment uses configured adjustment |
| Trace outputs | Enable text, summary, and CSV saving | Database, txt, summary, and `<Model>_<yyyyMMdd>.csv` are generated |
| Delivery dependencies | Inspect package output | `FunTestDll.dll`, `FunTestDllConfig.INI`, manifest, README, and CHANGELOG are included |

## First Checks

| Symptom | Check first |
| --- | --- |
| Modbus value `1` does not trigger | Modbus connection, IP/port, holding register address, polling log, and empty-SN policy |
| Register does not return to `0` | Flow completion path, exception path, and `SetRegisterValue(0)` return code |
| SN is blocked by MES | `Summary.AutoUploadSN`, `Summary.Stage`, `CheckWIP` return value, and customer DLL convention |
| MES upload fails | `FunTestDll.dll`, `FunTestDllConfig.INI`, `Summary.UseMes`, `IsCheckWIP`, and `Collect_test` parameters |
| Flow succeeds but key results are empty | Batch has `KB` / `KB_Raw`, `POI_Y` / `POI_Y_V2`, and matching KB template name |
| Some keys are missing | POI names, POI width, KB template key names, `KeyScale`, and `KBLVSacle` |
| Luminance is globally high or low | `KBLVSacle`, Recipe limits, autotune Q1/Q3, and sigmoid steepness |
| CSV or summary is missing | `ViewResultManager.Config` switches, path permission, and model subfolder |
| Socket trigger fails | Whether the site actually uses TCP Socket and whether messages contain both `#` and `*` |

## Handoff Notes

- `FunTestDll.dll` and `FunTestDllConfig.INI` must be part of delivery validation.
- `CheckWIP` return convention is site-sensitive; verify customer DLL version.
- `KBLVSacle` is calibration-sensitive and affects historical result interpretation.
- POI names and dimensions must match the KB template.
- Modbus, Socket, and MES are separate external paths; identify which one the site uses first.
