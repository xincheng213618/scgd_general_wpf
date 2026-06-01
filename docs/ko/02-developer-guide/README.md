#개발 가이드

이 장에서는 보조 개발, 확장 지점 및 전달 프로세스에 중점을 둡니다. 클래스 라이브러리 세부 정보 및 모듈 디자인에 대해서는 각각 API 참조 및 아키텍처 디자인으로 이동하세요.

## 여기에서 일반적인 시나리오

### 확장 메커니즘 이해

- [확장성 개요](./core-concepts/extensibility.md)

### 엔진이나 템플릿 관련 기능 수정

- [엔진 개발 가이드](./engine-development/README.md)
- [건축 설계](../03-architecture/README.md)
- [엔진 컴포넌트 API](../04-api-reference/engine-comComponents/README.md)

### 플러그인 개발

- [플러그인 개발 개요](./plugin-development/README.md)
- [플러그인 개발 시작하기](./plugin-development/getting-started.md)
- [플러그인 수명주기](./plugin-development/lifecycle.md)

### 빌드, 배포 및 업데이트

- [배포 개요](./deployment/overview.md)
- [자동 업데이트 시스템](./deployment/auto-update.md)
- [스크립트 빌드 및 릴리스](./scripts/README.md)

### 백엔드 및 보조 시스템

- [플러그인 마켓 백엔드](./backend/README.md)
- [성능 최적화 개요](./performance/overview.md)
- [소켓 통신 모듈 최적화 경로](./performance/socket-protocol-optimization-roadmap.md)

## 권장 읽기 경로

1. 먼저 [Architecture Design](../03-architecture/README.md)을 살펴보고 모듈 경계를 확인하세요.
2. [확장성 개요](./core-concepts/extensibility.md)를 다시 살펴보고 확장 지점과 플러그인 항목을 확인하세요.
3. 대상 주제(플러그인, 엔진, 배포 또는 백엔드)를 입력합니다.
4. 클래스 및 인터페이스 세부 사항이 필요한 경우 [API 참조](../04-api-reference/README.md)로 이동합니다.

## 장 경계

- 본 장에서는 API 매뉴얼을 대체하기보다는 "코드 입력 방법" 경로를 제공하는 것을 우선으로 합니다.
- 엔진 하위 디렉터리의 일부 하위 항목은 아직 통합 중이므로 기본 항목이 개요 페이지로 변경되어 관리되지 않은 작은 페이지가 기본 탐색에 배치되지 않습니다.
- AI/에이전트 관련 실험 자료는 하위 디렉터리에 남아 있지만 더 이상 기본 읽기 경로가 아닙니다.

## 보충입구

- [프로젝트 구조 개요](../05-resources/project-structure/README.md)
- [온라인 창고](https://github.com/xincheng213618/scgd_general_wpf)
- [이슈 추적](https://github.com/xincheng213618/scgd_general_wpf/issues)