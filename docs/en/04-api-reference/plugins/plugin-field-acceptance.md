# Existing Plugin Field Acceptance And Handoff Checklist

Use this page when releasing a plugin, replacing a plugin in the field, or handing a plugin over to another maintainer. It differs from [Plugin Capability & Handoff Matrix](./plugin-capability-matrix.md): the matrix answers what each plugin can do and where the risks are; this page answers how to prove a plugin package is deliverable and how to record rollback boundaries.

This page covers only the plugins that currently exist under `Plugins/`: Conoscope, Spectrum, SystemMonitor, EventVWR, and WindowsServicePlugin. Pattern, ImageProjector, and ScreenRecorder are outside the current plugin inventory and are not part of current acceptance. See [Current Plugin Documentation Coverage](./current-plugin-coverage.md) for the coverage mapping.

## Acceptance Layers

Do not treat a visible menu as full acceptance. Check every plugin across these 6 layers.

| Layer | Required check | Evidence |
| --- | --- | --- |
| Package shape | Plugin folder, main DLL, `manifest.json`, `README.md`, `CHANGELOG.md` | Expand `.cvxp` or inspect host `Plugins/<Name>/` |
| Versions | `manifest.version`, `.csproj VersionPrefix`, output DLL `FileVersion`, `.cvxp` file name | Compare manifest, project file, DLL properties, and package name |
| Host dependency | `ColorVision.*.dll`, `.deps.json`, native DLLs, data files | Inspect host root and plugin folder |
| Entry points | Main menu, window menu, status bar, setting page, Socket handler, wizard | Start the host and open each entry |
| Business smoke | Minimum usable plugin workflow | Result, export file, status bar refresh, Socket response, or readable service state |
| Rollback | Previous package, plugin folder backup, host DLL versions, external config recovery | Written in the handoff record |

## Shared Preparation

Record these facts before acceptance:

| Item | Record |
| --- | --- |
| Host version | Host build config, target framework, output folder |
| Plugin source | `.cvxp`, plugin folder, or build output path |
| Build command | `dotnet build Plugins/<Name>/<Name>.csproj -c Release -p:Platform=x64` |
| Package command | `Scripts\package_plugin.bat <Name> --no-upload` |
| Plugin folder | `ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/<Name>/` |
| Runtime permission | Normal user or administrator |
| External environment | Spectrometer, MVS camera, MySQL, MQTT, Windows service, license availability |

Quick folder check:

```powershell
$name = "Spectrum"
$root = "ColorVision/bin/x64/Release/net10.0-windows"
$plugin = Join-Path $root "Plugins/$name"
Get-ChildItem $plugin
Get-Content (Join-Path $plugin "manifest.json")
Get-ChildItem $plugin -Filter "*.deps.json"
```

## Current Plugin Acceptance Overview

| Plugin | Minimum entry acceptance | Minimum business acceptance | External boundary | Rollback points to record |
| --- | --- | --- | --- | --- |
| Conoscope | Tool -> `VAM`, ImageEditor context menu `OpenByConoscope` | Open image, add focus point, run gamut or contrast, export CSV | MVS camera, `MvCameraControl.dll`, image resources | Focus/reference config, export fields, previous plugin folder |
| Spectrum | Tool -> Spectrum, window menu, status bar, Socket handlers | No-device status; with device: connect, dark calibration, measure, persist result, export; Socket `SpectrumStatus` | Spectrometer native DLLs, serial, SMU/Shutter/CFW, license, SQLite | License folder, calibration groups, SQLite result DB, previous native DLLs |
| SystemMonitor | Tool -> monitor, setting page, status bar items | CPU/RAM/disk/network/process refresh, status-bar switches, cache stats | Windows performance counters, CUDA info, disk/log permissions | Setting switches, cache cleanup scope, status-bar config |
| EventVWR | Help -> event window, Help -> Dump submenu | Read Application Error events, switch DumpType, save current-process dump | HKLM LocalDumps registry, WER, administrator permission | Original registry values, DumpFolder, DumpType, DumpCount |
| WindowsServicePlugin | Help -> service manager, wizard entry | Refresh service state, read BaseLocation, open CFG; full install in test environment | Windows services, MySQL, MQTT, service ZIP, administrator permission | Service folder backup, MySQL/MQTT config, service state, previous service package |

