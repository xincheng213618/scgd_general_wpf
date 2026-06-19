# POI 템플릿

이 페이지는 현재 저장소에 실제로 존재하는 POI 템플릿군만 설명합니다. POI는 단일 검출 알고리즘이 아니라 점 집합을 만들고, 저장하고, 보정하고, 출력하며, 다른 알고리즘이 다시 사용하는 공유 템플릿 체계입니다.

## 현재 역할

- 주 POI 템플릿은 점 집합, 크기, 네 모서리, 설정 JSON을 저장합니다.
- 필터, 보정, 캘리브레이션, 출력은 각각 별도 동반 템플릿을 가집니다.
- 런타임 알고리즘은 이 템플릿들을 MQTT 요청으로 조립합니다.
- Flow 노드, ARVR, JSON 알고리즘도 POI 템플릿을 소비합니다.

## 중요한 파일

- `Engine/ColorVision.Engine/Templates/POI/TemplatePoi.cs`
- `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/PoiPoint.cs`
- `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
- `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIFilters/TemplatePoiFilterParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIRevise/TemplatePoiReviseParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIOutput/TemplatePoiOutputParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIGenCali/TemplatePOICalParam.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 현재 템플릿 매트릭스

| 템플릿 | 사전/코드 | 편집 진입점 | 주요 용도 |
| --- | --- | --- | --- |
| `TemplatePoi` | `TemplateDicId = -1`, `Code = POI` | 독립 `EditPoiParam` 창 | 점 집합, 크기, 네 모서리, 설정 JSON, 점 상세를 저장합니다. |
| `TemplateBuildPoi` | `TemplateDicId = 16`, `Code = BuildPOI` | 템플릿/배치 UI | 규칙 또는 CAD 매핑으로 POI를 생성합니다. |
| `TemplatePoiFilterParam` | `TemplateDicId = 23`, `Code = POIFilter` | 사용자 정의 필터 편집기 | POI 실행 시 선택 필터 템플릿입니다. |
| `TemplatePoiReviseParam` | `TemplateDicId = 24`, `Code = PoiRevise` | 템플릿 편집기 | POI 실행 시 선택 보정 템플릿입니다. |
| `TemplatePoiGenCalParam` | `TemplateDicId = 25`, `Code = POIGenCali` | 사용자 정의 캘리브레이션 편집기 | POI 캘리브레이션/보정 Flow 노드에서 사용합니다. |
| `TemplatePoiOutputParam` | `TemplateDicId = 27`, `Code = PoiOutput` | 사용자 정의 출력 편집기 | POI 실행 시 선택 파일 출력 템플릿입니다. |
| `TemplateBuildPOIAA` | `TemplateDicId = 41`, `Code = BuildPOI` | JSON 템플릿 편집기 | AA 검출 결과로 POI를 만드는 JSON V2 분기입니다. |

주 템플릿은 실제 점 위치를 저장하고, 동반 템플릿은 점을 어떻게 생성, 필터링, 보정, 출력할지 설명합니다.

## 주 템플릿과 데이터 모델

`TemplatePoi`는 `ITemplate<PoiParam>`을 상속하고 `IsSideHide = true`, `Code = POI`입니다. 목록 항목을 두 번 클릭하면 `EditPoiParam`이 열리므로 일반 오른쪽 `PropertyGrid`만으로 설명하면 안 됩니다.

`PoiParam`은 `Width`, `Height`, 네 모서리 좌표, `CfgJson`, `PoiConfig`, `ObservableCollection<PoiPoint>`를 보유합니다. `PoiPoint`는 `Id`, `Name`, `PointType`, `PixX`, `PixY`, `PixWidth`, `PixHeight`를 저장합니다.

## 지속화

POI 주 템플릿은 일반 `ModMasterModel`/`ModDetailModel`이 아니라 전용 테이블을 사용합니다.

