# 현재 프로젝트 문서 커버리지

이 페이지는 `Projects/` 아래 실제 프로젝트가 문서, 인수인계 입구, runtime metadata 를 가지고 있는지 확인합니다.

## 커버리지

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

## 현재 작업 트리 감사

2026-06-10 기준 작업 트리에는 `Projects/` 아래 9개 directory 가 있습니다. 8개 runtime project package 는 `.csproj`, `manifest.json`, `README.md`, `CHANGELOG.md`, docs project page 를 가지고 있습니다. `ProjectARVRPro.IntegrationDemo` 는 customer-side integration demo 이며 project file 이 `OutputType=Exe`, `TargetFrameworks=net48`, `IsPackable=false` 를 선언하므로 manifest 와 CHANGELOG 가 없는 것은 알려진 경계입니다.

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

`ProjectARVRPro.IntegrationDemo` 를 host application 과 함께 배포되는 정식 package 로 바꾸려면 먼저 `manifest.json`, `CHANGELOG.md`, PostBuild copy rule, packaging verification, release evidence 를 추가합니다.

## 반드시 유지할 인수인계 경계

| boundary | projects |
| --- | --- |
| JSON Socket picture-switch flow | ProjectARVR, ProjectARVRLite, ProjectARVRPro |
| Text Socket command flow | ProjectLUX |
| Serial/MES or PG control | ProjectBlackMura, ProjectHeyuan |
| Modbus/MES DLL production integration | ProjectKB |
| Manual/offline customer file export | ProjectShiyuan |
| Customer-side protocol demo | ProjectARVRPro.IntegrationDemo |

`Projects/<Name>/` 을 추가, 삭제, 이름 변경하면 이 표, 프로젝트 개요, 매트릭스, playbook, release evidence 를 함께 업데이트합니다.
