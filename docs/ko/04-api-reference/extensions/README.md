# 확장점 개요

이 장에서는 코드와 직접적으로 일치할 수 있는 확장 지점에 대한 주제만 유지하고 더 이상 "모든 확장 메커니즘을 한눈에 볼 수 있는" 기존의 일반 목록을 유지하지 않습니다.

## 현재 보장 범위

현재 이 브랜치에는 한 가지 유형의 안정적인 주제만 포함되어 있습니다.

- [FlowEngineLib 노드 확장](./flow-node.md)

이는 현재 `extensions/`가 완전한 확장 백과사전이 아니라 매우 좁은 "조직화된 확장 지점 항목"임을 의미합니다.

## 먼저 경계를 구분해보자

- 플러그인 검색, 로딩 및 배포는 여기에 속하지 않습니다. [플러그인 및 상태 페이지](../plugins/README.md) 및 [플러그인 개발 개요](../../02-developer-guide/plugin-development/overview.md)를 확인해야 합니다.
- 알고리즘 템플릿과 프로세스 템플릿은 여기에 속하지 않습니다. [알고리즘 및 템플릿 개요](../algorithms/README.md)로 이동해야 합니다.
- 런타임 모듈 간의 종속성은 여기에서 확장되지 않으며 [아키텍처 디자인](../../03-architecture/README.md)으로 반환되어야 합니다.

## 이 장을 사용하는 방법

1. 먼저 "흐름 노드" 또는 "플러그인/템플릿/서비스"를 확장할지 확인합니다.
2. Flow 노드인 경우 [FlowEngineLib 노드 확장자](./flow-node.md)를 입력합니다.
3. 런타임 실행 체인에 관한 문제가 더 많다면 [FlowEngineLib 아키텍처](../../03-architecture/comComponents/engine/flow-engine.md)와 함께 읽어보세요.

## 여기에는 왜 한 페이지만 있나요?

- 현재 웨어하우스에는 실제로 문서별로 안정적인 주제로 묶인 확장점이 많지 않습니다.
- 완료된 것처럼 보이지만 실제로는 빨리 만료되는 확장 디렉터리를 계속 유지하는 것보다 코드로 직접 확인할 수 있는 항목만 유지하는 것이 좋습니다.

## 계속 읽기

- [API 참조 개요](../README.md)
- [플러그인 및 상태 페이지](../plugins/README.md)
- [FlowEngineLib 아키텍처](../../03-architecture/comComponents/engine/flow-engine.md)