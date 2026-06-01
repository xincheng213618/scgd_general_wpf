# 템플릿 API 참조

이 페이지는 현재 소스 코드에서 상대적으로 안정적인 템플릿 호스트 항목만 유지하며 더 이상 "완전한 서명 매뉴얼"을 유지하려고 시도하지 않습니다. 그 이유는 간단합니다. 많은 템플릿 동작은 구체적인 하위 클래스 재정의, 데이터베이스 상태 및 사용자 제어 후크에 의존하며 이전 스타일의 API 테이블은 쉽게 표류할 수 있습니다.

## 먼저 어떤 입구가 가장 알아두면 좋은지 살펴보겠습니다.

현재 코드에 따르면 템플릿 시스템에서 가장 안정적이고 우선적으로 이해해야 할 가치가 있는 유형은 다음과 같습니다.

- `ITemplate`
- `I템플릿<T>`
- `ITemplateJson<T>`
- `TemplateControl` / `IITemplateLoad`
- `ParamBase` / `ModelBase` / `ParamModBase`
- `TemplateModel<T>`
- `TemplateEditorWindow` / `TemplateCreate`

이 페이지의 초점은 현재 구현에서 이러한 진입점이 어떤 역할을 하는지 설명하는 것입니다.

## 핵심 호스트 유형

### I템플릿

'ITemplate'은 모든 템플릿의 호스트 기본 클래스입니다. 현재 가장 중요한 책임은 다음과 같습니다.

- 생성 중에 `TemplateControl.ITemplateNames`에 자신을 등록하세요.
- `Load()`, `Save()`, `Import()`, `Export()`, `Delete()`, `Create()` 및 기타 수명 주기 후크 제공
- `ItemsSource`, `Count`, `GetValue(...)`, `GetParamValue(...)` 노출
- 'IsSideHide', 'IsUserControl'과 같은 호스트 창 동작 제어
- 생성 창에 `HasCreateTemplateSource`, `ImportName`, `CreateDefault()` 및 기타 소스 기능을 제공합니다.

중요 사항: `ITemplate`은 현재 단순한 인터페이스 정의가 아닌 구체적인 기본 클래스입니다.

### `ITemplate<T>`

`ITemplate<T>`는 `T : ParamModBase, new()`인 일반 매개변수 템플릿에 대한 가장 일반적인 일반 기본 클래스입니다. 현재 주로 다음을 처리합니다.

- `ObservableCollection<TemplateModel<T>> TemplateParams`
-`항목 소스`
- `카운트`
-`GetTemplateNames()`
- `GetTemplateIndex(...)`
- `GetParamValue(...)`

이러한 일반 목록 동작은 통합되었습니다.

또한 `TemplateDicId`를 기반으로 사전 템플릿에서 기본 매개변수 개체를 생성하는 역할도 담당하므로 이 레이어는 단순한 컬렉션 래퍼가 아닙니다.

### `ITemplateJson<T>`

`ITemplateJson<T>`은 JSON 템플릿 분기의 호스트 기본 클래스입니다. 여기서 `T : TemplateJsonParam, new()`입니다. `ITemplate<T>`과의 주요 차이점은 다음과 같습니다.

- 데이터 소스는 'ModMasterModel.JsonVal'입니다.
- `SysDictionaryModModel.JsonVal`을 사용하여 기본값을 생성합니다.
-`.cfg` 및 ZIP을 중심으로 가져오기 및 내보내기
- 복제 로직은 JSON 직렬 복사본을 기반으로 합니다.

템플릿 콘텐츠가 기본적으로 JSON 텍스트인 경우 이 레이어는 일반적으로 `ITemplate<T>`보다 실제 구현에 더 가깝습니다.

## 등록 및 검색 포털

### 템플릿컨트롤

`TemplateControl`은 현재 템플릿 레지스트리입니다. 주로 다음을 유지합니다.

- `ITemplateNames`
- `AddITemplateInstance(...)`
- `ExitsTemplateName(...)`
- `FindDuplicateTemplate(...)`

그리고 구체적인 템플릿 유형이 콘텐츠 자체를 로드할 수 있도록 초기화 시 모든 'IITemplateLoad' 구현을 검사합니다.

### II템플릿로드

`IITemplateLoad`는 템플릿 로딩 확장점입니다. 현재 많은 템플릿 클래스는 `TemplateControl.Init()`가 스캔될 때 자체 `Load()`를 수행하기 위해 이를 구현합니다.

이는 현재 템플릿 시스템과 애플리케이션 시작 순서가 결합되는 중요한 이유 중 하나이기도 합니다.

## 매개변수 및 모델 기본 클래스

### 매개변수베이스

`ParamBase`는 가장 얇은 레이어이며 다음만 제공합니다.

