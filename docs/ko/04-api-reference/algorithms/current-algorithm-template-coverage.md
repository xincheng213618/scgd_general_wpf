# 현재 알고리즘 템플릿 커버리지

이 문서는 실제 `Engine/ColorVision.Engine/Templates/` 소스 디렉터리와 현재 문서 진입점을 맞춰 둔 인수인계용 표입니다. 모든 알고리즘 기능을 완전히 보증하는 표가 아니라, 각 템플릿 디렉터리를 어디서부터 읽어야 하는지와 어떤 설명이 아직 부족한지를 판단하기 위한 지도입니다.

## 커버리지 상태

| 상태 | 의미 |
| --- | --- |
| 전용 문서 있음 | 주요 진입점, 실행 체인, 경계를 설명하는 인수인계용 문서가 있다. |
| 횡단 커버 | 현재 템플릿 관리, ROI/POI, 공통 알고리즘, Engine 체인 문서에서 함께 다룬다. |
| 전용 문서 필요 | 소유 영역은 분명하지만 업무 의미나 승인 기준을 별도 문서로 분리해야 한다. |

## Templates 디렉터리 커버리지

| 템플릿 디렉터리 | 업무 역할 | 먼저 읽을 문서 | 상태 | 인수인계 초점 |
| --- | --- | --- | --- | --- |
| `ARVR/` | AR/VR 검사 템플릿군. 파라미터, 알고리즘 요청, 결과 표시를 연결한다. | [ARVR 템플릿](./templates/arvr-template.md), [결과 인수인계 체인](../engine-components/result-handoff-chain.md) | 전용 문서 있음 | 템플릿 매트릭스, 수동 이벤트, Flow `operatorCode`, POI 의존성, 결과 테이블, handler, 검증 항목을 다룹니다. |
| `BuzProduct/` | 제품 마스터, 상세, POI, Validate 규칙을 묶는 제품/업무 템플릿. | [BuzProduct 제품 업무 파라미터 템플릿](./templates/buz-product-template.md), [Validate 판정 규칙 템플릿](./templates/validate-rules.md) | 전용 문서 있음 | `BuzProduc` 소스 철자, 마스터/상세 테이블, `poi_id`, `val_rule_temp_id` 를 추적한다. |
| `Compliance/` | Y/XYZ/JND 결과와 `ValidateResult` 를 읽는 결과 표시/판정 해석 계층. | [Compliance 결과 인수인계](./templates/compliance-results.md), [결과 인수인계 체인](../engine-components/result-handoff-chain.md) | 전용 문서 있음 | 세 결과 상세 테이블, handler 타입 매핑, `ValidateRuleResultType.M` 판정 로직을 추적한다. |
| `DataLoad/` | Flow DataLoad 노드에 장치, 시리얼 번호, 결과 타입, ZIndex 파라미터를 전달하는 데이터 로드 템플릿. | [DataLoad 데이터 로드 템플릿](./templates/data-load-template.md), [템플릿 및 Flow 체인](../engine-components/template-flow-chain.md) | 단독 페이지 있음 | `AlgDataLoadNode` 템플릿 경로와 `AlgDataLoadNode2` 명시 파라미터 경로를 구분합니다. |
| `FindLightArea/` | 발광 영역/ROI 위치 템플릿. OpenCV helper 및 ROI 출력과 관련된다. | [FindLightArea 발광 영역 템플릿](./templates/find-light-area.md), [ROI 프리미티브](./primitives/roi.md) | 전용 문서 있음 | `Event_LightArea2_GetData`, `RoiParam`, 포인트 테이블, 볼록 껍질 오버레이. |
| `Flow/` | 템플릿 시스템과 `FlowEngineLib` 시각적 플로우를 연결한다. | [Flow 엔진](./templates/flow-engine.md), [Engine 템플릿 및 Flow 체인](../engine-components/template-flow-chain.md) | 전용 문서 있음 | `TemplateFlow` 저장 경로, `.cvflow` 패키지, 가져오기/내보내기, 실행 스케줄링, 노드 구성자 경계를 다룹니다. |
| `FocusPoints/` | 기존 발광 영역/포커스 포인트 파라미터 템플릿으로 이진화, 필터, 형태학 처리, ROI 경계를 저장합니다. | [FocusPoints 포커스 포인트 템플릿](./templates/focus-points-template.md), [FindLightArea 발광 영역 템플릿](./templates/find-light-area.md) | 단독 페이지 있음 | 수동 `Event_LightArea_GetData` 와 Flow `operatorCode = "FocusPoints"` 를 구분합니다. |
| `ImageCropping/` | 네 점 ROI, Flow 두 입력 크롭 노드, 크롭 결과 표시를 연결하는 강타입 템플릿. | [ImageCropping 이미지 크롭 템플릿](./templates/image-cropping-template.md), [결과 인수인계 체인](../engine-components/result-handoff-chain.md) | 단독 페이지 있음 | `Event_Image_Cropping`, `OLED.GetRIAand`, `ROI_MasterId`, `ViewHandleImageCropping` 을 추적합니다. |
| `JND/` | JND 관련 검사 템플릿. AR/VR 또는 디스플레이 품질 업무와 관련된다. | [JND 템플릿](./templates/jnd-template.md), [POI 템플릿](./templates/poi-template.md) | 전용 문서 있음 | `CutOff`, `POITemplateParam`, `h_jnd/v_jnd`, 프로젝트 OK/NG 경계. |
| `Jsons/` | JSON 템플릿 체계. 텍스트/속성 편집과 가져오기/내보내기를 제공한다. | [JSON 템플릿](./templates/json-templates.md), [Templates API 참조](./templates/api-reference.md) | 전용 문서 있음 | 현재 JSON 하위 템플릿 목록, schema index, V2/구형 강타입 경계, handler, 검증 항목을 다룹니다. |
| `LedCheck/` | LED 검사 템플릿군. LED, 밝기, 결함 검사를 담당한다. | [LED 검출 템플릿](./templates/led-detection.md), [POI 템플릿](./templates/poi-template.md) | 전용 문서 있음 | `FindLED` 신구 진입점, POI 의존성, 결과 handler 등록, 내보내기 경계. |
| `LEDStripDetection/` | LED 스트립 검사 템플릿. JSON, 스트립 위치, 결함 결과와 관련된다. | [LED 검출 템플릿](./templates/led-detection.md), [JSON 템플릿](./templates/json-templates.md) | 전용 문서 있음 | 기존 강타입 `Event_LED_StripDetection`과 JSON V2 `Version = 2.0` 구분. |
| `Matching/` | 수동 UI, Flow 노드, MQTT 요청, AOI 결과 표시를 포함하는 템플릿 매칭/위치 지정 체인. | [Matching 템플릿 매칭](./templates/matching-template.md), [결과 인수인계 체인](../engine-components/result-handoff-chain.md) | 단독 페이지 있음 | `MatchTemplate`, `TemplateFile`, `t_scgd_algorithm_result_detail_aoi`, 네 점 overlay 를 추적합니다. |
| `Menus/` | 템플릿 메뉴 그룹, 부모-자식 관계, 기본 편집 창을 정의하는 진입점 래퍼. | [템플릿 메뉴 진입점](./templates/template-menu-entries.md), [템플릿 관리](./templates/template-management.md) | 단독 페이지 있음 | `OwnerGuid`, `Order`, `Header`, `Template`, `ShowTemplateWindow()` 를 추적합니다. |
| `POI/` | POI 템플릿군. 포인트, 영역, 상위 알고리즘 파라미터를 제공한다. | [POI 템플릿](./templates/poi-template.md), [POI 프리미티브](./primitives/poi.md) | 전용 문서 있음 | 주/동반 템플릿, 전용 점 테이블, 실행 파라미터, BuildPOI, Flow 소비, 결과 handler를 다룹니다. |
| `SysDictionary/` | `mod_type = 7` 알고리즘 기본 사전 master/detail 을 관리하는 시스템 사전 템플릿. | [SysDictionary 시스템 사전 템플릿](./templates/sys-dictionary-template.md), [Templates API 참조](./templates/api-reference.md) | 단독 페이지 있음 | `TemplateModParam`, `symbol`, `default_val`, `val_type`, 마이그레이션 경계를 추적합니다. |
| `Validate/` | 기본 합규 사전과 실제 판정 템플릿 두 계층을 가진 판정 규칙 체계. | [Validate 판정 규칙 템플릿](./templates/validate-rules.md), [템플릿 관리](./templates/template-management.md) | 전용 문서 있음 | `mod_type = 110/111/120`, `CIEParams/JNDParams`, 규칙 마스터/상세 테이블을 추적한다. |

