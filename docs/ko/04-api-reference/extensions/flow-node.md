# FlowEngineLib 노드 확장

이 페이지에서는 현재 창고에서 실제 사용 가능한 Flow 노드 확장 경로에 대해서만 설명하며, 도식 API를 기반으로 한 이전 버전의 "개발 가이드"는 더 이상 유지되지 않습니다.

## 먼저 노드 시스템이 실제로 어떤 모습인지 살펴보겠습니다.

현재 코드로 판단하면 Flow 노드 확장은 주로 다음 기본 클래스를 중심으로 진행됩니다.

- `CVCommonNode`: `NodeName`, `NodeType`, `DeviceCode`, `NodeID`, `ZIndex` 및 `nodeEvent` / `nodeRunEvent` / `nodeEndEvent`와 같은 공개 기능을 제공하는 모든 노드의 공통 기본 클래스입니다.
- `BaseStartNode`: `CVStartCFC` 생성, `startActions` 실행 유지, 프로세스 종료 시 `Finished` 발생을 담당하는 프로세스 시작 노드입니다.
- `CVBaseServerNode`: 입력 및 출력, MQTT 요청 어셈블리, 시간 초과 처리 및 노드 수준 완료 포스트백을 담당하는 가장 일반적인 서비스/알고리즘 클래스 노드 기본 클래스입니다.
- `CVEndNode`: 프로세스의 끝 노드이며, 마지막으로 `startAction.FireFinished()`를 호출하여 전체 프로세스가 완료된 것으로 표시합니다.

이는 현재 노드 확장이 "단순히 인터페이스를 구현하는" 경량 플러그인 모델이 아니라 'STNode' 및 구체적인 기본 클래스 세트를 기반으로 직접 구축된다는 것을 의미합니다.

## 현재 가장 살펴볼 가치가 있는 코드 앵커

노드를 추가하거나 이해하려면 먼저 다음 파일을 읽으십시오.

- `엔진/FlowEngineLib/Base/CVCommonNode.cs`
- `엔진/FlowEngineLib/Base/CVBaseServerNode.cs`
- `엔진/FlowEngineLib/Start/BaseStartNode.cs`
- `엔진/FlowEngineLib/End/CVEndNode.cs`
- `엔진/FlowEngineLib/Algorithm/AlgorithmNode.cs`

그 중 `AlgorithmNode`는 매우 전형적인 실제 사례로, 노드 내부에서 그래프를 직접 계산하는 것이 아니라 템플릿, 색상, 이미지 경로 등의 매개변수를 수집한 후 서버로 전송되는 실제 요청 데이터를 철자한다.

## 현재 서비스 노드가 확장되는 방식

'CVBaseServerNode' 구현으로 판단하면 현재 가장 일반적인 확장 방법은 다음과 같습니다.

1. 'CVBaseServerNode'를 상속받습니다.
2. 생성자에서 제목 'NodeType', 서비스 이름 및 장치 코드를 결정하고 'operatorCode'와 같은 노드 동작 필드를 설정합니다.
3. `OnCreate()`에 입력, 출력 또는 편집 컨트롤을 추가합니다.
4. `getBaseEventData(CVStartCFC start)`를 재정의하여 실제로 실행 끝으로 전송되는 매개변수 개체를 조립합니다.
5. 'OnServerResponse(...)', 'Reset(...)'을 다시 작성하거나 필요한 경우 관련 가상 메서드를 연결하여 응답 처리 및 정리 논리를 보완합니다.

"`DoServerWork`를 다시 작성하면 노드 개발이 완료됩니다"라는 이전 문서의 설명은 현재 `CVBaseServerNode`의 실제 구현과 일치하지 않습니다.

## 현 상태에 더 가까운 뼈대

```csharp
FlowEngineLib.Algorithm을 사용합니다.
FlowEngineLib.Base 사용;
FlowEngineLib.MQTT 사용;
ST.Library.UI.NodeEditor 사용;

[STNode("/사용자 정의/내 노드")]
공개 클래스 MyNode : CVBaseServerNode
{
    공개MyNode()
        : base("사용자 정의 노드", "알고리즘", "SVR.Custom", "DEV.Custom")
    {
        OperatorCode = "CustomEvent";
    }

    보호된 재정의 무효 OnCreate()
    {
        base.OnCreate();
        CreateTempControl(m_custom_item);
    }

    보호된 재정의 객체 getBaseEventData(CVStartCFC start)
    {
        var param = 새로운 AlgorithmParam();
        빌드온도(param);
        BuildImageParam(param);
        반환 매개변수;
    }

    보호된 재정의 void OnServerResponse(CVServerResponse resp, CVStartCFC startCFC)
    {
        base.OnServerResponse(resp, startCFC);
        // 필요에 따라 반환 데이터를 처리합니다.
    }
}
```
이 뼈대는 현재 코드에 더 가깝습니다. 노드의 핵심은 일반적으로 노드 자체에서 전체 비즈니스 계산을 완료하는 것이 아니라 "요청 데이터를 구성하고 기존 실행 체인에 연결하는 방법"입니다.

