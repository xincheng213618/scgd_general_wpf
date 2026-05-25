# 알고리즘 시스템 개요

이 페이지에서는 현재 웨어하우스에서 실제로 실행 중인 템플릿 및 알고리즘 액세스 링크에 대해서만 설명하며 "알고리즘 분류 백과사전 + 샘플 코드 + GPU 기능 일반 개요"의 이전 초안을 더 이상 유지하지 않습니다.

## 먼저 이 시스템이 실제로 어디에 속하는지 살펴보겠습니다.

현재 "알고리즘"과 가장 직접적으로 관련된 코드는 하나의 디렉토리에만 있는 것이 아닙니다.

- `Engine/ColorVision.Engine/Templates/`: 템플릿 정의, 템플릿 관리, 템플릿 편집 및 대부분의 비즈니스 알고리즘 UI 액세스 포인트.
- `Engine/FlowEngineLib/`: 프로세스 노드, 시작/끝 체인 및 실행 제어.
- `Engine/ColorVision.Engine/Services/Devices/Algorithm/`: 알고리즘 장치 서비스 접근 인터페이스입니다.
- `Engine/cvColorVision/` 및 하위 수준 기본 라이브러리: 실제 기본 계산 및 상호 운용성을 일부 수행합니다.

따라서 이 장을 "관리되는 알고리즘 함수 디렉터리"로만 이해한다면 현재 구현에서 직접적으로 벗어나게 됩니다.

## 현재 메인 체인은 어떻게 연결되어 있나요?

현재 상황으로 판단하면 가장 일반적으로 실행되는 알고리즘/템플릿 체인은 대략 다음과 같습니다.

1. 'TemplateContorl'은 로드된 어셈블리의 'IITemplateLoad' 구현을 검색하고 템플릿을 시스템에 등록합니다.
2. `TemplateManagerWindow` 및 `TemplateEditorWindow`는 사용자가 템플릿을 탐색, 생성 및 편집할 수 있도록 하는 역할을 합니다.
3. 특정 비즈니스 알고리즘을 위한 UI 클래스는 일반적으로 'DisplayAlgorithmBase'를 상속하고 'OpenTemplateCommand' 클래스 항목을 노출합니다.
4. 이러한 알고리즘 UI는 `CVTemplateParam`, 파일 경로, 장치 정보 및 기타 매개변수를 `SendCommand(...)`에 조합합니다.
5. 그런 다음 매개변수는 `MQTTAlgorithm` 또는 인접 서비스 체인을 통해 실제 실행 끝으로 전송됩니다.
6. 프로세스 템플릿인 경우 `TemplateFlow` + `FlowEngineToolWindow` + `FlowEngineLib`의 실행 체인에 들어갑니다.

이는 `Templates/*/Algorithm*.cs`에서 볼 수 있는 많은 클래스가 현재 최종 연산자 자체보다 "알고리즘 프런트 엔드 어댑터"에 더 가까운 책임을 갖고 있음을 의미합니다.

## 현재 템플릿 시스템에서 가장 중요한 부분

### 템플릿 등록 및 관리

이 부분의 핵심 초점은 다음과 같습니다.

-`ITemplate.cs`
- `TemplateContorl.cs`
- `TemplateManagerWindow.xaml(.cs)`
- `TemplateEditorWindow.xaml(.cs)`

템플릿이 표시되는 방식, 템플릿이 열리는 방식, 편집 프로세스에 들어가는 방식을 결정합니다.

### 흐름 템플릿

'템플릿/흐름/'은 일반 매개변수 템플릿의 단순한 분기가 아니라 흐름도, 프로세스 편집 창, 가져오기 및 내보내기, 일괄 실행을 연결하는 특수 템플릿 제품군입니다.

현재 주요 입구는 다음과 같습니다:

- `TemplateFlow.cs`
- `FlowEngineToolWindow.xaml(.cs)`
- `DisplayFlow.xaml(.cs)`

### JSON 템플릿

