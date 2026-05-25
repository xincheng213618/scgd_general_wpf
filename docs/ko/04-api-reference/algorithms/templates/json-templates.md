# JSON 템플릿

이 페이지에서는 현재 웨어하우스에서 실제로 사용 가능한 JSON 템플릿 호스트 체인에 대해서만 설명하고 "범용 알고리즘 DSL 플랫폼 + 프로젝트 간 구성 프레임워크"의 이전 초안을 더 이상 유지하지 않습니다.

## 먼저 이 모듈이 무엇인지 살펴보겠습니다.

현재 소스 코드 상태에 따르면 JSON 템플릿 시스템은 데이터베이스와 독립적으로 존재하는 구성 플랫폼이 아니라 `ColorVision.Engine` 템플릿 시스템의 특정 분기입니다. 현재 핵심 목표는 다음과 같습니다.

- 'ModMasterModel.JsonVal'의 JSON 콘텐츠를 템플릿 항목으로 호스팅합니다.
- 범용 편집기 `EditTemplateJson`을 통해 텍스트 편집과 속성 편집의 두 가지 모드를 제공합니다.
- 특정 템플릿 유형이 `ITemplateJson<T>` 형식으로 동일한 로드, 저장, 가져오기 및 내보내기 논리 세트를 재사용할 수 있도록 허용합니다.
- 'PoiAnalytic' 및 'SFFRFindROI'와 같은 JSON 드라이버 템플릿을 위한 통합 호스트를 제공합니다.

따라서 완전히 별도의 구성 하위 시스템이라기보다는 "데이터베이스의 JSON 템플릿 포크"에 가깝습니다.

## 현재 가장 중요한 파일

- `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
- `엔진/ColorVision.Engine/템플릿/Jsons/TemplateJsonParam.cs`
- `엔진/ColorVision.Engine/템플릿/Jsons/EditTemplateJson.xaml`
- `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
- `엔진/ColorVision.Engine/템플릿/Jsons/PoiAnalytic/TemplatePoiAnalytic.cs`
- `엔진/ColorVision.Engine/템플릿/Jsons/SRFFindROI/TemplateSFRFindROI.cs`

"지금 JSON 템플릿을 저장하는 방법, 편집하는 방법, 템플릿 창에 걸는 방법"만 보고 싶다면 이 파일들은 이미 본문을 다 다루었습니다.

## 현재 메인체인을 실행하는 방법

### 호스트 기본 클래스

`ITemplateJson<T>`은 JSON 템플릿 분기를 위한 범용 호스트입니다. 현재 다음을 담당하고 있습니다.

- `TemplateDicId`를 사용하여 MySQL에서 `ModMasterModel`을 읽습니다.
- 각 레코드를 `TemplateModel<T>`로 래핑합니다.
- 저장, 삭제, 복사, 가져오기, 내보내기 기능 제공
- 새 템플릿을 생성할 때 사전 템플릿 기본 JSON에서 초기 콘텐츠를 생성합니다.

즉, JSON 템플릿은 일반 텍스트 편집처럼 보이지만 현재는 여전히 템플릿 사전과 데이터베이스 레코드에 크게 의존하고 있습니다.

### 매개변수 객체

`TemplateJsonParam`은 현재 가장 기본적인 JSON 템플릿 매개변수 개체입니다. 다음을 보유합니다.

- `TemplateJsonModel`
- `재설정명령`
- `체크명령`
- `JsonValueChanged` 이벤트

`JsonValue`의 실제 의미는 다음과 같습니다.

- 읽을 때 `JsonHelper.BeautifyJson(...)`을 사용하여 형식을 지정하세요.
- 작성 시 JSON이 유효한 경우에만 'TemplateJsonModel.JsonVal'을 다시 작성하세요.

`ResetValue()`는 단순히 로컬 텍스트를 지우는 대신 사전 템플릿에 기록된 기본 JSON으로 돌아갑니다.

