# Flow 실행과 디버깅

이 페이지는 현재 구현에서 확인할 수 있는 Flow 실행 입구와 문제 분리 절차를 다룹니다. 핵심은 고급 디버깅 용어가 아니라 올바른 Flow template 선택, service online 상태, start node 존재 여부, 실패 시 어디부터 좁힐지입니다.

## 현재 사용할 수 있는 실행 입구

현재 구현에서 명확한 동작은 다음과 같습니다.

- 실행 시작: `F6`
- 실행 중지: `F7`

이전 문서에 `F5`, `F10`, breakpoint, single step 이 있더라도 현재 UI 와 code binding 을 우선합니다.

## 실행 전 확인할 것

### 유효한 Flow template 선택

유효한 template 이 선택되지 않으면 실행은 시작되지 않습니다. Flow window 를 열었다는 것만으로 충분하지 않고, dropdown 에서 실행할 template 이 선택되어야 합니다.

### start node 존재

실행 전 start node 를 확인합니다. start node 가 없는 Flow 는 실제 실행 단계에 들어가기 전에 실패합니다.

### registry center 와 service token 사용 가능

구현은 registry center 연결과 service list 를 먼저 확인합니다. service token 이 비어 있으면 service refresh 후 다시 시도합니다.

### preprocessing 통과

Flow 시작 전 preprocessing 단계가 있습니다. 여기서 실패하면 Flow 는 cancel 되고 다음 node 로 진행하지 않습니다.

## 일반적인 실행 순서

1. [Flow 설계](./design.md) 에서 template 내용과 start node 를 확인합니다.
2. 실행 화면에서 같은 template 을 선택합니다.
3. 관련 device service 가 online 인지 확인합니다.
4. `F6` 또는 실행 버튼으로 시작합니다.
5. 실행 중에는 log area, current node, progress 를 관찰합니다.
6. 중지가 필요하면 `F7` 또는 stop 동작을 사용합니다.

## 실행 중 볼 것

### current running node

실행 중에는 현재 동작 중인 node 이름이 표시됩니다. 멈춘 것처럼 보이면 먼저 어떤 node 에서 멈췄는지 봅니다.

### progress 와 duration

현재 구현은 duration, last run time, 이전 duration 기반 progress estimate 를 기록합니다. 이는 대략적인 단계 확인용이며 엄격한 업무 완료 판정이 아닙니다.

### result 와 status

완료 후 batch status, duration, result summary 가 기록됩니다. 중지한 경우 canceled 로 기록됩니다.

## 디버깅 시 문제 분리

### 처음 실패한 node 찾기

"Flow 전체 실패" 로 보지 말고, 처음 빨간색이 되거나 진행하지 않는 node 를 찾은 뒤 device, template, input data 로 돌아갑니다.

### 시작 전 실패와 중간 실패 구분

- 시작 전 중단: template not selected, start node missing, registry center disconnected, service token empty, preprocessing failed
- 시작 후 중단: node error, timeout, message mismatch

### log 우선 확인

실패 시 마지막 node, preprocessing failed, canceled, status message 여부를 보고 어느 계층으로 돌아갈지 결정합니다.

## Flow 인수인계에 기록할 것

Flow 인수인계는 파일 하나를 넘기는 것으로 충분하지 않습니다. device, template, input, result, external system 관계를 남깁니다.

| 기록 항목 | 적을 내용 | 목적 |
| --- | --- | --- |
| Flow template | name, version, import source, last editor | 이전 Flow 실행 방지 |
| start condition | start node, SN/batch input, project window, external trigger | 시작하지 않는 원인 판단 |
| device dependency | camera, motor, SMU, file service binding | device layer failure 분리 |
| template dependency | image template, calibration template, threshold | result drift 설명 |
| data destination | database table, export file, image folder, Socket/MES response | 결과 위치 확인 |
| failure evidence | first failed node, log timestamp, error message | 다음 담당자가 재현 가능 |

## 최소 재테스트 절차

현장 재테스트나 upgrade 후에는 full production chain 부터 돌리지 말고 최소 Flow 로 확인합니다.

1. Flow design 을 열고 template 과 start node 를 확인합니다.
2. execution 화면에서 같은 template 을 선택합니다.
3. 관련 device service 가 online 인지 확인하고, 필요하면 device smoke action 을 수행합니다.
4. production 에 영향을 주지 않는 SN, image, test input 을 준비합니다.
5. `F6` 으로 실행하고 start time, current node, final state, duration 을 기록합니다.
6. log, image, database, export file, external response 중 하나 이상에서 같은 run 을 확인합니다.
7. 중지할 경우 `F7` 후 canceled 로 기록되었는지 확인합니다.

## 실패 분리 표

| 실패 위치 | 대표 증상 | 우선 확인 |
| --- | --- | --- |
| 실행 전 | run 이 시작되지 않음, service refresh prompt, start node missing | registry center, service list, Flow template, start node |
| preprocessing | 즉시 cancel, preprocessing failed | input parameter, template validity, project window context |
| device node | timeout, device no response, abnormal return code | device page, hardware, device Code, MQTT/serial/IP |
| template node | 완료되지만 결과가 다름 | template version, threshold, image source, calibration data |
| data node | Flow 완료 후 결과가 보이지 않음 | database write, batch/SN, export target, permission |
| external system | ColorVision 은 완료되지만 MES/Socket 에 도착하지 않음 | protocol, port, project handler, response field |

## FAQ

### Run 했지만 시작하지 않음

- registry center 가 connected 인지 확인합니다.
- service list 가 refresh 되었는지 확인합니다.
- 유효한 Flow template 이 선택되었는지 확인합니다.
- start node 가 존재하는지 확인합니다.

### 시작 후 바로 중지

- preprocessing failed 인지 확인합니다.
- log 의 마지막 node 와 status 를 확인합니다.
- device 관련 node 라면 해당 device page 에서 connection 과 config 를 확인합니다.

### progress 가 움직이지 않음

- current running node 를 확인합니다.
- device 또는 message response 를 기다리는지 판단합니다.
- 필요하면 `F7` 로 중지하고 해당 node 가 의존하는 device service 를 단독 확인합니다.

### 수동은 성공하지만 Flow 에서는 실패

- Flow 가 같은 device 와 template 을 참조하는지 확인합니다.
- 실행 전 환경이 수동 테스트와 같은지 확인합니다.
- 필요하면 [장치 서비스 개요](../devices/overview.md) 와 [로그 뷰어](../interface/log-viewer.md) 를 확인합니다.

## 계속 읽기

- [Flow 설계](./design.md)
- [장치 서비스 개요](../devices/overview.md)
- [로그 뷰어](../interface/log-viewer.md)
- [데이터 관리](../data-management/README.md)
- [현장 작업 검수 체크리스트](../field-operation-acceptance.md)

## 설명

- 이 페이지는 현재 확인 가능한 실행 입구와 문제 분리 절차만 다룹니다.
- 관련 구현은 주로 `Engine/ColorVision.Engine/Templates/Flow/DisplayFlow.xaml.cs`, `ViewFlow.xaml.cs`, `FlowControl.cs`, `EngineCommands.cs` 에 있습니다.
