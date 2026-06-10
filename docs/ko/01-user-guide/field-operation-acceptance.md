# 현장 작업 검수 체크리스트

첫 납품, 업그레이드, 현장 재테스트, 운영자 교육 때 ColorVision 이 실제로 사용할 수 있는지 확인하는 체크리스트입니다. UI, 장치, Flow, 데이터, 외부 시스템, 플러그인, 프로젝트 패키지, rollback 증거를 한 곳에 묶습니다.

어디서 시작할지 모르겠다면 [사용자 작업 워크플로 매트릭스](./operation-workflow-matrix.md)부터 보세요. 실패한 항목은 연결된 주제 페이지에서 계속 확인합니다.

## 검수표

| 항목 | 최소 작업 | 통과 기준 | 실패 시 |
| --- | --- | --- | --- |
| Host 시작 | ColorVision 실행, main window 열기 | main window, menu, status bar, log entry 표시 | [메인 창](./interface/main-window.md), [로그 뷰어](./interface/log-viewer.md) |
| UI 입구 | settings, log, DB, Socket, scheduler, marketplace 열기 | 각 window 가 시작 오류 없이 열림 | [UI 구성요소 사용 설명서](./interface/ui-component-handbook.md) |
| 설정 저장 | 안전한 config 하나 변경 후 restart | 값이 유지되고 service state 정상 | [속성 편집기](./interface/property-editor.md) |
| 장치 | camera/motor/SMU/file service 확인 | device 존재, status refresh, 최소 동작 성공 | [장치 서비스 개요](./devices/overview.md) |
| 카메라 | 이미지 capture 또는 live preview | image 생성 및 열기 | [카메라 서비스](./devices/camera.md), [이미지 편집기](./image-editor/overview.md) |
| Flow design | 현장 Flow template 열기 | start node 와 key parameter 정상 | [Flow 설계](./workflow/design.md) |
| Flow run | 최소 Flow 또는 project flow 실행 | 완료 또는 first failed node 확인 | [Flow 실행과 디버깅](./workflow/execution.md) |
| Image/overlay | result image 와 ROI/POI/overlay 확인 | image, layer, coordinate 정렬 | [이미지 편집기](./image-editor/overview.md) |
| DB write | SN/time/batch 로 result 검색 | SQLite/MySQL record 존재 | [DB 작업](./data-management/database.md) |
| Export | CSV/Excel/PDF/image/project result 출력 | file 존재 및 고객 형식 일치 | [데이터 내보내기 및 가져오기](./data-management/export-import.md) |
| Socket/MES/Modbus | 현장 최소 command 전송 | 외부가 trigger 하고 status/data 수신 | [SocketProtocol](../04-api-reference/ui-components/ColorVision.SocketProtocol.md) |
| Plugin | 현장 plugin 열기, 최소 기능 실행 | menu, window, device connection, result/export 정상 | [기존 플러그인 기능](../04-api-reference/plugins/README.md) |
| Project package | project 열기, SN 으로 최소 Flow | customer result 와 response 가 project page 와 일치 | [프로젝트 설명](../00-projects/README.md) |
| Rollback | previous package/config/database backup 확인 | 이전 동작 상태로 복귀 가능 | plugin/project release evidence |

## Device / Flow / Data

| 확인 | 통과 기준 |
| --- | --- |
| Device resource | key device 가 생성되고 name/code 가 현장 실장비를 구분 |
| Communication | IP, port, serial, baud, device id, file path 가 현장과 일치 |
| Minimal action | camera capture, motor move/home, SMU read, file download/upload 가능 |
| Flow reference | Flow node 또는 project window 가 올바른 device 선택 |
| Failure location | Flow 실패 시 first failed node 와 log 식별 |
| Result review | result list, image, DB, export file 이 같은 run 을 가리킴 |

Export 가 비어 있으면 export button 을 반복하기 전에 source data 가 DB 에 있는지, 표시 중 batch 와 export target 이 같은지 확인합니다.

## External System

| 유형 | 최소 증거 |
| --- | --- |
| JSON Socket | `EventName`, SN, request JSON, response JSON, project window state |
| Text Socket | raw command 예 `T00XX,SN;`, return code, data |
| MES/serial | STX/ETX raw message, device id, return code, timeout |
| Modbus | IP, port, register, trigger value, completion write-back |
| File server | request path, file list, download/upload path |

## Handoff Record

```text
site/customer:
host version:
project package:
plugin package:
config folder:
device smoke result:
workflow smoke result:
image/overlay result:
database query result:
export file sample:
external protocol sample:
known failures:
rollback package/config:
operator trained:
owner/date:
```

## 계속 읽기

- [사용자 작업 워크플로 매트릭스](./operation-workflow-matrix.md)
- [UI 구성요소 사용 설명서](./interface/ui-component-handbook.md)
- [장치 서비스 개요](./devices/overview.md)
- [Flow 실행과 디버깅](./workflow/execution.md)
- [데이터 관리](./data-management/README.md)
- [프로젝트 설명](../00-projects/README.md)
- [기존 플러그인 기능](../04-api-reference/plugins/README.md)

