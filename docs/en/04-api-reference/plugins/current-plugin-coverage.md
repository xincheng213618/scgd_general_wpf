# Current Plugin Documentation Coverage

This page answers one concrete handoff question: does every real plugin under `Plugins/` have a matching documentation entry, handoff page, and release check?

Current plugins are recognized by source directory. A name without `Plugins/<Name>/`, `.csproj`, and `manifest.json` is not part of the current plugin capability entry.

## Current Coverage

| Plugin directory | Project file | manifest Id / version | Current capability page | Handoff and acceptance coverage | Package command |
| --- | --- | --- | --- | --- | --- |
| `Plugins/Conoscope/` | `Conoscope.csproj` | `Conoscope` / `1.4.6.1` | [Conoscope](./standard-plugins/conoscope.md) | [Capability matrix](./plugin-capability-matrix.md), [runtime playbook](./plugin-handoff-playbook.md), [field acceptance](./plugin-field-acceptance.md) | `Scripts\package_plugin.bat Conoscope --no-upload` |
| `Plugins/Spectrum/` | `Spectrum.csproj` | `Spectrum` / `1.0` | [Spectrum](./standard-plugins/spectrum.md) | [Capability matrix](./plugin-capability-matrix.md), [runtime playbook](./plugin-handoff-playbook.md), [field acceptance](./plugin-field-acceptance.md) | `Scripts\package_plugin.bat Spectrum --no-upload` |
| `Plugins/SystemMonitor/` | `SystemMonitor.csproj` | `SystemMonitor` / `1.0.1` | [SystemMonitor](./standard-plugins/system-monitor.md) | [Capability matrix](./plugin-capability-matrix.md), [runtime playbook](./plugin-handoff-playbook.md), [field acceptance](./plugin-field-acceptance.md) | `Scripts\package_plugin.bat SystemMonitor --no-upload` |
| `Plugins/EventVWR/` | `EventVWR.csproj` | `EventVWR` / `1.0` | [EventVWR](./standard-plugins/eventvwr.md) | [Capability matrix](./plugin-capability-matrix.md), [runtime playbook](./plugin-handoff-playbook.md), [field acceptance](./plugin-field-acceptance.md) | `Scripts\package_plugin.bat EventVWR --no-upload` |
| `Plugins/WindowsServicePlugin/` | `WindowsServicePlugin.csproj` | `WindowsServicePlugin` / `1.0` | [WindowsServicePlugin](./standard-plugins/windows-service.md) | [Capability matrix](./plugin-capability-matrix.md), [runtime playbook](./plugin-handoff-playbook.md), [field acceptance](./plugin-field-acceptance.md) | `Scripts\package_plugin.bat WindowsServicePlugin --no-upload` |

## Current Repository Audit Evidence

On 2026-06-10, the current worktree contains five plugin directories. All five meet the minimum evidence for a current plugin documentation entry: `.csproj`, `manifest.json`, runtime `README.md`, runtime `CHANGELOG.md`, a docs-site plugin page, and matrix/playbook/acceptance coverage.

| Plugin directory | `.csproj` | `manifest.json` | Runtime README | Runtime CHANGELOG | Docs plugin page | Result |
| --- | --- | --- | --- | --- | --- | --- |
| `Plugins/Conoscope/` | present | `Conoscope` / `1.4.6.1` | present | present | present | complete |
| `Plugins/EventVWR/` | present | `EventVWR` / `1.0` | present | present | present | complete |
| `Plugins/Spectrum/` | present | `Spectrum` / `1.0` | present | present | present | complete |
| `Plugins/SystemMonitor/` | present | `SystemMonitor` / `1.0.1` | present | present | present | complete |
| `Plugins/WindowsServicePlugin/` | present | `WindowsServicePlugin` / `1.0` | present | present | present | complete |

Runtime README/CHANGELOG files matter because plugin help, packages, and field handoff often read files from the plugin directory. The docs-site page explains capability, boundary, risk, and acceptance for handoff staff. Keep both sides aligned.

## External Boundary Coverage

| Plugin | Boundary that must be documented | Current documentation entry |
| --- | --- | --- |
| Conoscope | MVS camera, `MvCameraControl.dll`, image resources, focus points, CSV export | Plugin page, capability matrix, field acceptance |
| Spectrum | Spectrometer native DLLs, serial, SMU/Shutter/CFW, license, SQLite result DB, Socket JSON commands | Plugin page, capability matrix, runtime playbook, field acceptance |
| SystemMonitor | Windows performance counters, CUDA information, disk/network/process data, cache-directory permission | Plugin page, capability matrix, field acceptance |
| EventVWR | Windows EventLog, WER LocalDumps, HKLM registry, administrator permission | Plugin page, capability matrix, runtime playbook, field acceptance |
| WindowsServicePlugin | Windows services, MySQL, MQTT, service ZIP package, config synchronization, administrator permission | Plugin page, capability matrix, runtime playbook, field acceptance |

## Names Outside The Current Plugin Inventory

The following names are no longer maintained as current plugin capability entries:

| Name | Current state | Before restoring |
| --- | --- | --- |
| Pattern | No `Plugins/Pattern/` plugin project exists | Restore source folder, `.csproj`, `manifest.json`, README, CHANGELOG, build copy, and package verification |
| ImageProjector | No `Plugins/ImageProjector/` plugin project exists | Restore source folder, `.csproj`, `manifest.json`, README, CHANGELOG, build copy, and package verification |
| ScreenRecorder | No `Plugins/ScreenRecorder/` plugin project exists | Restore source folder, `.csproj`, `manifest.json`, README, CHANGELOG, build copy, and package verification |

If one of these names becomes a real plugin again, first follow the "restore a historical plugin" scenario in [Plugin Runtime And Handoff Playbook](./plugin-handoff-playbook.md), then add it back to this page, [Plugin Capability & Handoff Matrix](./plugin-capability-matrix.md), [Existing Plugin Field Acceptance](./plugin-field-acceptance.md), and the sidebar.

## Coverage Check

After adding, deleting, or restoring a plugin, audit it with:

```powershell
Get-ChildItem Plugins -Directory | Sort-Object Name | Select-Object -ExpandProperty Name
Get-ChildItem docs/04-api-reference/plugins/standard-plugins -File | Sort-Object Name | Select-Object -ExpandProperty Name
Get-ChildItem Plugins -Directory | Sort-Object Name | ForEach-Object {
  "$($_.Name): csproj=$([bool](Get-ChildItem $_.FullName -Filter *.csproj -File)) manifest=$(Test-Path (Join-Path $_.FullName 'manifest.json')) readme=$(Test-Path (Join-Path $_.FullName 'README.md')) changelog=$(Test-Path (Join-Path $_.FullName 'CHANGELOG.md'))"
}
```

The result must satisfy:

1. Every current `Plugins/<Name>/` has a single-plugin capability page.
2. Every single-plugin page points back to the real source directory, `.csproj`, `manifest.json`, and key classes.
3. Every runtime plugin directory keeps `README.md` and `CHANGELOG.md`, aligned with the docs-site page.
4. The matrix, playbook, field checklist, and sidebar only list real plugins as current capabilities.
5. Historical names can appear only in a restore-check context, not as current feature entries.
