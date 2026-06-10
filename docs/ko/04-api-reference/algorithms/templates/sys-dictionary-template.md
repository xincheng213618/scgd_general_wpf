# SysDictionary 시스템 사전 템플릿

이 문서는 `Engine/ColorVision.Engine/Templates/SysDictionary/` 의 역할을 설명합니다. 이 모듈은 알고리즘 템플릿 기본 사전을 관리하며, 핵심 데이터는 `t_scgd_sys_dictionary_mod_master` 와 `t_scgd_sys_dictionary_mod_item` 에 저장됩니다. 현재 `TemplateModParam` 은 `mod_type = 7` 만 로드합니다.

## 적용 범위

| 항목 | 현재 구현 |
| --- | --- |
| 템플릿 클래스 | `TemplateModParam : ITemplate<DicModParam>` |
| 파라미터 클래스 | `DicModParam : ParamModBase` |
| 편집 UI | `EditDictionaryMode.xaml(.cs)` |
| 마스터 생성 창 | `CreateDicTemplate.xaml(.cs)` |
| 상세 생성 창 | `CreateDicModeDetail.xaml(.cs)` |
| 메뉴 진입점 | `MenuDefalutDicAlg` |
| 마스터 테이블 | `t_scgd_sys_dictionary_mod_master` |
| 상세 테이블 | `t_scgd_sys_dictionary_mod_item` |
| 현재 범위 | `tenant_id = 0`, `mod_type = 7` |

## 데이터 모델

`SysDictionaryModModel` 은 사전 마스터를 저장합니다. 주요 컬럼은 `code`, `name`, `pid`, `p_type`, `mod_type`, `cfg_json`, `version`, `is_enable`, `is_delete`, `tenant_id` 입니다.

`SysDictionaryModDetaiModel` 은 사전 항목을 저장합니다. 주요 컬럼은 `pid`, `address_code`, `symbol`, `name`, `default_val`, `val_type`, `is_enable`, `is_delete` 입니다. `val_type` 은 `Integer`, `Float`, `Bool`, `String`, `Enum` 중 하나입니다.

## 수명 주기

1. `MenuDefalutDicAlg` 가 `TemplateEditorWindow(new TemplateModParam())` 를 엽니다.
2. `TemplateModParam.Load()` 가 `tenant_id = 0`, `mod_type = 7` 마스터를 읽습니다.
3. 상세는 `SysDictionaryModDetailDao.GetAllByPid(model.Id)` 로 읽습니다.
4. `EditDictionaryMode` 에서 기본값과 활성 상태를 편집합니다.
5. `CreateDicTemplate` 은 `ModType = 7` 마스터를 생성합니다.
6. `CreateDicModeDetail` 은 `ValueType = String`, `IsEnable = true` 상세를 생성합니다.
7. `Save()` 는 상세 행을 저장합니다.

현재 `Save()` 는 상세만 저장하고 마스터 필드는 저장하지 않습니다. 삭제 경로는 `SysResourceModel` 을 호출하므로, 예상한 사전 테이블에서 삭제되지 않으면 DAO 와 테이블을 먼저 확인하세요.

## 관계

| 모듈 | 관계 |
| --- | --- |
| 강타입 템플릿 | `TemplateDicId` 로 기본 사전 항목을 읽습니다. |
| JSON 템플릿 | 많은 JSON 템플릿 마스터도 `mod_type = 7` 이며 내용은 `cfg_json` 에 있습니다. |
| Flow 템플릿 | Flow 는 사전 항목을 읽어 노드/템플릿 파라미터를 구성합니다. |
| Validate | Validate 는 `mod_type = 110/111/120` 을 사용하므로 여기와 혼동하지 않습니다. |

## 문제 해결

| 현상 | 먼저 확인할 것 |
| --- | --- |
| 템플릿 필드가 없음 | `TemplateDicId` 와 사전 마스터 존재 여부. |
| 새 필드가 적용되지 않음 | 상세 `pid`, `symbol`, `address_code`, `is_enable`. |
| 기본값이 적용되지 않음 | `default_val` 과 `val_type` 일치 여부. |
| 메뉴 진입점 없음 | `MenuDefalutDicAlg` 스캔과 권한. |
| 삭제 후에도 보임 | 삭제 대상 테이블, 캐시, 메뉴/템플릿 새로고침. |

## 더 읽기

- [템플릿 관리](./template-management.md)
- [Templates API 참조](./api-reference.md)
- [Validate 판정 규칙 템플릿](./validate-rules.md)
- [Engine 템플릿 및 Flow 체인](../../engine-components/template-flow-chain.md)
- [현재 알고리즘 템플릿 커버리지](../current-algorithm-template-coverage.md)
