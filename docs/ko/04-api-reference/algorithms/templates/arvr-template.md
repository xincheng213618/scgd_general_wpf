# ARVR 템플릿

이 페이지에서는 현재 창고에서 실제로 볼 수 있는 ARVR 템플릿 계열에 대해서만 설명하고 있으며 "광학 알고리즘 교과서 + 통합 매개변수 매뉴얼"의 이전 초안은 더 이상 유지되지 않습니다.

## 이 템플릿 계열은 현재 무엇을 하고 있나요?

현재 소스 코드 상태에 따르면 ARVR은 단일 템플릿이 아니라 병렬로 존재하는 템플릿 및 디스플레이 알고리즘 세트입니다.

- `MTF`
-`SFR`
- 'FOV'
- `왜곡`
- `유령`

이러한 구현은 동일한 호스트 프레임워크를 공유하지만 매개변수 모델, 결과 성능 및 POI 의존 여부는 통합되지 않습니다. Flow 노드로 더 나아가면 `SFR_FindROI`와 같은 템플릿과 같은 JSON 변형도 혼합할 것입니다.

따라서 이 페이지는 보편적인 매개변수 테이블을 유지하려고 하기보다는 "ARVR 패밀리 맵"으로 더 적합합니다.

## 현재 가장 중요한 파일

- `엔진/ColorVision.Engine/템플릿/ARVR/MTF/TemplateMTF.cs`
- `엔진/ColorVision.Engine/템플릿/ARVR/MTF/MTFParam.cs`
- `엔진/ColorVision.Engine/템플릿/ARVR/MTF/AlgorithmMTF.cs`
- `엔진/ColorVision.Engine/템플릿/ARVR/MTF/ViewHandleMTF.cs`
- `엔진/ColorVision.Engine/템플릿/ARVR/SFR/SFRParam.cs`
- `엔진/ColorVision.Engine/템플릿/ARVR/SFR/AlgorithmSFR.cs`
- `엔진/ColorVision.Engine/템플릿/ARVR/SFR/WindowSFR.xaml.cs`
- `엔진/ColorVision.Engine/템플릿/ARVR/FOV/FOVParam.cs`
- `엔진/ColorVision.Engine/템플릿/ARVR/FOV/AlgorithmFOV.cs`
- `엔진/ColorVision.Engine/템플릿/ARVR/FOV/DisplayFOV.xaml.cs`
- `엔진/ColorVision.Engine/Templates/ARVR/Distortion/DistortionParam.cs`
- `엔진/ColorVision.Engine/Templates/ARVR/Distortion/AlgorithmDistortion.cs`
- `엔진/ColorVision.Engine/Templates/ARVR/Distortion/ViewResultDistortion.cs`
- `엔진/ColorVision.Engine/템플릿/ARVR/Ghost/GhostParam.cs`
- `엔진/ColorVision.Engine/템플릿/ARVR/Ghost/AlgorithmGhost.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs`
- `엔진/FlowEngineLib/알고리즘/AlgorithmARVRNode.cs`

## 현재 메인체인을 분할하는 방법

###기동특무부대

`TemplateMTF`는 현재 다음과 같은 고전적인 매개변수 템플릿입니다.

- `코드 = MTF`
- `TemplateDicId = 8`

현재 'MTFParam'에서 가장 직접적으로 표시되는 매개변수는 다음과 같습니다.

- `MTF_dRatio`
-`eEvaFunc`
-`dx`
- `디`
-`ksize`

`AlgorithmMTF`의 실제 동작은 기본 그래프가 아니지만 다음과 같습니다.

- `TemplateMTF` 열기
- `TemplatePoi` 열기
- `POITemplateParam` 어셈블
- `Event_MTF_GetData` 게시

이는 현재 MTF 실행 체인이 POI와 독립적으로 존재하는 것이 아니라 POI 템플릿에 명시적으로 의존한다는 것을 보여줍니다.

결과 측면에서 가장 흥미로운 점은 매개변수 클래스가 아니라 'ViewHandleMTF'입니다. 그것은:

- 결과를 CSV로 내보내기
- 통계적 최대, 최소, 평균, 분산 및 균일성
- `ViewResultAlgType.MTF`의 핸들러로 UI에 액세스합니다.

### SFR

`SFRParam`은 현재 이전 문서보다 훨씬 간단하며, 직접적으로 볼 수 있는 유일한 핵심 매개변수는 `Gamma`입니다. 실제 디스플레이 및 결과 상호 작용은 다음과 같습니다.

-`알고리즘SFR`
-`창SFR`

`AlgorithmSFR`은 MTF와 동일하며 추가적으로 `TemplatePoi`가 필요하고 `Event_SFR_GetData`를 게시합니다. 'WindowSFR'은 결과의 'Pd주파수' 및 'PdomainSamplingData'를 곡선으로 역직렬화하고 임계값 및 주파수 변환을 제공하는 역할을 합니다.

따라서 현재 SFR 문서는 더 이상 템플릿 매개변수에 대해서만 설명할 수 없을 뿐만 아니라 결과 창도 포함합니다.

### 시야

