# Engine 업무 체인 매트릭스

이 페이지는 인수인계 담당자가 Engine 쪽 업무 경로를 빠르게 판단하기 위한 지도입니다. 디바이스 리소스, 템플릿, Flow, 알고리즘 결과, 프로젝트 출력 중 어디가 변경 지점인지 먼저 나눕니다.

## 메인 체인

```text
SysResourceModel / DB resource
  -> ServiceTypes / DeviceServiceFactoryRegistry
  -> DeviceService / MQTTDeviceService / MQTTControl
  -> TemplateControl / TemplateModel / TemplateFlow
  -> FlowEngineControl / Flow node / NodeConfiguratorRegistry
  -> AlgResult / ViewResult / ImageEditor Overlay
  -> Projects/* / ObjectiveTestResult / export or Socket response
```

Engine은 단순한 알고리즘 라이브러리가 아닙니다. DB 리소스, 디바이스 서비스, 템플릿 설정, Flow 노드, 결과 표시, 프로젝트 후처리를 연결하는 업무 오케스트레이션 계층입니다.

## 시나리오 매트릭스

| 업무 시나리오 | 첫 진입점 | 주요 코드 | 인수인계 확인 |
| --- | --- | --- | --- |
| 시작과 초기화 | Engine 초기화 | `ColorVision.Engine/Services/`, `MQTT/`, `Templates/` | 서비스 등록, MQTT 연결, 템플릿 로드 |
| DB 디바이스 표시 | `SysResourceModel`, `ServiceTypes` | `Services/Type/TypeService.cs`, `Services/DeviceServiceFactoryRegistry.cs` | Type, Factory, 리소스 필터 |
| 디바이스 타입 추가 | `DeviceService<TConfig>` | `Services/Devices/**`, `Services/Devices/*/MQTT*.cs` | Config, Service, 화면, MQTT, Flow 설정기 |
| 원격 제어 / MQTT | `MQTTControl`, `MQTTDeviceService` | `Services/Devices/**/MQTT*.cs`, `MQTT/` | Topic, Token, 응답 필드, 타임아웃 |
| 템플릿 파라미터 | `TemplateControl`, `TemplateModel<T>` | `Templates/Jsons/`, `Templates/ARVR/`, `Templates/POI/` | `Code`, `Title`, `TemplateDicId` 호환성 |
| Flow 편집과 실행 | `TemplateFlow`, `FlowControl` | `Templates/Flow/`, `FlowEngineLib/` | 열기, 저장, 가져오기, 실행, `FlowCompleted` |
| Flow 노드 바인딩 | `NodeConfiguratorRegistry` | `Templates/Flow/NodeConfigurator/`, `Templates/Flow/Nodes/` | 노드, 템플릿, 디바이스 타입 일치 |
| 결과 표시 | `AlgResultMasterModel`, `ViewResultAlg` | `Templates/**/ViewHandle*.cs`, `Abstractions/IResultHandlers.cs` | DAO, 결과 필드, 이미지 경로, 좌표계 |
| 이미지 오버레이 | `IViewResult`, `IResultHandleBase` | `UI/ColorVision.ImageEditor/Draw/**` | 표시와 고객 판정 책임 분리 |
| 프로젝트 출력 | `Projects/<Project>/Process` | `Projects/*/Recipe`, `Fix`, `Process`, exporter | Engine 결과 key 와 프로젝트 읽기 일치 |
| 배치와 보관 | `MeasureBatchModel` | `Services/Batch`, DAO, CSV/SQLite/MySQL | 배치 번호, 결과 ID, 보관 경로 |
| 파일 / 이미지 | `ColorVision.FileIO`, `cvColorVision` | `Engine/ColorVision.FileIO/`, `Engine/cvColorVision/` | native DLL, 포맷, CopyLocal, x64 runtime |

## 디바이스 타입

대표 타입은 Camera, PG, Spectrum, SMU, Sensor, FileServer, Algorithm, Calibration, Motor, CfwPort, FlowDevice, ThirdPartyAlgorithms 입니다. 변경할 때는 다음을 함께 확인합니다.

- `ServiceTypes` 에 타입이 있다.
- `DeviceServiceFactoryRegistry` 가 서비스를 만들 수 있다.
- `ServiceManager.DeviceServices` 에 보관된다.
- Flow 설정기에서 선택할 수 있다.
- MQTT 명령, 상태, 결과 응답을 추적할 수 있다.

## 변경 위치

| 변경 내용 | 우선 위치 | 함께 업데이트 |
| --- | --- | --- |
| 새 디바이스 타입 | `Services/Type/TypeService.cs`, `Services/Devices/` | [디바이스 서비스 체인](./device-service-chain.md) |
| 새 Flow 노드 | `FlowEngineLib/`, `Templates/Flow/Nodes/`, `Templates/Flow/NodeConfigurator/` | [템플릿 및 Flow 체인](./template-flow-chain.md) |
| 새 템플릿 | `Templates/Jsons/`, `Templates/ARVR/`, `Templates/POI/` | 노드 설정, 결과 표시, 프로젝트 읽기 |
| 새 결과 표시 | `Templates/**/ViewHandle*.cs`, `UI/ColorVision.ImageEditor/Draw/` | [결과 표시 및 프로젝트 인수인계 체인](./result-handoff-chain.md) |
| 고객 필드 / 판정 | `Projects/<Project>/Recipe`, `Fix`, `Process` | 프로젝트 설명과 출력 형식 |

## 점검 순서

1. DB 리소스, 템플릿, 배치, 결과 ID 가 있는지 확인합니다.
2. 서비스 생성과 MQTT 명령/응답을 확인합니다.
3. Flow 노드가 올바른 템플릿과 디바이스에 묶였는지 확인합니다.
4. Engine 이 `AlgResultMasterModel` 과 상세 결과를 만들었는지 확인합니다.
5. 마지막으로 UI 오버레이와 `Projects/*` 의 판정, 출력, Socket 응답을 확인합니다.
