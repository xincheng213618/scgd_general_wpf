#유령탐지

이 페이지에서는 현재 웨어하우스의 실제 Ghost 감지 액세스 체인에 대해서만 설명하고 더 이상 "독립적인 '고스트 감지' 알고리즘 API" 스타일의 이전 초안을 유지하지 않습니다.

## 먼저 현재 페이지에서 실제로 무엇에 대해 이야기하고 있는지 살펴보겠습니다.

현재 소스 코드 상태에 따르면 고스트 감지는 독립적인 공개 알고리즘 패키지가 아니라 `ColorVision.Engine`에 있는 ARVR 템플릿 제품군의 분기입니다. 현재 다음 레이어로 구성됩니다.

- 고스트 매개변수 템플릿
- 고스트 알고리즘 UI 호스트
- 이미지 입력 및 색상 선택 인터페이스
- MQTT 명령 패키징
- 결과 로딩, 오버레이 표시, CSV 내보내기

따라서 이 페이지에서 실제로 이야기하고 싶은 것은 호스트와 독립적으로 존재하는 일련의 프로세스 API를 상상하기보다는 "Ghost가 기본 프로그램에서 호스팅되고 실행되는 방법"입니다.

## 현재 가장 중요한 파일

- `엔진/ColorVision.Engine/템플릿/ARVR/Ghost/TemplateGhost.cs`
- `엔진/ColorVision.Engine/템플릿/ARVR/Ghost/GhostParam.cs`
- `엔진/ColorVision.Engine/템플릿/ARVR/Ghost/AlgorithmGhost.cs`
- `엔진/ColorVision.Engine/템플릿/ARVR/Ghost/DisplayGhost.xaml.cs`
- `엔진/ColorVision.Engine/템플릿/ARVR/Ghost/ViewHandleGhost.cs`
- `엔진/ColorVision.Engine/템플릿/ARVR/Ghost/AlgResultGhostDao.cs`

현재 Ghost가 어떻게 구성되어 있는지, 명령을 보내는 방법, 결과를 표시하는 방법만 알고 싶다면 이 항목에서 이미 주요 사항을 다루었습니다.

## 현재 메인체인을 실행하는 방법

### 템플릿 항목

`TemplateGhost`는 Ghost의 매개변수 템플릿 항목입니다. 현재 구현은 매우 간단합니다.

- `ITemplate<GhostParam>` 상속
- `TemplateDicId = 7`
- `코드=유령`

이는 Ghost가 현재 JSON 템플릿이나 독립 구성 파일 체인 대신 기존의 강력한 유형 매개변수 템플릿 체인을 사용하고 있음을 보여줍니다.

### 파라메트릭 모델

'GhostParam'은 현재 이전 원고에 설정된 일반화된 임계값, 영역 및 형태학적 스위치가 아닌 고스트 격자 감지를 위한 매개변수 세트를 노출합니다. 현재 직접 볼 수 있는 핵심 필드는 다음과 같습니다.

-`고스트_반경`
-`Ghost_cols`
-`Ghost_rows`
-`고스트_비율H`
- `고스트_비율L`

필드 이름 지정 및 설명으로 판단하면 이 매개변수 세트는 이미지 결함 감지기에 대한 일반적인 매개변수 테이블보다는 "감지할 고스트 격자"의 기하학적 및 회색조 제약 조건에 더 편향되어 있습니다.

### 알고리즘 호스트

'AlgorithmGhost'는 현재 기본 이미지 처리 커널이 아니지만 'DisplayAlgorithmBase'에서 파생된 호스트 클래스입니다. 주로 다음을 담당합니다.

- `TemplateGhost` 편집창을 엽니다.
- `DisplayGhost` 사용자 컨트롤 제공
- 현재 색상 선택 `CVOLEDCOLOR` 유지
- 템플릿, 색상, 장치 정보 및 이미지 경로를 메시지에 담습니다.

결국 통합된 '유령 감지' 호출 인터페이스를 노출하는 대신 'Ghost'라는 이벤트 이름으로 메시지를 게시하게 됩니다.

### 입력 및 실행 인터페이스

'DisplayGhost'는 현재 사용자가 실제로 접촉하게 되는 실행 인터페이스입니다. 수행하는 작업은 이전 문서의 "입력 이미지 + 매개변수"보다 더 구체적입니다.

