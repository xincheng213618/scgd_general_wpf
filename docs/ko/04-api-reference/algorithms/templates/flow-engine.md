# 프로세스 엔진

이 페이지에서는 현재 웨어하우스에 있는 `Engine/ColorVision.Engine/Templates/Flow` 레이어의 실제 책임에 대해서만 설명합니다. 우리는 "전체 Flow 실행 커널, 호스트 브리지 및 노드 라이브러리를 하나의 페이지에 혼합"하는 이전 초안을 더 이상 유지하지 않을 것입니다.

## 먼저 이 페이지에서 무엇에 대해 이야기하고 있는지 살펴보겠습니다.

현재 페이지는 'FlowEngineLib' 실행 커널 자체에 관한 것이 아니라 메인 프로그램의 프로세스 템플릿을 둘러싼 호스트 계층에 관한 것입니다. 주요 내용은 다음과 같습니다.

- 데이터베이스 및 리소스 테이블에서 프로세스 템플릿을 로드하는 방법.
- 프로세스 템플릿을 더블클릭한 후 편집창을 여는 방법입니다.
- 편집 창이 `STNodeEditor`, 속성 패널 및 노드 트리를 호스팅하는 방법.
- 호스트 계층이 장치, 템플릿 및 노드 구성자를 프로세스 편집기에 연결하는 방법.

노드 실행 의미 체계와 노드 기본 클래스를 보려면 [FlowEngineLib](../../engine-components/FlowEngineLib.md)로 이동하세요.

## 현재 가장 중요한 파일

- `엔진/ColorVision.Engine/템플릿/Flow/TemplateFlow.cs`
- `엔진/ColorVision.Engine/템플릿/Flow/FlowEngineToolWindow.xaml.cs`
- `엔진/ColorVision.Engine/템플릿/Flow/STNodeEditorHelper.cs`
- `엔진/ColorVision.Engine/템플릿/Flow/NodeConfigurator/*.cs`

이러한 코드는 기본 프로그램의 프로세스 템플릿이 편집, 저장 및 구성되는 방법을 함께 결정합니다.

## 현재 메인체인을 실행하는 방법

### 프로세스 템플릿 항목

`MenuTemplateFlow`는 `TemplateEditorWindow(new TemplateFlow())`를 엽니다. `TemplateFlow` 자체는 `ITemplate<FlowParam>`의 특정 구현이며 현재 다음을 담당합니다.

- MySQL에서 프로세스 템플릿 마스터 테이블 읽기
- 'SysResourceModel.Value'에서 Base64로 노드 그래프 콘텐츠를 검색합니다.
- 이를 `FlowParam`으로 포장합니다.
- 저장, 삭제, 가져오기, 내보내기 및 생성 관리

따라서 현재 프로세스 템플릿은 단순한 디스크 파일 목록이 아니라 "데이터베이스 마스터 레코드 + 리소스 테이블 바이너리 콘텐츠"의 조합입니다.

### 더블클릭 후 편집창

`TemplateFlow.PreviewMouseDoubleClick(...)`은 `FlowEngineToolWindow`를 직접 엽니다. 이는 프로세스 템플릿이 많은 일반 템플릿과 다르다는 것을 보여줍니다.

- 리스트창은 바로 입구입니다
- 실제 프로세스 편집은 별도의 창에서 이루어집니다.

창은 `STNodeEditorHelper`를 사용하여 노드 캔버스, 속성 패널, 노드 트리, 클립보드 및 마우스 오른쪽 버튼 클릭 메뉴를 호스팅합니다.

### 에디터 보조 레이어

`STNodeEditorHelper`는 현재 "노드 트리 조정 지원"을 훨씬 넘어서는 많은 일을 담당하고 있습니다.

- 노드 복사 및 붙여넣기의 압축 직렬화
- 현재 선택된 노드는 속성 패널과 동기화됩니다.
- 노드 트리 초기화 및 조립
- 메뉴 우클릭, 삭제, 전체 선택 및 기타 명령
- 합법성 검사 및 자동 레이아웃
-장치 및 템플릿 선택 패널용 호스트 후크

이는 프로세스 편집 창의 많은 인터랙션 로직이 각 노드 컨트롤에 분산되어 있지 않고 이 헬퍼에 집중되어 있음을 의미합니다.

### 노드 구성자 브리지

