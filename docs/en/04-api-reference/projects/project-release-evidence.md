# Project Package Release Evidence Checklist

Use this page when publishing a project `.cvxp`, replacing a field `Plugins/{ProjectName}/` folder, or debugging a package that loads but uses the wrong menu, protocol, or result fields.

## Version Snapshot

| Project | manifest version | `.csproj VersionPrefix` | Note |
| --- | --- | --- | --- |
| ProjectARVR | `1.0` | `1.6.1.11` | Package name usually follows DLL FileVersion, not manifest |
| ProjectARVRLite | `1.0` | `1.2.5.17` | Record enabled test-type config with the release |
| ProjectARVRPro | `1.1.7.7` | `1.1.7.7` | Still verify the produced DLL FileVersion |
| ProjectBlackMura | `1.0` | `1.2.6.3` | Keep Excel/serial PG evidence |
| ProjectHeyuan | `1.0` | `1.1.6.3` | Confirm inherited target framework during delivery |
| ProjectKB | `1.0` | `1.4.2.19` | Keep MES DLL, Modbus, and backlight tuning evidence |
| ProjectLUX | `1.0` | `1.1.4.25` | ProcessGroups, Recipe/Fix, and SocketCode are release evidence |
| ProjectShiyuan | `1.0` | `1.3.5.3` | Explicitly targets `net8.0-windows` |
| ProjectARVRPro.IntegrationDemo | no manifest | no VersionPrefix | Publish as a customer demo, not as a `.cvxp` package |

## Evidence To Keep

| Evidence | Source |
| --- | --- |
| manifest | `Projects/{Name}/manifest.json` |
| project version | `TargetFramework`, `VersionPrefix`, references, and copied files in the csproj |
| output DLL | FileVersion of the built main DLL |
| `.cvxp` package | `Scripts\package_project.bat {Name} --no-upload` output |
| configuration | ProcessGroups, Recipe/Fix, Socket/MES, path and device settings |
| protocol sample | Socket/MES/serial/Modbus command and response |
| result sample | SQLite, CSV, XLSX, PDF, MES, or Socket response |
| external dependencies | NPOI/EPPlus/NModbus, MES DLL, `FunTestDll.dll`, serial/PG/device SDK |
| rollback | previous `.cvxp`, project folder backup, config backup, database backup |

## Commands

```powershell
$name = "ProjectLUX"
Get-Content "Projects/$name/manifest.json"
Select-String "Projects/$name/$name.csproj" -Pattern "TargetFramework|VersionPrefix|ProjectReference|PackageReference|CopyToOutputDirectory"

Scripts\package_project.bat ProjectLUX --no-upload
$pkg = Get-ChildItem Scripts -Filter "ProjectLUX-*.cvxp" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
```

## Project-Specific Evidence

| Project | Must keep |
| --- | --- |
| ProjectARVR | `ProjectARVRInit`, `SwitchPGCompleted`, `ProjectARVRResult`, PG order, full-unit CSV |
| ProjectARVRLite | enabled test-type config, preprocess flag, Socket init, CSV sample |
| ProjectARVRPro | ProcessGroups, Recipe, PictureSwitchConfig, legacy/new CSV, customer XLSX, SocketRelay/AOI sample |
| ProjectBlackMura | PG serial commands, five-color flow, Excel report, EPPlus, POI overlay/result image |
| ProjectHeyuan | STX/ETX raw messages, WBRO four-point result, CSV upload file, `HYMesConfig` |
| ProjectKB | `FunTestDll.dll`, `FunTestDllConfig.INI`, NModbus config, MES `Collect_test`, backlight tuning record |
| ProjectLUX | ProcessGroups, Recipe/Fix, `T00XX,SN;`, SocketCode map, SQLite/CSV/PDF output |
| ProjectShiyuan | `DataPath`, JND/POI CSV, fixed input images, pseudo-color output, serial-chain status |
| IntegrationDemo | publish folder, sample JSON, port, packet reader, exported CSV |

## Continue Reading

- [Current Project Documentation Coverage](./current-project-coverage.md)
- [Project Capability & Handoff Matrix](./project-capability-matrix.md)
- [Project Package Runtime And Handoff Playbook](./project-package-playbook.md)
- [Project Package Handoff Manual](./project-handoff.md)
