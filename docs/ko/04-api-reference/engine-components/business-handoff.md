# Engine 업무 인수인계 매뉴얼

이 매뉴얼은 Engine 의 주요 업무 로직을 인수인계하기 위한 문서입니다. 읽은 뒤에는 디바이스가 어디서 만들어지고, 템플릿이 어떻게 Flow 로 들어가며, 알고리즘 결과가 어떻게 표시되고, 프로젝트가 최종 결과를 어떻게 가져가는지 설명할 수 있어야 합니다.

## Engine 한 줄 설명

`ColorVision.Engine` 은 업무 오케스트레이션 계층입니다. 단일 알고리즘 DLL 도 아니고 단일 디바이스 드라이버도 아닙니다. DB 리소스, 디바이스 서비스, MQTT, 템플릿, Flow, 알고리즘 결과, 프로젝트 납품을 연결합니다.

## 주요 디렉터리

| 디렉터리 | 책임 |
| --- | --- |
| `Engine/ColorVision.Engine/Services/` | 디바이스 서비스, 리소스 매핑, MQTT, 배치, 결과 서비스 |
| `Engine/ColorVision.Engine/Templates/` | 템플릿 관리, Flow, 알고리즘 템플릿, 결과 표시 |
| `Engine/FlowEngineLib/` | Flow 노드 모델, 시작/종료 노드, 실행 제어 |
| `Engine/ColorVision.FileIO/` | CVRAW, CVCIE 등 파일 I/O |
| `Engine/cvColorVision/` | OpenCV 와 native 기능 래핑 |
| `Projects/*` | 고객 규칙, Recipe, Fix, 출력, Socket/MES |

## 디바이스 서비스

DB 리소스는 `SysResourceModel` 로 읽히고 `ServiceTypes` 로 Factory 를 찾습니다. Factory 는 `DeviceService<TConfig>` 를 만들고 `ServiceManager` 가 관리합니다. 원격 제어가 필요한 디바이스는 `MQTTDeviceService` 또는 `MQTTControl` 에 연결됩니다.

디바이스 추가 시 Type, Config, Service, Factory, 표시 화면, MQTT, Flow 노드 설정기를 모두 확인합니다. 일부만 구현하면 UI 에는 보이지만 Flow 에서 선택할 수 없거나, Flow 에서는 선택되지만 명령이 나가지 않는 상태가 됩니다.

## 템플릿과 Flow

템플릿 진입점은 `TemplateControl` 과 `TemplateModel<T>` 입니다. Flow 템플릿은 `TemplateFlow` 가 관리하고, 실행 시 `FlowEngineControl`, `FlowControl`, 각 노드로 이어집니다. 노드 표시와 파라미터 설정은 주로 `Templates/Flow/NodeConfigurator/` 에 있습니다.

인수인계 시 템플릿 ID, Code, 노드 타입, 디바이스 타입, 결과 key 를 확인합니다. 이 값이 어긋나면 Flow 는 돌지만 결과가 맞지 않는 문제가 자주 생깁니다.

## 결과 표시와 납품

결과는 `AlgResultMasterModel` 과 상세 결과에 저장되고 `ViewResultAlg`, `IResultHandleBase`, `IViewResult` 를 지나 표시 모델이 됩니다. 이미지 오버레이는 `UI/ColorVision.ImageEditor/Draw/` 에 있습니다.

프로젝트는 Engine 결과를 읽고 고객 판정, 보정, `ObjectiveTestResult`, CSV, DB, Socket, MES 형식으로 변환합니다. Engine 이 만들어야 할 결과를 프로젝트 쪽에서 임시로 꾸며내지 않도록 합니다.

## 변경 위치

| 요구사항 | 우선 변경 위치 |
| --- | --- |
| 디바이스 추가 | `Services/Devices/`, `Services/Type/TypeService.cs` |
| 디바이스 명령 추가 | `Services/Devices/*/MQTT*.cs`, `MQTT/` |
| 템플릿 필드 추가 | `Templates/**`, 해당 템플릿 모델 |
| Flow 노드 추가 | `FlowEngineLib/`, `Templates/Flow/Nodes/`, `NodeConfigurator/` |
| 결과 레이어 추가 | `Templates/**/ViewHandle*.cs`, `UI/ColorVision.ImageEditor/Draw/` |
| 고객 판정 / 출력 | `Projects/<Project>/Recipe`, `Fix`, `Process`, exporter |

## 점검 절차

1. 리소스, 템플릿, 배치, 결과 ID 가 있는지 확인합니다.
2. Engine 로그와 MQTT 로그를 봅니다.
3. `ServiceManager` 에 서비스가 있는지 확인합니다.
4. Flow 노드가 올바른 디바이스와 템플릿을 선택했는지 확인합니다.
5. DAO 에 주 결과와 상세 결과가 있는지 확인합니다.
6. ViewHandle 이 선택되는지 확인합니다.
7. 마지막으로 프로젝트의 필드 매핑과 출력을 확인합니다.

## 유지보수 규칙

Engine 업무 체인을 바꾸면 이 장도 함께 업데이트합니다. 디바이스는 [디바이스 서비스 체인](./device-service-chain.md), Flow 나 템플릿은 [템플릿 및 Flow 체인](./template-flow-chain.md), 결과나 고객 필드는 [결과 표시 및 프로젝트 인수인계 체인](./result-handoff-chain.md) 에 반영합니다.
