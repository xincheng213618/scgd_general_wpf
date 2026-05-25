#컬러비전.솔루션

이 페이지는 현재 `UI/ColorVision.Solution/`에서 가장 중요하고 안정적인 항목 유형과 하위 모듈만 유지하며 더 이상 이전 문서의 "완전한 API 백서 + 버전 목록 + 포괄적인 RBAC 인수" 작성 스타일을 유지하지 않습니다.

## 모듈 포지셔닝

`ColorVision.Solution`은 현재 순수한 "솔루션 관리자"보다는 데스크탑 작업 공간 쉘로 더 잘 이해됩니다.

실제로 수행되는 작업에는 다음이 포함됩니다.

- `.cvsln` 솔루션 생성, 열기 및 최근 파일 관리
- 나무 모양의 프로젝트 탐색 및 왼쪽에 새 프로젝트 입력
- 파일/폴더 편집기 선택 및 열기
- AvalonDock 문서 영역 및 패널 레이아웃 관리
- 내장된 터미널 컨트롤
- 다중 이미지 뷰어 및 썸네일 캐시
- 마크다운 미리보기
- 솔루션측 로컬 RBAC 서브모듈

이는 단일 창이 아니며 `SolutionManager` 주위에만 구성된 UI의 얇은 레이어도 아니라는 것을 의미합니다.

## 현재 가장 중요한 디렉토리

프로젝트 디렉터리를 살펴보면 먼저 알아야 할 가장 중요한 사항은 다음과 같습니다.

- `Editor/` : 파일 및 폴더 에디터 등록, 선택 및 열기
- `Explorer/`: 솔루션 트리, 노드 모델, 새 항목 및 상황에 맞는 메뉴
- `Workspace/`: AvalonDock 문서 영역 및 패널 레이아웃 관리
- `터미널/`: 내장된 터미널 제어 및 ConPTY 패키지
- `MultiImageViewer/`: 폴더 다중 이미지 미리보기 및 썸네일 캐싱
- `RecentFile/`: 최근 파일 기록
- `Rbac/`: 솔루션 측 로컬 사용자, 역할, 권한, 세션 및 감사 모듈
- 루트 디렉터리의 `SolutionManager.cs`: 솔루션 열기, 생성 및 현재 작업공간 전환 입구

## 키 입력 유형

### `솔루션매니저`

`SolutionManager`는 현재 작업공간 입구에 있는 중앙 개체입니다. 다음을 담당합니다.

- `.cvsln` 열기 또는 생성
- 최근 공개된 솔루션 목록을 유지합니다.
- 현재 `SolutionExplorer` 생성
- 명령줄 또는 최근 파일을 기반으로 시작 시 열 솔루션 결정

"솔루션이 어떻게 들어왔는지" 쫓고 싶다면 일반적으로 트리 컨트롤을 먼저 보는 것이 아니라 먼저 살펴봅니다.

### `솔루션 탐색기`

`SolutionExplorer`와 `Explorer/` 디렉토리의 노드 유형은 디렉토리, 파일, 새 항목 및 마우스 오른쪽 버튼 클릭 작업을 트리 작업 공간으로 구성하는 역할을 합니다.

이 부분은 "사용자가 프로젝트 구조를 보는 방법"에 대한 주요 입구입니다.

### `EditorManager`

EditorManager는 에디터 등록과 배포를 담당합니다. 현재 구현 기능은 매우 명확합니다.

- 로드된 어셈블리에서 'IEditor'를 구현하는 유형 검색
- `EditorForExtensionAttribute`, `GenericEditorAttribute`, `FolderEditorAttribute`를 통해 등록됨
- 확장에 대한 기본 편집기 구성 허용
- 폴더 편집기도 지원

따라서 현재 편집기 시스템은 손으로 쓴 스위치 테이블이 아니라 속성 기반 등록 메커니즘입니다.

### `WorkspaceManager` 및 `DockLayoutManager`

이 두 사람은 현재 문서 작업공간을 도킹하고 복원하는 역할을 담당합니다.

- 현재 문서 찾기 및 활성화
- `ContentId` 및 문서 선택 상태 유지
- AvalonDock 레이아웃 저장 및 로드
- 레이아웃 복원시 레지스트리별로 패널 및 문서 내용 복원

문제가 "탭이 어디로 갔나요?"로 나타나는 경우 "레이아웃이 복원되지 않았습니다." 또는 "문서 영역이 손실되었습니다.", 일반적으로 이 링크를 먼저 살펴보세요.

### `터미널컨트롤`

터미널 기능은 현재 별도의 외부 서비스가 아닌 이 프로젝트 내에 있습니다. `TerminalControl`은 현재 다음을 담당합니다.