## 핵심 진입 파일

| 파일 | 인수인계 용도 |
| --- | --- |
| `TemplateContorl.cs` | 템플릿 발견, `IITemplateLoad` 로드, 등록 진입점. |
| `TemplateManagerWindow.xaml(.cs)` | 템플릿 관리 창. UI 조작에서 템플릿 데이터로 추적할 때 사용한다. |
| `TemplateEditorWindow.xaml(.cs)` | 공통 템플릿 편집 창. 속성 편집, 저장, 검증을 추적한다. |
| `TemplateSearchProvider.cs` | 템플릿 검색 진입점. 템플릿이 검색되지 않는 문제를 추적한다. |
| `TemplateSampleLibrary.cs` | 템플릿 샘플과 재사용 진입점. 기본 템플릿 출처를 추적한다. |

## 유지보수 규칙

- 새 `Templates/<Name>/` 디렉터리를 추가하면 먼저 이 표에 행을 추가하고 전용 문서 필요 여부를 판단한다.
- `Algorithm*`, 결과 뷰, MQTT 실행 요청이 있으면 파라미터 출처, 실행 서비스, 결과 필드, 실패 처리를 문서화한다.
- 메뉴, 사전, 래퍼 계층뿐인 디렉터리도 어떤 템플릿군을 지원하는지 적는다.
- “전용 문서 필요” 디렉터리가 고객 납품, DLL 릴리스, 현장 승인 범위에 들어오면 먼저 독립 문서로 승격한다.

## 다음 우선 보강

1. Flow 변환/보정 노드는 [Flow 변환 및 보정 노드](../engine-components/flow-conversion-calibration-nodes.md) 로 이동했습니다. 현재 source tree 에는 `Templates/FileConvert/`, `Templates/ImageTransform/`, `Templates/Calibration/` 이 없으므로 앞으로는 node chain 으로 유지합니다.
2. `Menus/`, `SysDictionaryMod/`: 메뉴 진입점, 사전 기본값, 템플릿 창 등록 관계를 인수인계 체크리스트로 정리합니다.
3. `Projects/` 아래 아직 정리되지 않은 고객 프로젝트: 업무 진입점, 의존 템플릿, 플러그인 능력, 현장 검증 기준을 맞춥니다.
