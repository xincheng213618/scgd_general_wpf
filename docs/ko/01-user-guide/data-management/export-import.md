# 데이터 내보내기 및 가져오기

현재 repository 에는 "모든 데이터를 여기서 import/export" 하는 통합 센터가 없습니다. 실제로는 settings, Flow template, result data 가 각각 다른 입구를 가집니다.

## 먼저 확인할 것

import/export 전에 다음 세 가지를 확인합니다.

1. 대상이 settings, Flow template, 특정 result module data 중 무엇인지.
2. 필요한 것이 전체 config migration 인지, 단일 business object export 인지.
3. 이 기능이 data management center 가 아니라 특정 window 에 속한 기능인지.

## 현재 확인 가능한 입구

### settings import/export

명확한 menu entry 가 있습니다.

- Tools -> Import/Export Settings

여기서는 최소 다음 동작을 다룹니다.

- settings 를 `.cvsettings` 로 export
- `.cvsettings` 에서 settings import

목표가 result data migration 이 아니라 software config migration 이라면 여기서 시작합니다.

### Flow template import/export

Flow template import/export 는 data management page 가 아니라 Flow designer 에서 다룹니다.

- export current Flow
- import Flow
- import module

Flow 내용을 이동하려면 먼저 [Flow 설계](../workflow/design.md) 를 확인합니다.

### module 내부 result export

일부 business window 는 자체 export 기능을 가집니다.

- Flow node analysis window 의 CSV export
- 일부 plugin 또는 image/measurement window 의 CSV/image export

이런 export 는 특정 business object 와 강하게 묶이므로 unified global data export center 로 설명하지 않습니다.

## object 와 entry 대응

| 납품 대상 | 우선 입구 | 납품 전 확인 |
| --- | --- | --- |
| software settings | Tools -> Import/Export Settings | `.cvsettings` import 가능, restart 후 key settings 유지 |
| Flow template | Flow designer import/export | import 후 start node, device binding, template parameter 정상 |
| database record | database browser 또는 business result page | SN, time, batch 로 같은 run 검색 가능 |
| CSV/Excel | 대상 business window 또는 plugin export | field order, unit, PASS/FAIL, encoding 이 고객 요구와 일치 |
| PDF/report | project window 또는 plugin report entry | header, customer mark, result image, judgement item 정상 |
| image/overlay | image editor 또는 result window | original image, ROI/POI, annotation coordinate, file name 대응 |
| Socket/MES response | project window, SocketProtocol, integration tool | request/response sample 저장, status/Data field 정상 |

## export 납품 전 검수

export 는 "button 은 동작하지만 납품 file 이 틀림" 문제가 자주 생깁니다. 납품 전에 한 번 end-to-end 로 확인합니다.

| 단계 | 작업 | 통과 기준 |
| --- | --- | --- |
| 1 | 명확한 SN 또는 test batch 로 최소 Flow 실행 | query, export, external response 가 같은 식별자를 사용 |
| 2 | database 또는 result window 에서 source data 확인 | 빈 데이터나 이전 batch 가 아님 |
| 3 | target window 에서 export | file 생성, path/name 설명 가능 |
| 4 | export file 열어 fields 확인 | field order, unit, judgement, time, SN 이 고객 형식과 일치 |
| 5 | sample file 과 screenshot 저장 | upgrade 후 재테스트 기준으로 사용 |

## export failure triage

| 현상 | 먼저 확인 | 다음 확인 |
| --- | --- | --- |
| export button 을 찾을 수 없음 | 대상이 settings, Flow, business window 중 무엇인지 | plugin/project docs 가 export support 를 명시하는지 |
| file 은 생성되지만 비어 있음 | source data 존재, batch/SN 선택 정확성 | export filter 와 field mapping |
| field 누락/순서 다름 | 올바른 object/window 를 export 하는지 | project exporter, customer format version |
| image/overlay 정렬 안 됨 | result image 와 original image 가 같은 run 인지 | ROI/POI coordinate, scaling, template version |
| external system 이 받지 못함 | ColorVision 이 완료하고 result 를 생성했는지 | protocol, port, project handler, response field |
| migration 후 동작 변화 | export 한 것이 settings 만인지 | old config, Flow template, database backup 동기화 |

## Handoff Record Template

```text
export object:
source window:
source SN/batch/time:
database evidence:
export file path:
file format/version:
required fields:
sample screenshot:
external response sample:
known limitations:
owner/date:
```

## 일반적인 사용 순서

1. export 할 object 를 확인합니다.
2. global settings 라면 Import/Export Settings 를 사용합니다.
3. Flow content 라면 [Flow 설계](../workflow/design.md) 의 import/export 를 사용합니다.
4. result data 또는 business data 라면 해당 module window 의 export entry 를 찾습니다.
5. database data 가 관련되면 [데이터베이스 작업](./database.md) 으로 source range 를 확인합니다.

## 이 페이지가 보장하지 않는 것

특정 module window 에서 확인되지 않은 경우 다음 기능을 unified 기능으로 선언하지 않습니다.

- unified Excel export center
- unified JSON export center
- unified XML export center
- unified PDF report export center
- generic column mapping import wizard
- generic batch folder import wizard

plugin 또는 window 가 이런 기능을 갖는다면 해당 module 자체 페이지에 기록합니다.

## FAQ

### export 입구를 찾을 수 없음

- top-level menu 를 계속 찾기 전에 대상이 settings, Flow, business result window 중 무엇인지 확인합니다.
- 해당 module page 에서 export entry 를 확인합니다.

### settings 는 export 했지만 business result 는 이동되지 않음

- `.cvsettings` 는 주로 config migration 용이며 database result migration 이 아닙니다.
- actual result data 는 [데이터베이스 작업](./database.md) 또는 해당 business module 에서 다룹니다.

### Flow import 는 성공했지만 결과가 다름

- 올바른 Flow version 을 import 했는지 확인합니다.
- [Flow 실행과 디버깅](../workflow/execution.md) 에서 dependent device 와 template 을 확인합니다.
- 필요하면 module import 와 Flow parameter 를 다시 확인합니다.

## 계속 읽기

- [데이터 관리 개요](./README.md)
- [데이터베이스 작업](./database.md)
- [Flow 설계](../workflow/design.md)
- [FAQ](../troubleshooting/common-issues.md)
- [현장 작업 검수 체크리스트](../field-operation-acceptance.md)

## 설명

- 이 페이지는 현재 확인 가능한 import/export path 만 다룹니다.
- settings import/export 구현은 주로 `UI/ColorVision.UI.Desktop/Settings/ExportAndImport/` 에 있습니다.
