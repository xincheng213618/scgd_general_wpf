# Engine 서비스 개발 인수인계

이 문서는 `Engine/ColorVision.Engine/Services/`의 현재 서비스 개발 모델을 설명합니다. 여기서 서비스는 일반 DI 서비스가 아니라 호스트 런타임에 표시되고, 설정을 가지며, 필요하면 MQTT 명령을 보내는 장치 또는 업무 서비스입니다.

먼저 [Engine 장치 서비스 체인](../../04-api-reference/engine-components/device-service-chain.md)을 읽고 이 문서로 코드 위치를 확인하세요.

## 런타임 체인

| 단계 | 핵심 객체 | 설명 |
| --- | --- | --- |
| 서비스 유형 | `ServiceTypes` | Camera, PG, Spectrum, SMU, Sensor, FileServer, Algorithm, FilterWheel, Calibration, Motor, Flow, ThirdPartyAlgorithms 등 |
| 설정 | `SysResourceModel.Value` | 장치 설정 JSON. `DeviceService<T>`가 구체 `Config*`로 복원합니다 |
| 생성 | `DeviceServiceFactoryRegistry` | `SysResourceModel.Type`에 따라 구체 `Device*`를 생성합니다 |
| 런타임 목록 | `ServiceManager.GetInstance().DeviceServices` | 호스트에 로드된 장치 서비스 |
| UI | `GetDeviceInfo()`, `GetDisplayControl()` | 정보 패널, 제어 패널, 장치 트리 |
| 명령 | `GetMQTTService()`, `MQTTDeviceService<T>` | 명령 송신, 응답, 타임아웃, 메시지 기록 |

일반 로딩 흐름은 `SysResourceModel` 저장, `ServiceManager.Load()`, `DeviceServiceFactoryRegistry.CreateService()`, `DeviceService<T>` 설정 복원, 구체 `Device*`가 `MQTT*`와 UI를 만드는 순서입니다.

## 기본 등록

| 유형 | 디렉터리 | Device | MQTT | 역할 |
| --- | --- | --- | --- | --- |
| Camera | `Services/Devices/Camera/` | `DeviceCamera` | `MQTTCamera` | 카메라, 라이브, 촬영, 노출, 보정 |
| PG | `Services/Devices/PG/` | `DevicePG` | `MQTTPG` | 패턴 생성기 전환과 프로젝트 연동 |
| Spectrum | `Services/Devices/Spectrum/` | `DeviceSpectrum` | `MQTTSpectrum` | 분광기 연결, 다크 저장, 측정 |
| SMU | `Services/Devices/SMU/` | `DeviceSMU` | `MQTTSMU` | SMU, 스캔, 결과 읽기 |
| Sensor | `Services/Devices/Sensor/` | `DeviceSensor` | `MQTTSensor` | 센서 통신과 명령 템플릿 |
| FileServer | `Services/Devices/FileServer/` | `DeviceFileServer` | `MQTTFileServer` | 파일 경로, 다운로드, 캐시 |
| Algorithm | `Services/Devices/Algorithm/` | `DeviceAlgorithm` | `MQTTAlgorithm` | 알고리즘 서비스와 결과 조회 |
| FilterWheel | `Services/Devices/CfwPort/` | `DeviceCfwPort` | `MQTTCfwPort` | 필터휠 제어 |
| Calibration | `Services/Devices/Calibration/` | `DeviceCalibration` | `MQTTCalibration` | 보정 명령, 파일, 결과 |
| Motor | `Services/Devices/Motor/` | `DeviceMotor` | `MQTTMotor` | 원점 복귀, 이동, 위치 읽기 |
| Flow | `Services/Devices/FlowDevice/` | `DeviceFlowDevice` | `MQTTFlowDevice` | Flow 장치 서비스 |
| ThirdPartyAlgorithms | `Services/Devices/ThirdPartyAlgorithms/` | `DeviceThirdPartyAlgorithms` | `MQTTThirdPartyAlgorithms` | 외부 알고리즘 연동 |

## 새 서비스 추가

1. 기존 `ServiceTypes`를 재사용할 수 있는지 확인합니다.
2. `Config* : DeviceServiceConfig`를 추가하고 이전 JSON 복원을 깨지 않게 합니다.
3. `Device* : DeviceService<Config*>`를 추가하고 생성자에서 `DService = new MQTT*(Config)`를 만듭니다.
4. `GetDeviceInfo()`를 구현하고 필요하면 `GetDisplayControl()`과 `GetMQTTService()`도 구현합니다.
5. `MQTT* : MQTTDeviceService<Config*>`를 추가합니다. 고객 판정은 여기에 쓰지 않습니다.
6. `DeviceServiceFactoryRegistry.RegisterDefaults()`에 등록합니다.
7. UI, MQTT, Flow 또는 프로젝트 패키지에서 올바르게 선택되는지 검증합니다.

## 인수인계 검증

- 장치 트리에 서비스가 표시되고 재시작 후 복원됩니다.
- 설정 내보내기, 가져오기, 초기화, 저장이 동작합니다.
- `SendTopic` / `SubscribeTopic` 및 `MsgID` 응답 매칭이 정확합니다.
- 정보 패널과 표시 패널이 예외 없이 열립니다.
- 의존 Flow 노드 또는 프로젝트가 올바른 장치를 선택합니다.

## 관련 문서

- [Engine 장치 서비스 체인](../../04-api-reference/engine-components/device-service-chain.md)
- [MQTT 메시지 처리](./mqtt.md)
- [Engine 런타임 객체 맵](../../04-api-reference/engine-components/runtime-object-map.md)
- [테스트 및 검증 인수인계](../testing.md)
