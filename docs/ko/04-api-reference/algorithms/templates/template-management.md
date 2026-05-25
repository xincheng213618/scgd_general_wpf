# 템플릿 관리

이 페이지에서는 현재 웨어하우스에서 실제로 사용할 수 있는 템플릿 호스트 체인에 대해서만 설명하며 "통합 프레임워크 청사진 + 이상적인 MVVM 레이어링 + 대규모 의사 예제"의 이전 초안을 더 이상 유지하지 않습니다.

## 먼저 이 페이지에서 무엇에 대해 이야기하고 있는지 살펴보겠습니다.

현재 소스코드 현황에 따르면 템플릿 관리는 단일 백엔드 서비스가 아닌 'ITemplate' 기본 클래스, 글로벌 레지스트리, 관리창, 편집창, 생성창으로 구성된 호스트 체인이다. 현재 다음을 담당하고 있습니다.

- 시작 시 특정 템플릿 유형을 스캔하고 등록합니다.
- 메인 프로그램에서 네임스페이스별로 템플릿 항목을 구성합니다.
- 일반적인 편집, 생성, 가져오기 및 내보내기, 복사 및 이름 바꾸기 창을 제공합니다.
- JSON 템플릿, 프로세스 템플릿, POI 템플릿, 사전 템플릿 등이 호스트 인터페이스를 공유하도록 합니다.
- SQLite 샘플 라이브러리 및 전역 검색 액세스를 제공합니다.

따라서 이 페이지의 실제 내용은 "템플릿 이론"이 아니라 현재 기본 프로그램이 다양한 템플릿을 호스팅하는 방법에 관한 것입니다.

## 현재 가장 중요한 파일

- `엔진/ColorVision.Engine/템플릿/TemplateContorl.cs`
- `엔진/ColorVision.Engine/템플릿/ITemplate.cs`
- `엔진/ColorVision.Engine/템플릿/TemplateManagerWindow.xaml.cs`
- `엔진/ColorVision.Engine/템플릿/TemplateEditorWindow.xaml.cs`
- `엔진/ColorVision.Engine/템플릿/TemplateCreate.xaml.cs`
- `엔진/ColorVision.Engine/템플릿/TemplateSearchProvider.cs`
- `엔진/ColorVision.Engine/템플릿/TemplateSampleLibrary.cs`
- `엔진/ColorVision.Engine/템플릿/TemplateSampleSaveWindow.xaml.cs`

이 몇 가지 사항만 읽어도 현재 템플릿 시스템의 주요 정신 모델을 확립하는 데 충분합니다.

## 현재 메인체인을 실행하는 방법

### 초기화 및 등록

`TemplateInitializer`가 시작된 후 `TemplateControl.GetInstance()`가 트리거됩니다. 그런 다음 `TemplateControl`은 어셈블리의 모든 `IITemplateLoad` 구현을 검색하고 `Load()`를 실행합니다.

반면에 `ITemplate` 생성자 자체도 템플릿 인스턴스를 `TemplateControl.ITemplateNames`에 비동기적으로 등록합니다. 따라서 현재 템플릿 검색은 병렬로 작동하는 2계층 메커니즘입니다.

- 템플릿 개체는 전역 레지스트리에 구성됩니다.
- 구체적인 템플릿 로더는 MySQL을 사용할 수 있게 된 후 콘텐츠를 새로 고칩니다.

이것이 바로 초기화 및 데이터베이스 전제 조건 없이는 많은 템플릿 페이지를 이해할 수 없는 이유입니다.

### 템플릿 관리 창

`MenuTemplateManagerWindow`는 `TemplateManagerWindow`를 엽니다. 이 창은 현재 간단한 목록이 아니지만 다음과 같습니다.

- `TemplateControl.ITemplateNames` 읽기
- 유형 네임스페이스별로 그룹화
- 검색 및 필터링 지원
-카드별 템플릿 표시 지원
- 템플릿 선택 후 해당 에디터 바로 열기

따라서 단순한 메뉴 팝업 창이 아닌 "템플릿 항목 수집기" 역할을 합니다.

### 템플릿 편집 창

`TemplateEditorWindow`는 현재 가장 다양한 템플릿 호스트 창입니다. 먼저 `template.Load()`를 수행한 다음 템플릿 유형에 따라 두 가지 경로를 사용합니다.

- 일반 템플릿: `PropertyGrid`를 오른쪽에 배치
- 사용자 정의 템플릿: `GetUserControl()`을 호출하고 템플릿이 자체적으로 올바른 영역을 차지하도록 합니다.

창도 균일하게 연결되어 있습니다.