| 테이블 | 주요 필드 | 의미 |
| --- | --- | --- |
| `t_scgd_algorithm_poi_template_master` | `name`, `type`, `width`, `height`, 네 모서리 좌표, `cfg_json`, `tenant_id`, `is_delete` | POI 템플릿 본체, 캔버스 크기, 설정 JSON. |
| `t_scgd_algorithm_poi_template_detail` | `pid`, `pt_type`, `pix_x`, `pix_y`, `pix_width`, `pix_height`, `remark` | 각 POI 점 또는 영역의 위치와 크기. |

`PoiParam.LoadPoiDetailFromDB(...)`는 점 상세를 다시 읽습니다. 저장 시에는 주 레코드를 저장하고 기존 점 상세를 삭제한 뒤 `PoiDetailModel`을 일괄 다시 씁니다. 복사나 가져오기에서는 템플릿과 점의 `Id`를 `-1`로 되돌립니다.

## 런타임 POI 실행

`AlgorithmPoi`는 `Event_POI_GetData`를 보냅니다. 주 템플릿뿐 아니라 필터, 보정, 출력, 파일/DB 점 집합 선택도 함께 조립합니다.

| 파라미터 | 출처 | 설명 |
| --- | --- | --- |
| `TemplateParam` | `TemplatePoi` | 필수 주 POI 템플릿입니다. |
| `FilterTemplate` | `TemplatePoiFilterParam` | `Id != -1`일 때 전송합니다. |
| `ReviseTemplate` | `TemplatePoiReviseParam` | `Id != -1`일 때 전송합니다. |
| `OutputTemplate` | `TemplatePoiOutputParam` | `Id != -1`일 때 전송합니다. |
| `POIStorageType` | `POIStorageModel` | 파일 모드에서 DB 점 집합과 외부 점 파일을 구분합니다. |
| `POIPointFileName` | 파일 선택 | 파일 모드 외부 점 파일 경로입니다. |
| `IsSubPixel`, `IsCCTWave` | 알고리즘 UI | 서브픽셀/CCT 파형 관련 실행 옵션입니다. |

## BuildPOI

`AlgorithmBuildPoi`는 `Event_Build_POI`를 보냅니다. `TemplateBuildPoi`를 사용하고 `POILayoutReq`, `POIStorageType`, `BuildType`을 보냅니다. `POIBuildType == CADMapping`이면 `LayoutPolygon`과 `CADMappingParam`도 추가합니다.

`Event_Build_POI`는 점 집합을 생성하는 쪽이고, `Event_POI_GetData`는 점 집합에서 값을 읽는 쪽입니다. 현장 분석 시 두 이벤트의 템플릿 파라미터를 섞지 마세요.

## Flow 소비 경로

| Flow 구성 분기 | 장치/입력 | 템플릿 선택기 | 인수인계 핵심 |
| --- | --- | --- | --- |
| POI 캘리브레이션 보정 | `DeviceAlgorithm` | `TemplatePoiGenCalParam` | 주 POI가 아니라 캘리브레이션 보정 템플릿을 다룹니다. |
| POI 필터/보정/출력 | `DeviceAlgorithm` | `TemplatePoiFilterParam`, `TemplatePoiReviseParam`, `TemplatePoiOutputParam` | 기존 POI 결과의 후처리입니다. |
| POI 실행 | `DeviceAlgorithm` + 이미지 경로 | `TemplatePoi`, 필터, 보정, 출력 | `Event_POI_GetData`의 전체 실행 체인입니다. |
| BuildPOI | `DeviceAlgorithm` + 이미지 경로 | `TemplateBuildPoi` 또는 `TemplateBuildPOIAA`, `RePOI`, `LayoutROI`, `SavePOI` | 전통 배치와 JSON AA 배치를 모두 다룹니다. |
| PoiAnalysis | `DeviceAlgorithm` | `TemplatePoiAnalysis` | JSON 분석 템플릿도 POI 관련 결과를 소비합니다. |

