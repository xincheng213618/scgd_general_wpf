# 프로젝트 구조 개요

이 문서는 현재 창고의 기본 디렉토리의 업무 분업을 신속하게 설명하고 각 디렉토리에 가장 적합한 문서 항목을 제공하는 데 사용됩니다.

## 홈 디렉토리 파티션

| 목차 | 기능 | 먼저 읽어볼 것을 권장합니다 |
| --- | --- | --- |
| `컬러비전/` | 기본 WPF 애플리케이션 입구 및 기본 창 | [시작 가이드](../../00-getting-started/README.md) / [메인 창 탐색](../../01-user-guide/interface/main-window.md) |
| `UI/` | UI 프레임워크, 테마, 속성 편집기, 이미지 편집기 | [UI 컴포넌트 개요](../../04-api-reference/ui-comComponents/README.md) |
| `UI/ColorVision.SocketProtocol/` | 로컬 TCP 서비스, 메시지 기록 및 관리 창 | [ColorVision.SocketProtocol](../../04-api-reference/ui-comComponents/ColorVision.SocketProtocol.md) / [소켓 통신 최적화 경로](../../02-developer-guide/performance/socket-protocol-optimization-roadmap.md) |
| `엔진/` | 핵심 엔진, 디바이스 서비스, 템플릿 시스템, 프로세스 실행 | [엔진 개발 가이드](../../02-developer-guide/engine-development/README.md) / [엔진 컴포넌트 개요](../../04-api-reference/engine-comComponents/README.md) |
| `플러그인/` | 런타임 플러그인 및 확장 기능 | [플러그인 개발 개요](../../02-developer-guide/plugin-development/overview.md) |
| `프로젝트/` | 고객 프로젝트 패키지 및 비즈니스 맞춤형 구현 | [구성요소 상호작용](../../03-architecture/overview/comComponent-interactions.md) |
| `백엔드/마켓플레이스/` | 플러그인 마켓 백엔드 서비스 | [플러그인 마켓 백엔드](../../02-developer-guide/backend/README.md) |
| `스크립트/` | 빌드, 패키지, 릴리스 스크립트 | [빌드 및 릴리스 스크립트](../../02-developer-guide/scripts/README.md) |
| `ColorVisionSetup/` | 설치 프로그램 및 업데이트 프로그램 | [자동 업데이트 시스템](../../02-developer-guide/deployment/auto-update.md) |
| `테스트/` | 테스트 프로젝트 | [개발자 가이드](../../02-developer-guide/README.md) |
| `문서/` | VitePress 문서 소스 코드 | 현재 문서 / [모듈 및 문서 비교표](./module-documentation-map.md) |

## 역할별 읽기

### 신규 사용자 또는 구현 동급생

1. [시작하기](../../00-getting-started/README.md)
2. [사용자 가이드](../../01-user-guide/README.md)
3. [FAQ](../../01-user-guide/troubleshooting/common-issues.md)

### 엔진 또는 알고리즘 개발

1. [건축설계](../../03-architecture/README.md)
2. [엔진 개발 가이드](../../02-developer-guide/engine-development/README.md)
3. [알고리즘 개요](../../04-api-reference/algorithms/README.md)

### 플러그인 개발

1. [확장성 개요](../../02-developer-guide/core-concepts/extensibility.md)
2. [플러그인 개발 개요](../../02-developer-guide/plugin-development/overview.md)
3. [표준 플러그인 주제](../../04-api-reference/plugins/standard-plugins/pattern.md)

### 문서 유지관리

1. [부록 및 자료](../README.md)
2. [모듈 및 문서 비교표](./module-documentation-map.md)

## 설명

- 여기에 제공되는 내용은 "시작 위치" 항목이며 자세한 API 또는 주제 페이지를 대체하지 않습니다.
- 디렉토리에 독립된 문서가 부족한 경우 새로운 느슨한 페이지 색인을 계속 펼치는 대신 인접한 장의 개요 페이지에서 입력하는 것이 우선입니다.