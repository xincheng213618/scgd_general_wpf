# 플러그인 릴리스 증거 및 버전 확인표

이 페이지는 `.cvxp` 배포, 현장 plugin folder 교체, 또는 “복사했지만 host 가 로드하지 않음” 문제를 조사할 때 사용합니다. manifest, DLL FileVersion, `.cvxp` 이름, dependency, native/data file, 권한, rollback 을 기록합니다.

## 버전 증거가 필요한 이유

현재 plugin 에서는 `manifest.version` 과 `.csproj VersionPrefix` 가 다를 수 있습니다.

| Plugin | manifest version | `.csproj VersionPrefix` |
| --- | --- | --- |
| Conoscope | `1.4.6.1` | `1.4.6.9` |
| Spectrum | `1.0` | `2.3.3.1` |
| SystemMonitor | `1.0.1` | `1.4.3.3` |
| EventVWR | `1.0` | `1.1.8.1` |
| WindowsServicePlugin | `1.0` | `1.4.3.17` |

릴리스 기록에는 manifest version, DLL FileVersion, `.cvxp` file name, CHANGELOG 를 함께 남깁니다.

## 필수 증거

| 증거 | Source |
| --- | --- |
| plugin source list | `Get-ChildItem Plugins -Directory` |
| manifest | `Plugins/<Name>/manifest.json` |
| project version | `VersionPrefix` in `Plugins/<Name>/<Name>.csproj` |
| output DLL | field `Plugins/<Name>/<Name>.dll` file properties |
| `.cvxp` package | `Scripts\package_plugin.bat <Name> --no-upload` |
| README/CHANGELOG | plugin root and expanded package |
| host shared DLLs | host root `ColorVision.*.dll` |
| native/data files | expanded package or field plugin folder |
| permission evidence | admin mode, registry, service, dump records |
| rollback | previous package and plugin folder backup |

## 명령

```powershell
$name = "Spectrum"
Get-Content "Plugins/$name/manifest.json"
Select-String "Plugins/$name/$name.csproj" -Pattern "TargetFramework|VersionPrefix|ProjectReference|PackageReference|CopyToOutputDirectory"
Scripts\package_plugin.bat Spectrum --no-upload
```

## Plugin-Specific Evidence

| Plugin | Extra evidence |
| --- | --- |
| Conoscope | MVS SDK or `MvCameraControl.dll`, test image, focus point config, CSV export |
| Spectrum | native spectrometer DLLs, `Magiude.dat`, `WavaLength.dat`, CIE images, license folder, result database, `SpectrumStatus` response |
| SystemMonitor | status bar config, CPU/RAM/disk/network refresh, cache cleanup scope |
| EventVWR | admin mode, Windows Application Error sample, HKLM LocalDumps, dump file path |
| WindowsServicePlugin | admin mode, service root, service state, MySQL/MQTT config, ZIP package, config sync log |

