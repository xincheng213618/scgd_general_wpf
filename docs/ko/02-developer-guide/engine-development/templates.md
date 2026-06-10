# Engine 템플릿 시스템 개발 인수인계

이 문서는 `Engine/ColorVision.Engine/Templates/`의 현재 템플릿 모델을 설명합니다. 템플릿은 파라미터, 편집, 저장, 가져오기/내보내기, 알고리즘 명령 파라미터를 담당합니다. 고객 판정과 보고서 형식은 프로젝트 패키지 쪽에 둡니다.

## 런타임 체인

| 단계 | 핵심 객체 | 설명 |
| --- | --- | --- |
| 등록 | `ITemplate`, `TemplateControl.AddITemplateInstance` | 템플릿 인스턴스를 전역 템플릿 표에 등록 |
| 발견 | `IITemplateLoad`, `TemplateControl` | 시작 시 로드 가능한 템플릿 스캔 |
| 파라미터 목록 | `TemplateModel<T>`, `TemplateParams` | UI 콤보박스와 편집 창이 참조하는 목록 |
| MySQL 템플릿 | `ITemplate<T>`, `ParamModBase` | `TemplateDicId`로 `ModMasterModel` / `ModDetailModel` 조회 |
| JSON 템플릿 | `ITemplateJson<T>`, `TemplateJsonParam` | 복잡한 알고리즘 파라미터를 JSON으로 저장 |
| Flow 연동 | `Templates/Flow/`, `NodeConfigurator` | 노드 설정 패널에서 템플릿 선택 |

## 모델 선택

| 상황 | 권장 모델 |
| --- | --- |
| 안정적인 사전 기반 파라미터 | `ITemplate<T>` + `ParamModBase` |
| 복잡하고 버전 변경이 잦은 파라미터 | `ITemplateJson<T>` + `TemplateJsonParam` |
| 장치 실행 파라미터 | 장치 폴더의 `Templates/` |
| Flow 템플릿 | `Templates/Flow/TemplateFlow` |
| 고객 출력 형식 | 프로젝트 `Process` / exporter |

## 추가 절차

1. 파라미터가 공통 알고리즘, 장치, Flow, 고객 프로젝트 중 어디에 속하는지 정합니다.
2. MySQL 템플릿은 `ParamModBase`, JSON 템플릿은 `TemplateJsonParam`을 상속합니다.
3. `Template*`를 만들고 `ITemplate<T>` 또는 `ITemplateJson<T>`를 상속합니다.
4. 자동 로드가 필요하면 `IITemplateLoad`를 구현합니다.
5. 정적 `Params`를 만들고 `TemplateParams`에 할당합니다.
6. DB 복원이 필요하면 `GetMysqlCommand()`와 `TemplateDicId`를 확인합니다.
7. Flow 또는 `Algorithm*`이 참조하는 템플릿 ID/이름을 정확히 전달합니다.

## 자주 나는 문제

| 현상 | 먼저 확인 |
| --- | --- |
| 콤보박스에 없음 | `IITemplateLoad`, `TemplateParams`, `TemplateControl` |
| 재시작 후 사라짐 | `GetMysqlCommand()`, `TemplateDicId`, `SaveIndex`, MySQL 연결 |
| Flow가 이전 값을 사용 | 노드 저장 필드, 템플릿 ID, 템플릿 이름 |
| 알고리즘에 파라미터가 없음 | `TemplateParam` / `POITemplateParam` 기록 |

## 인수인계 검증

- 생성, 복사, 이름 변경, 가져오기, 내보내기, 삭제가 동작합니다.
- 재시작 후 저장값이 유지됩니다.
- 새 템플릿으로 최소 Flow를 실행합니다.
- 히스토리 결과, overlay, 테이블, 프로젝트 출력이 새 결과를 읽습니다.
- 기존 템플릿도 계속 실행됩니다.

## 관련 문서

- [Engine 템플릿 및 Flow 체인](../../04-api-reference/engine-components/template-flow-chain.md)
- [Engine 결과 표시 및 프로젝트 인수인계](../../04-api-reference/engine-components/result-handoff-chain.md)
- [FlowEngineLib](../../04-api-reference/engine-components/FlowEngineLib.md)
- [테스트 및 검증 인수인계](../testing.md)