## Conoscope Acceptance

### Entry And Version

| Item | Check |
| --- | --- |
| Manifest | `Id=Conoscope`, `version=1.4.6.1`, `dllpath=Conoscope.dll` |
| DLL version | `.csproj VersionPrefix=1.4.6.9`; record the real output DLL file version |
| Main menu | Tool menu `VAM`, from `MenuConoscopeWindow` |
| ImageEditor context menu | `ConoscopeImageViewContextMenu` provides `OpenByConoscope` |
| Window entries | `ConoscopeWindow` ribbon, current-view quick controls, MVS View menu |

### Minimum Business Smoke

1. Open Tool -> `VAM`.
2. Import a usable CVCIE or normal test image.
3. Create or move a focus circle and confirm overlay follows zoom and dragging.
4. Switch display channel and reference-graphic mode.
5. Run one preprocessing step such as filter, pseudo-color, or XYZ clamp.
6. Record R/G/B focus points and compute gamut, or record white/black and compute contrast.
7. Open the result window and export CSV.

Pass standards:

| Item | Standard |
| --- | --- |
| View | Tabs, current-view quick controls, reference axes, and focus points behave normally |
| Analysis | `ColorGamutResultWindow` or `ContrastResultWindow` shows focus-point results |
| Export | CSV is generated and matches result fields and focus-point count |
| MVS | If a camera is available, `MVSViewWindow` opens and displays the camera path; otherwise record as unverified |

### Failure Routing

| Symptom | Check first |
| --- | --- |
| Tool menu has no `VAM` | Plugin loading, `manifest.json`, `MenuConoscopeWindow` scan |
| Image cannot open | File format, `ColorVision.ImageEditor`, CVCIE/CVRAW path |
| Focus point missing or wrong coordinate | `ConoscopeView.FocusPoint.cs`, zoom state, active view |
| Gamut or contrast is empty | Whether R/G/B or white/black records are complete, focus snapshot |
| MVS camera unavailable | MVS SDK, `MvCameraControl.dll`, camera driver, camera occupancy |

## Spectrum Acceptance

### Entry And Version

| Item | Check |
| --- | --- |
| Manifest | `Id=Spectrum`, `version=1.0`, `dllpath=Spectrum.dll`, `requires=1.3.15.8` |
| DLL version | `.csproj VersionPrefix=2.3.3.1`; do not record only manifest version |
| Main menu | Tool menu Spectrum entry from `MenuSpectrumWindow` |
| Window menu | `LoadMenuForWindow("Spectrum", menu)` with help, layout, license, native log entries |
| Status bar | `SpectrumStatusBarProvider` shows connection, SN, calibration group, and status |
| Socket | 5 `ISocketJsonHandler` implementations under `Plugins/Spectrum/Socket/` |

### Minimum Business Smoke

Without device:

1. Open the Spectrum window.
2. Confirm help, license manager, layout menu, and native log panel open.
3. Use a no-device status path and confirm the window does not crash.
4. Enable SocketProtocol and send `SpectrumStatus`; confirm it returns plugin state or a clear not-connected error.

With device:

1. Sync license and confirm required license folders are available.
2. Connect the spectrometer and confirm SN and status bar update.
3. Select or create a calibration group.
4. Run dark calibration or auto integration.
5. Run one measurement and confirm curve, CIE, and result list update.
6. Export the result and confirm SQLite and exported file match.
7. Use Socket to run connect, dark calibration, measurement, and status queries.

Pass standards:

| Item | Standard |
| --- | --- |
| Device | Connection log, SN, calibration group, and status bar agree |
| Result | `Spectrum.db` or result DB has a new record; curve and export match |
| Socket | JSON handlers return Code/Msg, with clear errors when the device is not ready |
| Resources | `Magiude.dat`, `WavaLength.dat`, CIE images, and native DLLs are available at runtime |

### Failure Routing

