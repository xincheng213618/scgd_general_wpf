# プラグインリリース証跡とバージョン確認表

このページは、`.cvxp` の公開、現地での plugin folder 置換、または「コピーしたが host が読み込まない」問題の調査に使います。manifest、DLL FileVersion、`.cvxp` 名、依存 DLL、native/data file、権限、rollback を記録します。

## バージョン証跡が必要な理由

現在の plugin では `manifest.version` と `.csproj VersionPrefix` が一致しないことがあります。

| Plugin | manifest version | `.csproj VersionPrefix` |
| --- | --- | --- |
| Conoscope | `1.4.6.1` | `1.4.6.9` |
| Spectrum | `1.0` | `2.3.3.1` |
| SystemMonitor | `1.0.1` | `1.4.3.3` |
| EventVWR | `1.0` | `1.1.8.1` |
| WindowsServicePlugin | `1.0` | `1.4.3.17` |

リリース記録には manifest version、DLL FileVersion、`.cvxp` file name、CHANGELOG を同時に残します。

## 必須証跡

| 証跡 | Source |
| --- | --- |
| plugin source list | `Get-ChildItem Plugins -Directory` |
| manifest | `Plugins/<Name>/manifest.json` |
| project version | `VersionPrefix` in `Plugins/<Name>/<Name>.csproj` |
| output DLL | field `Plugins/<Name>/<Name>.dll` file properties |
| `.cvxp` package | `Scripts\package_plugin.bat <Name> --no-upload` |
| README/CHANGELOG | plugin root and expanded package |
| host shared DLLs | host root `ColorVision.*.dll` |
| native/data files | expanded package or field plugin folder |
| permission evidence | admin mode、registry、service、dump records |
| rollback | previous package and plugin folder backup |

## コマンド

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