### 편집기 제어

`EditTemplateJson`은 현재 실제 편집 항목입니다. 이제 다음 두 가지를 모두 지원합니다.

- AvalonEdit 텍스트 모드
- `JsonPropertyEditorControl` 속성 모드
- 설명 주석 보기 전환
- 확인 버튼
- 외부 JSON 웹사이트 보조 입구

오른쪽 하단에 있는 `json` 버튼의 현재 실제 동작은 매우 명확합니다.

-`https://www.json.cn/`을 엽니다.
- 현재 JSON을 클립보드에 복사

이는 다른 숨겨진 명령이 아닌 현재 활성 파일에 있는 `Button_Click_1`의 실제 기능입니다.

### 모드 전환 및 동기화

`EditTemplateJson`은 현재 간단한 텍스트 상자에 대한 래퍼가 아닙니다. 그것은:

- 디바운스 타이머로 텍스트 변경 사항 동기화
- 역방향 `IEditTemplateJson.JsonValueChanged`를 통해 인터페이스 새로 고침
- 텍스트 모드와 속성 모드 간 전환 시 JSON 콘텐츠 동기화
- `EditTemplateJsonConfig`를 사용하여 너비와 기본 편집 모드를 기억하세요.

따라서 여기서의 복잡성은 주로 알고리즘 자체가 아니라 "양쪽 편집 측면에서 동일한 JSON을 일관되게 유지"하는 데 있습니다.

## 현재 가장 흔히 저지르는 실수 중 일부는 다음과 같습니다.

### 범용 파일 템플릿 플랫폼이 아닙니다.

현재 JSON 템플릿의 기본 저장소는 웨어하우스에 있는 임의의 JSON 파일 집합이 아닌 MySQL의 'ModMasterModel.JsonVal'입니다. "디스크 구성 디렉터리를 읽는다"라고 계속해서 쓴다면 실제 구현에서 벗어나게 됩니다.

### 모든 JSON 템플릿이 동일한 비즈니스 스키마를 공유하는 것은 아닙니다.

`ITemplateJson<T>`은 호스트 체인만 제공합니다. 각 특정 템플릿에 필요한 실제 필드는 여전히 해당 JSON 규칙에 따라 결정됩니다. 문서는 더 이상 전체 시스템에 대한 통합 사양에 특정 유형의 JSON 구조를 작성할 수 없습니다.

### 편집기는 더 이상 단순한 텍스트 편집기가 아닙니다.

현재 'EditTemplateJson'은 이미 속성 모드와 설명 모드 간 전환을 지원합니다. AvalonEdit 텍스트 상자를 설명하는 것만으로는 사용자가 실제로 보는 기능의 절반을 놓칠 수 있습니다.

### "검증"은 현재 완전한 컴파일러가 아닌 주로 이벤트 트리거입니다.

'CheckCommand'는 'JsonValueChanged' 이벤트 체인을 트리거하며 특정 응답은 호출자에 따라 다릅니다. 독립형 JSON 규칙 엔진으로 작성하지 마세요.

## 추천읽기순서

1. `엔진/ColorVision.Engine/템플릿/Jsons/ITemplateJson.cs`
2. `엔진/ColorVision.Engine/템플릿/Jsons/TemplateJsonParam.cs`
3. `엔진/ColorVision.Engine/템플릿/Jsons/EditTemplateJson.xaml.cs`
4. `엔진/ColorVision.Engine/템플릿/Jsons/PoiAnalytic/TemplatePoiAnalytic.cs`
5. `엔진/ColorVision.Engine/템플릿/Jsons/SFRFindROI/TemplateSFRFindROI.cs`

## 계속 읽기

- [템플릿 API 참조](./api-reference.md)
- [템플릿 관리](./template-management.md)
- [ColorVision.Engine](../../엔진-컴포넌트/ColorVision.Engine.md)