`NodeConfigurator` 디렉터리는 현재 기본 프로그램과 노드 라이브러리 사이의 중요한 연결 계층입니다. 다음은 다음과 같습니다.

- 장치 서비스 목록
- 로컬 이미지 경로 입력
- 공통 템플릿 선택기
- JSON 템플릿 선택기

노드 속성 패널을 로드합니다.

예를 들어 POI 관련 구성자는 `TemplatePoi`, `TemplatePoiFilterParam`, `TemplatePoiReviseParam`, `TemplatePoiOutputParam` 및 기타 템플릿을 프로세스 노드에 다시 연결합니다. 즉, 호스트에 있는 노드의 편집 가능한 경험은 'FlowEngineLib'에 의해 전적으로 결정되지 않습니다.

## 현재 저장 및 내보내기 경계

### 주 저장소는 여전히 데이터베이스입니다

`TemplateFlow.Load()` 및 `Save2DB(...)`는 현재 MySQL 마스터 테이블, 세부 테이블 및 `SysResourceModel`을 중심으로 진행됩니다. Base64 노드 그래프의 내용은 리소스 테이블에 추가된 다음 세부 기록을 통해 다시 연결됩니다.

### 내보내기는 하나의 형식이 아닙니다

현재 프로세스 템플릿 내보내기에는 최소한 두 가지 실용적인 형태가 있습니다.

- `.stn`: 노드 그래프 원본 파일
- `.cvflow`: 관련 템플릿 정보가 포함된 프로세스 패키지

따라서 단순히 프로세스 템플릿을 "노드 그래프 파일"로 작성하면 현재 패키지를 내보내는 기능이 누락됩니다.

### 저장 경로는 데이터베이스와 로컬 파일을 구분해야 합니다

`FlowEngineToolWindow.Save()`에는 현재 두 가지 명확한 저장 경로가 있습니다.

| 상황 | 현재 동작 | 인수인계 시 주의점 |
| --- | --- | --- |
| 로컬 `.stn` 파일 | `SaveToFile(FileFlow)`가 캔버스 bytes를 파일에 직접 씁니다 | 디스크 파일만 갱신하며 템플릿 데이터베이스는 갱신하지 않습니다 |
| `FlowParam` 템플릿 | `CheckFlow()` -> `GetCanvasData()` -> Base64 -> `TemplateFlow.Save2DB(...)` | `ModMasterModel`, 상세 테이블, `SysResourceModel`에 저장합니다 |

즉 플로우 편집기는 단순 파일 편집기가 아닙니다. 템플릿 관리에서 열면 주 데이터 원천은 데이터베이스이고, 파일에서 열었을 때만 저장 대상이 로컬 `.stn`입니다.

### `.cvflow` 패키지 구조

단일 플로우를 `.cvflow`로 내보내면 `FlowPackageHelper`가 ZIP 패키지를 만듭니다.

| 파일 | 역할 |
| --- | --- |
| `flow.stn` | 노드 캔버스의 바이너리 내용 |
| `manifest.json` | `FlowPackageManifest`이며 플로우 이름, 버전, 관련 템플릿을 기록합니다 |

관련 템플릿은 사람이 목록을 직접 쓰는 방식이 아니라 노드 속성에서 `TempName`, `TemplateName`, `CalibTempName`, `POITempName`, `FilterTemplateName`, `ReviseTemplateName`, `XRTempName`, `CamTempName`, `AlgTempName`, `LayoutROITemplate` 같은 참조 필드를 스캔해 수집합니다. 가져오기 중 이름이 충돌하면 플로우 이름을 이용해 새 템플릿명을 만들고, 플로우 안의 참조도 함께 교체합니다.

다중 선택 내보내기는 여전히 구형 `.zip`이며 여러 `.stn`만 포함합니다. `manifest.json`이 없고 관련 템플릿도 재귀적으로 수집하지 않습니다. 인수인계 문서에서는 두 내보내기 방식을 분리해서 설명해야 합니다.

## 실행 및 스케줄링 체인

플로우 템플릿은 편집만 되는 것이 아니라 실제 실행 대상이기도 합니다.

