#FlowEngineLib 아키텍처

이 페이지에서는 현재 웨어하우스에서 실제로 실행되는 프로세스 편집 및 실행 체인에 대해서만 설명합니다. FlowEngineLib를 독립적인 계층 프레임워크로 작성하는 이전 초안은 더 이상 유지되지 않습니다.

## 먼저 시스템 내 위치를 살펴보세요.

FlowEngine 관련 기능은 `Engine/FlowEngineLib/`에만 존재하지 않습니다. 현재 실제 사용 체인은 두 계층에 걸쳐 있습니다.

- `FlowEngineLib/`는 노드 에디터에서 실행 제어, 시작 노드, 끝 노드, 서비스 노드에 대한 기본 기능을 제공합니다.
- `Engine/ColorVision.Engine/Templates/Flow/`는 프로세스 템플릿, 편집 창, 실행 창 및 일괄 처리를 기본 프로그램에 연결하는 역할을 담당합니다.

따라서 FlowEngine 아키텍처를 논의할 때 라이브러리 자체에 대해 작성하는 것만으로는 실제 런타임의 절반을 놓칠 수 있습니다.

## 현재 가장 중요한 객체

### `FlowEngineControl`

`FlowEngineControl`은 제어를 실행하는 중심 개체입니다. 현재 다음을 담당하고 있습니다.

- `STNodeEditor` 바인딩
- 에디터에 노드가 추가되면 시작 노드와 서비스 노드를 식별합니다.
- `startNodeNames` 및 `services` 유지
- 파일 또는 Base64에서 캔버스 로드
- 시작 노드를 선택하고 프로세스를 시작 및 중지합니다.
- 시작 노드가 완료되면 `Finished`를 발생시킵니다.

즉, 추상적인 스케줄링 인터페이스가 아니라 노드 편집기 인스턴스, 노드 개체 및 서비스 컬렉션에 직접 연결된 런타임 컨트롤러입니다.

### `BaseStartNode`

시작 노드는 'CVStartCFC'로 실행되는 프로세스를 캡슐화하고 흐름도에 따라 시작, 중지 및 완료 이벤트를 전달하는 역할을 합니다.

현재 시사점은 다음과 같습니다.

- `Start(serialNumber)`는 `CVStartCFC`를 생성합니다.
- `DoDispatch(...)`는 작업 다운스트림을 전달합니다.
- `FireFinished(...)`는 프로세스 종료 이벤트가 실제로 발생하는 곳입니다.

따라서 "프로세스 완료"는 컨트롤러 자체에서 추론되지 않고 궁극적으로 시작 노드에서 발행됩니다.

### `CVBaseServerNode`

대부분의 장치 노드와 알고리즘 노드는 'CVBaseServerNode' 시스템에 속합니다. 그들은 다음을 담당합니다:

- 런타임 작업을 보내고 기다립니다.
- 시간 초과, 실패 및 데이터 반환 처리
- 노드 결과를 다운스트림으로 전달
- `nodeEndEvent`를 통해 개별 노드 종료 상태 보고

여기서 `nodeEndEvent`는 중요하지만 노드 레벨의 끝을 나타낼 뿐 전체 프로세스가 종료되었음을 의미하지는 않습니다.

### `CVEndNode`

끝 노드는 프로세스 완료 체인의 마지막 홉입니다. 현재 구현에서는 최종 노드가 처리를 종료할 때 `startAction.FireFinished()`를 호출하여 전체 프로세스가 완료된 것으로 표시합니다.

이것이 "특정 노드의 실행이 완료되었습니다"와 "전체 프로세스가 완료되었습니다"가 시스템에서 서로 다른 두 가지 이유입니다.

## 프로세스는 실제로 어떻게 실행되나요?

현재 메인 체인은 대략 다음과 같습니다.

1. 'TemplateFlow' 또는 'FlowEngineToolWindow'는 프로세스 데이터를 준비합니다.
2. `FlowEngineToolWindow`는 `FlowEngineLib.dll`을 노드 편집기에 로드합니다.
3. 'FlowEngineControl'은 'STNodeEditor'를 바인딩하여 노드가 추가될 때 시작 노드와 서비스 노드를 식별합니다.
4. `LoadFromBase64(...)` 또는 `Load(...)`는 순서도를 캔버스에 로드합니다.
5. `StartNode(...)`는 시작 노드를 지정하도록 선택하거나 기본적으로 첫 번째 시작 노드를 지정합니다.
6. 'BaseStartNode'는 'CVStartCFC'를 생성하고 이를 다운스트림 노드에 전달합니다.
7. 각 'CVBaseServerNode' 파생 노드는 자체 작업, 시간 초과 및 데이터 전달을 처리합니다.
8. `CVEndNode`는 완료되면 `startAction.FireFinished()`를 호출합니다.
9. `BaseStartNode.Finished`가 트리거됩니다.
10. `FlowEngineControl.Start_Finished(...)`를 입력한 다음 이를 자체 `Finished` 이벤트로 변환합니다.

이 완료 체인은 이전 문서의 "특정 노드의 끝은 프로세스의 끝을 의미합니다"보다 엄격하며 현재 코드에 더 가깝습니다.

## 엔진 레이어는 이를 메인 프로그램에 어떻게 연결하나요?

### 프로세스 템플릿

'TemplateFlow'를 사용하면 순서도가 시스템에 템플릿으로 존재할 수 있으며 다음을 지원합니다.

- 템플릿 목록 관리
- 프로세스 편집기를 직접 열려면 두 번 클릭하세요.
- `.stn` / `.cvflow` 가져오기
- 프로세스 패키지를 가져올 때 프로세스 관련 템플릿

