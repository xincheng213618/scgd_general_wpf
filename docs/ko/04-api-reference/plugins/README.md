# 기존 플러그인 기능

이 장은 현재 `Plugins/` 디렉터리에 실제로 존재하는 플러그인만 현재 기능으로 다룹니다. `Plugins/<Name>/`, `.csproj`, `manifest.json` 이 없는 이름은 현재 플러그인 목록에 넣지 않습니다.

## 현재 플러그인

| 플러그인 | 소스 디렉터리 | manifest Id | 주요 기능 | 문서 |
| --- | --- | --- | --- | --- |
| Conoscope | `Plugins/Conoscope/` | `Conoscope` | VAM/코노스코프 이미지, 포커스 포인트, 색역, 대비 분석 | [Conoscope](./standard-plugins/conoscope.md) |
| Spectrum | `Plugins/Spectrum/` | `Spectrum` | 분광기 연결, 보정, 측정, EQE, SQLite 결과 | [Spectrum](./standard-plugins/spectrum.md) |
| SystemMonitor | `Plugins/SystemMonitor/` | `SystemMonitor` | 성능 모니터링, 상태 표시줄, 디스크/네트워크/프로세스 정보 | [SystemMonitor](./standard-plugins/system-monitor.md) |
| EventVWR | `Plugins/EventVWR/` | `EventVWR` | Windows 이벤트 오류 보기, Dump 설정 | [EventVWR](./standard-plugins/eventvwr.md) |
| WindowsServicePlugin | `Plugins/WindowsServicePlugin/` | `WindowsServicePlugin` | CVWindowsService 설치, 등록, MySQL/MQTT 설정 | [WindowsServicePlugin](./standard-plugins/windows-service.md) |

## 먼저 읽을 문서

| 목적 | 문서 |
| --- | --- |
| 현재 플러그인과 문서 대응 확인 | [현재 플러그인 문서 커버리지](./current-plugin-coverage.md) |
| 기능, 진입점, 위험 비교 | [플러그인 기능 및 인수인계 매트릭스](./plugin-capability-matrix.md) |
| 로딩, DLL, 권한, Socket, 패키징 문제 | [플러그인 런타임 및 인수인계 플레이북](./plugin-handoff-playbook.md) |
| 릴리스 또는 현장 교체 검수 | [기존 플러그인 현장 검수 체크리스트](./plugin-field-acceptance.md) |
| manifest, DLL version, `.cvxp`, native file, rollback 기록 | [플러그인 릴리스 증거 및 버전 확인표](./plugin-release-evidence.md) |
| 새 플러그인 개발 | [플러그인 개발 매뉴얼](../../02-developer-guide/plugin-development/README.md) |

## 로딩 및 배포 모델

플러그인은 `UI/ColorVision.UI/Plugins/PluginLoader.cs` 로 로드됩니다. 애플리케이션 출력 폴더의 `Plugins/` 1단계 하위 폴더를 스캔하고, `manifest.json`, `dllpath`, 필요한 경우 `.deps.json` 의 `ColorVision.*` 의존성을 확인한 뒤 `Assembly.LoadFrom(...)` 으로 로드합니다.

권장 배포 형태:

```text
ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/<PluginName>/
  <PluginName>.dll
  manifest.json
  README.md
  CHANGELOG.md
  PackageIcon.png        # optional
```

## 현재 목록에 없는 이름

Pattern, ImageProjector, ScreenRecorder 는 현재 `Plugins/<Name>/` 소스, `.csproj`, `manifest.json` 이 없으므로 현재 기능 진입점이 아닙니다. 다시 복원하려면 소스, manifest, README, CHANGELOG, 빌드 복사, 패키징 검증을 먼저 복구해야 합니다.
