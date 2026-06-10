# 사용자 작업 워크플로 매트릭스

이 페이지는 운영자, 테스트 엔지니어, 현장 납품 담당자가 “무엇을 해야 하는가”에서 문서 입구를 찾기 위한 문서입니다. 소스 구현 대신 일상 작업, 완료 기준, 실패 시 첫 확인 지점을 정리합니다.

## 먼저 읽을 상황

| 상황 | 이 페이지가 돕는 것 |
| --- | --- |
| 첫 장비 세팅 | 설치, 시작, 메인 창, 장치, Flow, 데이터 확인 순서 |
| 현장 납품 | 프로젝트, 장치, Flow, 출력, 외부 시스템 검수 |
| 생산 작업 | 실행, 프로젝트 전환, 결과 확인, 데이터 내보내기 입구 |
| 문제 해결 | UI, 장치, Flow, 데이터, 외부 시스템 중 어디부터 볼지 판단 |

## 작업 목표별 입구

| 작업 목표 | 먼저 읽기 | 핵심 작업 | 완료 기준 | 실패 시 첫 확인 |
| --- | --- | --- | --- | --- |
| 설치와 첫 실행 | [설치 가이드](../00-getting-started/installation.md), [첫 실행](../00-getting-started/first-steps.md) | 환경 설치, host 실행, 설정/로그 확인 | 메인 창이 열리고 시작 오류가 없음 | 요구 사항, DLL 누락, 권한, 로그 |
| 메인 UI와 구성요소 이해 | [메인 창](./interface/main-window.md), [UI 구성요소 사용 설명서](./interface/ui-component-handbook.md) | 메뉴, 상태 표시줄, 설정, 로그, DB, Socket, Scheduler 확인 | 장치, Flow, plugin, data, diagnosis 입구를 찾음 | 메뉴 등록, 권한, 언어, status provider |
| 설정 변경 | [속성 편집기](./interface/property-editor.md) | 설정 객체 열기, 필드 수정, 저장, 재시작 확인 | 재시작 후 값 유지 | config path, readonly, type, permission |
| 로그와 현장 오류 확인 | [로그 뷰어](./interface/log-viewer.md) | 시간, level, keyword 로 필터 | 의미 있는 첫 예외를 찾음 | log level, log folder, 대상 모듈 |
| 장치 추가/설정 | [장치 추가 및 구성](./devices/configuration.md), [장치 서비스 개요](./devices/overview.md) | 장치 리소스 생성, 통신/경로 저장 | 목록에 표시되고 상태 refresh | device type, driver, port/IP, enabled |
| 카메라 촬영 | [카메라 서비스](./devices/camera.md), [카메라 관리](./devices/camera-management.md) | 연결, exposure/gain 설정, capture/preview | 이미지 파일 생성 및 열기 | physical camera, driver, exposure, file server |
| Flow 설계 | [워크플로 개요](./workflow/README.md), [Flow 설계](./workflow/design.md) | node 추가, 연결, 장치/template binding | 저장 후 다시 열 수 있음 | node parameter, device list, template name |
| Flow 실행/디버깅 | [Flow 실행과 디버깅](./workflow/execution.md) | Flow 선택, 실행, node 상태 확인 | 완료 또는 첫 실패 node 확인 | start node, device state, template binding, log |
| 이미지와 overlay 확인 | [이미지 편집기 개요](./image-editor/overview.md) | 결과 이미지, layer, ROI/POI, pseudo-color 확인 | 이미지, layer, annotation 이 정상 | file path, write complete, overlay coordinate |
| DB/이력 조회 | [데이터 관리](./data-management/README.md), [DB 작업](./data-management/database.md) | DB/결과 화면에서 SN/시간 검색 | batch, Flow, result, project data 조회 | connection, filter, batch id, template name |
| 데이터 내보내기/가져오기 | [데이터 내보내기 및 가져오기](./data-management/export-import.md) | 출력 경로, 형식, 범위 선택 | CSV/Excel/PDF/image 파일과 필드 정상 | path permission, field mapping, exporter |
| 고객 프로젝트 실행 | [프로젝트 설명](../00-projects/README.md), [프로젝트 기능 매트릭스](../04-api-reference/projects/project-capability-matrix.md) | project window, SN, flow group/template 선택 후 실행 | project 완료 및 고객 결과 생성 | project config, ProcessGroup, Recipe/Fix, Socket/MES |
| 플러그인 사용 | [기존 플러그인 기능](../04-api-reference/plugins/README.md), [플러그인 기능 매트릭스](../04-api-reference/plugins/plugin-capability-matrix.md) | plugin window 열기, 장치 연결 또는 기능 실행 | menu/window/result/export 정상 | manifest, plugin DLL, device dependency, admin |
| 외부 시스템 연동 | [프로젝트 기능 매트릭스](../04-api-reference/projects/project-capability-matrix.md), [SocketProtocol](../04-api-reference/ui-components/ColorVision.SocketProtocol.md) | protocol, port, event/command, SN, response 확인 | 외부 시스템이 trigger 하고 결과 수신 | port conflict, protocol mode, Socket/MES/Modbus |
| 일반 문제 해결 | [FAQ](./troubleshooting/common-issues.md) | 증상 분류 후 로그/설정 확인 | 다음 확인 항목이 명확 | log, config, device, Flow, project boundary |

## 역할별 일상 흐름

| 역할 | 일상 작업 | 자주 보는 문서 |
| --- | --- | --- |
| 운영자 | host 실행, project/Flow 선택, SN 입력, 실행, PASS/FAIL, export | 이 페이지, 메인 창, 프로젝트 설명, 데이터 export |
| 테스트 엔지니어 | 장치 설정, 카메라 조정, Flow 조정, 결과 필드 확인 | 장치, Flow, 이미지 편집기, 데이터 관리 |
| 현장 납품 | 설치, plugin/project 검수, Socket/MES 연동, 운영자 교육 | 설치, 이 페이지, project/plugin matrix, FAQ |
| 유지보수 개발자 | UI, Engine, plugin, project 중 어디를 볼지 판단 | 이 페이지, UI 구성요소, Engine matrix, UI catalog |

## 문제 분류

| 증상 | 먼저 분류 | 다음 확인 |
| --- | --- | --- |
| 메뉴나 창이 없음 | plugin/project package loading | plugin matrix, project matrix, log |
| 장치 offline | service 문제인지 hardware 문제인지 | device page, driver, port/IP, service log |
| Flow 가 끝나지 않음 | node 또는 device command 정지 | execution page 와 첫 미완료 node |
| 결과는 있지만 export 가 비어 있음 | project result mapping | project Process/Recipe/Fix/exporter |
| 이미지에 overlay 없음 | result display handler | image editor 와 Engine result chain |
| 외부로 결과가 반환되지 않음 | protocol, port, project window state | project matrix, SocketProtocol, log |

## 장 경계

- 작업 절차와 현장 확인은 사용자 가이드에 둡니다.
- 코드 구조와 확장점은 [모듈 참조](../04-api-reference/README.md)에 둡니다.
- 프로젝트 업무는 [프로젝트 설명](../00-projects/README.md)에 둡니다.
- 플러그인 개발은 [플러그인 개발 설명서](../02-developer-guide/plugin-development/README.md)에 둡니다.
- UI DLL 릴리스는 [UI 구성요소 및 DLL 릴리스](../04-api-reference/ui-components/README.md)에 둡니다.

