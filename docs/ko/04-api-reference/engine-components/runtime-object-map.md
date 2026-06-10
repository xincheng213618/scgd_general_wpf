# Engine 런타임 객체 맵

이 페이지는 조사할 때 쓰는 클래스 색인입니다. 로그나 호출 스택에 나온 이름이 어떤 업무 체인에 속하는지 먼저 판단합니다.

| 객체 / 클래스 | 책임 | 일반 출처 | 첫 확인점 |
| --- | --- | --- | --- |
| Startup initializers | 시작 시 Engine 기능 등록 | 앱 시작, 모듈 로드 | 실행 여부, 예외 |
| `ServiceManager` | 디바이스 서비스 모음 보관 | `Services/` | 서비스 존재, 상태 |
| `DeviceServiceFactoryRegistry` | 디바이스 서비스 조회와 생성 | 리소스 로드 | Type 에 Factory 가 있는지 |
| `DeviceService<TConfig>` | 단일 디바이스 런타임 서비스 | `Services/Devices/**` | Config, Resource ID, 연결 상태 |
| `MQTTControl` | MQTT 연결과 명령 채널 | `MQTT/`, 디바이스 서비스 | Topic, Token, 응답, 타임아웃 |
| `TemplateControl` | 템플릿 관리 진입점 | `Templates/` | 로드 여부, dictionary ID |
| `TemplateModel<T>` | 구체 템플릿 모델 | 각 템플릿 디렉터리 | `Code`, `Title`, 기본값 |
| `TemplateFlow` | Flow 템플릿 관리 | `Templates/Flow/` | `.cvflow` 저장/가져오기 |
| `FlowControl` | Flow UI 와 실행 연결 | Flow 화면 | 노드, 연결, 상태 |
| `FlowEngineControl` | Flow 실행 제어 | `FlowEngineLib/` | 시작/종료 노드, `FlowCompleted` |
| `NodeConfiguratorRegistry` | 노드 설정기 등록 | `Templates/Flow/NodeConfigurator/` | 템플릿/디바이스 선택 가능 여부 |
| `AlgResultMasterModel` | 알고리즘 주 결과 | DAO / 결과 서비스 | 결과 ID, 배치 번호, 상태 |
| `ViewResultAlg` | 결과 표시 진입점 | 결과 화면 | 상세와 이미지를 가져오는지 |
| `IResultHandleBase` | 결과 핸들러 interface | `ViewHandleXxx` | `CanHandle` 일치 |
| `IViewResult` | 시각화 결과 모델 | 각 결과 타입 | 좌표, ROI, 표시 필드 |
| `MeasureBatchModel` | 배치 데이터 | 배치 / 결과 서비스 | 배치 번호와 결과 연결 |
| `ObjectiveTestResult` | 프로젝트 최종 판정 | `Projects/*` | 고객 필드, 결과 매핑, 출력 |

## 사용 방법

1. 로그 또는 오류 메시지에서 클래스명을 가져옵니다.
2. 표에서 디바이스, 템플릿, Flow, 결과, 프로젝트 출력 중 어디인지 판단합니다.
3. 해당 전문 페이지로 이동합니다.
4. 그 다음 코드의 구체적인 메서드를 확인합니다。