`Templates/Jsons/`는 현재 JSON 구성을 핵심으로 하는 일괄 템플릿 구현을 수행하고 있습니다. 일반적인 링크는 주로 다음과 같습니다.

- `ITemplateJson<T>`: 일반 논리를 로드, 저장, 가져오기 및 내보내기합니다.
- `TemplateJsonParam`: JSON 템플릿 매개변수 기본 유형입니다.
- `EditTemplateJson.xaml(.cs)`: 텍스트 편집 및 속성 편집 전환을 지원하는 듀얼 모드 편집 제어입니다.

이것이 바로 템플릿 시스템에 기존 매개변수 개체와 JSON 텍스트 편집기가 모두 표시되는 이유입니다.

### 비즈니스 템플릿 계열

여전히 직접적으로 표시되는 기본 템플릿 제품군은 다음과 같습니다.

-`POI/`
-`ARVR/`
-`JND/`
- `LedCheck/`
- `규정 준수/`
- `Jsons/`에서 여러 비즈니스 템플릿 구현

이러한 디렉토리는 동시에 동일한 규칙에 따라 설계되지 않았습니다. 읽을 때 정확히 동일한 수준의 추상화를 가져야 한다고 가정하지 마십시오.

## 현재 가장 오해하기 쉬운 몇 가지 사항

### 오해 1: `Algorithm*.cs`를 최종 알고리즘 구현으로 취급

이러한 클래스 중 현재 수행되는 작업은 다음과 같습니다.

- 템플릿 편집창 열기
- UI측 선택상태 유지
- 메시지 매개변수 조합
- `PublishAsyncClient(...)` 호출

실제 하위 수준 처리는 장치 서버, MQTT 피어, 기본 라이브러리 또는 기타 링크에서 수행되는 경우가 많습니다.

### 오해 2: `POI`가 단지 독립적인 작은 주제일 뿐이라고 생각하는 것

현재 코드로 판단하면 POI는 여전히 여러 ARVR/포지셔닝/분석 알고리즘이 공유하는 업스트림 템플릿 종속성입니다. 해당 템플릿과 포인트 데이터는 여러 알고리즘 UI에서 반복적으로 참조됩니다.

### 오해 3: 템플릿 시스템에서 Flow 템플릿을 제외

Flow 템플릿은 프레젠테이션이 더 복잡하지만 여전히 템플릿 시스템을 통해 기본 프로그램에 들어가고 후속 실행은 인접한 창과 프로세스 라이브러리에 의해 인계됩니다.

### 오해 4: JSON 템플릿이 단지 "임시 호환성 레이어"에 불과하다고 생각하는 것

현재 `Jsons/` 디렉터리와 `ITemplateJson<T>`은 여전히 사용 중인 실제 기본 경로 중 하나이므로 강력한 형식의 템플릿으로 완전히 대체된 것으로 작성해서는 안 됩니다.

## 추천읽기순서

1. `엔진/ColorVision.Engine/템플릿/TemplateContorl.cs`
2. `엔진/ColorVision.Engine/템플릿/TemplateManagerWindow.xaml.cs`
3. `엔진/ColorVision.Engine/템플릿/TemplateEditorWindow.xaml.cs`
4. `엔진/ColorVision.Engine/템플릿/Flow/TemplateFlow.cs`
5. `엔진/ColorVision.Engine/템플릿/Jsons/ITemplateJson.cs`
6. `엔진/ColorVision.Engine/템플릿/Jsons/EditTemplateJson.xaml.cs`
7. `POI/`, `ARVR/` 및 `Jsons/` 아래의 `Algorithm*.cs`와 같은 특정 비즈니스 알고리즘 디렉터리

## 계속 읽기

- [알고리즘 및 템플릿 개요](./README.md)
- [템플릿 모듈 분석](../../03-architecture/comComponents/templates/analytic.md)
- [FlowEngineLib 아키텍처](../../03-architecture/comComponents/engine/flow-engine.md)