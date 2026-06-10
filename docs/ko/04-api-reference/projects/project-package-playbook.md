# 프로젝트 실행 및 인수인계 플레이북

이 페이지는 고객 프로젝트의 현장 문제, 프로토콜, 결과 출력, `.cvxp` 납품을 처리하는 유지보수자를 위한 문서입니다. 개별 프로젝트 페이지를 대체하지 않고, 증상에서 조사 진입점을 고르는 용도입니다. 릴리스, 현장 교체, rollback 때는 [프로젝트 패키지 릴리스 증거 및 버전 확인표](./project-release-evidence.md)도 작성합니다.

## 시나리오 진입점

| 문제 | 먼저 확인 | 대표 프로젝트 |
| --- | --- | --- |
| 메뉴나 창이 열리지 않음 | A | 전체 프로젝트 |
| 외부 명령으로 검사가 시작되지 않음 | B | ARVR, ARVRLite, ARVRPro, LUX, KB, BlackMura, Heyuan |
| Flow는 시작되지만 항목이나 템플릿이 틀림 | C | ARVRPro, LUX, KB, Shiyuan |
| 결과는 있지만 PASS/FAIL 또는 고객 필드가 틀림 | D | ARVRPro, LUX, KB, BlackMura, Heyuan |
| CSV/XLSX/PDF/SQLite/MES가 누락됨 | E | ARVR 계열, LUX, KB, BlackMura, Heyuan, Shiyuan |
| Serial, Modbus, MES, PG, 이미지 전환 이상 | F | BlackMura, Heyuan, KB, ARVRPro |
| `.cvxp` 납품 | G | IntegrationDemo 제외 |
| 고객에게 연동 Demo만 필요 | H | IntegrationDemo |

## 공통 실행 모델

```text
manifest / PluginConfig
  -> project window
  -> external command or manual start
  -> active ProcessGroup / fixed workflow
  -> FlowTemplate
  -> Engine Flow
  -> IProcess reads result and applies Recipe/Fix
  -> ObjectiveTestResult
  -> SQLite / CSV / XLSX / PDF / MES / Socket
```

Exporter나 CSV만 보지 마세요. 많은 문제는 명령 불일치, active group, `FlowTemplate` 이름, Recipe/Fix 로딩, `IProcess.Execute()` 미실행에서 발생합니다.

## 조사 체크

| 시나리오 | 체크 항목 |
| --- | --- |
| A | `Plugins/<ProjectName>/`, `manifest.json`, `PluginConfig/`, `Menu*.cs`, host log |
| B | Socket/Serial/Modbus 서비스, 창 존재, SN, 상태, ProcessGroup |
| C | `ProcessGroups.json`, `ProcessMeta.FlowTemplate`, `ProcessTypeFullName`, `SocketCode` |
| D | Flow 완료, `IProcess.Execute()`, Recipe/Fix, `ObjectiveTestResult` |
| E | local result, Legacy switch, file output, Socket/MES, SQLite |
| F | raw command, return code, timeout, DeviceId, register, expected response |
| G | manifest, README/CHANGELOG, config, external DLL, output path, rollback |
| H | sample JSON, online connect, partial/sticky packet, CSV export |

## 인수인계 기록

매 인수인계마다 프로젝트, 고객/현장, 버전, 빌드 명령, 프로토콜, ProcessGroup, Recipe/Fix, 출력, 검수 결과, rollback, known limits를 남깁니다.

## 계속 읽기

- [프로젝트 기능 및 인수인계 매트릭스](./project-capability-matrix.md)
- [프로젝트 패키지 릴리스 증거 및 버전 확인표](./project-release-evidence.md)
- [프로젝트 인수인계 매뉴얼](./project-handoff.md)
