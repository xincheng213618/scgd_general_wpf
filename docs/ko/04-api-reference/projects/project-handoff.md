# 프로젝트 인수인계 매뉴얼

프로젝트 패키지는 일반 도구 플러그인이 아닙니다. 고객 검사 순서, FlowEngine 템플릿, 장치 동작, Recipe/Fix, Socket/MES, 결과 내보내기를 하나의 생산 흐름으로 묶습니다. 인수인계할 때는 단일 `Process` 클래스부터 보지 말고, 누가 시작하고, 어떤 Flow가 돌고, 어디에 결과를 쓰며, 외부로 어떻게 반환하는지 먼저 확인합니다. 릴리스와 현장 교체 증거는 [프로젝트 패키지 릴리스 증거 및 버전 확인표](./project-release-evidence.md)에 남깁니다.

## 유형

| 유형 | 프로젝트 | 중점 |
| --- | --- | --- |
| AR/VR ProcessGroup | `ProjectARVRPro/`, `ProjectLUX/` | `ProcessGroup`, `ProcessMeta`, FlowTemplate, Recipe, Socket |
| 경량/레거시 AR/VR | `ProjectARVR/`, `ProjectARVRLite/` | 호환성, 고정 순서, CSV |
| 업무 알고리즘 | `ProjectBlackMura/`, `ProjectKB/` | 파라미터, 결과 모델, 보고서 |
| 고객 전용 | `ProjectHeyuan/`, `ProjectShiyuan/` | 고객 프로토콜, 현장 설정, 장치 |
| 연동 데모 | `ProjectARVRPro.IntegrationDemo/` | 외부 JSON 송수신 |

## 공통 체인

| 단계 | 코드 진입점 | 확인점 |
| --- | --- | --- |
| 로드 | `manifest.json`, `PluginConfig/` | `Id`, `dllpath`, 메뉴, 최소 host 버전 |
| 초기화 | `InitTest()` | SN, 이전 결과 reset |
| 플로우 선택 | `ProcessManager`, `ProcessGroup` | active group, enabled steps, 순서 |
| 템플릿 | `ProcessMeta.FlowTemplate` | `TemplateFlow.Params`와 일치 |
| 실행 | `RunTemplate()`, `RunAllAsync()` | batch, 전처리, timeout, retry |
| 파싱 | `IProcess.Execute(ctx)` | Engine 결과, Recipe/Fix |
| 집계 | `ObjectiveTestResult` | customer field 채움 |
| 저장/출력 | `ViewResultManager`, exporter | SQLite, CSV/XLSX/PDF, Legacy |
| 외부 응답 | `Services/SocketControl.cs` | JSON/Text, status, final event |

## 고위험 필드

| 필드 | 위험 |
| --- | --- |
| `ProcessMeta.FlowTemplate` | 이름 불일치로 Flow가 시작되지 않음 |
| `ProcessMeta.ProcessTypeFullName` | 클래스명 변경으로 old config를 읽지 못함 |
| `ProcessMeta.IsEnabled` | 자동화와 최종 결과 completeness에 영향 |
| `ProcessMeta.SocketCode` | LUX의 `T00XX`와 대응 |
| `PictureSwitchConfig` | ARVRPro Serial 전환, timeout, delay |

## 체크리스트

| 항목 | 통과 조건 |
| --- | --- |
| manifest | `Id`, `dllpath`, `requires`, package name 일치 |
| 메뉴 | host에서 프로젝트 창을 열 수 있음 |
| ProcessGroup | 유효 group과 중요 steps 존재 |
| 템플릿 | 모든 `FlowTemplate`이 실제 존재 |
| Recipe/Fix | 열기, 저장, 재시작 후 유지 |
| 외부 프로토콜 | init, switch/run, result 확인 |
| 결과 | SQLite, CSV/XLSX/PDF, upload path 쓰기 가능 |
| 호환성 | old config, old output, old protocol 기록 |
