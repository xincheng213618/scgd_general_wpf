# 건축 디자인

이 장에서는 현재 시스템 설계의 주요 읽기 경로만 유지합니다. 과거 제안, 분할 초안 및 일회성 토론 문서는 디렉토리에 남아 있지만 더 이상 기본 항목이 아닙니다.

## 기본 읽기 경로

1. [시스템 아키텍처 개요](./overview/system-overview.md)
2. [아키텍처 런타임](./overview/runtime.md)
3. [컴포넌트 상호작용](./overview/comComponent-interactions.md)
4. [FlowEngineLib 아키텍처](./comComponents/engine/flow-engine.md)
5. [템플릿 아키텍처 디자인](./comComponents/templates/design.md)
6. [보안 개요](./security/overview.md)
7. [RBAC 모델](./security/rbac.md)

## 디렉토리 설명

- `개요/`는 시작, 런타임, 구성 요소 관계와 같은 시스템 수준 관점에 중점을 둡니다.
- `comments/engine/`은 프로세스 엔진과 실행 모델에 중점을 둡니다.
- `Components/templates/`는 템플릿 시스템의 설계 및 현재 상황 분석에 중점을 둡니다.
- `security/`는 권한 모델과 보안 경계에 중점을 둡니다.

## 읽는 방법을 제안합니다

- 처음 시스템을 접하실 때에는 "시스템 개요 → 런타임 → 컴포넌트 인터랙션"의 순서로 읽어주세요.
- 프로세스나 템플릿을 수정해야 하는 경우 `comments/` 아래에 항목 페이지를 입력하세요.
- 인터페이스 및 타입에 대한 자세한 내용이 필요한 경우 [API 참조](../04-api-reference/README.md)를 참조하세요.

## 추가 자료

- [템플릿 모듈 분석](./comComponents/templates/analytic.md): 템플릿 디자인의 주요 내용을 이해한 후 다시 돌아와서 디렉토리 진화, 등록 경계 및 현재 상태 제약 조건을 살펴보는 것이 적합합니다.

## 과거 데이터 설명

- 이 디렉토리에서 'ColorVision.Engine-Refactoring-'으로 시작하는 문서는 과거 설계 데이터에 속하며 아이디어 추적에 사용되며 더 이상 현재 기본 구성표로 간주되지 않습니다.
- 과거 솔루션이 현재 코드 구현과 충돌하는 경우 코드 및 현재 모듈 문서가 우선합니다.