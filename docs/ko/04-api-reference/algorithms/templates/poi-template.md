# POI 템플릿

이 페이지에서는 현재 창고에 실제로 존재하는 POI 템플릿 계열에 대해서만 설명하고 "감지 인터페이스 백과사전 + 플러그형 알고리즘 샘플" 스타일의 이전 초안을 더 이상 유지하지 않습니다.

## 이 템플릿 계열은 현재 무엇을 하고 있나요?

현재 소스 코드 상태에 따르면 POI는 격리된 템플릿이 아니라 "포인트 세트 데이터"를 중심으로 한 템플릿 및 알고리즘 호스트 집합입니다.

- 기본 POI 템플릿은 포인트 세트, 치수 및 구성을 저장하는 역할을 합니다.
- 필터링, 수정, 교정 및 출력에는 각각 고유한 관련 템플릿이 있습니다.
- 런타임 알고리즘은 이러한 템플릿을 MQTT 요청에 연결하는 역할을 합니다.
- 흐름 노드와 여러 JSON 알고리즘은 POI 템플릿을 계속 사용합니다.

따라서 이 페이지의 실제 내용은 "특정 POI 감지 알고리즘"이 아니라 현재 시스템에서 POI 템플릿이 어떻게 생성, 편집, 저장 및 재사용되는지에 대한 것입니다.

## 현재 가장 중요한 파일