1. 사용자 또는 호출자가 `DisplayFlow.TemplateCombox`에서 플로우 템플릿을 선택합니다.
2. `DisplayFlow.RunFlow(sn)`가 `MeasureBatchModel`을 만들고, `TId`에는 `TemplateFlow.Params[selectedIndex].Id`를 넣습니다.
3. `FlowControl.Start(sn)`가 노드 그래프를 시작합니다.
4. `FlowControl_FlowCompleted(...)`가 배치 상태, 총 시간, 결과를 갱신하고 `FlowExecutionCompleted`를 발생시킵니다.
5. `RunFlowAndWaitAsync()`가 이 이벤트를 기다릴 수 있는 작업으로 감쌉니다.
6. `FlowJob`은 Quartz 작업 안에서 WPF Dispatcher로 돌아와 `DisplayFlow.RunFlowAndWaitAsync()`를 호출하고 `FlowJobResult`를 만듭니다.

“예약 작업이 왜 플로우를 실행할 수 있는가”를 추적할 때는 Quartz만 보지 말고 `DisplayFlow`와 `FlowExecutionCompleted` 이벤트도 함께 확인해야 합니다.

## 현재 가장 흔히 저지르는 실수 중 일부는 다음과 같습니다.

### 이 페이지는 FlowEngineLib의 중복 페이지가 아닙니다.

`FlowEngineLib`은 노드 실행 및 기본 클래스 시스템을 담당합니다. 이 페이지의 이 레이어는 기본 프로그램의 템플릿 관리, 창 편집 및 호스트 브리징을 담당합니다. 두 계층 모두 "프로세스 엔진"이라고 불리지만 경계가 다릅니다.

### 프로세스 템플릿은 순수한 디스크 자산이 아닙니다.

현재 기본 경로는 특정 디렉터리의 `.stn` 파일을 검색하는 대신 여전히 데이터베이스 + 리소스 테이블입니다. 가져오기 및 내보내기는 추가 기능일 뿐입니다.

### 노드 속성 편집은 호스트 코드에 크게 의존합니다.

실제로 노드 클래스 자체가 아니라 장치 드롭다운 상자, 템플릿 드롭다운 상자 및 JSON 템플릿 드롭다운 상자를 노드 속성 영역에 걸어 두는 것은 `NodeConfigurator` 및 `STNodeEditorHelper` 레이어입니다.

### 창 동작이 일반 템플릿 편집기와 다릅니다

일반 템플릿은 대부분 `TemplateEditorWindow` 오른쪽에서 편집됩니다. 프로세스 템플릿은 현재 "목록 창 + 독립 프로세스 편집기 창"의 경로를 따릅니다. 일반적인 템플릿 내러티브를 계속 따르면 독자를 오도할 수 있습니다.

### 가져오기와 내보내기에는 두 가지 호환 경로가 있습니다

`.cvflow`는 패키지 가져오기 경로를 사용해 `manifest.json`을 읽고 관련 템플릿을 가져옵니다. 다른 파일은 bytes를 읽어 Base64로 바꾸고 `FlowParam`을 만드는 경로로 되돌아갑니다. `.stn`만 테스트하면 템플릿 참조 교체라는 중요한 인수인계 체인을 놓칠 수 있습니다.

## 인수인계 검증

- 템플릿 관리자에서 플로우를 새로 만들고 저장 후 `ModMasterModel`과 `SysResourceModel` 기록을 확인합니다.
- 단일 플로우를 `.cvflow`로 내보낸 뒤 ZIP 안에 `flow.stn`과 `manifest.json`이 있는지 확인합니다.
- 같은 이름의 플로우 패키지를 가져와 관련 템플릿명이 충돌할 때 새 이름으로 바뀌고 플로우 참조도 함께 바뀌는지 확인합니다.
- `DisplayFlow.RunFlowAndWaitAsync()` 또는 `FlowJob`으로 한 번 실행해 배치 상태와 결과가 갱신되는지 확인합니다.

## 추천읽기순서

1. `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
2. `Engine/ColorVision.Engine/Templates/Flow/FlowEngineToolWindow.xaml.cs`
3. `Engine/ColorVision.Engine/Templates/Flow/STNodeEditorHelper.cs`
4. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator` 아래의 기타 구성자

## 계속 읽기

- [FlowEngineLib](../../engine-components/FlowEngineLib.md)
- [플로우 노드 확장](../../extensions/flow-node.md)
- [ColorVision.Engine](../../engine-components/ColorVision.Engine.md)
