# Project Packages

`Projects/` contains customer projects, business solution packages, and integration demos. At runtime they usually enter the host as plugin folders, but they should be documented separately from general plugins. Project packages care more about customer workflow, Recipe/Fix rules, Socket/MES integration, and result export.

For a horizontal comparison, start with [Project Capability & Handoff Matrix](./project-capability-matrix.md). For concrete field issues around triggers, workflow, templates, Recipe/Fix, result output, and packaging, use [Project Package Runtime And Handoff Playbook](./project-package-playbook.md). For release, field replacement, and rollback evidence, use [Project Package Release Evidence Checklist](./project-release-evidence.md). For the common project execution chain, read [Project Package Handoff Manual](./project-handoff.md). To verify project-to-document coverage, use [Current Project Documentation Coverage](./current-project-coverage.md).

## Current Project Inventory

| Project | Source directory | manifest Id | Business positioning | Docs |
| --- | --- | --- | --- | --- |
| ProjectARVR | `Projects/ProjectARVR/` | `ProjectARVR` | AR/VR optical testing | [Details](./project-arvr.md) |
| ProjectARVRLite | `Projects/ProjectARVRLite/` | `ProjectARVRLite` | Lightweight AR/VR quick testing | [Details](./project-arvr-lite.md) |
| ProjectARVRPro | `Projects/ProjectARVRPro/` | `ProjectARVRPro` | Professional AR/VR flow groups, Recipe, Socket integration | [Details](./project-arvr-pro.md) |
| ProjectARVRPro.IntegrationDemo | `Projects/ProjectARVRPro.IntegrationDemo/` | No manifest | TCP/JSON integration sample for customers | [Details](./project-arvr-pro-integration-demo.md) |
| ProjectBlackMura | `Projects/ProjectBlackMura/` | `ProjectBlackMura` | Display panel Black Mura inspection | [Details](./project-black-mura.md) |
| ProjectHeyuan | `Projects/ProjectHeyuan/` | `ProjectHeyuan` | Heyuan Jingdian customer-specific testing | [Details](./project-heyuan.md) |
| ProjectKB | `Projects/ProjectKB/` | `ProjectKB` | Keyboard backlight luminance and uniformity testing | [Details](./project-kb.md) |
| ProjectLUX | `Projects/ProjectLUX/` | `ProjectLUX` | Automated luminance/color/MTF/distortion testing | [Details](./project-lux.md) |
| ProjectShiyuan | `Projects/ProjectShiyuan/` | `ProjectShiyuan` | Shiyuan customer-specific testing | [Details](./project-shiyuan.md) |

## How Project Packages Differ from Plugins

Project packages usually also have `manifest.json` and are copied to `Plugins/<Name>/` in the main application output. Their core purpose is not to provide a general tool; they combine Engine, Flow, templates, algorithm results, and customer protocols into a business workflow.

Typical project packages contain:

- Plugin menu entry or window singleton.
- Flow group or flow template selection.
- Recipe limit configuration.
- Fix/calibration correction configuration.
- Socket, Modbus, MES, or serial integration.
- `ObjectiveTestResult` or project-specific result models.
- CSV, PDF, SQLite, or customer-system upload.

## Build and Package

Build one project:

```powershell
dotnet build Projects/ProjectLUX/ProjectLUX.csproj -c Release -p:Platform=x64
```

Create a `.cvxp` package:

```powershell
Scripts\package_project.bat ProjectLUX --no-upload
```

`package_project.bat` follows the same packaging flow as plugins. It calls `Scripts/package_cvxp.py` and packages project outputs together with root README, CHANGELOG, manifest, and PackageIcon files.

## Handoff Focus

| Goal | Start here |
| --- | --- |
| Compare all project capabilities | [Project Capability & Handoff Matrix](./project-capability-matrix.md) |
| Verify project documentation coverage | [Current Project Documentation Coverage](./current-project-coverage.md) |
| Handle concrete project-package issues | [Project Package Runtime And Handoff Playbook](./project-package-playbook.md) |
| Release, replace, or roll back a project `.cvxp` | [Project Package Release Evidence Checklist](./project-release-evidence.md) |
| Understand customer workflow | [Project Package Handoff Manual](./project-handoff.md), Project README, main window, `Process/` |
| Understand judgment rules | `Recipe/`, `Fix/`, project configuration classes |
| Understand automation integration | `Services/SocketControl.cs`, Modbus/MES/Serial classes |
| Understand result storage and export | `ObjectiveTestResult.cs`, `ViewResultManager.cs`, result windows |
| Understand menu entry | `PluginConfig/` or project root `Menu*.cs` |

## Maintenance Rules

- Every `Projects/<Name>/` must have a project README, a docs-site page, and a row in [Current Project Documentation Coverage](./current-project-coverage.md).
- Protocol, result-output, flow organization, or delivery-acceptance changes must update [Project Package Runtime And Handoff Playbook](./project-package-playbook.md), [Project Capability & Handoff Matrix](./project-capability-matrix.md), and [Project Package Release Evidence Checklist](./project-release-evidence.md).
- Manifest, menu name, Socket event, Recipe field, or export field changes must update the corresponding project page.
- Project pages should describe only behavior that can be matched to current source code.
