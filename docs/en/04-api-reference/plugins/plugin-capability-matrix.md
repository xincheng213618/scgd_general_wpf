# Plugin Capability & Handoff Matrix

This page compares the plugins that actually exist under `Plugins/` in the current repository. Individual plugin details remain in their own pages. This matrix focuses on host entry, dependencies, external boundaries, release acceptance, and common misreadings.

For concrete issues such as missing plugin entries, missing menus, `.deps.json` version failures, incomplete `.cvxp` packages, administrator permission, or Socket command failures, use [Plugin Runtime And Handoff Playbook](./plugin-handoff-playbook.md) first. For release or field replacement acceptance, continue with [Existing Plugin Field Acceptance And Handoff Checklist](./plugin-field-acceptance.md).

## Current Plugin Matrix

| Plugin | Source directory | manifest version | `.csproj` version | Host entry | Main capability | Key risk |
| --- | --- | --- | --- | --- | --- | --- |
| Conoscope | `Plugins/Conoscope/` | `1.4.6.1` | `1.4.6.9` | Tool menu `VAM`, ImageEditor context menu | VAM/conoscope image view, focus points, reference axes, preprocessing, color gamut, contrast, MVS camera view | manifest version differs from assembly version; MVS depends on Hikvision `MvCameraControl.dll`; focus-point logic is plugin-local |
| Spectrum | `Plugins/Spectrum/` | `1.0` | `2.3.3.1` | Tool menu spectrum window, window-level menus/status bar, Socket JSON commands | Spectrometer connection, calibration groups, measurement, EQE, CIE, SQLite results, license, remote control | manifest and assembly versions differ; native DLL, serial, license, and window/device state all matter |
| SystemMonitor | `Plugins/SystemMonitor/` | `1.0.1` | `1.4.3.3` | Tool menu, settings page, host status bar | CPU/RAM/disk/network/process/GPU/cache monitoring and status-bar projection | Performance counters can fail and degrade; monitor singleton lives under `ColorVision.UI.Configs` namespace |
| EventVWR | `Plugins/EventVWR/` | `1.0` | `1.1.8.1` | Help menu event window, Dump submenu | Windows Application error events, WER LocalDumps config, current-process dump saving | HKLM registry and dump operations require administrator permissions |
| WindowsServicePlugin | `Plugins/WindowsServicePlugin/` | `1.0` | `1.4.3.17` | Help menu service manager, wizard entry | CVWindowsService install/register/start/stop, MySQL/MQTT install/config, service folder sync | Changes Windows services, MySQL, MQTT, and local files; requires administrator permissions; not an incremental service package |

`manifest.version` and `.csproj VersionPrefix` are not always the same. For delivery, verify manifest version, DLL file version, `.cvxp` file name, and `CHANGELOG.md`.

## Entry and Extension Points

| Plugin | Main menu | Window menu | Status bar | Settings | Socket | Other extension |
| --- | --- | --- | --- | --- | --- | --- |
| Conoscope | `MenuConoscopeWindow` -> Tool / `VAM` | Conoscope Ribbon/View menu, `MenuMVSVideo` | Internal window status | `ConoscopeConfig` and preprocessing settings | None | `ConoscopeImageViewContextMenu` in ImageEditor |
| Spectrum | `MenuSpectrumWindow` -> Tool | `LoadMenuForWindow("Spectrum", menu)` | `SpectrumStatusBarProvider` | Multiple `ConfigService` config objects | 5 `ISocketJsonHandler` classes | Quartz jobs, SQLite results, license sync |
| SystemMonitor | `SystemMonitorProvider` -> Tool | None | `SystemMonitorIStatusBarProvider` | `IConfigSettingProvider` | None | `SystemMonitorControl` in settings and window |
| EventVWR | `ExportEventWindow` -> Help; `MenuDump` -> Help | Dump submenu from `IMenuItemProvider` | None | `DumpConfig` writes registry | None | Dump-file collection |
| WindowsServicePlugin | `MenuServiceManager` -> Help | Service manager internal commands | None | `ServiceManagerConfig`, `MySqlServiceConfig`, `MqttServiceConfig` | None | `InstallServiceManager` wizard step |

## External Boundaries

| Plugin | External device/service | Files and databases | Permission | First checks |
| --- | --- | --- | --- | --- |
| Conoscope | MVS camera, `MvCameraControl.dll` | Focus/reference/preprocess config, CSV export | Usually normal user; driver is system environment | MVS SDK, image open, focus points, export file |
| Spectrum | SP100/SP10 spectrometer, shutter, CFW, SMU, serial, native DLL | `AppData\Spectromer\Config\Spectrum.db`, calibration groups, license folder, CIE images | Usually normal user; driver/license must be ready | Device log, license sync, calibration group, Socket service, SQLite result |
| SystemMonitor | Windows performance counters, CUDA info, network interfaces | Cache/log directories | Cache cleanup depends on target permissions | Counter initialization, config switches, status-bar provider refresh |
| EventVWR | Windows EventLog, WER LocalDumps | Dump output folder, HKLM LocalDumps registry | Administrator for registry/dump actions | Admin mode, registry path, DumpFolder, Application log |
| WindowsServicePlugin | Windows service, MySQL, MQTT, service package ZIP | `BaseLocation`, `cfg/*.config`, MySQL ZIP, MQTT installer, SQL, backup folder | Mostly administrator | Service state, BaseLocation, package structure, MySQL/MQTT status, CFG sync log |