| Symptom | Check first |
| --- | --- |
| Plugin fails to load | `.deps.json`, host-root `ColorVision.*.dll` versions |
| Device connection fails | Spectrometer native DLLs, driver, serial, license, device occupancy |
| Socket has no response | SocketProtocol enabled state, port, JSON mode, Spectrum window and device state |
| Measurement has no data | Calibration group, integration time, dark-calibration state, ViewResultManager config |
| CIE or curve resource missing | CIE images, data files, `CopyToOutputDirectory` |

## SystemMonitor Acceptance

### Entry And Version

| Item | Check |
| --- | --- |
| Manifest | `Id=SystemMonitor`, `version=1.0.1`, `requires=1.3.12.23` |
| DLL version | `.csproj VersionPrefix=1.4.3.3` |
| Tool menu | Tool -> performance monitor, from `SystemMonitorProvider` |
| Setting page | `SystemMonitorProvider` also implements `IConfigSettingProvider` |
| Status bar | `SystemMonitorIStatusBarProvider` dynamically outputs time, uptime, CPU, RAM, disk |

### Minimum Business Smoke

1. Open the performance monitor window.
2. Confirm CPU/RAM, disk, network, process, and runtime information refresh.
3. Open the settings page and toggle `IsShowTime`, `IsShowRAM`, `IsShowCPU`, `IsShowUptime`, and `IsShowDisk`.
4. Confirm host status-bar items are added and removed dynamically.
5. Run refresh disk, refresh network, and refresh process commands.
6. Run cache statistics; confirm target folders before clearing cache.

Pass standards:

| Item | Standard |
| --- | --- |
| Control | `SystemMonitorControl` does not crash the whole plugin when one counter fails |
| Status bar | Config changes trigger `StatusBarItemsChanged` |
| Performance | `UpdateSpeed` is respected and the host does not noticeably slow down |
| Permission | Cache cleanup touches only expected app data and log folders |

### Failure Routing

| Symptom | Check first |
| --- | --- |
| CPU/RAM does not refresh | Windows performance-counter initialization; current implementation should degrade |
| Status bar does not change | Config switches, `IStatusBarProviderUpdatable`, status-bar refresh |
| GPU info missing | CUDA environment and `ConfigCuda.Instance` |
| Cache cleanup fails | Target folder permissions and file locks |

## EventVWR Acceptance

### Entry And Version

| Item | Check |
| --- | --- |
| Manifest | `Id=EventVWR`, `version=1.0`, `requires=1.3.15.10` |
| DLL version | `.csproj VersionPrefix=1.1.8.1` |
| Event window | Help -> `EventWindow`, from `ExportEventWindow` |
| Dump menu | Help -> `MenuDump` plus DumpType, clear, and save submenu items |
| Permission | Event window and Dump writes are administrator-sensitive |

### Minimum Business Smoke

1. Start the host as administrator.
2. Open Help -> event window.
3. Confirm Windows Application Error events can be read.
4. Select an error and confirm the details area shows Message.
5. Open Dump submenu and switch DumpType.
6. Save DumpFolder, DumpCount, and DumpType.
7. Save current-process dump and confirm the file is generated.
8. Clear Dump config and confirm registry state.

Pass standards:

| Item | Standard |
| --- | --- |
| Events | EventLog read failure shows a clear message and is not confused with plugin load failure |
| Registry | Writes and cleanup under `HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps` can be confirmed |
| Dump | Dump file is generated in target folder with traceable name/time |
| Permission | Normal-user failure is recorded as a permission limit, not a pass |

### Failure Routing

| Symptom | Check first |
| --- | --- |
| Menu disabled or cannot open | `RequiresPermission(PermissionMode.Administrator)`, host permission mode |
| Event list empty | Whether Windows Application log contains Error entries and EventLog permissions |
| Dump save fails | DumpFolder permission, HKLM write permission, target file lock |
| Config still applies after cleanup | Default LocalDumps versus process-level LocalDumps confusion |

## WindowsServicePlugin Acceptance

### Entry And Version

| Item | Check |
| --- | --- |
| Manifest | `Id=WindowsServicePlugin`, `version=1.0`, `requires=1.3.12.34` |
| DLL version | `.csproj VersionPrefix=1.4.3.17` |
| Help menu | Help -> service manager, from `MenuServiceManager` |
| Wizard | `InstallServiceManager` as wizard entry |
| Permission | Install, register, start/stop services, MySQL/MQTT work must be accepted as administrator |

