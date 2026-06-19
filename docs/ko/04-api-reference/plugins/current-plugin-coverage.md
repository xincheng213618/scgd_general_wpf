# 현재 플러그인 문서 커버리지

이 페이지는 현재 `Plugins/` 에 있는 실제 플러그인마다 기능 페이지, 인수인계 페이지, 검수 체크, runtime README/CHANGELOG 가 있는지 확인합니다.

## 커버리지

| plugin directory | project | manifest | capability page | handoff / acceptance |
| --- | --- | --- | --- | --- |
| `Plugins/Conoscope/` | `Conoscope.csproj` | `Conoscope` / `1.4.6.1` | [Conoscope](./standard-plugins/conoscope.md) | [Matrix](./plugin-capability-matrix.md), [Playbook](./plugin-handoff-playbook.md), [Acceptance](./plugin-field-acceptance.md) |
| `Plugins/EventVWR/` | `EventVWR.csproj` | `EventVWR` / `1.0` | [EventVWR](./standard-plugins/eventvwr.md) | [Matrix](./plugin-capability-matrix.md), [Playbook](./plugin-handoff-playbook.md), [Acceptance](./plugin-field-acceptance.md) |
| `Plugins/Spectrum/` | `Spectrum.csproj` | `Spectrum` / `1.0` | [Spectrum](./standard-plugins/spectrum.md) | [Matrix](./plugin-capability-matrix.md), [Playbook](./plugin-handoff-playbook.md), [Acceptance](./plugin-field-acceptance.md) |
| `Plugins/SystemMonitor/` | `SystemMonitor.csproj` | `SystemMonitor` / `1.0.1` | [SystemMonitor](./standard-plugins/system-monitor.md) | [Matrix](./plugin-capability-matrix.md), [Playbook](./plugin-handoff-playbook.md), [Acceptance](./plugin-field-acceptance.md) |
| `Plugins/WindowsServicePlugin/` | `WindowsServicePlugin.csproj` | `WindowsServicePlugin` / `1.0` | [WindowsServicePlugin](./standard-plugins/windows-service.md) | [Matrix](./plugin-capability-matrix.md), [Playbook](./plugin-handoff-playbook.md), [Acceptance](./plugin-field-acceptance.md) |

## 현재 작업 트리 감사

2026-06-10 기준 작업 트리에서 5개 plugin directory 모두 `.csproj`, `manifest.json`, runtime `README.md`, runtime `CHANGELOG.md`, docs plugin page 를 가지고 있습니다.

| plugin directory | `.csproj` | `manifest.json` | runtime README | runtime CHANGELOG | result |
| --- | --- | --- | --- | --- | --- |
| `Plugins/Conoscope/` | present | `Conoscope` / `1.4.6.1` | present | present | complete |
| `Plugins/EventVWR/` | present | `EventVWR` / `1.0` | present | present | complete |
| `Plugins/Spectrum/` | present | `Spectrum` / `1.0` | present | present | complete |
| `Plugins/SystemMonitor/` | present | `SystemMonitor` / `1.0.1` | present | present | complete |
| `Plugins/WindowsServicePlugin/` | present | `WindowsServicePlugin` / `1.0` | present | present | complete |

runtime README/CHANGELOG 는 package 와 현장 디렉터리에서 읽힙니다. docs site page 는 기능, 경계, 위험, 검수 방법을 인수인계 담당자에게 설명합니다. 양쪽을 같이 유지합니다.

## 현재 목록에 없는 이름

Pattern, ImageProjector, ScreenRecorder 는 현재 플러그인이 아닙니다. 복원 전에는 `Plugins/<Name>/`, `.csproj`, `manifest.json`, README, CHANGELOG, 빌드 복사, 패키징 검증, 문서 내비게이션을 먼저 복구해야 합니다.

## 커버리지 확인

```powershell
Get-ChildItem Plugins -Directory | Sort-Object Name | Select-Object -ExpandProperty Name
Get-ChildItem docs/ko/04-api-reference/plugins/standard-plugins -File | Sort-Object Name | Select-Object -ExpandProperty Name
```

결과는 현재 5개 plugin 만 current capability 로 다뤄야 합니다. historical name 은 restore check 문맥에만 둡니다.
