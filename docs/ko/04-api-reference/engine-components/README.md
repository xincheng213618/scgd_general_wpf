# 엔진 구성요소 개요

이 장은 이제 현재 창고 구조와 직접 일치할 수 있는 엔진 측면 모듈 입구만 유지하며 더 이상 "버전 테이블 + 샘플 코드 + 통합 계층 청사진" 스타일의 이전 초안을 유지하지 않습니다.

## 이 장에서는 실제로 무엇을 다루고 있나요?

`Engine/` 디렉토리의 코드는 단일 알고리즘 라이브러리가 아니라 서로 협력하는 런타임 모듈 세트입니다:

- `ColorVision.Engine/`: 메인 엔진 계층, 서비스, 템플릿, MQTT, 데이터베이스 및 프로세스 액세스를 수행합니다.
- `FlowEngineLib/`: 프로세스 노드 및 실행 제어 코어.
- `cvColorVision/`: 기본 기능 캡슐화 및 상호 운용성 브리징.
- `ColorVision.FileIO/`: 이미지와 사용자 정의 형식 파일을 읽고 씁니다.
- `ST.Library.UI/`: 노드 편집기 및 관련 UI 기본 컨트롤입니다.
- `ColorVision.ShellExtension/`: Windows Explorer의 `.cvraw` / `.cvcie` thumbnail extension.

따라서 엔진 장을 읽을 때 "단순한 알고리즘 구현"으로 해석하지 마십시오. 또한 런타임 조정, 프로세스 실행, 기본 캡슐화 및 편집기 지원 계층도 포함됩니다.

## 인수인계 먼저 읽기

Engine 업무 로직을 넘겨받는다면 개별 모듈보다 먼저 아래 페이지를 읽는 것이 좋습니다.

- [현재 Engine 문서 커버리지](./current-engine-coverage.md): `Engine/` project, key business directory, handoff page 매핑을 확인합니다.
- [Engine 업무 체인 매트릭스](./business-flow-matrix.md): 업무 시나리오에서 코드 진입점, 설정, 수락 확인을 찾습니다.
- [Engine 업무 시나리오 인수인계 플레이북](./business-scenario-playbook.md): 자주 나오는 요구사항과 장애 설명에서 변경 위치를 판단합니다.
- [Engine 업무 인수인계 매뉴얼](./business-handoff.md): 디바이스 리소스, 템플릿, Flow, 결과, 프로젝트 출력을 하나의 흐름으로 설명합니다.
- [Engine 런타임 객체 맵](./runtime-object-map.md): 클래스명으로 책임, 출처, 첫 확인점을 찾습니다.
- [디바이스 서비스 체인](./device-service-chain.md): DB 리소스가 `DeviceService`, MQTT, Flow 선택지가 되는 흐름입니다.
- [템플릿 및 Flow 체인](./template-flow-chain.md): 템플릿, 노드 설정, Flow 저장/가져오기/실행을 설명합니다.
- [Flow 변환 및 보정 노드](./flow-conversion-calibration-nodes.md): 데이터 변환, 이미지 변환, 보정, 보정 ROI, 기존 색차 보정 노드의 실제 진입점입니다.
- [결과 표시 및 프로젝트 인수인계 체인](./result-handoff-chain.md): 알고리즘 결과, 이미지 오버레이, `Projects/*` 의 경계를 정리합니다.

## 이 장을 읽는 방법

엔진 코드를 처음 입력하는 경우 다음 순서로 인식을 설정하는 것이 좋습니다.

1. 먼저 'ColorVision.Engine'을 살펴보고 서비스, 템플릿 및 프로세스가 기본 프로그램에 의해 어떻게 연결되는지 이해합니다.
2. 'FlowEngineLib'을 다시 살펴보고 노드 실행, 체인 시작/종료 및 프로세스 완료 이벤트가 어디서 오는지 이해합니다.
3. 그런 다음 'ColorVision.FileIO' 및 'cvColorVision'을 추가하여 파일 읽기 및 쓰기 계층을 기본 알고리즘/장치 캡슐화 계층과 구별합니다.
4. 마지막으로 프로세스 편집기가 의존하는 노드 UI 인프라를 이해하려면 'ST.Library.UI'를 살펴보세요.
5. Explorer 파일 미리보기 문제라면 `ColorVision.ShellExtension`을 확인합니다. 이 모듈은 main business chain이 아닙니다.

