#ColorVision.ImageEditor

이 페이지에서는 현재 구현된 UI/ColorVision.ImageEditor의 주요 제어 체인과 확장 지점에 대해서만 설명합니다. 이전 문서에서 "완전한 기능 + 튜토리얼 예제 + 성능 번호 약속"이라는 작성 방법을 더 이상 유지하지 않습니다.

## 모듈 포지셔닝

ColorVision.ImageEditor는 현재 단순한 그림 표시 컨트롤이 아니라 "이미지 호스트 + 확장 가능한 캔버스 + 그리기 도구 + 오프너 + 런타임 도구 오버레이 + 설정 시스템"의 결합 모듈 세트입니다.

메인 라인은 다음에 더 가깝습니다.

- 호스트로서의 `ImageView`
- 현재 뷰 런타임 컨테이너인 'EditorContext'
- 실제 드로잉 캔버스인 'DrawCanvas'
- `IEditorToolFactory`는 도구, 메뉴 및 오프너를 검색하고 조합하는 역할을 담당합니다.
- `IImageOpen`은 다양한 파일 형식의 열기 체인을 담당합니다.

## 현재 가장 중요한 디렉토리

프로젝트 디렉토리에서 가장 먼저 읽어야 할 내용은 다음과 같습니다.

- `ImageView.xaml(.cs)`: 메인 컨트롤 및 런타임 오케스트레이션 입구
- `EditorContext.cs`: 각 뷰 인스턴스에 대한 런타임 컨테이너
- `DrawCanvas.cs`: 시각적 트리 및 그리기 캔버스
- `EditorToolFactory.cs`: 도구, 메뉴, 오프너 검색 및 어셈블리
- `Abstractions/`: 편집기 확장 지점 경계
- `Draw/`: 그래픽 요소, 도구, 선택 상자 및 주석 가져오기 및 내보내기
- `EditorTools/`: 확대/축소, 의사 색상, 3D, 알고리즘, 전체 화면과 같은 비원시 도구
-`비디오/`: 비디오 오프너
- `Layers/`: 레이어/채널 전환 의미
- `Realtime/`, `Settings/`: 실시간 이미지 및 설정 지원

## 키 입력 유형

### 이미지뷰

`ImageView`는 현재 편집기 모듈의 주요 항목입니다. 다음을 담당합니다.

- `EditorContext` 초기화
- `DrawCanvas`, `Zoombox`, 컨텍스트 메뉴 및 상태 표시줄 바인딩
- `IImageComponent` 일회성 초기화 실행
- 표준 파일 명령 관리
- 배선 구성 변경, 스케일링 변경 및 오버레이 새로 고침
- 이미지 열기, 정리, 저장, 주석 가져오기 및 내보내기 등과 같은 프로세스를 처리합니다.

이 모듈을 이해하려면 첫 번째 진입점은 `ImageView.xaml.cs`입니다.

### EditorContext

`EditorContext`는 현재 중앙에 저장되어 있는 각 `ImageView` 인스턴스의 런타임 컨테이너입니다.

- 현재 `ImageView`
-`그리기 캔버스`
- '줌박스'
-`ImageViewConfig`
- `DrawEditorManager`
- `IEditorToolFactory`
- 현재 오프너, 상황에 맞는 메뉴, 기본 목록
- 경량 서비스 레지스트리 세트

이는 런타임 상태 컨테이너이자 로컬 서비스 로케이터이며 현재 모듈의 중요한 구현 경계이기도 합니다.

### 그리기캔버스

`DrawCanvas`는 실제 그림을 포함하는 레이어입니다. 컨트롤을 표시할 뿐만 아니라 다음 작업도 담당합니다.

- 시각적 객체 컬렉션 유지
- 적중 테스트 수행
- 그래픽 요소의 추가 및 삭제 처리
- 유지보수 실행 취소/다시 실행
- 다수의 그리기 도구에 마우스 이벤트를 연결하기 위한 대상 역할을 합니다.

### IEditorToolFactory

이름은 `IEditorToolFactory`이지만 현재는 실제로 인터페이스가 아닌 구체적인 클래스입니다. 건설 시 반사적으로 스캔되어 조립됩니다.

- `IDVContextMenu`
- `IIEditorToolContextMenu`
- `IEditorTool`
- `IImageComponent`
-`IImageOpen`

동시에 "전역 도구 + 현재 오프너 런타임 도구"의 효과적인 보기도 유지하고 `GuidId`에 따라 도구 모음을 덮어쓰고 다시 빌드합니다.

이는 현재 ImageEditor 초기화 비용과 확장 기능이 가장 집중되는 곳이기도 합니다.

### IImageOpen 및 확장 인터페이스

현재 오픈 체인은 통합 파일 관리자가 아닌 개별 `IImageOpen` 구현에 의해 처리됩니다.

또한 'IImageOpen'도 선택적으로 구현할 수 있습니다.

- `IImageOpenEditorToolProvider`
- `IImageOpenEditorToolLifecycle`

이러한 방식으로 특정 특수 파일 형식은 모든 분기를 전역 도구에 쌓는 대신 도구 모음 기능을 연 후 일시적으로 인계하거나 재정의할 수 있습니다.