- 생성, 복사, 저장, 삭제 명령
- 선택 항목 전환 시 `SetSaveIndex(...)`
- `SetUserControlDataContext(...)` 또는 `GetParamValue(...)`
- 열 정렬, 검색 및 더블클릭 동작

이는 인터페이스가 매우 다르지만 다양한 현재 템플릿이 여전히 동일한 호스트 셸을 공유할 수 있는 이유이기도 합니다.

### 템플릿 생성 창

`TemplateCreate`는 더 이상 "단순한 이름 입력 상자 제공" 창이 아닙니다. 현재 구현된 대로 새 템플릿에 대한 여러 소스를 제공합니다.

- 시스템 기본 템플릿
- 현재 사본(복사 후 템플릿 내용이 일시적으로 저장됨)
- SQLite 샘플 라이브러리의 역사적 예

이러한 소스는 카드로 렌더링되고 그룹별로 필터링됩니다. 마지막으로 `ApplyTemplateSource(...)`는 선택된 소스를 생성할 템플릿에 삽입합니다.

이는 현재 템플릿 생성 체인이 더 이상 "CreateDefault() + 직접 채운 매개변수"가 아님을 보여줍니다.

### 검색 및 샘플 라이브러리

`TemplateSearchProvider`는 모든 템플릿 이름을 전역 검색 항목에 등록합니다. `TemplateSampleLibrary`는 템플릿 샘플을 사용자 문서 디렉터리의 SQLite 라이브러리에 저장합니다.

- `.../템플릿/TemplateSamples.db`

현재 보유하고 있는 내용은 다음과 같습니다.

- 템플릿 코드 및 템플릿 유형
- 그룹명 및 샘플명
- 설명 텍스트
- 직렬화된 템플릿 콘텐츠

따라서 템플릿 관리에는 이제 MySQL 기본 스토리지 외에 로컬 샘플 재사용 체인이 있습니다.

## 현재 가장 흔히 저지르는 실수 중 일부는 다음과 같습니다.

### 순수한 서비스 계층 시스템이 아닙니다.

현재 `TemplateManagerWindow`, `TemplateEditorWindow` 및 `TemplateCreate`와 같은 많은 주요 논리가 WPF 창에 직접 작성됩니다. 계속해서 "호스트는 ViewModel만 바인딩하고 로직은 모두 서비스 계층에 있습니다"라고 설명하는데 이는 실제 코드와 일치하지 않습니다.

### 다양한 템플릿의 지속성 방법은 동일하지 않습니다.

일부 템플릿은 주로 MySQL을 사용하고, 일부 템플릿은 파일 가져오기 및 내보내기를 지원하며, 일부 템플릿은 SQLite 샘플 라이브러리도 사용합니다. 문서에서는 더 이상 모든 템플릿이 동일한 스토리지 모델을 가지고 있다고 가정할 수 없습니다.

### `IsUserControl` 및 `IsSideHide`는 동작을 크게 변경할 수 있습니다.

현재 템플릿 호스트는 고정된 레이아웃이 아닙니다. 'IsUserControl'은 오른쪽을 템플릿 사용자 정의 컨트롤로 변경하고, 'IsSideHide'는 창 레이아웃과 두 번 클릭 동작도 변경합니다. 이 두 스위치를 무시하면 많은 템플릿 페이지를 설명할 수 없게 됩니다.

### 템플릿 등록과 데이터베이스 연결은 여전히 결합되어 있습니다.

'ITemplate' 구성이 인스턴스를 등록하더라도 특정 템플릿 콘텐츠의 대부분은 실제로 로드되기 전에 MySQL이 연결될 때까지 기다려야 합니다. 템플릿 시스템을 "순수한 로컬 정적 등록"으로 작성하면 핵심 전제가 누락됩니다.

## 추천읽기순서1. `엔진/ColorVision.Engine/템플릿/ITemplate.cs`
2. `엔진/ColorVision.Engine/템플릿/TemplateContorl.cs`
3. `엔진/ColorVision.Engine/템플릿/TemplateManagerWindow.xaml.cs`
4. `엔진/ColorVision.Engine/템플릿/TemplateEditorWindow.xaml.cs`
5. `엔진/ColorVision.Engine/템플릿/TemplateCreate.xaml.cs`
6. `엔진/ColorVision.Engine/템플릿/TemplateSearchProvider.cs`
7. `엔진/ColorVision.Engine/템플릿/TemplateSampleLibrary.cs`

## 继续阅读

- [JSON 板](./json-templates.md)
- [流程引擎](./flow-engine.md)
- [템플릿 분할](../../../03-architecture/comments/templates/analytic.md)