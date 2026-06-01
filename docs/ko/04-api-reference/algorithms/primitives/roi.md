#ROI

이 페이지에서는 현재 창고에 실제로 존재하는 ROI 관련 프리미티브에 대해서만 설명하고 있으며, "통합 ROI 모듈 설계 도면"의 이전 초안은 더 이상 유지되지 않습니다.

## 먼저 현재 창고의 ROI가 실제로 몇 개의 지점으로 나누어져 있는지 살펴보겠습니다.

현재 소스 코드 상태에 따르면 ROI는 별도의 디렉터리에 있는 통합 라이브러리가 아니지만 적어도 세 가지 관련 분기가 있습니다.

1. 'Templates/FindLightArea'에 있는 클래식 발광 영역 위치 지정 템플릿
2. `Templates/Jsons/ImageROI`에 있는 이미지 자르기 JSON 템플릿
3. `Templates/Jsons/SFRFindROI`에 있는 ARVR의 `SFR_FindROI` JSON 템플릿

따라서 이 페이지는 "글로벌 ROI 추상 클래스 설명"이라기보다는 "ROI 포털 맵"에 가깝습니다.

## 현재 가장 중요한 파일

- `엔진/ColorVision.Engine/템플릿/FindLightArea/TemplateRoi.cs`
- `엔진/ColorVision.Engine/템플릿/FindLightArea/ROIParam.cs`
- `엔진/ColorVision.Engine/템플릿/FindLightArea/AlgorithmRoi.cs`
- `엔진/ColorVision.Engine/템플릿/FindLightArea/DisplayRoi.xaml.cs`
- `엔진/ColorVision.Engine/템플릿/Jsons/ImageROI/TemplateImageROI.cs`
- `엔진/ColorVision.Engine/템플릿/Jsons/ImageROI/AlgorithmImageROI.cs`
- `엔진/ColorVision.Engine/템플릿/Jsons/SRFFindROI/TemplateSFRFindROI.cs`
- `엔진/ColorVision.Engine/템플릿/Jsons/SFRFindROI/AlgorithmSFRFindROI.cs`

## 현재 클래식 ROI 체인은 어떤 모습인가요?

### 템플릿 항목

현재의 클래식 ROI는 실제로 이전 문서에 작성된 '템플릿/ROI'가 아닌 'FindLightArea' 코드 그룹에 속합니다.

`TemplateRoi`의 구현 특성은 매우 명확합니다.

- `이름 = FindLightArea`
- `코드 = FindLightArea`
- `TemplateDicId = 31`
- `GetMysqlCommand()`를 통해 `MysqlRoi`를 반환합니다.

따라서 이 체인은 현재 본질적으로 전체 시스템에 대한 통합 ROI 정의가 아닌 "광 영역 위치 지정 템플릿"입니다.

### 파라메트릭 모델

`RoiParam`은 현재 매우 간단하며 세 가지 매개변수만 노출합니다.

- '임계값'
- '타임즈'
- '부드러운 크기'

이는 이전 초안의 일반 직사각형 ROI 또는 다각형 ROI API와는 다릅니다. 이는 추상적인 기하학적 객체라기보다는 특정 알고리즘에 대한 임계값 템플릿과 더 유사합니다.

### 실행 및 UI

'AlgorithmRoi'는 다음을 담당합니다.

- `TemplateRoi` 편집창을 엽니다.
- 'DisplayRoi' 가져오기
- `Event_LightArea2_GetData` 요청 수집

'DisplayRoi'는 현재 실제 사용자 입력 프로세스를 담당합니다.

- 템플릿을 선택하세요
- 이미지 소스 서비스 선택
- 세 가지 입력 지원: 배치 번호, 원본 파일, 로컬 이미지
- Raw 파일 목록 가져오기 및 직접 열기 지원

이는 현재의 클래식 ROI가 별도의 도면 구성 요소가 아닌 "발광 영역 감지 알고리즘을 위한 프런트 엔드 호스트"에 더 가깝다는 것을 보여줍니다.

## 두 개의 JSON ROI 분기

### 이미지ROI

`TemplateImageROI`는 현재 JSON 템플릿 브랜치입니다.

- `코드 = Image.ROI`
- `TemplateDicId = 52`
- `IsUserControl = true`

'EditTemplateJson'을 통해 구조화된 클리핑 매개변수를 전달하고 'AlgorithmImageROI'는 'Image.ROI' 이벤트를 게시합니다.

이 체인은 고전적인 발광 영역 템플릿의 복제본이 아닌 이미지 자르기 구성에 관한 것입니다.

### SFR_ROI 찾기

`TemplateSFRFindROI`는 현재 JSON 템플릿 브랜치이기도 합니다.

- `코드 = ARVR.SFR.FindROI`
- `TemplateDicId = 36`
- `IsUserControl = true`

설명 텍스트에 `SfrRoiParam` 구조 힌트를 명확하게 제공합니다. JSON 템플릿 자체 외에도 `AlgorithmSFRFindROI`에는 추가 `POITemplateParam`이 함께 제공되고 `ARVR.SFR.FindROI`를 게시합니다.

이는 ARVR에서 "ROI 찾기"가 더 이상 단순한 ROI 템플릿이 아니라 ROI와 POI를 연결하는 알고리즘 체인임을 보여줍니다.

## 현재 가장 흔히 저지르는 실수 중 일부는 다음과 같습니다.

### ROI는 통합된 기본 라이브러리가 아닙니다.

현재 웨어하우스의 ROI 관련 구현은 클래식 매개변수 템플릿과 JSON 템플릿이라는 두 가지 경로로 분산되어 있습니다. 모든 시나리오를 담당하는 통합 'ROI' 루트 모듈은 없습니다.

### 현재 클래식 ROI는 주로 발광 영역의 위치 지정을 나타냅니다.

'FindLightArea'를 메인 앵커로 사용하지 않으면 이 페이지는 존재하지 않는 "유니버설 ROI SDK"로 쉽게 작성될 수 있습니다.

### JSON ROI와 클래식 ROI는 동일한 구성 모델 세트가 아닙니다.

`TemplateImageROI`와 `TemplateSFRFindROI`는 모두 JSON 템플릿 호스트인 반면 `TemplateRoi`는 전통적인 매개변수 템플릿입니다. 세 가지를 하나의 매개변수 테이블에 혼합할 수 없습니다.

### 일부 ROI 체인은 이미 POI에 바인딩되어 있습니다.

'AlgorithmSFRFindROI'에는 명시적으로 'TemplatePoi'가 필요합니다. 현재 ARVR 체인에서 ROI와 POI는 더 이상 완전히 별개의 개념적 레이어가 아닙니다.

## 추천읽기순서

1. `엔진/ColorVision.Engine/템플릿/FindLightArea/TemplateRoi.cs`
2. `엔진/ColorVision.Engine/템플릿/FindLightArea/AlgorithmRoi.cs`
3. `엔진/ColorVision.Engine/템플릿/FindLightArea/DisplayRoi.xaml.cs`
4. `엔진/ColorVision.Engine/템플릿/Jsons/ImageROI/TemplateImageROI.cs`
5. `엔진/ColorVision.Engine/템플릿/Jsons/SFRFindROI/TemplateSFRFindROI.cs`

## 계속 읽기- [POI 프리미티브](./poi.md)
- [POI 템플릿](../templates/poi-template.md)
- [ARVR 템플릿](../templates/arvr-template.md)