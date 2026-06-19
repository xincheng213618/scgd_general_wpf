# 現在のプロジェクト文書カバレッジ

このページは `Projects/` 配下の実プロジェクトが、文書、引き継ぎ入口、runtime metadata を持っているか確認するための表です。

## カバレッジ一覧

| project directory | project file | manifest Id / version | docs page | handoff entry |
| --- | --- | --- | --- | --- |
| `Projects/ProjectARVR/` | `ProjectARVR.csproj` | `ProjectARVR` / `1.0` | [ProjectARVR](./project-arvr.md) | [Matrix](./project-capability-matrix.md), [Playbook](./project-package-playbook.md), [Handoff](./project-handoff.md) |
| `Projects/ProjectARVRLite/` | `ProjectARVRLite.csproj` | `ProjectARVRLite` / `1.0` | [ProjectARVRLite](./project-arvr-lite.md) | [Matrix](./project-capability-matrix.md), [Playbook](./project-package-playbook.md), [Handoff](./project-handoff.md) |
| `Projects/ProjectARVRPro/` | `ProjectARVRPro.csproj` | `ProjectARVRPro` / `1.1.7.7` | [ProjectARVRPro](./project-arvr-pro.md) | [Matrix](./project-capability-matrix.md), [Playbook](./project-package-playbook.md), [Handoff](./project-handoff.md) |
| `Projects/ProjectARVRPro.IntegrationDemo/` | `ProjectARVRPro.IntegrationDemo.csproj` | none | [ARVRPro Integration Demo](./project-arvr-pro-integration-demo.md) | [Matrix](./project-capability-matrix.md), [Playbook](./project-package-playbook.md) |
| `Projects/ProjectBlackMura/` | `ProjectBlackMura.csproj` | `ProjectBlackMura` / `1.0` | [ProjectBlackMura](./project-black-mura.md) | [Matrix](./project-capability-matrix.md), [Playbook](./project-package-playbook.md), [Handoff](./project-handoff.md) |
| `Projects/ProjectHeyuan/` | `ProjectHeyuan.csproj` | `ProjectHeyuan` / `1.0` | [ProjectHeyuan](./project-heyuan.md) | [Matrix](./project-capability-matrix.md), [Playbook](./project-package-playbook.md), [Handoff](./project-handoff.md) |
| `Projects/ProjectKB/` | `ProjectKB.csproj` | `ProjectKB` / `1.0` | [ProjectKB](./project-kb.md) | [Matrix](./project-capability-matrix.md), [Playbook](./project-package-playbook.md), [Handoff](./project-handoff.md) |
| `Projects/ProjectLUX/` | `ProjectLUX.csproj` | `ProjectLUX` / `1.0` | [ProjectLUX](./project-lux.md) | [Matrix](./project-capability-matrix.md), [Playbook](./project-package-playbook.md), [Handoff](./project-handoff.md) |
| `Projects/ProjectShiyuan/` | `ProjectShiyuan.csproj` | `ProjectShiyuan` / `1.0` | [ProjectShiyuan](./project-shiyuan.md) | [Matrix](./project-capability-matrix.md), [Playbook](./project-package-playbook.md), [Handoff](./project-handoff.md) |

## 現在の作業ツリー監査

2026-06-10 時点の作業ツリーでは、`Projects/` に 9 directory があります。8 個の runtime project package は `.csproj`、`manifest.json`、`README.md`、`CHANGELOG.md`、docs project page を持っています。`ProjectARVRPro.IntegrationDemo` は customer-side integration demo で、project file は `OutputType=Exe`、`TargetFrameworks=net48`、`IsPackable=false` を宣言しているため、manifest と CHANGELOG が無いことは既知の境界です。

| project directory | `.csproj` | `manifest.json` | README | CHANGELOG | result |
| --- | --- | --- | --- | --- | --- |
| `Projects/ProjectARVR/` | present | `ProjectARVR` / `1.0` | present | present | runtime package complete |
| `Projects/ProjectARVRLite/` | present | `ProjectARVRLite` / `1.0` | present | present | runtime package complete |
| `Projects/ProjectARVRPro/` | present | `ProjectARVRPro` / `1.1.7.7` | present | present | runtime package complete |
| `Projects/ProjectARVRPro.IntegrationDemo/` | present | none | present | none | customer integration demo, not manifest-gated |
| `Projects/ProjectBlackMura/` | present | `ProjectBlackMura` / `1.0` | present | present | runtime package complete |
| `Projects/ProjectHeyuan/` | present | `ProjectHeyuan` / `1.0` | present | present | runtime package complete |
| `Projects/ProjectKB/` | present | `ProjectKB` / `1.0` | present | present | runtime package complete |
| `Projects/ProjectLUX/` | present | `ProjectLUX` / `1.0` | present | present | runtime package complete |
| `Projects/ProjectShiyuan/` | present | `ProjectShiyuan` / `1.0` | present | present | runtime package complete |

`ProjectARVRPro.IntegrationDemo` を host application と一緒に出荷する正式 package にする場合は、先に `manifest.json`、`CHANGELOG.md`、PostBuild copy rule、packaging verification、release evidence を追加します。

## 必ず残す引き継ぎ境界

| boundary | projects |
| --- | --- |
| JSON Socket picture-switch flow | ProjectARVR, ProjectARVRLite, ProjectARVRPro |
| Text Socket command flow | ProjectLUX |
| Serial/MES or PG control | ProjectBlackMura, ProjectHeyuan |
| Modbus/MES DLL production integration | ProjectKB |
| Manual/offline customer file export | ProjectShiyuan |
| Customer-side protocol demo | ProjectARVRPro.IntegrationDemo |

`Projects/<Name>/` を追加、削除、改名した場合は、この表、プロジェクト概要、マトリクス、playbook、release evidence を同時に更新します。