## 시작 노드와 끝 노드는 각각 무엇을 제어하나요?

### `BaseStartNode`

시작 노드는 현재 다음을 담당합니다.

- `CVStartCFC` 생성 및 저장
- `m_op_start` 및 여러 `m_op_loop`를 통해 시작 작업을 배포합니다.
- `Ready`, `Running` 및 진행 중인 `startActions` 관리
- 프로세스가 실제로 종료된 후 `Finished`를 발생시킵니다.

따라서 프로세스 입력 노드를 확장하는 경우 템플릿 매개변수가 아닌 시작, 루프 출력 및 프로세스 상태 관리에 중점을 둡니다.

### `CVEndNode`

최종 노드는 현재 다음을 담당합니다.

- `CVStartCFC`를 수신하거나 루프를 실행하여 작업을 계속합니다.
- `DoNodeEnded(...)`에서 `startAction.DoFinishing()`을 호출합니다.
-`startAction.FireFinished()`에 대한 최종 호출

이는 현재 코드에서 "전체 프로세스가 완료되었습니다"의 실제 종료이기도 합니다.

## 현재 가장 흔히 저지르는 실수 중 일부는 다음과 같습니다.

### `nodeEndEvent`는 프로세스 완료와 동일하지 않습니다.

`CVCommonNode.nodeEndEvent`는 노드 수준 종료 피드백만 나타냅니다. 전체 프로세스는 `CVEndNode`로 이동한 다음 `startAction.FireFinished()`에 의해 트리거되어야 합니다.

### 존재하지 않는 `DoServerWork` 주변에 새 노드를 설계하지 마세요.

현재 `CVBaseServerNode`의 실제 확장 지점은 다음과 더 가깝습니다.

-`OnCreate()`
- `getBaseEventData(...)`
- `OnServerResponse(...)`
- `리셋(...)`

기존 문서에 따라 `DoServerWork`를 찾아보면 확장 경로를 직접적으로 오해하게 됩니다.

### 노드와 서비스 토픽은 자동으로 범용 일치로 추론되지 않습니다.`CVBaseServerNode`는 현재 `GetSendTopic()`, `GetRecvTopic()`, `operatorCode` 및 `FlowServiceManager`를 통해 메시지 체인과 협력합니다. 이러한 필드가 서버 계약과 일치하지 않으면 노드가 시간 초과되거나 응답을 받지 못합니다.

### 분류 경로에 대한 단일 고정 사양은 없습니다.

`[STNode("...")]`의 현재 경로 문자열은 실제 트리 구조의 일부이지만 웨어하우스의 기존 노드에는 `/00 Global`, `/03_2 Algorithm` 및 기타 스타일이 혼합되어 있습니다. 확장은 이전 문서에서 가정된 분류 테이블을 복사하는 대신 인접 노드의 기존 그룹화를 따라야 합니다.

## 추천읽기순서

1. `CVCommonNode`: 먼저 공용 속성, 이벤트 및 제어 보조 메서드를 이해합니다.
2. `CVBaseServerNode`: 일반적인 서비스 노드가 요청을 시작하고, 응답을 기다리고, 시간 초과를 처리하는 방법을 살펴보겠습니다.
3. `BaseStartNode`: 프로세스 시작, 루프 출력 및 `Finished` 이벤트 소스를 이해합니다.
4. `CVEndNode`: 프로세스가 끝나고 루프가 닫히는 위치를 확인합니다.
5. `AlgorithmNode` 또는 기타 인접한 실제 노드: 이전 튜토리얼 템플릿에서 시작하는 대신 기존 노드에 따라 최종적으로 확장합니다.

## 계속 읽기

- [FlowEngineLib 아키텍처](../../03-architecture/comComponents/engine/flow-engine.md)
- [엔진 구성요소 개요](../engine-comComponents/README.md)
- [알고리즘 시스템 개요](../algorithms/overview.md)