- `엔진/ColorVision.Engine/템플릿/POI/TemplatePoi.cs`
- `엔진/ColorVision.Engine/템플릿/POI/PoiParam.cs`
- `엔진/ColorVision.Engine/템플릿/POI/PoiPoint.cs`
- `엔진/ColorVision.Engine/템플릿/POI/AlgorithmImp/AlgorithmPOI.cs`
- `엔진/ColorVision.Engine/템플릿/POI/BuildPoi/AlgorithmBuildPoi.cs`
- `엔진/ColorVision.Engine/템플릿/POI/POIFilters/TemplatePoiFilterParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIRevise/TemplatePoiReviseParam.cs`
- `엔진/ColorVision.Engine/템플릿/POI/POIOutput/TemplatePoiOutputParam.cs`
- `엔진/ColorVision.Engine/템플릿/POI/POIGenCali/TemplatePoiGenCalParam.cs`
- `엔진/ColorVision.Engine/템플릿/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 현재 메인체인을 실행하는 방법

### 기본 템플릿 및 데이터 모델

'TemplatePoi'가 정문입니다. 현재 몇 가지 중요한 구현 기능이 있습니다.

- `ITemplate<PoiParam>` 상속
- `IsSideHide = true`
- 템플릿 코드는 'POI'로 고정됩니다.
- 목록 항목을 두 번 클릭하면 `EditPoiParam`을 직접 엽니다.

많은 일반 템플릿과 달리 기본 POI 템플릿은 오른쪽의 'PropertyGrid'에만 의존하지 않고 자체 편집 창이 있습니다.

PoiParam은 몇 가지 값만 저장하는 단순한 매개변수 클래스가 아닙니다. 현재 다음을 호스팅합니다.

- 템플릿 크기 `너비`, `높이`
- 네 모서리 좌표 `LeftTopX/Y`, `RightTopX/Y`, `RightBottomX/Y`, `LeftBottomX/Y`
- `CfgJson`과 `PoiConfig` 간의 양방향 변환
- `ObservableCollection<PoiPoint> PoiPoints`

'PoiPoint' 자체는 현재 시스템에서 실제로 사용되는 포인트 정보를 저장합니다.

- `이드`
- `이름`
- `포인트 유형`
- `PixX`, `PixY`
- `PixWidth`, `PixHeight`

따라서 현재 POI 템플릿은 "점 세트 템플릿 + 구성 템플릿"의 조합에 더 가깝습니다.

### 현재 지속 방법

POI 메인 템플릿은 일반 `ModMasterModel`/`ModDetailModel`의 기본 경로가 아닙니다. 현재는 전용 테이블을 사용합니다.

- `포이마스터다오`
- `포이디테일다오`

`PoiParam.LoadPoiDetailFromDB(...)`는 포인트 세부정보를 `PoiPoints`로 다시 로드합니다. 확장 메서드 `Save2DB(...)`는 다음을 수행합니다.

- 마스터 레코드 저장
- 기존 포인트 내역 삭제
- BulkCopy를 사용하여 `PoiDetailModel` 전체 세트를 다시 작성합니다.

이는 또한 POI 페이지가 편향되기 가장 쉬운 위치 중 하나입니다. "일반 템플릿 테이블의 일반 세부 항목 집합"이 아니라 자체 포인트 테이블입니다.

### 가져오기, 복사 및 만들기

'TemplatePoi'는 현재 다음을 지원합니다.

- 현재 템플릿에서 JSON의 임시 복사본으로 복사
- `.cfg`에서 점 세트 템플릿 가져오기
- 내보내기 전에 포인트 세부정보를 적극적으로 로드합니다.
- 생성 시 가져온 복사본이나 빈 템플릿을 데이터베이스에 다시 씁니다.

또한 복사하거나 가져온 후에는 이전 기본 키를 직접 재사용하지 않도록 템플릿 'Id'와 각 포인트의 'Id'가 '-1'로 재설정됩니다.

### 런타임 알고리즘 체인

'AlgorithmPoi'는 현재 메인 POI 실행 입구입니다. 다음을 담당합니다.

- POI 메인 템플릿 편집창 열기
- 필터링, 수정, 출력 템플릿 편집 창 열기
- 파일 모드에서 외부 포인트 파일 선택
- `Event_POI_GetData`의 MQTT 매개변수를 조합합니다.

현재 전송된 매개변수에는 기본 템플릿뿐만 아니라 다음도 포함될 수 있습니다.

- `필터 템플릿`
- `템플릿 수정`
- `출력 템플릿`
- `POIStorageType`
- `POIPoint파일 이름`
- `IsSubPixel`
-`IsCCTWave`

이는 POI 실행 체인이 현재 단독으로 실행되는 단일 템플릿이 아닌 "다중 템플릿 조합 요청"임을 보여줍니다.

### 포인트 레이아웃 및 그에 따른 템플릿

'AlgorithmBuildPoi'는 또 다른 중요한 체인입니다. 현재 다음을 담당하고 있습니다.

- 포인트 레이아웃 템플릿 `TemplateBuildPoi`를 엽니다.
- CAD 파일의 선택적 로딩
- `POIBuildType == CADMapping`인 경우 4점 다각형 및 `CADMappingParam` 사용
- `Event_Build_POI` 게시

이 외에도 POI 제품군에는 현재 여러 동반 템플릿이 포함되어 있습니다.

- `TemplatePoiFilterParam`: 필터 템플릿, `Code = POIFilter`, 사용자 정의 편집 컨트롤 사용
- `TemplatePoiReviseParam`: 올바른 템플릿, `Code = PoiRevise`
- `TemplatePoiGenCalParam`: 올바른 교정 템플릿, `Code = POIGenCali`, 사용자 정의 편집 컨트롤 사용
- `TemplatePoiOutputParam`: 출력 템플릿, `Code = PoiOutput`, 사용자 정의 편집 컨트롤 사용

이러한 템플릿은 주석의 "선택적 확장"이 아니라 현재 흐름 및 알고리즘 체인에서 실제로 참조되는 개체입니다.

### Flow와 기타 알고리즘은 POI를 어떻게 소비하나요?

POI는 이제 단일 알고리즘 개인 템플릿이 아닌 공유 기본 요소입니다. 현재 최소한 세 가지 명확한 소비 경로가 있습니다.1. `POINodeConfigurators`는 `TemplatePoi`, 필터링, 수정, 출력, 보정 및 기타 템플릿을 POI 노드 속성 패널에 걸어 놓습니다.
2. `AlgorithmPoiAnalytic`에는 JSON 분석 템플릿 외에도 `POITemplateParam`이 계속 제공됩니다.
3. AlgorithmSFRFindROI`, AlgorithmOLEDAOI` 및 기타 알고리즘도 `TemplatePoi`를 추가로 참조합니다.

## 현재 가장 흔히 저지르는 실수 중 일부는 다음과 같습니다.

### POI는 별도의 알고리즘이 아닙니다.

현재 창고의 POI는 포인트를 생성하고, 포인트를 필터링하고, 다른 알고리즘에서 사용할 수 있는 공유 포인트 세트 템플릿 시스템과 비슷합니다.

### 메인스토리지는 일반적인 디테일 테이블이 아닙니다

메인 템플릿은 `PoiMasterDao`와 `PoiDetailDao`를 사용합니다. 일반 템플릿 표에 따라 계속 설명하다 보면 세부 수준을 놓치게 됩니다.

### 메인 편집기는 순수한 `PropertyGrid`가 아닙니다.

`TemplatePoi`는 두 번 클릭한 후 `EditPoiParam`을 입력합니다. 필터링 및 출력 템플릿에는 자체 `UserControl` 편집기도 있습니다. 계속 통합된 오른쪽 속성 패널로 작성하면 실제 인터페이스와 일치하지 않게 됩니다.

### 파일 모드와 데이터베이스 모드가 공존합니다.

`AlgorithmPoi`는 `POIStorageModel.Db`와 `POIStorageModel.File` 두 경로를 명시적으로 지원합니다. 문서에서는 더 이상 POI를 "데이터베이스에만 존재함"으로 쓸 수 없습니다.

## 추천읽기순서

1. `엔진/ColorVision.Engine/템플릿/POI/TemplatePoi.cs`
2. `엔진/ColorVision.Engine/템플릿/POI/PoiParam.cs`
3. `엔진/ColorVision.Engine/템플릿/POI/AlgorithmImp/AlgorithmPOI.cs`
4. `엔진/ColorVision.Engine/템플릿/POI/BuildPoi/AlgorithmBuildPoi.cs`
5. `엔진/ColorVision.Engine/템플릿/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 계속 읽기

- [POI 프리미티브](../primitives/poi.md)
- [JSON 템플릿](./json-templates.md)
- [프로세스 엔진](./flow-engine.md)