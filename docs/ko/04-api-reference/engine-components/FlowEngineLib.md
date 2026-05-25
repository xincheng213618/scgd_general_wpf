#FlowEngineLib

이 페이지에서는 현재 웨어하우스에서 사용할 수 있는 실제 FlowEngineLib 구현에 대해서만 설명하고 "클래스 다이어그램 + 이상적인 데이터 흐름 + 의사 API 테이블"의 이전 초안을 더 이상 유지하지 않습니다.

## 먼저 이 모듈이 무엇인지 살펴보겠습니다.

현재 소스 코드 상태에 따르면 FlowEngineLib은 추상적인 프로세스 설계 개념이 아니라 노드 편집기에 직접 구축된 런타임 실행 코어 세트입니다. 현재 최소한 네 가지 유형의 작업을 수행합니다.

- 호스트 프로세스 캔버스 및 노드 개체.
- 시작 노드, 서비스 노드 및 로드된 캔버스를 관리합니다.
- 'FlowNodeManager'의 장치 보기에 노드를 추가합니다.
- 시작 노드와 끝 노드 사이의 전체 프로세스를 닫는 완료 이벤트입니다.

따라서 이전 문서처럼 호스트와 독립적으로 존재하는 범용 DSL 플랫폼보다는 "노드 실행 커널"에 더 가깝습니다.

## 현재 가장 중요한 파일

- `엔진/FlowEngineLib/FlowEngineControl.cs`
- `엔진/FlowEngineLib/CVFlowContainer.cs`
- `엔진/FlowEngineLib/Base/CVCommonNode.cs`
- `엔진/FlowEngineLib/Base/CVBaseServerNode.cs`
- `엔진/FlowEngineLib/Start/BaseStartNode.cs`
- `엔진/FlowEngineLib/End/CVEndNode.cs`
- `엔진/FlowEngineLib/Algorithm/AlgorithmNode.cs`
- `엔진/FlowEngineLib/Base/CVStartCFC.cs`

프로세스가 어떻게 로드되고, 시작되고, 전달되고, 끝나는지 알고 싶다면 이 코드가 이미 기본 링크를 다루고 있습니다.

## 현재 제어 평면을 계층화하는 방법

### 프로세스 컨트롤러

'FlowEngineControl'은 현재 핵심 런타임 컨트롤러입니다. 구현에 따르면 다음을 담당합니다.

-'STNodeEditor' 후크
- 추적 시작 노드 사전 `startNodeNames`
- 서비스 노드 사전 '서비스' 추적
- 캐시 로드 캔버스 `loadedCanvas`
- 프로세스 완료 이벤트 `Finished` 트리거

노드가 편집기에 들어간 후 `FlowEngineControl`은 `NodeAdded` 이벤트에서 노드를 두 가지 범주로 나눕니다.

- `BaseStartNode`는 시작 노드 사전에 들어가고 `Finished`를 구독합니다.
- `CVBaseServerNode`는 서비스 노드 컬렉션에 들어가 `FlowNodeManager`와 동기화됩니다.

기존 문서의 "그래프 로딩 후 바로 실행"이라는 설명보다 실제 구현에 더 가깝습니다.

### 다중 프로세스 컨테이너

`CVFlowContainer`는 `FlowEngineControl`에 인접한 또 다른 제어 라인입니다. 다음을 유지합니다.

- 여러 시작 노드 매핑
-`startNodesFlowMap`
- 추가/로드/시작 조합 기능

이는 FlowEngineLib이 현재 단일 고정 캔버스를 제공할 뿐만 아니라 키별로 프로세스를 추가하고 시작하는 시나리오도 고려하고 있음을 보여줍니다.

## 현재 노드 시스템은 실제로 어떤 모습인가요?

### `CVCommonNode`

이는 모든 핵심 노드에 대한 공통 기본 클래스이며 현재 다음을 제공합니다.

- `노드 이름`
- `노드 유형`
- `디바이스 코드`
- `노드ID`
- `Z인덱스`
- `노드이벤트`
-`nodeRunEvent`
- `nodeEndEvent`

또한 컨트롤 생성 도우미 메서드를 통일하고 `OnOwnerChanged()` 시 노드 에디터에 유형 색상을 등록합니다.

### `BaseStartNode`

시작 노드는 현재 다음을 담당합니다.

- 여러 개의 'OUT_LOOP' 출력으로 'OUT_START' 생성
- `Ready`, `Running` 및 `startActions` 유지
- 연결된 노드의 첫 번째 배치에 `CVStartCFC`를 배포합니다.
- 프로세스가 완료된 후 'Finished'가 발생합니다.

따라서 프로세스의 "시작"은 외부 컨트롤러만으로 완료되지 않고 시작 노드 내부에서 구현됩니다.

### `CVBaseServerNode`

이는 현재 가장 일반적인 실행 노드 기본 클래스입니다. 구현에 따르면 다음을 담당합니다.

- 'IN' / 'OUT' 및 기타 노드 포트 생성
- 템플릿 ID, 템플릿 이름, 이미지 파일 이름, 토큰 및 시간 제한 구성 유지
- 기본 요청 데이터 수집
- 서버 응답을 수신하고 프로세스를 계속 전달합니다.

이전 문서에 항상 등장했던 `DoServerWork`는 현재 강조해야 할 확장 측면이 아닙니다. 이제 더 실제적인 초점은 `OnCreate()`, 요청 매개변수 구성, 응답 처리 및 재설정 논리입니다.

### `CVEndNode`

현재 최종 노드가 수행하는 작업은 매우 구체적입니다.

