# Existing Plugin Capabilities

This chapter explains what the general plugins that actually exist under `Plugins/` can do, where they enter the host, what they depend on, how to build them, and how to maintain them. Plugin development methods live in the [Plugin Development Manual](../../02-developer-guide/plugin-development/README.md), while customer project packages live in the [Project Guide](../../00-projects/README.md).

Historical names are handled only as restore-check context, not as current plugin capability entries.

## Current Plugin Inventory

To confirm that every current plugin has matching documentation, start with [Current Plugin Documentation Coverage](./current-plugin-coverage.md). For a horizontal handoff and release view, use [Plugin Capability & Handoff Matrix](./plugin-capability-matrix.md). For concrete tasks such as missing plugins, field DLL issues, `.cvxp` packaging, administrator permission, or Socket commands, use [Plugin Runtime And Handoff Playbook](./plugin-handoff-playbook.md). For release or field replacement acceptance, use [Existing Plugin Field Acceptance And Handoff Checklist](./plugin-field-acceptance.md). For manifest, DLL FileVersion, `.cvxp`, native file, and rollback evidence, use [Plugin Release Evidence Checklist](./plugin-release-evidence.md).

| Plugin | Source directory | manifest Id | Capability | Docs |
| --- | --- | --- | --- | --- |
| Conoscope | `Plugins/Conoscope/` | `Conoscope` | VAM/conoscope image viewing, focus points, color gamut and contrast analysis | [Conoscope](./standard-plugins/conoscope.md) |
| Spectrum | `Plugins/Spectrum/` | `Spectrum` | Spectrometer connection, calibration, measurement, EQE, SQLite results | [Spectrum](./standard-plugins/spectrum.md) |
| SystemMonitor | `Plugins/SystemMonitor/` | `SystemMonitor` | Performance monitoring, status bar, disk/network/process information | [SystemMonitor](./standard-plugins/system-monitor.md) |
| EventVWR | `Plugins/EventVWR/` | `EventVWR` | Windows event error viewing and dump configuration | [EventVWR](./standard-plugins/eventvwr.md) |
| WindowsServicePlugin | `Plugins/WindowsServicePlugin/` | `WindowsServicePlugin` | CVWindowsService installation, registration, MySQL/MQTT configuration | [WindowsServicePlugin](./standard-plugins/windows-service.md) |

## Where To Start

| Need | Start with |
| --- | --- |
| Confirm that every current plugin has a document | [Current Plugin Documentation Coverage](./current-plugin-coverage.md) |
| Take over all existing plugins and judge complexity/risk | [Plugin Capability & Handoff Matrix](./plugin-capability-matrix.md) |
| Troubleshoot missing plugin, missing DLL, permission, or Socket issues | [Plugin Runtime And Handoff Playbook](./plugin-handoff-playbook.md) |
| Accept or replace an existing plugin in the field | [Existing Plugin Field Acceptance And Handoff Checklist](./plugin-field-acceptance.md) |
| Record manifest, DLL version, `.cvxp`, native files, and rollback packages | [Plugin Release Evidence Checklist](./plugin-release-evidence.md) |
| Add a general plugin | [Plugin Development Manual](../../02-developer-guide/plugin-development/README.md) |
| Understand one plugin's business behavior | Open that plugin's page |

## Runtime Loading Model

Plugins are loaded by `UI/ColorVision.UI/Plugins/PluginLoader.cs`:

1. Scan first-level subdirectories under the application output `Plugins/` directory.
2. Prefer each directory's `manifest.json`.
3. Use the manifest `Id` to update the plugin configuration cache.
4. Resolve the plugin DLL from `dllpath`.
5. If the directory contains exactly one `.deps.json`, check `ColorVision.*` dependency versions.
6. Load the assembly through `Assembly.LoadFrom(...)`.
7. If no `manifest.json` exists, fall back to a compatibility mode that tries a DLL with the same name as the folder.

The recommended delivery shape is:

```text
ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/<PluginName>/
  <PluginName>.dll
  manifest.json
  README.md
  CHANGELOG.md
  PackageIcon.png        # optional
```

## manifest Fields

| Field | Meaning |
| --- | --- |
| `manifest_version` | Manifest schema version, currently `1` |
| `id` / `Id` | Unique plugin id, used as the runtime cache key |
| `name` | Display name |
| `version` | Plugin manifest version |
| `requires` | Minimum ColorVision version |
| `description` | Plugin description |
| `dllpath` | Main assembly file name |
| `author`, `url`, `entry_point`, `icon` | Optional metadata |

Existing manifests mix casing. Runtime deserialization uses `PluginManifest` through Newtonsoft.Json, so maintenance should preserve compatibility with existing fields.

## Build and Package

Build one plugin:

```powershell
dotnet build Plugins/Spectrum/Spectrum.csproj -c Release -p:Platform=x64
```

Create a `.cvxp` package:

```powershell
Scripts\package_plugin.bat Spectrum --no-upload
```

`package_plugin.bat` calls `Scripts/package_cvxp.py` and will:

- Optionally run `dotnet build`.
- Collect DLLs and dependencies from the plugin output directory.
- Remove files already shared by the host according to `Scripts/shared_files.json`.
- Copy root plugin files such as `README.md`, `CHANGELOG.md`, `manifest.json`, and `PackageIcon.png`.
- Generate `<PluginName>-<FileVersion>.cvxp`.

## Names Outside The Current Plugin Inventory

These names currently have no matching `Plugins/<Name>/` source directory, `.csproj`, and `manifest.json`, so they are not entries under existing plugin capabilities:

- Pattern
- ImageProjector
- ScreenRecorder

If one of these plugins returns to `Plugins/`, restore source code, manifest, build scripts, README, CHANGELOG, and package verification first, then add it back to [Current Plugin Documentation Coverage](./current-plugin-coverage.md) and the current inventory.

## Maintenance Rules

- Every new plugin must have `Plugins/<Name>/README.md` and a docs site page.
- Adding, deleting, or restoring a plugin must update [Current Plugin Documentation Coverage](./current-plugin-coverage.md).
- Capability, external dependency, package, or smoke-test changes must update [Plugin Runtime And Handoff Playbook](./plugin-handoff-playbook.md), [Plugin Capability & Handoff Matrix](./plugin-capability-matrix.md), [Existing Plugin Field Acceptance And Handoff Checklist](./plugin-field-acceptance.md), and [Plugin Release Evidence Checklist](./plugin-release-evidence.md).
- Manifest or post-build copy rule changes must update the loading and packaging sections here.
- If the runtime help window reads plugin `README.md` or `CHANGELOG.md`, update both runtime files and docs pages together.