## 결과 저장 및 표시

| 결과 타입 | 표시/내보내기 진입점 | 테이블/파일 단서 |
| --- | --- | --- |
| `POI`, `POI_Y` | `ViewHanlePOIY` | CSV 내보내기. 값은 POI 상세 결과에서 옵니다. |
| `POI_XYZ` | `ViewHanlePOIXZY` | CSV와 XYZ 표시. |
| `POI_XYZ_File`, `POI_Y_File`, `POI_CIE_File` | `ViewHanlePOIXZY` | 파일형 결과. `t_scgd_algorithm_result_detail_poi_cie_file`을 확인합니다. |
| `RealPOI`, `POI_XYZ_V2`, `POI_Y_V2`, `KB_Output_Lv`, `KB_Output_CIE` | `ViewHandleRealPOI` | V2/프로젝트 출력 체인입니다. 실제 `ResultType`을 확인합니다. |
| `BuildPOI`, `BuildPOI_File` | `ViewHandleBuildPoi`, `ViewHandleBuildPoiFile` | 배치 결과 또는 파일 결과입니다. 새 POI 데이터를 만들 수 있습니다. |

점 값 상세에는 `t_scgd_algorithm_result_detail_poi_mtf`가 있으며 `poi_id`, `poi_name`, `poi_type`, `poi_x/y`, `poi_width/height`, `value`를 다룹니다.

## 자주 생기는 오해

### POI는 단일 알고리즘이 아닙니다

POI는 공유 점 집합 템플릿 체계이며 점 생성, 필터, 보정, 다른 알고리즘의 참조를 담당합니다.

### 주 저장소는 일반 detail 테이블이 아닙니다

주 템플릿은 `PoiMasterDao`와 `PoiDetailDao`에 의존합니다. 일반 템플릿 테이블만으로 설명하면 점 상세를 놓칩니다.

### 주 편집기는 순수 `PropertyGrid`가 아닙니다

`TemplatePoi`는 `EditPoiParam`을 열고, 필터와 출력 템플릿도 전용 편집 UI를 가집니다.

### 파일 모드와 DB 모드가 공존합니다

`AlgorithmPoi`는 `POIStorageModel.Db`와 `POIStorageModel.File`을 모두 처리합니다.

## 인수인계 검증

| 상황 | 필수 확인 |
| --- | --- |
| POI 생성/저장 | master 테이블에 주 레코드, detail 테이블에 점 상세가 생성됩니다. |
| POI 복사/가져오기 | 템플릿과 점 상세의 `Id`가 리셋되고 기존 템플릿을 덮어쓰지 않습니다. |
| 파일 모드 실행 | MQTT 파라미터에 `POIStorageType`과 `POIPointFileName`이 포함됩니다. |
| 필터/보정/출력 | 선택 시 `FilterTemplate`, `ReviseTemplate`, `OutputTemplate`이 전송됩니다. |
| BuildPOI CADMapping | `LayoutPolygon`과 `CADMappingParam`이 있고 네 점 ROI와 CAD 파일 경로가 올바릅니다. |
| 결과 표시 | `ViewResultAlgType`에 맞는 handler로 들어가고 CSV와 결과 테이블/파일이 일치합니다. |

## 추천 읽기 순서

1. `Engine/ColorVision.Engine/Templates/POI/TemplatePoi.cs`
2. `Engine/ColorVision.Engine/Templates/POI/PoiParam.cs`
3. `Engine/ColorVision.Engine/Templates/POI/AlgorithmImp/AlgorithmPOI.cs`
4. `Engine/ColorVision.Engine/Templates/POI/BuildPoi/AlgorithmBuildPoi.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 계속 읽기

- [POI 원형](../primitives/poi.md)
- [JSON 템플릿](./json-templates.md)
- [Flow 엔진](./flow-engine.md)
