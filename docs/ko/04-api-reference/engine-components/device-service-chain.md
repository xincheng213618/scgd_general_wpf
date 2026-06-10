# Engine 디바이스 서비스 체인

디바이스 서비스 체인은 DB 리소스를 표시 가능하고, 제어 가능하고, Flow 에서 사용할 수 있는 런타임 서비스로 바꿉니다.

## 체인 개요

```text
SysDictionaryModel / SysResourceModel
  -> ServiceTypes
  -> DeviceServiceFactoryRegistry
  -> DeviceService<TConfig>
  -> display control / MQTTDeviceService / Flow NodeConfigurator
```

## 주요 클래스와 디렉터리

| 위치 | 역할 |
| --- | --- |
| `Services/ServiceManager.cs` | 만들어진 디바이스 서비스 보관 |
| `Services/Type/TypeService.cs` | 리소스 Type 과 서비스 타입 매핑 |
| `Services/DeviceService.cs` | 디바이스 서비스 기반 |
| `Services/DeviceServiceFactory.cs` | 서비스 생성 Factory |
| `Services/DeviceServiceFactoryRegistry.cs` | Factory 등록과 조회 |
| `Services/MQTTDeviceService.cs` | MQTT 명령과 상태 기반 |
| `Services/Devices/**` | 각 디바이스의 Service, Config, MQTT 구현 |

## 디바이스 추가 체크리스트

1. `ServiceTypes` 를 추가하거나 확인합니다.
2. `DeviceServiceConfig` 를 상속하는 설정 클래스를 추가합니다.
3. `DeviceService<TConfig>` 를 상속하는 서비스를 추가합니다.
4. `SysResourceModel.Type` 에서 만들 수 있도록 Factory 를 등록합니다.
5. UI 표시와 설정 진입점을 추가합니다.
6. MQTT 명령, 상태 응답, 타임아웃 로그를 추가합니다.
7. Flow 에서 쓰면 `NodeConfigurator` 선택 조건을 추가합니다.
8. 문서, 테스트 절차, 프로젝트 사용 설명을 업데이트합니다.

## 자주 나는 문제

| 현상 | 우선 확인 |
| --- | --- |
| DB 에 리소스가 있지만 UI 에 안 보임 | Type 매핑, Factory 등록, 서비스 생성 |
| UI 에는 보이지만 Flow 에서 선택 불가 | `NodeConfigurator` 타입 필터 |
| Flow 에서 선택되지만 명령 실패 | MQTT Topic, Token, 명령 형식, 온라인 상태 |
| 상태가 갱신되지 않음 | 상태 이벤트, MQTT 응답, UI 바인딩 |
| 여러 대가 섞임 | Resource ID, Code, Name, 서비스 dictionary key |

## 경계

디바이스 서비스는 디바이스 능력을 시스템에 공개합니다. 템플릿은 언제 쓸지, Flow 는 어떻게 엮을지, 결과 표시는 어떻게 보여줄지, 프로젝트는 고객에게 어떻게 전달할지를 담당합니다.