## Build and Package

| Plugin | Build command | Package command | PostBuild output | Must include |
| --- | --- | --- | --- | --- |
| Conoscope | `dotnet build Plugins/Conoscope/Conoscope.csproj -c Release -p:Platform=x64` | `Scripts\package_plugin.bat Conoscope --no-upload` | `ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/Conoscope/` | DLL, manifest, README, CHANGELOG, MVS/native dependencies |
| Spectrum | `dotnet build Plugins/Spectrum/Spectrum.csproj -c Release -p:Platform=x64` | `Scripts\package_plugin.bat Spectrum --no-upload` or `Scripts\build_spectrum.py` | `ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/Spectrum/` | DLL, manifest, README, CHANGELOG, `Magiude.dat`, `WavaLength.dat`, CIE images, spectrometer native DLL |
| SystemMonitor | `dotnet build Plugins/SystemMonitor/SystemMonitor.csproj -c Release -p:Platform=x64` | `Scripts\package_plugin.bat SystemMonitor --no-upload` | `ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/SystemMonitor/` | DLL, manifest, README, CHANGELOG |
| EventVWR | `dotnet build Plugins/EventVWR/EventVWR.csproj -c Release -p:Platform=x64` | `Scripts\package_plugin.bat EventVWR --no-upload` | `ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/EventVWR/` | DLL, manifest, README, CHANGELOG |
| WindowsServicePlugin | `dotnet build Plugins/WindowsServicePlugin/WindowsServicePlugin.csproj -c Release -p:Platform=x64` | `Scripts\package_plugin.bat WindowsServicePlugin --no-upload` | `ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/WindowsServicePlugin/` | DLL, manifest, README, CHANGELOG |

The common `.cvxp` package flow is in `Scripts/package_cvxp.py`: optionally build, collect plugin output, remove host-shared files from `Scripts/shared_files.json`, copy root README/CHANGELOG/manifest/icon, read DLL `FileVersion`, and generate `<PluginName>-<FileVersion>.cvxp`.

## Post-Release Smoke Tests

| Plugin | Minimum smoke | Pass standard |
| --- | --- | --- |
| Conoscope | Open Tool -> VAM, load an image, add/move focus point, run color gamut or contrast, export CSV | Window, Ribbon, overlay, result view, and CSV all work |
| Spectrum | Open Spectrum, check status bar, query no-device state; with device connect/zero/measure/export; with Socket send `SpectrumStatus` | Status bar shows connection/SN/group, result persists, Socket returns correct code/message |
| SystemMonitor | Open monitor, toggle status-bar switches, refresh disk/network/process, clean cache only after confirming directory | Data refreshes and one failed counter does not break the plugin |
| EventVWR | Run as administrator, load Application errors, change DumpType, save current process dump, clear config | Event list loads, registry changes match expectation, dump file is created |
| WindowsServicePlugin | Run as administrator, open service manager, refresh service state, select test root, inspect config, run install only in test environment | Status and config sync are clear and failures do not silently start old config |

## Risk Checklist

| Risk | Impact | Handling |
| --- | --- | --- |
| manifest version differs from DLL file version | Market, plugin manager, and field DLL version disagree | Check manifest, `.csproj`, output DLL `FileVersion`, `.cvxp` file name |
| README/CHANGELOG copy casing differs | Build succeeds but runtime help is missing | Check project copy rules and file casing |
| Plugin depends on host-shared DLLs | Copying only plugin DLL fails at runtime | Package with `shared_files.json` and check host `ColorVision.*.dll` versions |
| Native DLL missing | Spectrum or Conoscope device path fails | Inspect `.cvxp` contents for native DLLs and OpenCV runtime |
| Administrator-only plugin tested normally | EventVWR dump or WindowsServicePlugin fails | Mark admin-mode requirement in docs and smoke test |
| Socket handler compiled but service disabled | External client cannot reach plugin command | Check `ColorVision.SocketProtocol` config, port, protocol mode, and assembly loading |

## Continue Reading

- [Plugin Runtime And Handoff Playbook](./plugin-handoff-playbook.md)
- [Existing Plugin Field Acceptance And Handoff Checklist](./plugin-field-acceptance.md)
- [Conoscope](./standard-plugins/conoscope.md)
- [Spectrum](./standard-plugins/spectrum.md)
- [SystemMonitor](./standard-plugins/system-monitor.md)
- [EventVWR](./standard-plugins/eventvwr.md)
- [WindowsServicePlugin](./standard-plugins/windows-service.md)
- [Plugin Development Manual](../../02-developer-guide/plugin-development/README.md)
