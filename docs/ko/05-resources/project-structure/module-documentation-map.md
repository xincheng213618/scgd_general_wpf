# 모듈 및 문서 비교표

이 문서는 현재 창고 구조와 여전히 유효한 문서 항목만 유지하며, 이는 "코드의 위치와 먼저 읽을 문서"를 빠르게 찾는 데 사용됩니다.

## 문서 입력을 위한 코드 영역

| 코드 영역 | 관심 장소 | 선호하는 서류 항목 | 보충입구 |
| --- | --- | --- | --- |
| `ColorVision/` | 메인 프로그램 진입, 메인 창, 애플리케이션 시작 | [시작 가이드](../../00-getting-started/README.md) | [메인 창 탐색](../../01-user-guide/interface/main-window.md) |
| `UI/` | WPF UI 프레임워크, 테마, 편집기 | [UI 컴포넌트 개요](../../04-api-reference/ui-components/README.md) | [사용자 가이드](../../01-user-guide/README.md) |
| `UI/ColorVision.SocketProtocol/` | TCP 서비스, JSON/Text 배포, 메시지 기록, 관리창 | [ColorVision.SocketProtocol](../../04-api-reference/ui-components/ColorVision.SocketProtocol.md) | [소켓 통신 모듈 최적화 경로](../../02-developer-guide/performance/socket-protocol-optimization-roadmap.md) |
| `Engine/ColorVision.Engine/Services/` | 장치 서비스, 서비스 조정 | [장치 서비스 개요](../../01-user-guide/devices/overview.md) | [엔진 개발 가이드](../../02-developer-guide/engine-development/README.md) |
| `Engine/ColorVision.Engine/Templates/` | 템플릿 시스템, 매개변수화된 알고리즘, 결과 처리 | [알고리즘 개요](../../04-api-reference/algorithms/README.md) | [현재 알고리즘 템플릿 커버리지](../../04-api-reference/algorithms/current-algorithm-template-coverage.md), [BuzProduct 제품 업무 파라미터 템플릿](../../04-api-reference/algorithms/templates/buz-product-template.md), [Validate 판정 규칙 템플릿](../../04-api-reference/algorithms/templates/validate-rules.md), [Compliance 결과 인수인계](../../04-api-reference/algorithms/templates/compliance-results.md), [DataLoad 데이터 로드 템플릿](../../04-api-reference/algorithms/templates/data-load-template.md), [Matching 템플릿 매칭](../../04-api-reference/algorithms/templates/matching-template.md), [SysDictionary 시스템 사전 템플릿](../../04-api-reference/algorithms/templates/sys-dictionary-template.md), [FocusPoints 포커스 포인트 템플릿](../../04-api-reference/algorithms/templates/focus-points-template.md), [ImageCropping 이미지 크롭 템플릿](../../04-api-reference/algorithms/templates/image-cropping-template.md), [템플릿 메뉴 진입점](../../04-api-reference/algorithms/templates/template-menu-entries.md), [템플릿 아키텍처 디자인](../../03-architecture/components/templates/design.md) |
| `Engine/FlowEngineLib/` | 프로세스 노드, 실행 모델, 시각적 프로세스 | [FlowEngineLib 아키텍처](../../03-architecture/components/engine/flow-engine.md) | [FlowNode 개발](../../04-api-reference/extensions/flow-node.md) |
| `Engine/cvColorVision/` | OpenCV 통합, 낮은 수준의 시각적 처리 | [엔진 컴포넌트 개요](../../04-api-reference/engine-components/README.md) | [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md) |
| `Engine/ColorVision.ShellExtension/` | `.cvraw` / `.cvcie` 파일의 Explorer 썸네일 확장 | [ColorVision.ShellExtension](../../04-api-reference/engine-components/ColorVision.ShellExtension.md) | [엔진 컴포넌트 개요](../../04-api-reference/engine-components/README.md) |
| `Plugins/` | 런타임 플러그인 및 확장 기능 | [기존 플러그인 기능](../../04-api-reference/plugins/README.md) | [현재 플러그인 문서 커버리지](../../04-api-reference/plugins/current-plugin-coverage.md), [플러그인 기능 및 인수인계 매트릭스](../../04-api-reference/plugins/plugin-capability-matrix.md), [플러그인 개발 개요](../../02-developer-guide/plugin-development/overview.md) |
| `Projects/` | 고객 프로젝트, 업무 맞춤 구현, 연동 데모 | [프로젝트 설명](../../00-projects/README.md) | [프로젝트 패키지 개요](../../04-api-reference/projects/README.md), [현재 프로젝트 문서 커버리지](../../04-api-reference/projects/current-project-coverage.md), [프로젝트 기능 및 인수인계 매트릭스](../../04-api-reference/projects/project-capability-matrix.md), [프로젝트 실행 및 인수인계 플레이북](../../04-api-reference/projects/project-package-playbook.md) |
| `ColorVisionSetup/` | 설치 프로그램 및 업데이트 프로세스 | [배포 개요](../../02-developer-guide/deployment/overview.md) | [자동 업데이트 시스템](../../02-developer-guide/deployment/auto-update.md) |
| `Backend/marketplace/` | 플러그인 마켓 백엔드 | [플러그인 마켓 백엔드](../../02-developer-guide/backend/README.md) | [개발 가이드](../../02-developer-guide/README.md) |
| `Scripts/` | 빌드, 패키지, 릴리스 스크립트 | [빌드 및 릴리스 스크립트](../../02-developer-guide/scripts/README.md) | [배포 개요](../../02-developer-guide/deployment/overview.md) |
| `Test/` | xUnit, native helper, 백엔드, 스크립트 검증 | [테스트 및 검증 인수인계](../../02-developer-guide/testing.md) | [개발 가이드](../../02-developer-guide/README.md) |
| `docs/` | 현재 문서 사이트 소스 코드 | [부록 및 자료](../README.md) | 현재 문서 |

