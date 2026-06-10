# Plugin Runtime And Handoff Playbook

This page is for maintainers who take over existing plugins, troubleshoot plugin loading, package `.cvxp` files, or validate field plugin packages. It connects `PluginLoader`, `manifest.json`, `.deps.json`, `.cvxp`, administrator permissions, native DLLs, and Socket behavior into executable checks.

For a cross-plugin capability comparison, start with [Plugin Capability & Handoff Matrix](./plugin-capability-matrix.md). For release, field replacement, or handoff acceptance of existing plugins, continue with [Existing Plugin Field Acceptance And Handoff Checklist](./plugin-field-acceptance.md). For new plugin development, start with [Plugin Development Manual](../../02-developer-guide/plugin-development/README.md).

## How To Use This Page

1. Use [Scenario Entry Points](#scenario-entry-points) to decide whether the issue is loading, entry registration, packaging, device dependency, permission, or Socket behavior.
2. Follow the matching scenario to inspect source, output folder, and runtime logs.
3. Fill in [Plugin Handoff Record](#plugin-handoff-record) with manifest, DLL version, `.cvxp`, smoke tests, and known limits.

## Scenario Entry Points

| Problem | Start here | Related plugins |
| --- | --- | --- |
| Plugin folder exists, but plugin manager or menu cannot see it | [Scenario A](#scenario-a-plugin-folder-exists-but-does-not-load) | All plugins |
| Plugin loads, but menu, status bar, or settings entry does not appear | [Scenario B](#scenario-b-plugin-loads-but-entry-does-not-appear) | Conoscope, Spectrum, SystemMonitor, EventVWR, WindowsServicePlugin |
| Field machine reports missing or insufficient `ColorVision.*.dll` | [Scenario C](#scenario-c-dependency-version-or-colorvision-dll-issue) | All plugins |
| You need to publish a `.cvxp` package | [Scenario D](#scenario-d-package-and-publish-cvxp) | All plugins |
| Plugin depends on device, native DLL, or driver | [Scenario E](#scenario-e-device-or-native-dll-plugin) | Spectrum, Conoscope |
| Plugin needs administrator permission | [Scenario F](#scenario-f-administrator-permission-plugin) | EventVWR, WindowsServicePlugin |
| Socket command does not arrive or return | [Scenario G](#scenario-g-socket-plugin-command-does-not-work) | Spectrum |
| A previously mentioned plugin name should become current again | [Scenario H](#scenario-h-restore-a-historical-plugin) | Pattern, ImageProjector, ScreenRecorder |

## Runtime Loading Model

The host loads plugins through `UI/ColorVision.UI/Plugins/PluginLoader.cs`:

1. Scan first-level folders under the application output `Plugins/` directory.
2. Prefer `manifest.json`.
3. Use `manifest.id` as the plugin configuration cache key.
4. Resolve the main DLL from `manifest.dllpath`; if absent, use folder name plus `.dll`.
5. If the folder contains exactly one `.deps.json`, check `ColorVision.*` dependency versions.
6. Load the main DLL through `Assembly.LoadFrom(...)`.
7. If `manifest.json` is missing, use compatibility loading for a folder-name DLL, but it will not have normal manifest metadata in the plugin cache.

Recommended field folder shape:

```text
ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/<PluginName>/
  <PluginName>.dll
  manifest.json
  README.md
  CHANGELOG.md
  PackageIcon.png        # optional
```

## Scenario A: Plugin Folder Exists But Does Not Load

Steps:

1. Confirm the plugin is directly under host output `Plugins/<PluginName>/`, not nested as `Plugins/<PluginName>/<PluginName>/`.
2. Open `manifest.json` and check `id`, `name`, `version`, and `dllpath`.
3. Confirm the DLL named by `dllpath` exists in the same folder.
4. If `.deps.json` exists, confirm there is exactly one and check host-root `ColorVision.*.dll` versions.
5. Read host logs for `PluginDllNotFound`, `DependencyVersionInsufficient`, or `PluginLoadError`.

Quick check:

```powershell
$plugin = "ColorVision/bin/x64/Release/net10.0-windows/Plugins/Spectrum"
Get-ChildItem $plugin
Get-Content "$plugin/manifest.json"
Get-ChildItem $plugin -Filter "*.deps.json"
```

If `manifest.json` is missing, the host tries compatibility loading by folder-name DLL. Current deliverable plugins should not rely on that mode.

## Scenario B: Plugin Loads But Entry Does Not Appear

Loading the plugin DLL only means the assembly entered the process. Menus, status bars, setting pages, and Socket handlers still depend on provider registration.

| Plugin | Entry checks |
| --- | --- |
| Conoscope | Tool menu `VAM`, `ConoscopeWindow` Ribbon, ImageEditor context menu |
| Spectrum | Tool menu spectrum window, Spectrum window menu, status bar, Socket handlers |
| SystemMonitor | Tool menu, settings page, host status bar |
| EventVWR | Help menu event window and Dump submenu |
| WindowsServicePlugin | Help menu service manager and install wizard entry |

Steps:

1. Confirm the plugin DLL loaded without dependency-version errors.
2. Open the plugin page and identify menu provider, status-bar provider, setting provider, or Socket handler names.
3. If a menu is missing, check whether the plugin implements the provider interface or initializer scanned by the host.
4. If the status bar is missing, check plugin config switches.
5. If only a window-level menu is missing, confirm that the target plugin window has been created.

## Scenario C: Dependency Version Or ColorVision DLL Issue

When a plugin folder contains exactly one `.deps.json`, `PluginLoader` reads its `ColorVision.*` dependencies and looks for matching DLLs in the host root. If the actual version is lower than required, the plugin is skipped.

Steps:

1. Do not replace only the plugin folder; inspect host-root `ColorVision.*.dll` files too.
2. Compare required versions in `.deps.json` with actual DLL assembly versions.
3. If the plugin was built with newer UI DLLs, update the host UI DLL set as well.
4. If the missing file is a native DLL, go to [Scenario E](#scenario-e-device-or-native-dll-plugin).

Version check:

```powershell
$out = "ColorVision/bin/x64/Release/net10.0-windows"
Get-ChildItem $out -Filter "ColorVision*.dll" |
  Select-Object Name, @{Name="AssemblyVersion";Expression={[Reflection.AssemblyName]::GetAssemblyName($_.FullName).Version}}, LastWriteTime
```

## Scenario D: Package And Publish cvxp

Common command:

```powershell
Scripts\package_plugin.bat Spectrum --no-upload
```

`package_plugin.bat` calls `Scripts/package_cvxp.py`. By default it builds, collects plugin output, removes host-shared files from `Scripts/shared_files.json`, copies `README.md`, `CHANGELOG.md`, `manifest.json`, `PackageIcon.png`, reads the main DLL `FileVersion`, and creates `<PluginName>-<FileVersion>.cvxp`.

Pre-release checks:

| Check | Meaning |
| --- | --- |
| manifest version | Plugin manager or marketplace may display `manifest.version` |
| DLL file version | `.cvxp` file name comes from main DLL `FileVersion` |
| CHANGELOG | Must match the actual DLL release, not only manifest |
| README | Runtime help and marketplace description usually read plugin README |
| shared files | Host-shared `ColorVision.*.dll` should not be packaged, but the host must have compatible versions |
| native DLLs | Spectrum and Conoscope need more than managed DLL checks |

Inspect `.cvxp`:

```powershell
$pkg = Get-ChildItem Scripts -Filter "Spectrum-*.cvxp" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$tmp = Join-Path $env:TEMP "cv-plugin-cvxp"
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
New-Item $tmp -ItemType Directory | Out-Null
Copy-Item $pkg.FullName "$tmp/plugin.zip"
Expand-Archive "$tmp/plugin.zip" "$tmp/plugin"
Get-ChildItem "$tmp/plugin" -Recurse
```

## Scenario E: Device Or Native DLL Plugin

| Plugin | External boundary | Check first |
| --- | --- | --- |
| Spectrum | Spectrometer native DLL, serial, SMU, shutter, CFW, license, SQLite result DB | Device logs, license, calibration group, `Magiude.dat`, `WavaLength.dat`, CIE images |
| Conoscope | MVS camera, `MvCameraControl.dll`, image resources, CSV export | MVS SDK, camera driver, image open, focus/reference config |

Steps:

1. Separate “plugin did not load” from “plugin loaded but device cannot work.”
2. For device failure, check driver, native DLL, license, and config before changing plugin logic.
3. Expand `.cvxp` and confirm native DLLs or data files are included, or explicitly document that the field machine must provide them.
4. Record which dependencies are packaged and which are installed on the field machine.

## Scenario F: Administrator Permission Plugin

EventVWR and WindowsServicePlugin touch registry, dump files, Windows services, MySQL, MQTT, local folders, and configuration synchronization. Testing them as a normal user often produces false failures.

Steps:

1. Mark administrator-only operations clearly.
2. For EventVWR, verify Windows Application log, WER LocalDumps registry, and dump output folder.
3. For WindowsServicePlugin, verify service root, service state, MySQL/MQTT installers, and `cfg/*.config` synchronization.
4. Install, uninstall, start, and stop services only in a test environment or explicitly authorized field environment.

## Scenario G: Socket Plugin Command Does Not Work

Spectrum is the current real plugin with Socket JSON commands. It depends on the `ColorVision.SocketProtocol` TCP service, protocol mode, port config, and Spectrum assembly loading.

Steps:

1. Confirm the host Socket service is enabled and the port is free.
2. Confirm protocol mode is JSON, not Text.
3. Confirm Spectrum loaded without `.deps.json` version issues.
4. Open the Spectrum window and confirm device and calibration-group state meet command preconditions.
5. Compare behavior with [Spectrum Plugin](./standard-plugins/spectrum.md) and handlers under `Plugins/Spectrum/Socket/`.

## Scenario H: Restore A Historical Plugin

Pattern, ImageProjector, and ScreenRecorder are not current source plugins under `Plugins/`, and they are not entries under existing plugin capabilities.

Before restoring one as a current plugin, provide:

1. `Plugins/<Name>/` source folder.
2. `<Name>.csproj` with target framework and WPF settings.
3. `manifest.json` with at least `id`, `name`, `version`, and `dllpath`.
4. `README.md` and `CHANGELOG.md`.
5. Post-build or packaging script rules.
6. Current docs page, [Current Plugin Documentation Coverage](./current-plugin-coverage.md), capability matrix row, and navigation entry.
7. At least one `Scripts\package_plugin.bat <Name> --no-upload` run and host loading smoke test.

## Plugin Handoff Record

Every plugin release or handoff should record:

| Item | Content |
| --- | --- |
| Plugin | Name, source directory, manifest id |
| Versions | `manifest.version`, `.csproj VersionPrefix`, output DLL `FileVersion`, `.cvxp` file name |
| Build commands | `dotnet build` and `Scripts\package_plugin.bat ... --no-upload` |
| Required files | DLL, manifest, README, CHANGELOG, PackageIcon, native DLLs, device data files |
| Host dependencies | Whether host-root `ColorVision.*.dll` satisfies `.deps.json` |
| Entry acceptance | Menu, status bar, setting page, window menu, Socket handler |
| External boundary | Device, driver, license, database, registry, Windows service, administrator permission |
| Smoke result | Minimum smoke from the plugin page |
| Rollback | Previous `.cvxp`, plugin-folder backup, host DLL versions |
| Known limits | Untested device, permission, protocol, or field-environment differences |

## Continue Reading

- [Plugin Capability & Handoff Matrix](./plugin-capability-matrix.md)
- [Current Plugin Documentation Coverage](./current-plugin-coverage.md)
- [Existing Plugin Field Acceptance And Handoff Checklist](./plugin-field-acceptance.md)
- [Conoscope](./standard-plugins/conoscope.md)
- [Spectrum](./standard-plugins/spectrum.md)
- [SystemMonitor](./standard-plugins/system-monitor.md)
- [EventVWR](./standard-plugins/eventvwr.md)
- [WindowsServicePlugin](./standard-plugins/windows-service.md)
- [Plugin Development Manual](../../02-developer-guide/plugin-development/README.md)
