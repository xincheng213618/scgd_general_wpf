# 엔진 구성요소 개요

이 장은 이제 현재 창고 구조와 직접 일치할 수 있는 엔진 측면 모듈 입구만 유지하며 더 이상 "버전 테이블 + 샘플 코드 + 통합 계층 청사진" 스타일의 이전 초안을 유지하지 않습니다.

## 이 장에서는 실제로 무엇을 다루고 있나요?

`Engine/` 디렉토리의 코드는 단일 알고리즘 라이브러리가 아니라 서로 협력하는 런타임 모듈 세트입니다:

- `ColorVision.Engine/`: 메인 엔진 계층, 서비스, 템플릿, MQTT, 데이터베이스 및 프로세스 액세스를 수행합니다.
- `FlowEngineLib/`: 프로세스 노드 및 실행 제어 코어.
- `cvColorVision/`: 기본 기능 캡슐화 및 상호 운용성 브리징.
- `ColorVision.FileIO/`: 이미지와 사용자 정의 형식 파일을 읽고 씁니다.
- `ST.Library.UI/`: 노드 편집기 및 관련 UI 기본 컨트롤입니다.

따라서 엔진 장을 읽을 때 "단순한 알고리즘 구현"으로 해석하지 마십시오. 또한 런타임 조정, 프로세스 실행, 기본 캡슐화 및 편집기 지원 계층도 포함됩니다.

## 이 장을 읽는 방법

엔진 코드를 처음 입력하는 경우 다음 순서로 인식을 설정하는 것이 좋습니다.

1. 먼저 'ColorVision.Engine'을 살펴보고 서비스, 템플릿 및 프로세스가 기본 프로그램에 의해 어떻게 연결되는지 이해합니다.
2. 'FlowEngineLib'을 다시 살펴보고 노드 실행, 체인 시작/종료 및 프로세스 완료 이벤트가 어디서 오는지 이해합니다.
3. 그런 다음 'ColorVision.FileIO' 및 'cvColorVision'을 추가하여 파일 읽기 및 쓰기 계층을 기본 알고리즘/장치 캡슐화 계층과 구별합니다.
4. 마지막으로 프로세스 편집기가 의존하는 노드 UI 인프라를 이해하려면 'ST.Library.UI'를 살펴보세요.

## 모듈 맵

### 메인 엔진 레이어

- [ColorVision.Engine](./ColorVision.Engine.md): 현재 시스템에서 가장 중요한 엔진 항목으로, 주로 `Services/`, `Templates/`, `MQTT/`, `Messages/` 및 기타 디렉토리에 중점을 둡니다.

### 프로세스 실행 레이어

- [FlowEngineLib](./FlowEngineLib.md): 노드 실행 및 프로세스 제어 핵심이지만 완전한 실제 실행 체인이 되려면 `ColorVision.Engine/Templates/Flow/`와 함께 보아야 합니다.

### 하단 지지층

- [ColorVision.FileIO](./ColorVision.FileIO.md): 파일 형식, 가져오기 및 내보내기 및 관련 I/O 처리.
- [cvColorVision](./cvColorVision.md): 기본 시각적 기능 캡슐화 및 장치/알고리즘 상호 운용성 브리지입니다.

### 에디터 베이스 레이어

- [ST.Library.UI](./ST.Library.UI.md): 프로세스 노드 편집기 및 속성 패널과 같은 기본 UI 기능입니다.

## 현재 잘못 작성되기 쉬운 경계가 여러 개 있습니다.

- `ColorVision.Engine`은 "모든 알고리즘이 여기에서 계산되는" 단일 모듈이 아니며 템플릿, 장치, 프로세스 및 메시지 체인을 구성하는 데 더 가깝습니다.
- 'FlowEngineLib'은 전체 프로세스 시스템의 전체 구현이 아닙니다. 실제로 메인 프로그램에 들어가면 `Templates/Flow/`에서 템플릿과 창 레이어를 거쳐야 합니다.
- `cvColorVision` 및 `ColorVision.FileIO`는 모두 지원 레이어에 속하며 템플릿/UI 측 기능과 동일한 레이어에 혼합되어서는 안 됩니다.
- `Engine/ColorVision.ShellExtension/`은 현재 소스 트리에 존재하지만, 이 장에서는 이를 안정적인 API 참조 항목으로 확장하지 않았습니다.

## 소스코드 앵커를 먼저 읽어보는 것을 권장합니다

엔진 측의 실제 제어 화면을 이해하는 것이 목표라면 이전 문서를 먼저 살펴보는 것보다 이 코드를 먼저 살펴보는 것이 더 효과적입니다.

- `엔진/ColorVision.Engine/템플릿/TemplateContorl.cs`
- `엔진/ColorVision.Engine/템플릿/TemplateManagerWindow.xaml.cs`
- `엔진/ColorVision.Engine/템플릿/Flow/TemplateFlow.cs`
- `엔진/FlowEngineLib/FlowEngineControl.cs`
- `엔진/FlowEngineLib/Start/BaseStartNode.cs`
- `엔진/FlowEngineLib/End/CVEndNode.cs`

## 계속 읽기

- [템플릿 모듈 분석](../../03-architecture/comComponents/templates/analytic.md)
- [FlowEngineLib 아키텍처](../../03-architecture/comComponents/engine/flow-engine.md)
- [시스템 런타임](../../03-architecture/overview/runtime.md)