### VideoOpen/Window3D/ModelViewer3DControl

비디오 및 3D 기능은 현재 편집기 모듈의 실제 하위 기능이지만 추가 오프너 또는 도구이며 전체 모듈의 유일한 기본 스레드는 아닙니다.

- `Video/VideoOpen.cs`: 비디오 오프너
- `EditorTools/ThreeD/Window3D.xaml.cs`: 이미지를 3D 표면 창으로
- `EditorTools/ThreeD/ModelViewer3DControl.xaml.cs`: OBJ/STL 보기 제어

이러한 기능은 이전 문서에서 많이 논의되지만 이를 읽는 더 안정적인 방법은 먼저 `ImageView`와 도구 팩토리 메인 체인을 이해하는 것입니다.

## 현재 런타임 메인 체인

기존 제어 체인은 대략 다음과 같습니다.

1. `ImageView`를 생성합니다.
2. 'EditorContext', 'SelectionVisual', 'CompactInspectorPresenter'를 초기화합니다.
3. 'IEditorToolFactory'를 생성하고 전역 도구, 상황에 맞는 메뉴, 이미지 구성 요소 및 오프너를 반사적으로 조합합니다.
4. `IImageComponent.Execute(ImageView)`를 모두 실행합니다.
5. 사용자가 파일을 연 후 확장자에 따라 `IImageOpen`을 선택합니다.
6. 오프너는 `SetImageSource(...)`를 호출하고 선택적으로 자체 런타임 도구를 제공합니다.
7. `DrawCanvas`, 오버레이, 상태 표시줄, 레이어 전환, 의사 색상 등은 현재 이미지 컨텍스트를 중심으로 계속 작동합니다.

## 현재 구현의 경계는 무엇입니까?

### ImageView는 순수한 디스플레이 컨트롤이 아닙니다.

`SetImageSource(...)`는 현재 `ImageShow.Source`를 설정할 뿐만 아니라 의사 색상 구성, 보정 서비스 및 기타 편집기 런타임 부작용을 유발할 수도 있습니다.

장면만 표시하는 경우 'EnableEditorImageServices'와 같은 스위치에 주의해야 합니다. 기본적으로 전체 ImageView를 부작용이 없는 사진 프레임으로 처리할 수는 없습니다.

### 도구 발견은 반영 중심입니다.

`IEditorToolFactory`는 현재 각 뷰 인스턴스에 대해 여러 라운드의 검색 및 생성을 수행합니다. 이것은 실제적이고 중요한 제어 체인입니다. 이전 문서의 "정적 도구 아키텍처 다이어그램"은 이 문제를 숨깁니다.

### EditorContext는 상태 컨테이너이자 서비스 로케이터 속성입니다.

현재 디자인은 "구성", "도구 상태" 및 "런타임 서비스"를 완전히 분리하지 않고 `EditorContext`, `ImageViewConfig` 및 소수의 서비스에 부분적으로 집중되어 있습니다. 이는 독서와 후속 재구성 과정에서 인정해야 할 현실입니다.

### 가져오기 및 내보내기에 대한 설명에는 실제 배치 지점이 있습니다.그래픽 요소 지속성은 개념적 수준에 머물지 않지만 현재 실제로 `Draw/Annotations/` 및 `ImageView`의 가져오기 및 내보내기 입구에 해당됩니다. 주석 기능을 읽을 때 "모든 그리기 도구는 자동으로 유지될 수 있습니다"라고 일반화하기보다는 이 체인을 직접 살펴봐야 합니다.

## 이 모듈을 읽는 방법이 현재 더 적합합니다.

### 메인 컨트롤 체인과 초기화 배열을 보고 싶습니다.

먼저 살펴보세요:

-`ImageView.xaml.cs`
- `EditorContext.cs`
- `EditorToolFactory.cs`

### 그리기 및 선택 논리를 보고 싶습니다.

먼저 살펴보세요:

- `DrawCanvas.cs`
- `그리기/`
- `추상화/그리기/`

### 파일 열기 체인 및 런타임 도구 적용 범위를 보고 싶습니다.

먼저 살펴보세요:

- `추상화/IImageEditor.cs`
- `비디오/VideoOpen.cs`
- 특정 오프너 구현이 위치한 디렉토리

### 주석, 의사 색상, 3D 등 하위 기능을 보고 싶습니다.

먼저 살펴보세요:

- `그리기/주석/`
- `EditorTools/PseudoColor/`
- `EditorTools/ThreeD/`

## 이 페이지에서는 더 이상 아무것도 수행하지 않습니다.

이 페이지에서는 더 이상 다음과 같은 고위험 콘텐츠를 유지하지 않습니다.

- 큰 성과 수치 약속
- 모든 모듈을 다루는 튜토리얼 예제 모음
-전체 모듈의 유일한 메인 라인으로 비디오 또는 3D를 작성합니다.
- 현재 코드와 일치하지 않는 통합된 뷰 모델 또는 추상 인터페이스를 구성합니다.

## 계속 읽기

- [UI 컴포넌트 개요](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Themes](./ColorVision.Themes.md)