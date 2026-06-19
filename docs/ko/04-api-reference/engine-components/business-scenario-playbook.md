# Engine 업무 시나리오 인수인계 플레이북

이 페이지는 자주 나오는 요구사항과 장애 설명에서 Engine 의 관련 코드로 이동하기 위한 절차입니다. 먼저 세 가지를 정합니다.

1. 진입점이 UI, Flow, 프로젝트, Socket/MES, 스케줄러, MQTT 중 무엇인지.
2. 소속 계층이 리소스/디바이스, 템플릿/Flow, 원격 서비스, 결과 표시, 프로젝트 매핑, 출력 중 무엇인지.
3. 증거가 로그, 리소스 ID, 템플릿 ID, 배치 번호, 결과 ID, 응답 패킷 중 무엇인지.

## A. DB 에 디바이스가 있지만 UI 또는 Flow 에 보이지 않음

먼저 `SysResourceModel` 이 존재하고 Type 이 깨지지 않았는지 확인합니다. 다음으로 `Services/Type/TypeService.cs` 가 Type 을 올바른 `ServiceTypes` 로 매핑하는지 봅니다. 그 다음 `DeviceServiceFactoryRegistry` 에 Factory 가 있고 `ServiceManager.DeviceServices` 에 서비스가 만들어졌는지 확인합니다.

Flow 쪽은 `Templates/Flow/NodeConfigurator/` 의 타입 필터도 확인합니다. “디바이스가 없다”는 문제는 서비스 생성이 아니라 설정기 필터가 원인일 수 있습니다.

## B. 새 디바이스 추가

최소 체크리스트:

- `ServiceTypes` 에 타입을 추가합니다.
- `ConfigXxx : DeviceServiceConfig` 를 추가합니다.
- `DeviceXxx : DeviceService<ConfigXxx>` 를 추가합니다.
- Factory 등록을 추가합니다.
- 표시 컨트롤과 설정 진입점을 추가합니다.
- Flow 에서 쓰려면 `NodeConfigurator` 와 선택 조건을 추가합니다.
- 원격 제어가 필요하면 `MQTTDeviceService`, Topic, 응답, 타임아웃 로그를 추가합니다.
- 이 장과 프로젝트 사용 설명을 업데이트합니다.

## C. 템플릿 파라미터 변경

대상이 JSON, POI, Flow, 디바이스 액션 템플릿 중 무엇인지 먼저 판단합니다. 진입점은 `Templates/Jsons/`, `Templates/POI/`, `Templates/Flow/`, 각 디바이스 서비스 아래에 있습니다.

`Code`, `Title`, `TemplateDicId`, 기본값, 속성 설명, 기존 데이터 역직렬화를 깨지 않는 것이 중요합니다. Flow 노드가 사용하는 값이면 노드 설정기와 결과 표시도 함께 업데이트합니다.

## D. Flow 노드 추가 또는 변경

Flow 는 두 계층으로 봅니다. `FlowEngineLib/` 는 실행 모델이고, `ColorVision.Engine/Templates/Flow/` 는 메인 앱의 템플릿, 설정기, 화면 연결입니다.

수락 확인:

- 기존 Flow 열기.
- 노드 추가 후 저장.
- 닫은 뒤 다시 열기.
- `.cvflow` 가져오기.
- 실행 후 `FlowCompleted` 확인.
- 배치, 결과 표시, 프로젝트에서 결과를 읽을 수 있는지 확인.

## E. 결과는 있지만 이미지 표시가 없음

먼저 `AlgResultMasterModel` 과 상세 DAO 가 있는지 확인합니다. 다음으로 `ViewResultAlg` 가 표시 모델을 만들 수 있는지, `DisplayAlgorithmManager` 가 올바른 `ViewHandleXxx` 를 선택했는지, `IResultHandleBase.CanHandle` 이 맞는지 봅니다.

모델이 맞다면 이미지 경로, 좌표계, ROI, 배율, `UI/ColorVision.ImageEditor/Draw/` 의 그리기 객체를 확인합니다. 고객 판정은 오버레이가 아니라 프로젝트 쪽에 둡니다.

## F. 프로젝트 결과가 비어 있거나 필드가 다름

Engine 결과가 존재하는지 먼저 확인합니다. 그 후 `Projects/<Project>/Process` 가 읽는 key, `Recipe`, `Fix`, `ObjectiveTestResult`, exporter, Socket/MES 응답 필드를 확인합니다.

## G. 원격 서비스에 결과가 없음

MQTT 연결, Topic, 서비스 Token, 명령 순서, FileServer 경로, 결과 ID, DAO 저장, Flow 상태 순서로 확인합니다. 코드는 `Services/Devices/*/MQTT*.cs` 와 `MQTT/` 를 우선 봅니다.

## H. Socket/MES 응답이 다름

공통 `SocketProtocol` 인지 `Projects/<Project>/SocketControl` 의 고객 프로토콜인지 분리합니다. EventName, SN, 템플릿, Flow, 결과 ID, 응답 필드, 오류 코드를 확인합니다.

## 클래스 빠른 표

| 목적 | 먼저 볼 것 |
| --- | --- |
| 디바이스 생성 | `ServiceManager`, `DeviceServiceFactoryRegistry` |
| 템플릿 로드 | `TemplateControl`, `TemplateModel<T>` |
| Flow 실행 | `TemplateFlow`, `FlowControl`, `NodeConfiguratorRegistry` |
| 결과 표시 | `ViewResultAlg`, `IResultHandleBase`, `IViewResult` |
| 프로젝트 판정 | `ObjectiveTestResult`, `Projects/<Project>/Process` |