- `TemplateGhost.Params` 바인딩
- 세 가지 `CVOLEDCOLOR` 옵션 제공: `BLUE`, `GREEN`, `RED`
- `ServiceManager`에서 이미지 소스 장치 가져오기
- 세 가지 입력 경로 지원: 배치 번호, Raw/CIE 파일, 로컬 이미지
- 장치 측 Raw/CIE 파일 목록 새로 고침 허용
- 로컬로 또는 장치 측에서 이미지를 직접 열 수 있습니다.

따라서 현재 Ghost 실행 표면은 본질적으로 순수한 알고리즘 기능 입구가 아니라 장치 상호 작용 기능을 갖춘 WPF 패널입니다.

### MQTT 명령 체인

`AlgorithmGhost.SendCommand(...)`는 현재 다음 정보를 포함합니다:

- `Img파일 이름`
- `파일 유형`
- `디바이스 코드`
- `장치 유형`
- `TemplateParam`
- `색상`

그런 다음 `MsgSend`를 구성하고 `Ghost` 이벤트를 게시합니다.

이는 또한 현재 Ghost 계산의 실제 실행 끝이 이 UI 클래스 내부가 아니라 메시지 체인의 반대편에 있음을 보여줍니다.

## 현재 결과를 어떻게 처리하나요?

'ViewHandleGhost'는 현재 결과 표시 체인에서 가장 중요한 항목입니다. 다음을 담당합니다.

- `AlgResultGhostDao.Instance.GetAllByPid(...)`를 통해 결과 세부 정보를 로드합니다.
- 결과 목록을 `ViewResultAlg`에 다시 연결합니다.
- `GhostPixel` 및 `LedPixel`을 기반으로 이미지에 오버레이 지점을 그립니다.
- 왼쪽 목록에 `LEDCenters`, `LEDBlobGray`, `GhostAverageGray`를 표시합니다.
- CSV 내보내기

이전 초안의 "통합된 JSON 구조 반환"과 달리 현재 Ghost 결과는 주로 데이터베이스 결과 모델, 이미지 오버레이 및 목록 보기를 통해 표시됩니다.

## 현재 가장 흔히 저지르는 실수 중 일부는 다음과 같습니다.

### 독립형 공개 API가 아닙니다.

현재 고스트 감지는 분명히 ARVR 템플릿 계열의 일부이며 항목은 'Templates/ARVR/Ghost'에 있으며 일반적인 '유령 감지' 라이브러리가 아닙니다.

### 알고리즘 클래스는 로컬 컴퓨팅 커널이 아닙니다.

'AlgorithmGhost'는 현재 주로 창, 입력, 템플릿 및 메시지 어셈블리를 담당합니다. `Mat`을 직접 처리하는 기본 알고리즘 구현으로 작성하면 실제 코드와 일치하지 않습니다.

### 매개변수는 이전 초안보다 훨씬 더 좁습니다.

현재 `GhostParam`은 격자 반경, 행과 열의 수, 회색조 비율의 상한 및 하한을 노출합니다. 이전 문서에는 완전한 임계값/면적/형태학 테이블 세트가 없습니다.

### 결과 표시는 UI 및 결과 프로세서에 따라 다릅니다.

실제 출력 체인은 샘플 JSON을 반환하는 단일 호출이 아니라 `ViewHandleGhost` + 결과 DAO + 이미지 오버레이입니다.

## 추천읽기순서1. `엔진/ColorVision.Engine/템플릿/ARVR/Ghost/TemplateGhost.cs`
2. `엔진/ColorVision.Engine/템플릿/ARVR/Ghost/GhostParam.cs`
3. `엔진/ColorVision.Engine/템플릿/ARVR/Ghost/AlgorithmGhost.cs`
4. `엔진/ColorVision.Engine/템플릿/ARVR/Ghost/DisplayGhost.xaml.cs`
5. `엔진/ColorVision.Engine/템플릿/ARVR/Ghost/ViewHandleGhost.cs`

## 继续阅读

- [ARVR 模板](../templates/arvr-template.md)
- [算法系统概览](../overview.md)
- [ColorVision.Engine](../../엔진-컴포넌트/ColorVision.Engine.md)