- `이드`
- `이름`

모든 템플릿 매개변수 객체의 공통 상위 클래스로 적합합니다.

### 모델베이스

현재 구현에서 'ModelBase' 값은 이름보다 더 구체적입니다. 'ModDetailModel' 목록을 기호 이름으로 색인화된 매개변수 사전에 매핑하고 다음을 제공합니다.

- `GetValue<T>(...)`
-`속성 설정(...)`
- `GetParameter(...)`
-`GetDetail(...)`
- `StringToDoubleArray(...)`
- `DoubleArrayToString(...)`

즉, 많은 템플릿 매개변수 속성을 일반 C# 속성처럼 작성할 수 있는 이유는 하위 계층에서 실제로 사전 매핑 및 유형 변환을 수행하기 때문입니다.

### ParamModBase

'ParamModBase'는 계속해서 템플릿 마스터 레코드를 매개변수 세부 레코드와 결합합니다. 대부분의 데이터베이스 드라이버 템플릿 매개변수 개체에 대한 직접적인 기본 클래스입니다.

## UI 호스트 관련 유형

### `템플릿모델<T>`

`TemplateModel<T>`은 현재 목록 항목 래퍼 개체입니다. '값' 외에도 다음도 가정합니다.

- '열쇠'
- `선택됨`
- `IsEditMode`
- 마우스 오른쪽 버튼 클릭 메뉴
- 이름 바꾸기 및 이름 복사 명령

따라서 사용자가 목록에서 보는 "템플릿 항목"은 기본 매개변수 개체가 아니라 UI 상태가 포함된 패키징 계층입니다.

### 템플릿 편집기 창

`TemplateEditorWindow`는 가장 다양한 템플릿 편집 호스트입니다. 템플릿이 'IsUserControl'인지 여부에 따라 오른쪽에 표시됩니다.

-`PropertyGrid`
- 템플릿 사용자 정의 `UserControl`

동시에 생성, 복사, 저장, 삭제, 이름 바꾸기, 검색, 정렬 및 선택 전환을 통합된 방식으로 수행합니다.

### 템플릿 생성

`TemplateCreate`는 현재 템플릿 생성 소스 선택을 담당합니다. 기본 템플릿 외에도 다음을 지원합니다.

- 현재 사본
- SQLite 샘플 라이브러리의 샘플

그래서 더 이상 템플릿 이름만 입력하는 작은 팝업창이 아닙니다.

## 현재 가장 흔히 저지르는 실수 중 일부는 다음과 같습니다.

### `ITemplate`은 순수한 인터페이스가 아닙니다.

현재 등록, 창 생성 및 다양한 수명 주기 방법을 포함하여 많은 기본 동작이 'ITemplate' 기본 클래스에 직접 작성됩니다. 순전히 추상적인 계약으로 작성하면 독자가 오해할 수 있습니다.

### 많은 동작은 특정 템플릿이 재정의된 경우에만 설정됩니다.

예를 들어 `Import()`, `Export()`, `CreateDefault()`, `GetUserControl()` 등과 같은 메서드는 기본 클래스에서 완전히 구현되지 않을 수 있습니다. 기본 클래스 메서드 테이블은 "모든 템플릿에서 완전히 지원되는 함수 목록"으로 직접 간주될 수 없습니다.

### 데이터 모델과 UI 모델이 혼합되어 있습니다.`TemplateModel<T>`, `TemplateEditorWindow`, `TemplateCreate` 이러한 유형은 현재 템플릿 시스템이 UI 상태를 완전히 분리하지 않음을 나타냅니다. API 해석은 이러한 현실 경계를 보존해야 합니다.

### JSON 템플릿과 일반 매개변수 템플릿은 두 개의 호스트 브랜치입니다.

둘 다 템플릿으로 분류되지만 `ITemplate<T>` 및 `ITemplateJson<T>`의 기본 지속성, 생성, 가져오기 및 내보내기 경로가 다릅니다.

## 추천읽기순서

1. `엔진/ColorVision.Engine/템플릿/ITemplate.cs`
2. `엔진/ColorVision.Engine/템플릿/Jsons/ITemplateJson.cs`
3. `엔진/ColorVision.Engine/템플릿/ModelBase.cs`
4. `엔진/ColorVision.Engine/템플릿/ParamModBase.cs`
5. `엔진/ColorVision.Engine/템플릿/TemplateModel.cs`
6. `엔진/ColorVision.Engine/템플릿/TemplateEditorWindow.xaml.cs`
7. `엔진/ColorVision.Engine/템플릿/TemplateCreate.xaml.cs`

## 계속 읽기

- [템플릿 관리](./template-management.md)
- [JSON 템플릿](./json-templates.md)
- [프로세스 엔진](./flow-engine.md)