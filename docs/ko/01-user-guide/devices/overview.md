# 장치 서비스 개요

이 페이지는 장치 장의 입구입니다. 어떤 장치 문서를 봐야 하는지, 보통 어떤 순서로 설정하는지, 문제가 생겼을 때 어디부터 확인할지 정리합니다.

## 장치 서비스란

ColorVision 에서 장치는 보통 "service" 로 관리됩니다. 메인 프로그램은 장치 서비스 목록을 유지하고, 사용자는 장치 창에서 표시, 설정, 저장, 동작 확인을 합니다.

장치 관련 구현은 주로 다음 위치에 있습니다.

- `Engine/ColorVision.Engine/Services/`
- `Engine/ColorVision.Engine/Services/Devices/`

현재 코드에서 볼 수 있는 대표 분류는 다음과 같습니다.

- Camera
- Calibration
- Motor
- FileServer
- FlowDevice
- Sensor
- SMU
- Spectrum

## 이 장을 읽는 방법

### 공통 입구

- [장치 추가 및 설정](./configuration.md)

### 개별 장치

- [카메라 서비스](./camera.md)
- [카메라 관리](./camera-management.md)
- [카메라 파라미터 설정](./camera-configuration.md)
- [교정 서비스](./calibration.md)
- [모터 서비스](./motor.md)
- [SMU 서비스](./smu.md)
- [Flow 장치 서비스](./flow-device.md)
- [파일 서버](./file-server.md)

## 일반적인 사용 순서

1. 먼저 [장치 추가 및 설정](./configuration.md) 에서 추가와 저장의 기본 절차를 확인합니다.
2. 다음으로 개별 장치 페이지에서 해당 장치의 파라미터와 동작을 확인합니다.
3. 카메라가 포함되면 [카메라 관리](./camera-management.md) 와 [카메라 파라미터 설정](./camera-configuration.md) 도 확인합니다.
4. 장치를 자동화 Flow 에 포함해야 하면 [워크플로 개요](../workflow/README.md) 로 이동합니다.

## 사용 중 자주 만나는 상황

- 장치 서비스는 실제 하드웨어에 연결될 수도 있고, 통신 또는 파일 처리 서비스일 수도 있습니다.
- 장치 목록 순서, enabled 상태, 설정 내용은 이후 창과 Flow 에서 보이는 방식에 영향을 줍니다.
- 일부 장치는 기본 설정 외에 물리 장치 관리, 교정, 파라미터 설정 창을 따로 가집니다.

## 장치 인수인계에 기록할 것

현장에서 "설정 완료" 만 남기면 다음 담당자가 복구하기 어렵습니다. 식별, 연결, 최소 동작, Flow 참조를 남깁니다.

| 기록 항목 | 적을 내용 | 목적 |
| --- | --- | --- |
| 장치 식별 | name, Code, device type, 대응 실장비 또는 service | Flow 가 잘못된 장치를 참조하지 않게 함 |
| 통신 설정 | IP, port, serial, baud, MQTT topic, file path | 현장 연결 조건을 재현 |
| 최소 동작 | camera capture, motor home, SMU point measure, file list | 목록에만 있는 것이 아니라 동작함을 증명 |
| Flow 참조 | 어떤 Flow, node, project window 가 사용하는지 | 수동은 되지만 Flow 가 실패하는 경우의 시작점 |
| 로그 증거 | 연결 성공 시각, error, timeout, permission issue | 원격 조사 시작점 |
| rollback | 이전 config, driver, firmware, project package | 업그레이드 후 복구에 사용 |

## 현장 최소 검수

| 단계 | 작업 | 통과 기준 |
| --- | --- | --- |
| 1 | 장치 목록을 열고 refresh | 대상 장치가 보이고 name/Code 가 기록과 일치 |
| 2 | 상세 또는 전용 창 열기 | key parameter 와 저장 상태 확인 가능 |
| 3 | 안전한 최소 동작 실행 | response, log, state change 를 설명 가능 |
| 4 | 의존 Flow 또는 project window 열기 | 같은 장치를 선택 가능 |
| 5 | 최소 Flow 또는 simulated Flow 실행 | 장치 node 까지 도달하고 결과 또는 실패 원인을 추적 가능 |

3단계가 실패하면 장치 계층부터 봅니다. 3단계는 통과하지만 5단계가 실패하면 Flow 참조, template parameter, project mapping 을 우선 확인합니다.

## 장치 문제 분리

| 현상 | 먼저 확인 | 다음 확인 |
| --- | --- | --- |
| 목록에 없음 | 생성/저장 여부, type 정확성 | plugin/project package 가 장치 입구를 제공하는지 |
| 있지만 열리지 않음 | hardware online, driver, port/IP, permission | log 의 connection error 와 timeout |
| 수동은 성공, Flow 는 실패 | Flow node 가 참조하는 device Code | node parameter, template version, project window selection |
| 결과는 있지만 이미지/데이터가 다름 | 장치 parameter 와 capture condition | downstream template, export field, database record |
| 업그레이드 후만 이상 | config 가 덮였는지 | old package/config 와 new DLL 혼용 여부 |

## 문제 해결

### 장치가 목록에 나타나지 않음

- 장치 설정 창에서 생성하고 저장했는지 확인합니다.
- 실장비, driver, 통신 환경 같은 의존 조건을 확인합니다.

### 장치는 있지만 동작하지 않음

- 먼저 개별 장치 페이지의 파라미터를 확인합니다.
- 다음으로 log 와 connection status 를 확인합니다.
- Flow 호출에서만 실패하면 [Flow 실행과 디버깅](../workflow/execution.md) 도 확인합니다.

## 설명

- 이 페이지는 입구와 사용 경로를 정리하며, 장치 서비스 코드 분석은 다루지 않습니다.
- 구현 상세는 `Engine/ColorVision.Engine/Services/` 아래 실제 코드를 기준으로 합니다.
- 현장 납품 때는 [현장 작업 검수 체크리스트](../field-operation-acceptance.md) 에 전체 결과도 기록합니다.