### 편집 창

'FlowEngineToolWindow'는 독립적인 프로세스 편집 화면입니다. 다음을 담당합니다.

- `STNodeEditor` 호스트
- `FlowEngineLib.dll` 로드
- 실행 취소, 다시 실행, 복사, 붙여넣기, 확대/축소 및 자동 정렬에 액세스합니다.
- `STNodeEditorHelper`를 통해 속성 패널과 노드 트리를 연결합니다.

따라서 현재 편집 환경에서는 'FlowEngineLib'이 자체 UI와 함께 제공되는 것이 아니라 엔진 레이어가 이를 선택하기 위해 WPF 창 레이어를 래핑하는 것입니다.

### 실행창

실제로 메인 프로그램의 일상적인 사용에 해당하는 것은 'DisplayFlow' 및 'FlowControl' 라인입니다.

'DisplayFlow'는 현재 다음을 담당합니다.

- 현재 프로세스 템플릿 새로 고침
- 시작하기 전에 전처리를 수행합니다.
- 모니터링 프로세스가 완료되었습니다.
- 실행 로그, 배치 정보 및 진행 상황 작성
- 프로세스 완료 후 사용자 정의 배치 트리거

이는 메인 프로그램에서 실행되는 프로세스가 단순히 "그래프 실행"이 아니라 배치 레코드, 로그 텍스트 및 후처리 확장과도 연결되어 있음을 보여줍니다.

## 잘못 쓰여지기 쉬운 현재의 경계는 무엇인가요?

### `nodeEndEvent`는 프로세스 완료 이벤트가 아닙니다.

`CVCommonNode`의 `nodeEndEvent`는 노드 수준 피드백에만 사용됩니다. 실제 프로세스 완료 체인은 다음과 같습니다.

- EndNode가 `startAction.FireFinished()`를 호출합니다.
- `CVStartCFC.FireFinished()`는 시작 노드로 돌아갑니다.
- `BaseStartNode.Finished`가 트리거됩니다.
- `FlowEngineControl.Finished`가 다시 버려집니다.

이 두 가지 이벤트가 합쳐지면 실패 전파, 진행 업데이트 및 최종 완료 판단이 모두 편향됩니다.

### 시작 노드는 어떤 노드도 아닙니다.

`FlowEngineControl`은 노드가 추가될 때 `startNodeNames`에 `BaseStartNode`만 포함합니다. 프로세스를 시작할 때 이름을 지정하지 않으면 기본적으로 첫 번째 시작 노드가 사용됩니다.

따라서 프로세스를 시작할 수 있는지 여부는 시작 노드가 존재하는지, 준비가 되었는지 여부와 직접적인 관련이 있습니다.

### 장애 전파는 노드 유형에 따라 다릅니다.

프로세스 실패는 컨트롤러에 의해 균일하게 결정되지 않습니다. 많은 오류, 시간 초과 또는 취소가 노드 내에서 내부적으로 생성된 다음 회선을 따라 전파됩니다. 특히 다중 입력 노드의 경우 오류 전파 동작은 컨트롤러의 표면 상태보다는 특정 노드 구현에 따라 달라집니다.

## 확장할 때 주로 빠지는 곳

### 새 노드

새 프로세스 노드를 추가하는 경우 일반적으로 'FlowEngineLib' 자체의 노드 구현과 시작, 끝 또는 서비스 노드 체인에 연결하는 방법에 중점을 둡니다.

### 새로운 템플릿 프로세스

새로운 유형의 편집 가능한 프로세스 템플릿이 추가되면 일반적으로 'TemplateFlow'에 인접한 템플릿 관리, 가져오기, 내보내기 및 편집 창 액세스에 중점을 둡니다.

### 노드 속성 패널 확장새로운 프로세스 노드 구성 UI를 추가하는 경우 일반적으로 노드 클래스 자체를 변경하는 대신 `STNodeEditorHelper` 또는 `NodeConfigurator`에 속하게 됩니다.

## 추천읽기순서

다음 줄을 읽는 것이 좋습니다.

1. `엔진/FlowEngineLib/FlowEngineControl.cs`
2. `엔진/FlowEngineLib/Start/BaseStartNode.cs`
3. `엔진/FlowEngineLib/End/CVEndNode.cs`
4. `엔진/FlowEngineLib/Base/CVBaseServerNode.cs`
5. `엔진/ColorVision.Engine/템플릿/Flow/TemplateFlow.cs`
6. `엔진/ColorVision.Engine/템플릿/Flow/FlowEngineToolWindow.xaml.cs`
7. `엔진/ColorVision.Engine/템플릿/Flow/DisplayFlow.xaml.cs`

이러한 방식으로 먼저 기본 실행 체인을 설정한 다음 편집 및 기본 프로그램 통합으로 돌아갈 수 있습니다.

## 이 페이지에서는 더 이상 아무것도 수행하지 않습니다.

이 페이지에서는 더 이상 다음과 같은 고위험 콘텐츠를 유지하지 않습니다.

- FlowEngineLib을 현재 구현과 분리된 표준 계층 프레임워크로 설명합니다.
- 추상적인 디자인 패턴 세트로 모든 노드 동작을 포괄합니다.
- MQTT, 로깅, 직렬화 및 기타 주변 장치를 독립적인 인프라 계층 약정으로 패키징

나중에 재구성 방향을 논의하려면 특정 실행 체인과 실제 노드 시스템을 출발점으로 삼아야 합니다.

## 계속 읽기

- [구성요소 상호작용](../../overview/comComponent-interactions.md)
- [아키텍처 런타임](../../overview/runtime.md)
- [템플릿 아키텍처 디자인](../templates/design.md)