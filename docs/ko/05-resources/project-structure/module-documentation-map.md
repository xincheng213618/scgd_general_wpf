# 모듈 및 문서 비교표

이 문서는 현재 창고 구조와 여전히 유효한 문서 항목만 유지하며, 이는 "코드의 위치와 먼저 읽을 문서"를 빠르게 찾는 데 사용됩니다.

## 문서 입력을 위한 코드 영역

| 코드 영역 | 관심 장소 | 선호하는 서류 항목 | 보충입구 |
| --- | --- | --- | --- |
| `컬러비전/` | 메인 프로그램 진입, 메인 창, 애플리케이션 시작 | [시작 가이드](../../00-getting-started/README.md) | [메인 창 탐색](../../01-user-guide/interface/main-window.md) |
| `UI/` | WPF UI 프레임워크, 테마, 편집기 | [UI 컴포넌트 개요](../../04-api-reference/ui-comComponents/README.md) | [사용자 가이드](../../01-user-guide/README.md) |
| `UI/ColorVision.SocketProtocol/` | TCP 서비스, JSON/Text 배포, 메시지 기록, 관리창 | [ColorVision.SocketProtocol](../../04-api-reference/ui-comComponents/ColorVision.SocketProtocol.md) | [소켓 통신 모듈 최적화 경로](../../02-developer-guide/performance/socket-protocol-optimization-roadmap.md) |
| `엔진/ColorVision.Engine/Services/` | 장치 서비스, 서비스 조정 | [장치 서비스 개요](../../01-user-guide/devices/overview.md) | [엔진 개발 가이드](../../02-developer-guide/engine-development/README.md) |
| `엔진/ColorVision.Engine/템플릿/` | 템플릿 시스템, 매개변수화된 알고리즘, 결과 처리 | [알고리즘 개요](../../04-api-reference/algorithms/README.md) | [템플릿 아키텍처 디자인](../../03-architecture/comComponents/templates/design.md) |
| `엔진/FlowEngineLib/` | 프로세스 노드, 실행 모델, 시각적 프로세스 | [FlowEngineLib 아키텍처](../../03-architecture/comComponents/engine/flow-engine.md) | [FlowNode 개발](../../04-api-reference/extensions/flow-node.md) |
| `엔진/cvColorVision/` | OpenCV 통합, 낮은 수준의 시각적 처리 | [엔진 컴포넌트 개요](../../04-api-reference/engine-comComponents/README.md) | [cvColorVision](../../04-api-reference/engine-comComponents/cvColorVision.md) |
| `플러그인/` | 런타임 플러그인 및 확장 기능 | [플러그인 개발 개요](../../02-developer-guide/plugin-development/overview.md) | [표준 플러그인 주제](../../04-api-reference/plugins/standard-plugins/pattern.md) |
| `프로젝트/` | 고객 프로젝트, 맞춤형 비즈니스 조립 | [구성요소 상호작용](../../03-architecture/overview/comComponent-interactions.md) | [프로젝트 구조 개요](./README.md) |
| `ColorVisionSetup/` | 설치 프로그램 및 업데이트 프로세스 | [배포 개요](../../02-developer-guide/deployment/overview.md) | [자동 업데이트 시스템](../../02-developer-guide/deployment/auto-update.md) |
| `백엔드/마켓플레이스/` | 플러그인 마켓 백엔드 | [플러그인 마켓 백엔드](../../02-developer-guide/backend/README.md) | [개발 가이드](../../02-developer-guide/README.md) |
| `스크립트/` | 빌드, 패키지, 릴리스 스크립트 | [빌드 및 릴리스 스크립트](../../02-developer-guide/scripts/README.md) | [배포 개요](../../02-developer-guide/deployment/overview.md) |
| `문서/` | 현재 문서 사이트 소스 코드 | [부록 및 자료](../README.md) | 현재 문서 |

## 작업으로 검색

### 장치 서비스를 추가하고 싶습니다

1. 먼저 [디바이스 서비스 개요](../../01-user-guide/devices/overview.md)를 살펴보세요.
2. [엔진 개발 가이드](../../02-developer-guide/engine-development/README.md)를 다시 읽어보세요.
3. 마지막으로 [엔진 구성요소 개요](../../04-api-reference/engine-comComponents/README.md)를 입력하여 특정 모듈 페이지를 찾습니다.

### 플러그인을 개발하고 싶습니다

1. [확장성 개요](../../02-developer-guide/core-concepts/extensibility.md)
2. [플러그인 개발 개요](../../02-developer-guide/plugin-development/overview.md)
3. [플러그인 개발 시작하기](../../02-developer-guide/plugin-development/getting-started.md)

### 템플릿이나 프로세스를 이해하고 싶습니다.

1. [알고리즘 개요](../../04-api-reference/algorithms/README.md)
2. [FlowEngineLib 아키텍처](../../03-architecture/comComponents/engine/flow-engine.md)
3. [템플릿 아키텍처 디자인](../../03-architecture/comComponents/templates/design.md)
4. [템플릿 API 참조](../../04-api-reference/algorithms/templates/api-reference.md)

### UI를 변경하거나 속성을 편집하고 싶습니다.1. [사용자 가이드](../../01-user-guide/README.md)
2. [UI 컴포넌트 개요](../../04-api-reference/ui-comComponents/README.md)
3. [속성 편집기](../../01-user-guide/interface/property-editor.md)

### 빌드, 릴리스, 업데이트를 보고 싶습니다.

1. [배포 개요](../../02-developer-guide/deployment/overview.md)
2. [자동 업데이트 시스템](../../02-developer-guide/deployment/auto-update.md)
3. [빌드 및 릴리스 스크립트](../../02-developer-guide/scripts/README.md)

## 사용 원칙

- 먼저 챕터 홈페이지에서 입장 후 해당 주제 페이지로 이동합니다.
- 역사 초안, 고아 문서 및 오래된 경로 페이지는 더 이상 정문 역할을 하지 않습니다.
- 정확한 해당 주제 페이지를 찾을 수 없는 경우 이전 디렉터리 이름에 계속 의존하는 대신 먼저 개요 페이지로 돌아갑니다.