# ST.Library.UI

이 페이지에서는 현재 웨어하우스에서 실제로 사용할 수 있는 `ST.Library.UI` 모듈만 설명하며 "완전한 UI 플랫폼 매뉴얼 + 대규모 예제 + 통합 확장 프레임워크"의 이전 초안을 더 이상 유지하지 않습니다.

## 먼저 이 모듈이 무엇인지 살펴보겠습니다.

현재 소스 코드 상태에 따르면 'ST.Library.UI'는 하위 수준 WinForms 노드 편집기 라이브러리 세트입니다. 현재 가장 명확한 역할은 독립형 애플리케이션 셸이 아니라 Flow 관련 기능을 제공하는 것입니다.

- 노드 캔버스 및 대화형 편집기
- 노드 기본 클래스 및 포트 연결 모델
- 속성 편집 패널
- 노드트리와 노드패널 조합 제어

따라서 ColorVision 비즈니스 계층 자체보다 "노드 편집기 인프라"에 더 가깝습니다.

## 현재 가장 중요한 파일

- `엔진/ST.Library.UI/NodeEditor/STNodeEditor.cs`
- `엔진/ST.Library.UI/NodeEditor/STNode.cs`
- `엔진/ST.Library.UI/NodeEditor/STNodeOption.cs`
- `엔진/ST.Library.UI/NodeEditor/STNodePropertyGrid.cs`
- `엔진/ST.Library.UI/NodeEditor/STNodeTreeView.cs`
- `엔진/ST.Library.UI/NodeEditor/STNodeEditorPannel.cs`
- `엔진/ST.Library.UI/FrmSTNodePropertyInput.cs`

현재 저장소에서 이 라이브러리가 실제로 무엇을 하는지 알고 싶다면 이 파일들이 이미 본문을 다루고 있습니다.

## 현재 제어 평면을 블록으로 나누는 방법

### 캔버스 컨트롤

`STNodeEditor`는 전체 라이브러리의 중앙 제어입니다. 현재 구현에 따르면 다음을 담당합니다.

- `노드` 유지
- 캔버스 오프셋 및 크기 조정 유지
- 노드 선택, 호버, 활성 상태 관리
- 노드 연결, 연결 해제 및 캔버스 상호 작용 처리
- 트리거 노드 및 캔버스 관련 이벤트

이는 노드 편집기의 현재 제어 논리가 여러 개의 독립적인 MVVM 서비스로 분할되는 대신 WinForms `Control`에 집중되어 있음을 보여줍니다.

### 노드 객체 모델

`STNode`는 현재 모든 노드의 공통 기본 클래스이며 다음을 담당합니다.

- 제목, 크기, 위치
- 입출력 옵션 모음
- 노드 임베디드 컨트롤 컬렉션
- 선택된 상태와 활성화된 상태
- 자동 크기 조정 및 다시 그리기

`STNodeOption`은 포트 모델을 가정하고 현재 다음을 제공합니다.

- 포트 텍스트 및 데이터 유형
- 단일 연결/다중 연결 제한
- 연결 수 및 연결 포트 세트
- 연결, 연결 해제 및 데이터 전송 이벤트

따라서 이 라이브러리의 기본 정신 모델은 "노드는 단순한 그림이다"가 아니라 "노드 + 포트 + 제어 + 이벤트"의 결합된 개체입니다.

### 속성 패널

`STNodePropertyGrid`는 현재 노드 속성을 위해 특별히 설계된 컨트롤이며 .NET 표준 PropertyGrid를 직접 재사용하지 않습니다. 현재 `STNode`를 둘러쌉니다:

- 속성 설명자 읽기
- 렌더링 항목, 설명, 오류 영역
- 노드 제목 색상 또는 사용자 정의 색상을 기반으로 강조 표시
- 읽기 전용 및 편집 모드 전환 처리

'FrmSTNodePropertyInput'은 단일 속성 값을 편집하는 데 사용되는 지원 경량 입력 양식입니다.

### 노드 트리 및 조합 패널

`STNodeTreeView`는 현재 다음을 담당합니다.

