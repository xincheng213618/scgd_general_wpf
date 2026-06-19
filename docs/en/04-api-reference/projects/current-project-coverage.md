# Current Project Documentation Coverage

This page verifies that every real project package under `Projects/` has a matching documentation entry and handoff path.

## Coverage Result

| Project directory | Project file | manifest Id / version | Current docs page | Handoff coverage |
| --- | --- | --- | --- | --- |
| `Projects/ProjectARVR/` | `ProjectARVR.csproj` | `ProjectARVR` / `1.0` | [ProjectARVR](./project-arvr.md) | [Matrix](./project-capability-matrix.md), [playbook](./project-package-playbook.md), [handoff](./project-handoff.md) |
| `Projects/ProjectARVRLite/` | `ProjectARVRLite.csproj` | `ProjectARVRLite` / `1.0` | [ProjectARVRLite](./project-arvr-lite.md) | [Matrix](./project-capability-matrix.md), [playbook](./project-package-playbook.md), [handoff](./project-handoff.md) |
| `Projects/ProjectARVRPro/` | `ProjectARVRPro.csproj` | `ProjectARVRPro` / `1.1.7.7` | [ProjectARVRPro](./project-arvr-pro.md) | [Matrix](./project-capability-matrix.md), [playbook](./project-package-playbook.md), [handoff](./project-handoff.md) |
| `Projects/ProjectARVRPro.IntegrationDemo/` | `ProjectARVRPro.IntegrationDemo.csproj` | No manifest | [ARVRPro Integration Demo](./project-arvr-pro-integration-demo.md) | [Matrix](./project-capability-matrix.md), [playbook](./project-package-playbook.md) |
| `Projects/ProjectBlackMura/` | `ProjectBlackMura.csproj` | `ProjectBlackMura` / `1.0` | [ProjectBlackMura](./project-black-mura.md) | [Matrix](./project-capability-matrix.md), [playbook](./project-package-playbook.md), [handoff](./project-handoff.md) |
| `Projects/ProjectHeyuan/` | `ProjectHeyuan.csproj` | `ProjectHeyuan` / `1.0` | [ProjectHeyuan](./project-heyuan.md) | [Matrix](./project-capability-matrix.md), [playbook](./project-package-playbook.md), [handoff](./project-handoff.md) |
| `Projects/ProjectKB/` | `ProjectKB.csproj` | `ProjectKB` / `1.0` | [ProjectKB](./project-kb.md) | [Matrix](./project-capability-matrix.md), [playbook](./project-package-playbook.md), [handoff](./project-handoff.md) |
| `Projects/ProjectLUX/` | `ProjectLUX.csproj` | `ProjectLUX` / `1.0` | [ProjectLUX](./project-lux.md) | [Matrix](./project-capability-matrix.md), [playbook](./project-package-playbook.md), [handoff](./project-handoff.md) |
| `Projects/ProjectShiyuan/` | `ProjectShiyuan.csproj` | `ProjectShiyuan` / `1.0` | [ProjectShiyuan](./project-shiyuan.md) | [Matrix](./project-capability-matrix.md), [playbook](./project-package-playbook.md), [handoff](./project-handoff.md) |

## Current Repository Audit Evidence

On 2026-06-10, the current worktree contains nine directories under `Projects/`. Eight runtime project packages have `.csproj`, `manifest.json`, `README.md`, `CHANGELOG.md`, and a docs-site project page. `ProjectARVRPro.IntegrationDemo` is a customer-side integration demo; its project file declares `OutputType=Exe`, `TargetFrameworks=net48`, and `IsPackable=false`, so the missing manifest and CHANGELOG are a documented boundary, not a runtime package gap.

| Project directory | `.csproj` | `manifest.json` | README | CHANGELOG | Docs project page | Result |
| --- | --- | --- | --- | --- | --- | --- |
| `Projects/ProjectARVR/` | present | `ProjectARVR` / `1.0` | present | present | present | runtime package complete |
| `Projects/ProjectARVRLite/` | present | `ProjectARVRLite` / `1.0` | present | present | present | runtime package complete |
| `Projects/ProjectARVRPro/` | present | `ProjectARVRPro` / `1.1.7.7` | present | present | present | runtime package complete |
| `Projects/ProjectARVRPro.IntegrationDemo/` | present | none | present | none | present | customer integration demo, not manifest-gated |
| `Projects/ProjectBlackMura/` | present | `ProjectBlackMura` / `1.0` | present | present | present | runtime package complete |
| `Projects/ProjectHeyuan/` | present | `ProjectHeyuan` / `1.0` | present | present | present | runtime package complete |
| `Projects/ProjectKB/` | present | `ProjectKB` / `1.0` | present | present | present | runtime package complete |
| `Projects/ProjectLUX/` | present | `ProjectLUX` / `1.0` | present | present | present | runtime package complete |
| `Projects/ProjectShiyuan/` | present | `ProjectShiyuan` / `1.0` | present | present | present | runtime package complete |

If `ProjectARVRPro.IntegrationDemo` later becomes a package shipped with the host application, add `manifest.json`, `CHANGELOG.md`, PostBuild copy rules, packaging verification, and release evidence first. Until then, it remains a customer protocol demo in this coverage table.

## Required Handoff Boundaries

| Boundary | Projects |
| --- | --- |
| JSON Socket picture-switch flow | ProjectARVR, ProjectARVRLite, ProjectARVRPro |
| Text Socket command flow | ProjectLUX |
| Serial/MES or PG control | ProjectBlackMura, ProjectHeyuan |
| Modbus/MES DLL production line integration | ProjectKB |
| Manual/offline customer file export | ProjectShiyuan |
| Customer-side protocol demo | ProjectARVRPro.IntegrationDemo |

## Coverage Check

```powershell
Get-ChildItem Projects -Directory | Sort-Object Name | Select-Object -ExpandProperty Name
Get-ChildItem docs/en/04-api-reference/projects -File | Sort-Object Name | Select-Object -ExpandProperty Name
Get-ChildItem Projects -Directory | Sort-Object Name | ForEach-Object {
  "$($_.Name): csproj=$([bool](Get-ChildItem $_.FullName -Filter *.csproj -File)) manifest=$(Test-Path (Join-Path $_.FullName 'manifest.json')) readme=$(Test-Path (Join-Path $_.FullName 'README.md')) changelog=$(Test-Path (Join-Path $_.FullName 'CHANGELOG.md'))"
}
```

Every current project directory must have a project page. Project-specific protocol, result fields, packaging, or acceptance changes must update the project page, [Project Capability & Handoff Matrix](./project-capability-matrix.md), [Project Package Runtime And Handoff Playbook](./project-package-playbook.md), and [Project Package Release Evidence Checklist](./project-release-evidence.md).