'FOVParam'은 현재 다음을 직접 포함하는 보다 완전한 파라메트릭 모델입니다.

- '라디오'
-`CameraDegrees`
- 'ThresholdValus'
-`DFovDist`
- `Fov패턴`
- `FovType`
- `Xc`, `Yc`, `Xp`, `Yp`

`AlgorithmFOV`는 `Event_FOV_GetData` 패키징을 담당하는 반면, `DisplayFOV`는 현재 매우 실용적인 작업 계층을 담당합니다.

- 서비스 관리자로부터 이미지 소스 장치 받기
- 배치, 원본 파일, 로컬 이미지의 세 가지 입력 지원
- Raw 파일 목록을 끌어와 직접 열기 허용

이는 FOV가 현재 “단지 매개변수를 구성한 후 알고리즘을 실행하는” 최소한의 템플릿이 아님을 보여줍니다.

### 왜곡

`DistortionParam`은 현재 다음과 같은 여러 블롭 임계값 세트, 영역 필터링, 모양 필터링 및 전역 정책 항목을 포함하는 실제 대형 매개변수 개체입니다.

- `filterByColor`
- `minThreshold` / `maxThreshold`
- `minArea` / `maxArea`
-`filterByCircularity`
- `filterByConvexity`
-`filterByInertia`
- `코너 유형`
- `경사 유형`
- `레이아웃 유형`
- `왜곡 유형`

'AlgorithmDistortion'은 'Distortion' 이벤트 게시를 담당하고, 'ViewResultDistortion'은 열거 값과 최종 격자 결과를 표시 가능한 설명 개체에 다시 매핑합니다.

###유령

현재 보이는 `GhostParam`의 핵심 매개변수는 복잡하지 않으며 주로 격자 감지에 중점을 둡니다.-`고스트_반경`
-`Ghost_cols`
-`Ghost_rows`
-`고스트_비율H`
- `고스트_비율L`

`AlgorithmGhost`는 추가 `Color` 매개변수와 함께 제공되고 `Ghost` 이벤트를 게시합니다. 즉, 색상 채널은 현재 페이지 주석의 추가 항목이 아닌 Ghost 체인의 일류 입력입니다.

## 흐름에 액세스하는 방법은 무엇입니까?

'AlgorithmARVRnode' 및 'AlgorithmNodeConfigurators'는 Flow에서 현재 ARVR 제품군의 실제 사용법을 공동으로 보여줍니다.

- `MTF` 및 `SFR` 노드에는 매개변수 템플릿과 `POI` 템플릿이 모두 필요합니다.
- 'FOV' 및 'Distortion' 노드는 클래식 매개변수 템플릿과 JSON 변형 모두에 연결할 수 있습니다.
- `SFR_FindROI` 이 유형의 분기는 `TemplateSFRFindROI` 및 `TemplatePoi`에 모두 연결됩니다.

따라서 현재 ARVR 제품군은 평면 디렉터리가 아니라 기존 템플릿, JSON 템플릿, POI 템플릿 및 Flow 노드로 구성된 실행 표면입니다.

## 현재 가장 흔히 저지르는 실수 중 일부는 다음과 같습니다.

### ARVR은 통합 스키마가 아닙니다.

각 하위 디렉터리는 동일한 매개변수 필드 집합이 아닌 템플릿 호스트 및 디스플레이 알고리즘 스타일을 공유합니다.

### 대부분의 알고리즘 클래스는 호스트 및 명령 어댑터입니다.

`AlgorithmMTF`, `AlgorithmSFR`, `AlgorithmFOV`, `AlgorithmDistortion`, `AlgorithmGhost`는 로컬에서 수치 계산을 직접 완료하는 대신 창 열기, 입력 가져오기, MQTT 요청 생성을 주로 담당합니다.

### POI는 ARVR에 남은 것이 아닙니다

적어도 MTF, SFR, SFR_FindROI는 현재 모두 명시적으로 `TemplatePoi`에 의존합니다. POI가 생략되면 이 페이지에서는 현재 실행 체인을 설명하지 않습니다.

### 결과 처리 코드도 중요합니다

'ViewHandleMTF', 'WindowSFR' 및 'ViewResultDistortion'과 같은 결과 레이어 구현은 사용자가 궁극적으로 보는 내용을 이해하는 데 중요한 입구이며 이전 문서에서 생략해서는 안 됩니다.

## 추천읽기순서

1. `엔진/ColorVision.Engine/템플릿/ARVR/MTF/AlgorithmMTF.cs`
2. `엔진/ColorVision.Engine/템플릿/ARVR/SFR/AlgorithmSFR.cs`
3. `엔진/ColorVision.Engine/템플릿/ARVR/FOV/DisplayFOV.xaml.cs`
4. `엔진/ColorVision.Engine/Templates/ARVR/Distortion/ViewResultDistortion.cs`
5. `엔진/ColorVision.Engine/템플릿/Flow/NodeConfigurator/AlgorithmNodeConfigurators.cs`

## 계속 읽기

- [POI 템플릿](./poi-template.md)
- [JSON 템플릿](./json-templates.md)
- [프로세스 엔진](./flow-engine.md)