- `cmd` 또는 `powershell`을 시작합니다.
- ConPTY 출력 수행
- 화면 버퍼 및 명령 기록 유지
- 스크립트 실행 및 URL 클릭 처리

따라서 단순히 "시스템 터미널을 호출"하는 것보다 내장된 터미널 UI 구성 요소에 더 가깝습니다.

### `멀티이미지 뷰어`

`MultiImageViewer`는 독립형 `UserControl`로 사용하거나 `MultiImageViewerEditor`를 통해 편집기 시스템에 연결할 수 있습니다.

현재 주로 다음을 담당합니다.

- 폴더에 여러 이미지 로드
- 확장자 필터링 지원
- 썸네일 표시
- 작업공간 문서 탭과 연동하여 열기 및 해제

## RBAC와 관련하여 이 모듈은 현재 어떤 역할을 담당하고 있나요?

이전 문서의 가장 큰 문제는 `ColorVision.Solution`이 "전체 프로젝트에 대한 통합 RBAC 권한 제어 레이어"로 작성되었다는 것입니다. 현재 코드는 이 상태가 아닙니다.

### 현재 실제 상황

`Rbac/`는 실제로 다음을 이미 포함하고 있는 `ColorVision.Solution`의 중요한 하위 모듈입니다.

-`RbacManager`
- `LoginWindow`, `UserManagerWindow`, `PermissionManagerWindow`
- 사용자, 역할, 권한, 세션, 감사 관련 엔터티 및 서비스
- 네이티브 SQLite 지속성
- `PermissionChecker`에 대한 세분화된 권한 코드 캐싱

### 하지만 현재 경계도 명확하게 작성해야 합니다.

이 RBAC 세트는 현재 자체 관리 창과 솔루션 측의 로컬 권한 하위 시스템에서 주로 작동합니다.

현재 검색 결과로 판단하면 'HasPermissionAsync' 및 'PermissionChecker'의 세부적인 호출은 거의 모두 여전히 'Rbac/' 하위 디렉터리에 있습니다. 동시에 많은 창 항목은 여전히 ​​전역 `Authorization.Instance.PermissionMode`에 의존하여 대략적인 판단을 내립니다.

따라서 더 정확한 설명은 다음과 같습니다.

- `ColorVision.Solution`에는 기본 RBAC 하위 모듈이 포함되어 있습니다.
- 전역 `PermissionMode`와 공존합니다.
- 전체 솔루션 트리, 모든 편집기 및 모든 파일 작업이 세분화된 권한 코드 제어에 대한 전체 액세스 권한을 갖고 있다고 설명할 수 없습니다.

## 현재 이 프로젝트를 어떻게 읽는 것이 더 적합합니까?

### 솔루션 입구를 보고싶다

먼저 살펴보세요:

-`SolutionManager.cs`
-`SolutionManagerInitializer.cs`
- `OpenSolutionWindow.xaml(.cs)`

### 트리 및 파일 노드를 보고 싶습니다.

먼저 살펴보세요:

- `탐색기/SolutionExplorer.cs`
- `탐색기/SolutionNodeFactory.cs`
- `TreeViewControl.xaml(.cs)`

### 다른 편집기에서 파일이 어떻게 열리는지 보고 싶습니다.

먼저 살펴보세요:

-`편집기/EditorManager.cs`
-`편집기/EditorForExtensionAttribute.cs`
- `편집기/*.cs`

### 작업공간 레이아웃과 문서 호스트를 보고 싶어요

먼저 살펴보세요:

-`작업 공간/WorkspaceManager.cs`
-`작업 공간/DockLayoutManager.cs`
- `작업 공간/LayoutMenuItems.cs`

### 로컬 권한 하위 시스템을 보고 싶습니다.

먼저 살펴보세요:-`Rbac/RbacManager.cs`
-`Rbac/서비스/`
-`Rbac/엔티티/`

## 이 페이지에서는 더 이상 아무것도 수행하지 않습니다.

이 페이지에서는 더 이상 다음과 같은 고위험 콘텐츠를 유지하지 않습니다.

- 오래된 버전 번호 및 대상 프레임워크 목록
- 완전한 공개 API가 존재한다고 가정하는 대규모 의사코드 조각
- 전체 프로젝트에 대한 통합 권한 항목으로 `RbacManager`를 작성합니다.
- 세분화된 권한으로 모든 파일 작업을 완전히 차단하도록 작성합니다.

구체적인 클래스나 메소드를 추가하려면 여기에 의사 API 전체 페이지를 계속 쌓아두는 대신 해당 하위 모듈 페이지에서 별도로 확장해야 합니다.

## 계속 읽기

- [UI 컴포넌트 개요](./README.md)
- [보안 및 권한 제어](../../03-architecture/security/overview.md)
- [RBAC 모듈](../../03-architecture/security/rbac.md)