- `CVStartCFC` 수신 또는 다음 입력 루프
- `startAction.DoFinishing()` 호출
- 마지막으로 `startAction.FireFinished()`를 호출합니다.

이는 전체 프로세스가 완료된 실제 폐쇄 루프 위치입니다.

### `알고리즘 노드`

'AlgorithmNode'는 서비스 노드를 이해하는 대표적인 예입니다. 현재 수행되는 작업은 다음과 같습니다.

- 운영자 유형, 템플릿, POI 템플릿, 색상 및 캐시 길이 유지
- `OnCreate()`에서 노드 내 편집 컨트롤을 만듭니다.
- `getBaseEventData(...)`의 알고리즘 요청 매개변수에 템플릿, 이미지, 색상 및 SMU 데이터를 압축합니다.

이는 FlowEngineLib 현재 노드의 핵심 작업이 노드에서 로컬로 전체 알고리즘을 실행하는 것이 아니라 "실행 매개변수를 구축 및 전달"하는 것임을 다시 한 번 보여줍니다.

## 현재 프로세스 완료 체인을 닫는 방법은 무엇입니까?

'CVStartCFC'는 현재 노드 간에 전체 프로세스 상태를 전송하는 핵심 개체입니다. 다음 내용이 기록됩니다.

- 시작 및 종료 시간
- 처리현황
- IMEI
- 데이터 사전
- 해당 시작 노드

프로세스가 끝나면 `CVEndNode`는 `DoFinishing()` 및 `FireFinished()`를 호출한 다음 `BaseStartNode`의 `Finished` 이벤트로 돌아가고 마지막으로 `FlowEngineControl`은 `FlowEngineEventArgs`를 외부 세계로 던집니다.

이 체인이 연결되어 있지 않으면 "노드 끝"과 "프로세스 끝"을 같은 것으로 혼동하기 쉽습니다.

## 현재 코드와 호스트 코드 사이의 경계

FlowEngineLib 자체는 노드 실행 커널만 담당합니다. 실제로 ColorVision 메인 프로그램에 연결하는 것은 `Engine/ColorVision.Engine/Templates/Flow/` 레이어입니다. 예를 들면 다음과 같습니다:

- `FlowEngineManager.cs`
-`DisplayFlow.xaml.cs`
- `TemplateFlow.cs`

여기에 책임이 있습니다.

- MQTT RC 서비스 토큰과 결합된 새로 고침 프로세스 캔버스
- Base64의 프로세스 템플릿을 컨트롤러에 로드합니다.
- UI에서 프로세스 선택, 편집 및 실행

따라서 템플릿 레이어를 보지 않고 FlowEngineLib만 읽으면 "어떻게 실행하는지"는 알 수 있지만 "메인 프로그램에서 실행하도록 트리거한 사람"은 알 수 없습니다.

## 현재 가장 흔히 저지르는 실수 중 일부는 다음과 같습니다.

### 호스트 수준의 완전한 워크플로 시스템의 전체 코드는 아닙니다.FlowEngineLib은 노드 실행 커널만 구현합니다. 기본 프로그램에 들어간 후 템플릿 관리, 창 상호 작용 및 데이터 로딩은 여전히 ​​`ColorVision.Engine/Templates/Flow/` 레이어에 있습니다.

### "노드 완료"는 "프로세스 완료"와 동일하지 않습니다.

현재 실제로 프로세스를 완료하는 것은 `nodeEndEvent`를 발행하는 노드가 아닌 `CVEndNode -> CVStartCFC.FireFinished() -> BaseStartNode.Finished -> FlowEngineControl.Finished` 체인입니다.

### 서비스 노드 확장점은 더 이상 기존 초안 작성 방식으로 이해하면 안 됩니다.

현재 실제 확장 경로는 다음과 같습니다.

-`OnCreate()`
- 매개변수 조립
- 응답 처리
-`재설정()`

문서에서 평소처럼 통합된 "로컬 실행 비즈니스 기능"을 계속 찾으면 노드 모델을 오해하게 됩니다.

### `loadedCanvas`는 장식 캐시가 아닙니다.

`FlowEngineControl`과 `CVFlowContainer`는 모두 캔버스 콘텐츠 해싱을 사용하여 반복 로드를 방지합니다. 이 세부 사항은 동일한 프로세스가 다시 빌드되지 않는 이유를 이해하는 데 영향을 미칩니다.

## 추천읽기순서

1. `엔진/FlowEngineLib/FlowEngineControl.cs`
2. `엔진/FlowEngineLib/Base/CVCommonNode.cs`
3. `엔진/FlowEngineLib/Start/BaseStartNode.cs`
4. `엔진/FlowEngineLib/Base/CVBaseServerNode.cs`
5. `엔진/FlowEngineLib/End/CVEndNode.cs`
6. `엔진/FlowEngineLib/알고리즘/AlgorithmNode.cs`
7. `엔진/FlowEngineLib/Base/CVStartCFC.cs`
8. `엔진/ColorVision.Engine/템플릿/Flow/DisplayFlow.xaml.cs`

이를 통해 먼저 커널 인식을 설정한 다음 이를 호스트 측 UI 트리거 체인에 연결할 수 있습니다.

## 계속 읽기

- [docs/04-api-reference/extensions/flow-node.md](../extensions/flow-node.md)
- [문서/03-아키텍처/컴포넌트/엔진/플로우-엔진.md](../../03-아키텍처/컴포넌트/엔진/플로우-엔진.md)
- [docs/04-api-reference/engine-comComponents/ColorVision.Engine.md](./ColorVision.Engine.md)