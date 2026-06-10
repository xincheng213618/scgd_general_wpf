# UI 구성요소 사용 설명서

이 페이지는 ColorVision 의 일반적인 UI 구성요소를 운영자, 테스트 엔지니어, 현장 납품 담당자 관점에서 설명합니다. 언제 열고, 어디에서 들어가며, 무엇을 하고, 성공 기준이 무엇이며, 실패 시 무엇을 먼저 볼지 정리합니다.

DLL 릴리스나 소스 변경은 [UI DLL 구성요소 설명서](../../04-api-reference/ui-components/component-handbook.md)와 [UI 구성요소 카탈로그](../../04-api-reference/ui-components/control-catalog.md)를 사용하세요. 이 페이지는 사용자 작업 수준에 머뭅니다.

## 구성요소 개요

| UI 구성요소 | 언제 사용 | 일반 입구 | 주요 작업 | 성공 기준 | 실패 시 첫 확인 |
| --- | --- | --- | --- | --- | --- |
| 메인 워크벤치 | 시작 후 일상 작업 출발점 | 실행 후 자동 표시 | menu, search, workspace, status bar 확인 | device, Flow, image, log, plugin 입구를 찾음 | plugin loading, permission, language, layout |
| 상단 메뉴 | 전역 tool, device, plugin, help 열기 | main window menu | 목적에 맞는 tool/plugin 선택 | 대상 window/기능 열림 | menu permission, plugin state, hotkey conflict |
| 검색창 | 기능 위치를 모를 때 | main window search | keyword 입력, command/page 열기 | 목표 입구를 찾고 열 수 있음 | keyword, plugin loading, search index |
| 상태 표시줄 | service, plugin, Socket, scheduler 상태 | bottom status bar | 상태 확인 또는 icon click | 현장 상태와 일치 | provider, service config, log |
| 설정 창 | global/module config 변경 | settings/options menu | 설정 검색, 수정, 저장, 재시작 확인 | 값이 재시작 후 유지 | config path, permission, field type, readonly |
| 속성 편집기 | device/template/Flow node/config 편집 | property panel/dialog | category 별 parameter 수정 | 객체 동작이 바뀜 | metadata, validation, readonly, save path |
| 로그 뷰어 | startup/device/Flow/plugin 진단 | Help -> Log, `Ctrl+L` | time, level, keyword filter | 의미 있는 첫 error 확인 | log level, timestamp, module name |
| 터미널 | 현장 command/script/file check | terminal/workspace | command 실행, output 확인 | 명확한 결과 반환 | current directory, permission, environment |
| 이미지 편집기 | image, overlay, ROI, video, 3D, pseudo-color | result image, file open, workspace | zoom, annotation, measure, import/export | image 와 overlay 정렬 | file path, writing, coordinate mapping |
| DB 브라우저 | MySQL/SQLite table 확인 | Tools -> Database Browser | source/table/search/page | record 를 찾거나 미기록 확인 | connection, filter, page, primary key |
| Socket 관리자 | local TCP server 와 message 확인 | Socket status icon | enabled, port, connection, history | 외부 시스템이 송수신 가능 | port conflict, protocol mode, handler |
| Scheduler | Quartz job 관리 | Tools -> Task Manager | create/pause/resume/run/history | next fire time 과 history 정상 | Cron, assembly, exception |
| Workspace/file tree | `.cvsln` 과 project file 관리 | Solution workspace | create/open/edit/layout | 올바른 editor 로 열림 | file type, editor registration, layout cache |
| Plugin marketplace | plugin/DLL install/update | Help -> Marketplace | version, download, update | host 가 package load 가능 | admin, network, manifest, version |
| Downloader | package/resource download | marketplace/download window | task, progress, retry | file 이 완전히 저장 | aria2c, path permission, network |
| Wizard | 단계별 초기화/설정 | wizard entry | 입력, next, finish | 각 step validation 통과 | required field, device, output path |

## 기본 UI

처음 실행할 때는 menu 가 보이는지, search 가 동작하는지, workspace 가 window/image/Flow/editor 를 열 수 있는지, status bar 가 service 상태를 표시하는지 확인합니다. 메인 창은 입구를 정리하는 곳이지 특정 업무 Flow 자체가 아닙니다. 기능이 보이지 않으면 menu 미등록, plugin 미로드, permission 부족, project package 비활성 중 무엇인지 먼저 분리합니다.

| 구성요소 | 사용 방법 | 인수인계 주의 |
| --- | --- | --- |
| Menu | 고정 입구와 admin tool 은 menu 로 열기 | host, UI module, plugin, project package 항목이 섞일 수 있음 |
| Search | 신규 사용자가 입구를 모를 때 사용 | 결과가 없으면 plugin 미로드일 수 있음 |
| Status bar | service 와 background task 상태 확인 | Socket/Scheduler icon 에서 관리 window 를 열 수 있음 |