## 작업으로 검색

### 장치 서비스를 추가하고 싶습니다

1. 먼저 [디바이스 서비스 개요](../../01-user-guide/devices/overview.md)를 살펴보세요.
2. [엔진 개발 가이드](../../02-developer-guide/engine-development/README.md)를 다시 읽어보세요.
3. 마지막으로 [엔진 구성요소 개요](../../04-api-reference/engine-components/README.md)를 입력하여 특정 모듈 페이지를 찾습니다.

### 플러그인을 개발하고 싶습니다

1. [확장성 개요](../../02-developer-guide/core-concepts/extensibility.md)
2. [플러그인 개발 개요](../../02-developer-guide/plugin-development/overview.md)
3. [플러그인 개발 시작하기](../../02-developer-guide/plugin-development/getting-started.md)

### 고객 프로젝트를 유지보수하고 싶습니다

1. [프로젝트 설명](../../00-projects/README.md)
2. [프로젝트 패키지 개요](../../04-api-reference/projects/README.md)
3. [현재 프로젝트 문서 커버리지](../../04-api-reference/projects/current-project-coverage.md)
4. [프로젝트 기능 및 인수인계 매트릭스](../../04-api-reference/projects/project-capability-matrix.md)
5. [프로젝트 실행 및 인수인계 플레이북](../../04-api-reference/projects/project-package-playbook.md)

### 템플릿이나 프로세스를 이해하고 싶습니다.

1. [알고리즘 개요](../../04-api-reference/algorithms/README.md)
2. [현재 알고리즘 템플릿 커버리지](../../04-api-reference/algorithms/current-algorithm-template-coverage.md)
3. [FlowEngineLib 아키텍처](../../03-architecture/components/engine/flow-engine.md)
4. [템플릿 아키텍처 디자인](../../03-architecture/components/templates/design.md)
5. [템플릿 API 참조](../../04-api-reference/algorithms/templates/api-reference.md)
6. 구체적인 인수인계 문서는 [FindLightArea 발광 영역 템플릿](../../04-api-reference/algorithms/templates/find-light-area.md), [JND 템플릿](../../04-api-reference/algorithms/templates/jnd-template.md), [LED 검출 템플릿](../../04-api-reference/algorithms/templates/led-detection.md), [BuzProduct 제품 업무 파라미터 템플릿](../../04-api-reference/algorithms/templates/buz-product-template.md), [Validate 판정 규칙 템플릿](../../04-api-reference/algorithms/templates/validate-rules.md), [Compliance 결과 인수인계](../../04-api-reference/algorithms/templates/compliance-results.md), [DataLoad 데이터 로드 템플릿](../../04-api-reference/algorithms/templates/data-load-template.md), [Matching 템플릿 매칭](../../04-api-reference/algorithms/templates/matching-template.md), [SysDictionary 시스템 사전 템플릿](../../04-api-reference/algorithms/templates/sys-dictionary-template.md), [FocusPoints 포커스 포인트 템플릿](../../04-api-reference/algorithms/templates/focus-points-template.md), [ImageCropping 이미지 크롭 템플릿](../../04-api-reference/algorithms/templates/image-cropping-template.md), [템플릿 메뉴 진입점](../../04-api-reference/algorithms/templates/template-menu-entries.md)

### UI를 변경하거나 속성을 편집하고 싶습니다.1. [사용자 가이드](../../01-user-guide/README.md)
2. [UI 컴포넌트 개요](../../04-api-reference/ui-components/README.md)
3. [속성 편집기](../../01-user-guide/interface/property-editor.md)

### 빌드, 릴리스, 업데이트를 보고 싶습니다.

1. [배포 개요](../../02-developer-guide/deployment/overview.md)
2. [자동 업데이트 시스템](../../02-developer-guide/deployment/auto-update.md)
3. [빌드 및 릴리스 스크립트](../../02-developer-guide/scripts/README.md)

### 테스트 및 승인 명령을 선택하고 싶습니다

1. [테스트 및 검증 인수인계](../../02-developer-guide/testing.md)
2. 변경 모듈에 따라 `Test/ColorVision.UI.Tests/`, `Test/opencv_helper_test/`, 백엔드 테스트, 스크립트 테스트 또는 `npm run docs:build`를 선택합니다

## 사용 원칙

- 먼저 챕터 홈페이지에서 입장 후 해당 주제 페이지로 이동합니다.
- 역사 초안, 고아 문서 및 오래된 경로 페이지는 더 이상 정문 역할을 하지 않습니다.
- 정확한 해당 주제 페이지를 찾을 수 없는 경우 이전 디렉터리 이름에 계속 의존하는 대신 먼저 개요 페이지로 돌아갑니다.
