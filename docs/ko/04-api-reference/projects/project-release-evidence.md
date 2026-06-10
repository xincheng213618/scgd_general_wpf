# 프로젝트 패키지 릴리스 증거 및 버전 확인표

프로젝트 `.cvxp` 배포, 현장 `Plugins/{ProjectName}/` 교체, 또는 메뉴/프로토콜/결과 필드 불일치 문제를 조사할 때 사용합니다.

## 버전 스냅샷

| Project | manifest version | `.csproj VersionPrefix` | Note |
| --- | --- | --- | --- |
| ProjectARVR | `1.0` | `1.6.1.11` | package name 은 보통 DLL FileVersion 을 따릅니다 |
| ProjectARVRLite | `1.0` | `1.2.5.17` | enabled test-type config 도 기록합니다 |
| ProjectARVRPro | `1.1.7.7` | `1.1.7.7` | 실제 DLL FileVersion 도 확인합니다 |
| ProjectBlackMura | `1.0` | `1.2.6.3` | Excel 과 PG serial 증거가 필요합니다 |
| ProjectHeyuan | `1.0` | `1.1.6.3` | inherited TargetFramework 를 확인합니다 |
| ProjectKB | `1.0` | `1.4.2.19` | MES DLL, Modbus, backlight tuning 을 기록합니다 |
| ProjectLUX | `1.0` | `1.1.4.25` | ProcessGroups, Recipe/Fix, SocketCode 가 증거입니다 |
| ProjectShiyuan | `1.0` | `1.3.5.3` | `net8.0-windows` 를 명시합니다 |
| ProjectARVRPro.IntegrationDemo | no manifest | no VersionPrefix | `.cvxp` 가 아니라 customer demo 로 publish 합니다 |

## 보관할 증거

| Evidence | Source |
| --- | --- |
| manifest | `Projects/{Name}/manifest.json` |
| project version | csproj 의 TargetFramework, VersionPrefix, references, copy files |
| output DLL | main DLL FileVersion |
| `.cvxp` package | `Scripts\package_project.bat {Name} --no-upload` |
| configuration | ProcessGroups, Recipe/Fix, Socket/MES, paths, device settings |
| protocol sample | Socket/MES/serial/Modbus command and response |
| result sample | SQLite, CSV, XLSX, PDF, MES, Socket response |
| external dependencies | NPOI/EPPlus/NModbus, MES DLL, `FunTestDll.dll`, serial/PG/device SDK |
| rollback | previous `.cvxp`, project folder backup, config/database backup |

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

## 계속 읽기

- [현재 프로젝트 문서 커버리지](./current-project-coverage.md)
- [프로젝트 기능 및 인수인계 매트릭스](./project-capability-matrix.md)
- [프로젝트 실행 및 인수인계 플레이북](./project-package-playbook.md)
- [프로젝트 인수인계 매뉴얼](./project-handoff.md)