버튼이 반응하지 않으면 [로그 뷰어](./log-viewer.md)에서 클릭 시각 주변의 `Error` / `Warn` 을 확인합니다.

## 설정과 파라미터

설정 창은 global/module config 용입니다. 현장에서 변경하기 전에 global/device/Flow/project config 중 무엇인지 확인하고, port, path, database, Socket, file server 의 기존 값을 기록합니다. 저장 후 필요하면 restart 또는 service refresh 를 하고 status bar, log, device page, project page 로 검증합니다.

속성 편집기는 device, template, Flow node, drawing object, plugin config, project config 의 parameter 를 편집합니다. 기대한 editor control 이 보이지 않으면 개발 측은 `Category`, `DisplayName`, `Description`, custom editor type, visibility metadata 를 확인해야 합니다.

## 진단 구성요소

| 문제 | Log keyword | 다음 페이지 |
| --- | --- | --- |
| 시작 실패 | `Error`, `DllNotFoundException`, plugin name, config file | [FAQ](../troubleshooting/common-issues.md) |
| device 연결 실패 | device name, port, IP, `timeout`, service name | [장치 서비스 개요](../devices/overview.md) |
| Flow 실패 | Flow name, node name, template name, `failed` | [Flow 실행과 디버깅](../workflow/execution.md) |
| plugin 없음 | plugin folder, `manifest`, `deps.json`, DLL name | [기존 플러그인 기능](../../04-api-reference/plugins/README.md) |
| data 미기록 | table, batch, SN, export path | [데이터 관리](../data-management/README.md) |

Terminal 은 현장 납품 담당자와 개발 지원용입니다. directory, network, script, helper tool 확인에 사용하며 일반 운영자의 일상 작업에는 필요하지 않습니다.

## 데이터, 통신, 스케줄링

| 구성요소 | 용도 | 성공 기준 | 실패 시 |
| --- | --- | --- | --- |
| DB browser | 결과 기록과 record 검색 확인 | SN/시간/batch 로 조회 가능 | connection, filter, primary key, readonly |
| Socket manager | TCP server 와 message history | 외부가 접속하고 송수신 가능 | port, firewall, protocol mode, handler |
| Scheduler window | Quartz job 관리 | next fire time 과 history 정상 | Cron, scheduler state, job assembly |

Project package 가 Socket 으로 시작되면 [프로젝트 설명](../../00-projects/README.md)과 해당 project page 에서 event name, field, response 를 확인합니다.

## 이미지와 Workspace

이미지 편집기는 viewer 만이 아니라 result, ROI/POI, annotation, overlay, video, pseudo-color, histogram, 3D, CIE 를 담당합니다. 표시 이상은 file write 완료, coordinate mapping, overlay source 순서로 확인합니다.

Workspace 는 `.cvsln`, file tree, editor tabs, terminal, multi-image view 를 다룹니다. Engine Flow 실행 장소가 아닙니다. 고객 project 를 실행하려면 project window 또는 [프로젝트 설명](../../00-projects/README.md)으로 이동합니다.

## 경계

| 증상 | 먼저 볼 곳 |
| --- | --- |
| window 가 열리지 않음, menu 없음, button 무반응 | UI 작업과 log |
| device state 이상, camera capture 불가, motor 미동작 | device service |
| Flow node 실패, result 미생성 | Workflow 와 Engine |
| Socket 은 연결되지만 field 가 다름 | project protocol 또는 Socket handler |
| plugin 이 보이지 않음, version incompatible | plugin loading 과 marketplace |
| UI DLL/native DLL 누락 | UI DLL release 와 installer |
| DB 에 result 없음 | data management, Flow write path, project export |

## 인수인계 체크

- main window 에서 log, settings, DB browser, scheduler, marketplace 를 연다.
- 안전한 config 하나를 변경하고 저장/재시작 후 유지되는지 확인한다.
- image 를 열고 zoom, annotation, property, export 를 수행한다.
- DB browser 에서 time 또는 SN 으로 result 를 한 건 조회한다.
- Socket 사용 시 port, connection, message history 를 확인한다.
- plugin/project package 납품 시 menu, status icon, project window 가 나타나고 열리는지 확인한다.

## 계속 읽기

- [메인 창](./main-window.md)
- [속성 편집기](./property-editor.md)
- [로그 뷰어](./log-viewer.md)
- [이미지 편집기 개요](../image-editor/overview.md)
- [DB 작업](../data-management/database.md)
- [UI DLL 구성요소 설명서](../../04-api-reference/ui-components/component-handbook.md)

