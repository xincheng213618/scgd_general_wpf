# Validate 판정 규칙 템플릿

이 문서는 `Engine/ColorVision.Engine/Templates/Validate/` 의 두 계층 규칙 체계를 설명합니다. Validate 는 단일 템플릿이 아니라 기본 합규 사전 계층과 실제 판정 템플릿 계층으로 구성됩니다.

## 적용 범위

| 항목 | 현재 구현 |
| --- | --- |
| 기본 사전 템플릿 | `TemplateDicComply : ITemplate<DicComplyParam>` |
| 실제 판정 템플릿 | `TemplateComplyParam : ITemplate<ValidateParam>` |
| 사전 편집 UI | `DicEditComply.xaml(.cs)` |
| 규칙 편집 UI | `ValidateControl.xaml(.cs)` |
| 메뉴 진입점 | `ExportComply.cs`, `ExportDicComply.cs` |
| 규칙 마스터 테이블 | `t_scgd_rule_validate_template_master` |
| 규칙 상세 테이블 | `t_scgd_rule_validate_template_detail` |
| 런타임 캐시 | `TemplateComplyParam.CIEParams`, `TemplateComplyParam.JNDParams` |

## 두 계층 모델

`TemplateDicComply` 는 `SysDictionaryModMasterDao` 와 `SysDictionaryModItemValidateDao` 에서 기본 사전과 기본 규칙 항목을 읽습니다.

| 사전 `mod_type` | 현재 용도 |
| --- | --- |
| `110` | 포인트 CIE/합규 판정 메뉴. |
| `111` | 포인트 목록 합규 판정 메뉴. |
| `120` | JND 합규 판정 메뉴. |

`TemplateComplyParam(code, type)` 은 사전 `Code` 로 실제 판정 템플릿을 로드합니다. `t_scgd_rule_validate_template_master` 를 읽고 이어서 `t_scgd_rule_validate_template_detail` 을 읽습니다.

| 테이블 | 주요 필드 | 목적 |
| --- | --- | --- |
| `t_scgd_rule_validate_template_master` | `dic_pid`, `code`, `name`, `is_enable`, `is_delete`, `tenant_id` | 특정 사전 코드 아래의 판정 템플릿. |
| `t_scgd_rule_validate_template_detail` | `dic_pid`, `pid`, `code`, `val_max`, `val_min`, `val_equal`, `val_radix`, `val_type` | 개별 판정 항목과 임계값. |

## 동적 메뉴

| 원본 | 메뉴 동작 |
| --- | --- |
| `mod_type = 110` | `TemplateComplyParam(item.Code)` 를 열어 포인트 규칙 진입점으로 사용합니다. |
| `mod_type = 111` | `TemplateComplyParam(item.Code)` 를 열어 포인트 목록 규칙 진입점으로 사용합니다. |
| `mod_type = 120` | `TemplateComplyParam(item.Code, 1)` 를 열어 JND 규칙 진입점으로 사용합니다. |
| `ExportDicComply` | `TemplateDicComply` 를 열어 기본 합규 사전을 관리합니다. |

## 생성과 저장

`TemplateDicComply.Create(templateCode, templateName)` 는 `SysDictionaryModModel` 을 생성합니다. 현재 기본 `ModType` 은 `111` 입니다.

`TemplateComplyParam.Create(templateName)` 는 마스터 행을 만들고, `Code` 로 사전을 찾고, 활성화된 사전 검증 항목을 복사하여 `ValMax`, `ValMin`, `ValEqual`, `ValRadix`, `ValType` 을 상세 행에 저장합니다.

`TemplateComplyParam.Save()` 는 실제 판정 템플릿 이름과 상세 규칙을 저장합니다. `TemplateDicComply.Save()` 는 기본 사전과 기본 규칙 상세를 저장합니다.

## 런타임 캐시

| 캐시 | 설명 |
| --- | --- |
| `CIEParams` | CIE/일반 합규 판정 템플릿 컬렉션입니다. BuzProduct 가 이 목록을 읽습니다. |
| `JNDParams` | JND 판정 템플릿 컬렉션입니다. |

현재 생성자는 `type == 1` 일 때 `JNDParams` 에 추가하고, 이후 `CIEParams` 에도 추가합니다. 인수인계 시 JND 템플릿이 `JNDParams` 에만 있다고 가정하지 마세요.

## 가져오기 제한

`TemplateComplyParam.Import()` 는 현재 가져오기를 지원하지 않습니다. 현장 이전 시 사전 테이블과 `t_scgd_rule_validate_template_*` 데이터를 함께 이전하거나 별도 가져오기 흐름을 추가해야 합니다.

## 다른 모듈과의 관계

| 모듈 | 의존 방식 |
| --- | --- |
| [BuzProduct 제품 업무 파라미터 템플릿](./buz-product-template.md) | 상세 `val_rule_temp_id` 가 Validate 템플릿을 참조합니다. |
| [Compliance 결과 인수인계](./compliance-results.md) | `ValidateResult` 를 읽고 `ValidateRuleResultType.M` 으로 판정합니다. |
| [JND 템플릿](./jnd-template.md) | JND 규칙은 `mod_type = 120` 에서 옵니다. |
| 프로젝트 패키지 | Validate/Compliance 데이터를 보고서와 OK/NG 에 사용할 수 있습니다. |

## 문제 해결

| 현상 | 먼저 확인할 것 |
| --- | --- |
| 메뉴 진입점이 없음 | `SysDictionaryModMaster` 의 `mod_type` 과 `is_delete = false` 를 확인합니다. |
| 새 템플릿에 상세가 없음 | 사전 아래 활성화된 검증 항목이 있는지 확인합니다. |
| BuzProduct 에 규칙 후보가 없음 | `TemplateComplyParam.CIEParams` 가 해당 `Code` 를 로드했는지 확인합니다. |
| JND 규칙이 CIE 목록에 표시됨 | 현재 생성자 동작입니다. |
| 가져오기가 불가 | 실제 판정 템플릿은 현재 가져오기를 지원하지 않습니다. |

## 인수인계 체크리스트

- 기본 사전 계층과 실제 판정 템플릿 계층을 분리해서 설명합니다.
- 판정 필드를 추가할 때 사전, 상세, 검수 샘플, 결과 설명을 함께 갱신합니다.
- 임계값 의미를 바꾸면 서비스가 쓰는 `ValidateResult` 도 검증합니다.
- 이전 시 `SysDictionaryMod*` 와 `t_scgd_rule_validate_template_*` 를 함께 이전합니다.
- 메뉴를 바꾸면 `mod_type = 110/111/120` 세 경로를 확인합니다.

## 더 읽기

- [BuzProduct 제품 업무 파라미터 템플릿](./buz-product-template.md)
- [Compliance 결과 인수인계](./compliance-results.md)
- [템플릿 관리](./template-management.md)
- [Engine 결과 표시와 프로젝트 인수인계 체인](../../engine-components/result-handoff-chain.md)
- [현재 알고리즘 템플릿 커버리지](../current-algorithm-template-coverage.md)
