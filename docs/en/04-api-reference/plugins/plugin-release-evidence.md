# Plugin Release Evidence Checklist

This page records the evidence needed when publishing `.cvxp` packages, replacing field plugin folders, or debugging a plugin that was copied but not loaded by the host.

## Why This Exists

For current plugins, `manifest.version` and `.csproj VersionPrefix` are often different.

| Plugin | manifest version | `.csproj VersionPrefix` |
| --- | --- | --- |
| Conoscope | `1.4.6.1` | `1.4.6.9` |
| Spectrum | `1.0` | `2.3.3.1` |
| SystemMonitor | `1.0.1` | `1.4.3.3` |
| EventVWR | `1.0` | `1.1.8.1` |
| WindowsServicePlugin | `1.0` | `1.4.3.17` |

Every release record should include manifest version, DLL FileVersion, `.cvxp` file name, and CHANGELOG entry.

## Required Evidence

| Evidence | Source |
| --- | --- |
| Plugin source list | `Get-ChildItem Plugins -Directory` |
| manifest | `Plugins/<Name>/manifest.json` |
| project version | `VersionPrefix` in `Plugins/<Name>/<Name>.csproj` |
| output DLL | field `Plugins/<Name>/<Name>.dll` file properties |
| `.cvxp` package | output of `Scripts\package_plugin.bat <Name> --no-upload` |
| README/CHANGELOG | plugin root and expanded package |
| host shared DLLs | host root `ColorVision.*.dll` |
| native/data files | expanded package or field plugin folder |
| permission evidence | admin mode, registry, service, or dump records |
| rollback | previous package and plugin folder backup |

## Commands

```powershell
$name = "Spectrum"
Get-Content "Plugins/$name/manifest.json"
Select-String "Plugins/$name/$name.csproj" -Pattern "TargetFramework|VersionPrefix|ProjectReference|PackageReference|CopyToOutputDirectory"

Scripts\package_plugin.bat Spectrum --no-upload

$root = "ColorVision/bin/x64/Release/net10.0-windows"
$plugin = Join-Path $root "Plugins/$name"
Get-ChildItem $plugin
Get-ChildItem $root -Filter "ColorVision*.dll" |
  Select-Object Name, @{Name="FileVersion";Expression={$_.VersionInfo.FileVersion}}, LastWriteTime
```

## Plugin-Specific Evidence

| Plugin | Extra evidence |
| --- | --- |
| Conoscope | MVS SDK or `MvCameraControl.dll`, test image, focus point config, CSV export |
| Spectrum | native spectrometer DLLs, `Magiude.dat`, `WavaLength.dat`, CIE images, license folder, result database, `SpectrumStatus` response |
| SystemMonitor | status bar config, CPU/RAM/disk/network refresh, cache cleanup scope |
| EventVWR | admin mode, Windows Application Error sample, HKLM LocalDumps, dump file path |
| WindowsServicePlugin | admin mode, service root, service state, MySQL/MQTT config, ZIP package, config sync log |

## Record Template

```text
Plugin:
Source directory:
manifest Id/version/requires:
csproj VersionPrefix:
output DLL FileVersion:
cvxp file:
build command:
package command:
package content check:
host ColorVision.*.dll versions:
entry acceptance:
business smoke test:
device/native/permission evidence:
README/CHANGELOG synced:
known limits:
rollback package and folder:
```

