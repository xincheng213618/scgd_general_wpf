# BuzProduct 제품 업무 파라미터 템플릿

이 문서는 `Engine/ColorVision.Engine/Templates/BuzProduct/` 의 업무 경계를 설명합니다. `BuzProduct` 는 독립 알고리즘 실행 진입점이 아니라 제품 설정, POI 참조, Validate 판정 템플릿을 묶는 제품/업무 템플릿입니다.

## 적용 범위

| 항목 | 현재 구현 |
| --- | --- |
| 템플릿 코드 | `BuzProduc`, 소스의 현재 철자를 그대로 사용 |
| 템플릿 클래스 | `TemplateBuzProduc : ITemplateBuzProduc<TemplateBuzProductParam>, IITemplateLoad` |
| 파라미터 클래스 | `TemplateBuzProductParam` |
| 편집 UI | `EditTemplateBuzProduct.xaml(.cs)` |
| MySQL 복구 진입점 | `MysqlBuzProduct` |
| 마스터 테이블 | `t_scgd_buz_product_master` |
| 상세 테이블 | `t_scgd_buz_product_detail` |
| 주요 의존성 | `TemplateComplyParam.CIEParams`, POI 템플릿, Validate 규칙 템플릿 |

## 소스 진입점

| 파일 | 인수인계 용도 |
| --- | --- |
| `TemplateBuzProduc.cs` | 제목, 코드, 편집 컨트롤, MySQL 복구 명령을 등록합니다. |
| `ITemplateBuzProduc.cs` | 로드, 저장, 생성, 복사, 가져오기, 내보내기, 삭제를 구현합니다. |
| `TemplateBuzProductParam.cs` | 마스터 모델, 상세 목록, 상세 추가 명령을 편집기에 제공합니다. |
| `BuzProductMasterDao.cs` | `t_scgd_buz_product_master` 의 SqlSugar 모델과 DAO입니다. |
| `BuzProductDetailDao.cs` | `t_scgd_buz_product_detail` 의 SqlSugar 모델과 DAO입니다. |
| `EditTemplateBuzProduct.xaml(.cs)` | 제품 업무 항목을 편집하고 `CIEParams` 에서 Validate 후보를 로드합니다. |
| `MysqlBuzProduct.cs` | 마스터/상세 테이블 구조를 복구합니다. |

## 데이터 테이블

`t_scgd_buz_product_master` 는 제품 또는 업무 템플릿 마스터를 저장합니다. 주요 컬럼은 `code`, `name`, `buz_type`, `cfg_json`, `img_file`, `is_enable`, `is_delete`, `tenant_id`, `remark` 입니다.

`t_scgd_buz_product_detail` 은 마스터 아래의 검사 항목 또는 업무 포인트 설정을 저장합니다. 주요 컬럼은 `code`, `name`, `pid`, `poi_id`, `order_index`, `cfg_json`, `val_rule_temp_id` 입니다.

`val_rule_temp_id` 는 Validate 규칙 템플릿을 가리키는 핵심 필드입니다. 이 값이 바뀌면 해당 제품 항목의 Compliance 또는 프로젝트 OK/NG 가 바뀔 수 있습니다.

## 수명 주기

1. 템플릿 시스템이 `TemplateBuzProduc` 를 발견하고 `Load()` 를 호출합니다.
2. `Load()` 는 `is_delete = 0` 인 마스터 행을 읽습니다.
3. 각 마스터는 `BuzProductDetailDao.GetAllByPid(...)` 로 상세를 읽습니다.
4. 편집기는 `TemplateBuzProductParam.BuzProductDetailModels` 에 바인딩됩니다.
5. `Save()` 는 마스터 이름과 상세 행을 저장합니다.
6. `Create()` 는 새 마스터를 만들고 가져오기/복사 원본의 상세를 새 ID 로 저장합니다.
7. `Delete()` 는 마스터와 연결된 상세를 삭제합니다.

## Validate 와의 관계

`EditTemplateBuzProduct` 는 판정 규칙 드롭다운을 다음 목록에서 초기화합니다.

```csharp
TemplateComplyParam.CIEParams.SelectMany(kvp => kvp.Value).ToList()
```

| BuzProduct 상세 | Validate 규칙 | 결과 영향 |
| --- | --- | --- |
| `poi_id` | 검사 포인트 또는 영역을 지정합니다. | 결과가 어떤 업무 포인트에 속하는지 결정합니다. |
| `val_rule_temp_id` | 사용할 규칙 템플릿을 지정합니다. | Compliance 또는 프로젝트 OK/NG 에 영향을 줍니다. |
| `cfg_json` | 상세 항목의 추가 설정을 저장합니다. | 프로젝트 패키지에서 다시 해석될 수 있습니다. |

## 가져오기와 내보내기

단일 템플릿은 `.cfg`, 여러 템플릿은 `.zip` 으로 내보냅니다. 가져오기/복사 시 데이터베이스 ID 는 다시 생성됩니다. 다른 장비로 옮길 때는 POI 템플릿, Validate 사전, Validate 규칙 템플릿도 함께 확인해야 합니다.

## 문제 해결

| 현상 | 먼저 확인할 것 |
| --- | --- |
| 템플릿을 찾을 수 없음 | 수정된 철자 `BuzProduct` 가 아니라 `BuzProduc` 로 검색합니다. |
| 규칙 드롭다운이 비어 있음 | `TemplateComplyParam.CIEParams` 가 로드되었는지 확인합니다. |
| 저장 후 판정이 바뀌지 않음 | 상세 행의 `val_rule_temp_id` 가 원하는 템플릿을 가리키는지 확인합니다. |
| 프로젝트 변경 후 포인트가 맞지 않음 | `poi_id` 가 현재 프로젝트의 POI 와 일치하는지 확인합니다. |
| 가져온 뒤 ID 가 맞지 않음 | 복사/가져오기는 ID 를 재생성하므로 대상 DB 의 참조를 다시 확인합니다. |

## 인수인계 체크리스트

- 마스터 테이블과 상세 테이블의 역할을 분리해서 설명합니다.
- 각 제품 템플릿이 사용하는 POI, Validate 규칙, 프로젝트 패키지를 기록합니다.
- `val_rule_temp_id` 를 바꾸면 검수 샘플과 프로젝트 설명도 갱신합니다.
- 이전 시 BuzProduct, POI, Validate 사전, Validate 규칙을 함께 확인합니다.
- `BuzProduc` 철자는 영속화된 템플릿 코드 경계이므로 쉽게 변경하지 않습니다.

## 더 읽기

- [Validate 판정 규칙 템플릿](./validate-rules.md)
- [Compliance 결과 인수인계](./compliance-results.md)
- [POI 템플릿](./poi-template.md)
- [템플릿 관리](./template-management.md)
- [현재 알고리즘 템플릿 커버리지](../current-algorithm-template-coverage.md)