### Minimum Business Smoke

Read-only acceptance:

1. Start the host as administrator.
2. Open Help -> service manager.
3. Refresh `RegistrationCenterService`, `CVMainService_x64`, and `CVMainService_dev` states.
4. Confirm `BaseLocation` is readable.
5. Open service folder and `cfg/*.config`.
6. Refresh MySQL and MQTT state.

Full install acceptance in test environment:

1. Prepare a complete `CVWindowsService` ZIP, not an incremental package.
2. Choose a service root such as `D:\CVService`.
3. Select MySQL ZIP and MQTT installer as needed.
4. Run install and confirm managed services are stopped first.
5. Confirm service package extraction, `CommonDll` copy, old-service unregister, and new-service register.
6. Confirm MySQL/MQTT/WinService config synchronization succeeds.
7. Optionally execute `SQL/color_vision_all.sql`.
8. Start services and refresh state.

Pass standards:

| Item | Standard |
| --- | --- |
| State | Service install state, running state, BaseLocation, and window display agree |
| Config | `cfg/MySql.config`, `cfg/MQTT.config`, and `cfg/WinService.config` synchronize successfully |
| Install | Complete service package registers services; failed sync does not continue with old config |
| Database | MySQL user, port, database, and SQL import record are clear |

### Failure Routing

| Symptom | Check first |
| --- | --- |
| Service state cannot be read | Administrator mode, service names, Windows Service Manager |
| Package validation fails | ZIP root structure, `RegWindowsService`, `CVMainWindowsService_x64/dev` |
| MySQL install fails | ZIP path, port occupancy, data folder permission, SQL encoding |
| MQTT start fails | Installer path, port, service/process state |
| Config sync fails | `BaseLocation`, `cfg` folder, file permissions, window log |

## Unified Rollback Requirements

Every plugin handoff must answer where rollback starts. Keep these materials for each delivery:

| Material | Meaning |
| --- | --- |
| Previous `.cvxp` | Can roll back through Marketplace or manual install |
| Old plugin folder backup | Directly restores DLL, manifest, README, CHANGELOG, and native files |
| Host DLL version table | Prevents rollback from being blocked by incompatible `ColorVision.*.dll` |
| External config backup | License, calibration group, registry, service folder, MySQL/MQTT config |
| Operation log | Who operated on which machine, with which permission, and what install/service actions were performed |

Rollback is not only replacing the plugin DLL. Spectrum and Conoscope may be affected by native DLLs, licenses, or device drivers; EventVWR and WindowsServicePlugin also touch registry, services, and local folders.

## Plugin Field Handoff Record Template

| Item | Content |
| --- | --- |
| Plugin name |  |
| Delivery package | `.cvxp` file name, generated time, source path |
| Versions | `manifest.version`, DLL `FileVersion`, `.csproj VersionPrefix` |
| Host | Host version, output folder, whether `ColorVision.*.dll` versions satisfy dependencies |
| Permission | Normal/admin user, whether restart-as-admin is required |
| Entry acceptance | Main menu, window menu, status bar, settings page, Socket/wizard |
| Business smoke | Pass/fail for the plugin-specific minimum flow on this page |
| External dependencies | Device, driver, license, database, registry, service, native DLL |
| Generated data | CSV, SQLite record, dump file, service config, log path |
| Unverified items | No device, no admin, no test service package, etc. |
| Rollback material | Previous package, folder backup, config backup, service backup |
| Owners | Developer, tester, field engineer, customer confirmer |

## Continue Reading

- [Plugin Capability & Handoff Matrix](./plugin-capability-matrix.md)
- [Plugin Runtime And Handoff Playbook](./plugin-handoff-playbook.md)
- [Conoscope](./standard-plugins/conoscope.md)
- [Spectrum](./standard-plugins/spectrum.md)
- [SystemMonitor](./standard-plugins/system-monitor.md)
- [EventVWR](./standard-plugins/eventvwr.md)
- [WindowsServicePlugin](./standard-plugins/windows-service.md)