- 노드 유형 트리 구성
- 검색 및 그룹 표시 유지
- 에디터 및 속성 패널과 연동

`STNodeEditorPannel` 다음:

-`STNodeEditor`
-`STNodeTreeView`
-`STNodePropertyGrid`

직접 사용할 수 있는 전체 패널로 결합하고 구분선, 확대/축소 프롬프트 및 연결 상태 프롬프트를 추가합니다.

이는 `ST.Library.UI`가 현재 단일 편집기 컨트롤이 아니라 결합된 호스트 패널의 비교적 완전한 세트를 제공한다는 것을 보여줍니다.

## ColorVision과의 현재 관계

이 저장소에서는 `ST.Library.UI`가 `FlowEngineLib` 및 해당 호스팅 계층의 인프라로 더 많이 사용됩니다. 현재 비즈니스 계층은 일반적으로 다음을 수행합니다.

-`STNode`를 상속하여 자신만의 노드 유형을 만듭니다.
- `STNodeEditor`를 프로세스 캔버스로 사용
- `STNodePropertyGrid`를 빌려 노드 속성을 노출합니다.
- 'STNodeTreeView'를 사용하여 노드 분류 및 드래그 앤 드롭 생성 관리

따라서 문서에서는 이를 비즈니스와 동일한 수준의 '프로세스 시스템'으로 기술해서는 안 됩니다. 프로세스 시스템 아래의 UI 기본 라이브러리입니다.

## 현재 가장 흔히 저지르는 실수 중 일부는 다음과 같습니다.

### WPF 프로세스 프레임워크가 아닌 WinForms 라이브러리입니다.

상위 메인 프로그램은 WPF를 광범위하게 사용하지만 `ST.Library.UI`의 현재 핵심 컨트롤은 여전히 WinForms `Control`입니다. 이 경계는 호스트가 내장되는 방식을 이해하는 데 중요합니다.

### 이 라이브러리는 단순한 편집기 컨트롤 이상의 기능을 제공합니다.

현재 `STNodeEditor` 외에도 노드 개체 모델, 포트 모델, 속성 그리드, 노드 트리 및 구성 패널이 있습니다. "캔버스 컨트롤"로 축약하면 실제 범위가 과소평가됩니다.

### 속성 편집은 시스템 PropertyGrid를 직접 사용하는 것이 아니라 사용자 정의 구현입니다.

`STNodePropertyGrid` 및 `FrmSTNodePropertyInput`은 라이브러리의 자체 노드 속성 편집 체인입니다. 문서에서 평소처럼 범용 반사 패널로 계속 설명하면 현재 독점 구현이 모호해집니다.

### 주로 상위 노드 시스템에서 소비됩니다.

현재 실제 사용법은 `ST.Library.UI`에 비즈니스 노드 로직을 직접 작성하는 대신 상위 계층에서 노드 유형을 정의한 다음 여기에서 편집기, 트리 및 속성 패널에 전달하는 것입니다.

## 추천읽기순서

1. `엔진/ST.Library.UI/NodeEditor/STNodeEditor.cs`
2. `엔진/ST.Library.UI/NodeEditor/STNode.cs`
3. `엔진/ST.Library.UI/NodeEditor/STNodeOption.cs`
4. `엔진/ST.Library.UI/NodeEditor/STNodePropertyGrid.cs`
5. `엔진/ST.Library.UI/NodeEditor/STNodeTreeView.cs`
6. `엔진/ST.Library.UI/NodeEditor/STNodeEditorPannel.cs`

이런 식으로 먼저 캔버스와 노드 모델을 설정한 다음 속성 패널과 노드 라이브러리가 어떻게 연결되어 있는지 이해할 수 있습니다.

## 계속 읽기

- [docs/04-api-reference/engine-comComponents/FlowEngineLib.md](./FlowEngineLib.md)
- [docs/04-api-reference/extensions/flow-node.md](../extensions/flow-node.md)
- [문서/03-아키텍처/컴포넌트/엔진/플로우-엔진.md](../../03-아키텍처/컴포넌트/엔진/플로우-엔진.md)