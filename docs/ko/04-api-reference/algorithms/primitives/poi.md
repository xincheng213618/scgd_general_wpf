# POI

이 페이지에서는 현재 웨어하우스에 공유 기본 형식으로 존재하는 POI에 대해서만 설명하고 더 이상 이전 "POI 감지 알고리즘 백과사전" 스타일 초안을 유지하지 않습니다.

## 먼저 현재 시스템에서 POI가 어떤 역할을 하는지 살펴보겠습니다.

현재 소스 코드 상태에 따르면 POI는 단일 알고리즘 결과라기보다는 재사용 가능한 데이터 및 템플릿 시스템 세트에 가깝습니다.

- 마스터 템플릿은 포인트 세트와 구성을 저장합니다.
- 포인트 배치, 필터링, 수정, 교정 및 출력은 이 포인트 세트를 중심으로 작동합니다.
- JSON 알고리즘과 ARVR 알고리즘은 POI 템플릿을 계속 참조합니다.
- 흐름 노드는 POI를 공유 입력 및 출력 객체로 처리합니다.

따라서 이 페이지의 초점은 "특징점을 찾는 방법"이 아니라 POI 프리미티브가 현재 어떻게 저장, 전송 및 소비되는지입니다.

## 현재 가장 중요한 파일

- `엔진/ColorVision.Engine/템플릿/POI/PoiPoint.cs`
- `엔진/ColorVision.Engine/템플릿/POI/PoiParam.cs`
- `엔진/ColorVision.Engine/템플릿/POI/TemplatePoi.cs`
- `엔진/ColorVision.Engine/템플릿/POI/AlgorithmImp/AlgorithmPOI.cs`
- `엔진/ColorVision.Engine/템플릿/POI/BuildPoi/AlgorithmBuildPoi.cs`
- `엔진/ColorVision.Engine/템플릿/POI/POIFilters/TemplatePoiFilterParam.cs`
- `Engine/ColorVision.Engine/Templates/POI/POIRevise/TemplatePoiReviseParam.cs`
- `엔진/ColorVision.Engine/템플릿/POI/POIOutput/TemplatePoiOutputParam.cs`
- `엔진/ColorVision.Engine/템플릿/POI/POIGenCali/TemplatePoiGenCalParam.cs`
- `엔진/ColorVision.Engine/템플릿/Jsons/PoiAnalytic/AlgorithmPoiAnalytic.cs`
- `엔진/ColorVision.Engine/템플릿/Jsons/SFRFindROI/AlgorithmSFRFindROI.cs`
- `엔진/ColorVision.Engine/템플릿/Jsons/OLEDAOI/AlgorithmOLEDAOI.cs`
- `엔진/ColorVision.Engine/템플릿/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 현재 데이터는 어떤 모습인가요?

### 포인트 객체

이제 'PoiPoint'는 매우 간단한 표시 및 위치 지정 필드 세트를 저장합니다.

- `이드`
- `이름`
- `포인트 유형`
- `PixX`, `PixY`
- `PixWidth`, `PixHeight`

추상적인 "관심 지점 인터페이스"가 아니라 현재 이미지 편집 및 결과 표시에 필요한 분야에 가까운 구체적인 개체입니다.

### 템플릿 객체

'PoiParam'은 포인트 세트, 치수, 모서리 및 구성을 템플릿으로 패키징하는 역할을 합니다. 현재 최소한 다음이 포함되어 있습니다.

- 템플릿 크기 `너비`, `높이`
- 템플릿 유형 `유형`
- 네 모서리 좌표
- `포이포인트`
- `CfgJson` 및 `PoiConfig`

게다가 `CfgJson`은 단순한 문자열 캐시가 아니라 현재 `PoiConfig`를 사용하여 직렬화 및 역직렬화합니다.

## 현재 보관방법

POI의 핵심 현실은 현재 자체 전용 마스터-슬레이브 데이터 구조를 가지고 있다는 것입니다.

- 마스터 기록은 `PoiMasterDao`를 통해 저장됩니다.
- 포인트 세부정보는 `PoiDetailDao`를 통해 저장됩니다.

`PoiParam.LoadPoiDetailFromDB(...)`는 `Pid`로 포인트 컬렉션을 다시 채웁니다. 확장 메서드 `Save2DB(...)`는 이전 세부 정보를 지우고 새 포인트를 일괄적으로 작성합니다.

이로 인해 POI는 'ModMasterModel'/'ModDetailModel'에만 의존하는 일반 템플릿과 크게 다릅니다.

## 현재 실행 중인 체인에서 POI를 어떻게 소비하나요?

### 주요 POI 알고리즘

'AlgorithmPoi'는 가장 직접적인 POI 소비자이자 생산자입니다. 현재 다음을 지원합니다.

- 메인 템플릿 `TemplatePoi`
- 필터 템플릿 `TemplatePoiFilterParam`
- 수정된 템플릿 `TemplatePoiReviseParam`
- 출력 템플릿 `TemplatePoiOutputParam`
- 파일 모드 `POIStorageModel.File`

마지막으로 `Event_POI_GetData`를 통해 여러 템플릿 매개변수가 포함된 MQTT 요청을 발행합니다.

### 포인트 배치 알고리즘

'AlgorithmBuildPoi'는 다른 정보를 POI 포인트 세트로 변환하는 역할을 담당합니다. 현재 다음을 지원합니다.

- 일반 레이아웃
- CADMapping 포인트 레이아웃
- 4점 다각형 `LayoutPolygon`
-`CADMappingParam`
- `이벤트_빌드_POI`

따라서 현재 시스템에서 "POI를 얻는 것"은 탐지에만 의존하는 것이 아니라 구성에도 의존합니다.

### 다운스트림 알고리즘 참조

이제 POI는 여러 다른 알고리즘 체인에서 사용되었습니다.

- `AlgorithmPoiAnalytic`은 `POITemplateParam`과 함께 제공됩니다.
- `AlgorithmSFRFindROI`는 `POITemplateParam`과 함께 제공됩니다.
- 'AlgorithmOLEDAOI'에는 'POITemplateParam'도 함께 제공됩니다.

따라서 POI는 현재 다른 알고리즘의 입력 형식 중 하나이며 결과 페이지 끝에 표시되는 보조 개체가 아닙니다.

### 흐름 노드 참조

'POINodeConfigurators'는 POI가 Flow에서 공유 노드 리소스가 되었음을 설명합니다.

- `POINode`에는 기본 템플릿, 필터링, 수정 및 출력 템플릿이 필요합니다.
- 'BuildPOINode'는 포인트 템플릿, 후기입 POI 템플릿 및 레이아웃 ROI 템플릿을 동시에 수신합니다.
- 'POIReviseNode'가 교정 교정 템플릿에 연결됩니다.
- `POIAnalyticNode`는 JSON 분석 템플릿에 연결됩니다.

이는 또한 현재 POI가 프로세스 설계 단계에서 선택해야 하는 핵심 프리미티브임을 보여줍니다.

## 현재 가장 흔히 저지르는 실수 중 일부는 다음과 같습니다.

### POI는 단일 탐지 알고리즘의 결과 구조가 아닙니다.

현재 감지, 레이아웃, 분석, AOI 및 Flow 노드에 동시에 사용되며 공유 데이터 템플릿 세트입니다.

### 스토리지는 단순한 데이터베이스도 아니고 파일도 아닙니다.

기본 템플릿은 데이터베이스를 사용하지만 `AlgorithmPoi`는 파일 모드와 외부 포인트 파일도 명시적으로 지원합니다.

### 동반 템플릿은 현재 시스템의 최고 수준 구성원입니다.

필터링, 수정, 보정 및 출력 템플릿은 모두 실제 구현 및 편집 입구가 있으며 주석의 "향후 확장"이 아닙니다.

### 일부 알고리즘은 POI를 생성하는 대신 POI를 소비합니다.`PoiAnalytic`, `SFR_FindROI`, `OLEDAOI`와 같은 체인은 기본적으로 기존 POI 템플릿을 읽고 사용합니다.

## 추천읽기순서

1. `엔진/ColorVision.Engine/템플릿/POI/PoiPoint.cs`
2. `엔진/ColorVision.Engine/템플릿/POI/PoiParam.cs`
3. `엔진/ColorVision.Engine/템플릿/POI/AlgorithmImp/AlgorithmPOI.cs`
4. `엔진/ColorVision.Engine/템플릿/POI/BuildPoi/AlgorithmBuildPoi.cs`
5. `엔진/ColorVision.Engine/템플릿/Flow/NodeConfigurator/POINodeConfigurators.cs`

## 계속 읽기

- [POI 템플릿](../templates/poi-template.md)
- [JSON 템플릿](../templates/json-templates.md)
- [프로세스 엔진](../templates/flow-engine.md)