## 모듈 맵

### 메인 엔진 레이어

- [ColorVision.Engine](./ColorVision.Engine.md): 현재 시스템에서 가장 중요한 엔진 항목으로, 주로 `Services/`, `Templates/`, `MQTT/`, `Messages/` 및 기타 디렉토리에 중점을 둡니다.
- [Engine 업무 인수인계 매뉴얼](./business-handoff.md): 디바이스, 템플릿, Flow, 결과, 프로젝트를 인수인계 관점으로 연결합니다.

### 프로세스 실행 레이어

- [FlowEngineLib](./FlowEngineLib.md): 노드 실행 및 프로세스 제어 핵심이지만 완전한 실제 실행 체인이 되려면 `ColorVision.Engine/Templates/Flow/`와 함께 보아야 합니다.
- [템플릿 및 Flow 체인](./template-flow-chain.md): Flow 템플릿, 노드 설정기, 실행 수락 확인을 보충합니다.

### 하단 지지층

- [ColorVision.FileIO](./ColorVision.FileIO.md): 파일 형식, 가져오기 및 내보내기 및 관련 I/O 처리.
- [cvColorVision](./cvColorVision.md): 기본 시각적 기능 캡슐화 및 장치/알고리즘 상호 운용성 브리지입니다.

### 에디터 베이스 레이어

- [ST.Library.UI](./ST.Library.UI.md): 프로세스 노드 편집기 및 속성 패널과 같은 기본 UI 기능입니다.

### 외부 Shell 통합 레이어

- [ColorVision.ShellExtension](./ColorVision.ShellExtension.md): Explorer thumbnail extension, COM registration, OpenCvSharp runtime, registry, rollback acceptance.

## 현재 잘못 작성되기 쉬운 경계가 여러 개 있습니다.

- `ColorVision.Engine`은 "모든 알고리즘이 여기에서 계산되는" 단일 모듈이 아니며 템플릿, 장치, 프로세스 및 메시지 체인을 구성하는 데 더 가깝습니다.
- 'FlowEngineLib'은 전체 프로세스 시스템의 전체 구현이 아닙니다. 실제로 메인 프로그램에 들어가면 `Templates/Flow/`에서 템플릿과 창 레이어를 거쳐야 합니다.
- `cvColorVision` 및 `ColorVision.FileIO`는 모두 지원 레이어에 속하며 템플릿/UI 측 기능과 동일한 레이어에 혼합되어서는 안 됩니다.
- `ColorVision.ShellExtension`은 Engine main business chain이 아니며 Explorer 안의 `.cvraw` / `.cvcie` thumbnail preview만 담당합니다.

## 소스코드 앵커를 먼저 읽어보는 것을 권장합니다

엔진 측의 실제 제어 화면을 이해하는 것이 목표라면 이전 문서를 먼저 살펴보는 것보다 이 코드를 먼저 살펴보는 것이 더 효과적입니다.

- `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
- `Engine/ColorVision.Engine/Templates/TemplateManagerWindow.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
- `Engine/FlowEngineLib/FlowEngineControl.cs`
- `Engine/FlowEngineLib/Start/BaseStartNode.cs`
- `Engine/FlowEngineLib/End/CVEndNode.cs`
- `Engine/ColorVision.ShellExtension/CVThumbnailProviderBase.cs`

## 계속 읽기

- [템플릿 모듈 분석](../../03-architecture/components/templates/analysis.md)
- [FlowEngineLib 아키텍처](../../03-architecture/components/engine/flow-engine.md)
- [Flow 변환 및 보정 노드](./flow-conversion-calibration-nodes.md)
- [ColorVision.ShellExtension](./ColorVision.ShellExtension.md)
- [시스템 런타임](../../03-architecture/overview/runtime.md)
