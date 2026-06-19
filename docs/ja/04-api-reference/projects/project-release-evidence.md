# プロジェクトパッケージリリース証跡とバージョン確認表

プロジェクト `.cvxp` の公開、現地 `Plugins/{ProjectName}/` の置換、またはメニュー・プロトコル・結果フィールドの不一致を調査するときに使います。

## バージョンスナップショット

| Project | manifest version | `.csproj VersionPrefix` | Note |
| --- | --- | --- | --- |
| ProjectARVR | `1.0` | `1.6.1.11` | package name は通常 DLL FileVersion に従います |
| ProjectARVRLite | `1.0` | `1.2.5.17` | enabled test-type config も記録します |
| ProjectARVRPro | `1.1.7.7` | `1.1.7.7` | 実際の DLL FileVersion も確認します |
| ProjectBlackMura | `1.0` | `1.2.6.3` | Excel と PG serial の証跡が必要です |
| ProjectHeyuan | `1.0` | `1.1.6.3` | inherited TargetFramework を確認します |
| ProjectKB | `1.0` | `1.4.2.19` | MES DLL、Modbus、backlight tuning を記録します |
| ProjectLUX | `1.0` | `1.1.4.25` | ProcessGroups、Recipe/Fix、SocketCode が証跡です |
| ProjectShiyuan | `1.0` | `1.3.5.3` | `net8.0-windows` を明示しています |
| ProjectARVRPro.IntegrationDemo | no manifest | no VersionPrefix | `.cvxp` ではなく customer demo として publish します |

## 保持する証跡

| Evidence | Source |
| --- | --- |
| manifest | `Projects/{Name}/manifest.json` |
| project version | csproj の TargetFramework、VersionPrefix、references、copy files |
| output DLL | main DLL FileVersion |
| `.cvxp` package | `Scripts\package_project.bat {Name} --no-upload` |
| configuration | ProcessGroups、Recipe/Fix、Socket/MES、paths、device settings |
| protocol sample | Socket/MES/serial/Modbus command and response |
| result sample | SQLite、CSV、XLSX、PDF、MES、Socket response |
| external dependencies | NPOI/EPPlus/NModbus、MES DLL、`FunTestDll.dll`、serial/PG/device SDK |
| rollback | previous `.cvxp`、project folder backup、config/database backup |

```powershell
$name = "ProjectLUX"
Get-Content "Projects/$name/manifest.json"
Select-String "Projects/$name/$name.csproj" -Pattern "TargetFramework|VersionPrefix|ProjectReference|PackageReference|CopyToOutputDirectory"
Scripts\package_project.bat ProjectLUX --no-upload
```

## Project-Specific Evidence

| Project | Must keep |
| --- | --- |
| ProjectARVR | `ProjectARVRInit`, `SwitchPGCompleted`, `ProjectARVRResult`, PG order, CSV |
| ProjectARVRLite | enabled test config, preprocess flag, Socket init, CSV |
| ProjectARVRPro | ProcessGroups, Recipe, PictureSwitchConfig, legacy/new CSV, customer XLSX, SocketRelay/AOI |
| ProjectBlackMura | PG serial commands, five-color flow, Excel report, EPPlus, POI overlay |
| ProjectHeyuan | STX/ETX raw messages, WBRO result, CSV, `HYMesConfig` |
| ProjectKB | `FunTestDll.dll`, `FunTestDllConfig.INI`, NModbus, MES `Collect_test`, tuning record |
| ProjectLUX | ProcessGroups, Recipe/Fix, `T00XX,SN;`, SocketCode, SQLite/CSV/PDF |
| ProjectShiyuan | `DataPath`, JND/POI CSV, fixed images, pseudo-color output, serial status |

## 続けて読む

- [現在のプロジェクト文書カバレッジ](./current-project-coverage.md)
- [プロジェクト能力と引き継ぎマトリクス](./project-capability-matrix.md)
- [プロジェクト実行と引き継ぎプレイブック](./project-package-playbook.md)
- [プロジェクト引き継ぎマニュアル](./project-handoff.md)
