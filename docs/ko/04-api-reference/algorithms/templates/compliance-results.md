# Compliance 결과 인수인계

이 문서는 `Engine/ColorVision.Engine/Templates/Compliance/` 의 결과 모델과 표시 흐름을 설명합니다. 이 디렉터리는 규칙을 만드는 곳이 아니라 합규 결과를 읽고 표시하며 `ValidateResult` 를 해석하는 계층입니다.

## 적용 범위

| 항목 | 현재 구현 |
| --- | --- |
| Y 결과 | `ComplianceYModel`, `ComplianceYDao`, `ViewHandleComplianceY` |
| XYZ 결과 | `ComplianceXYZModel`, `ComplianceXYZDao`, `ViewHandleComplianceXYZ` |
| JND 결과 | `ComplianceJNDModel`, `ComplianceJNDDao`, `ViewHandleComplianceJND` |
| 판정 원본 | `ValidateResult` JSON |
| 역직렬화 타입 | `ObservableCollection<ValidateRuleResult>` |
| 통과 조건 | 모든 규칙이 `Result == ValidateRuleResultType.M` |
| 실행 진입점 | `IResultHandleBase` handler |

## 결과 타입 매핑

| Handler | 처리 결과 타입 | 테이블 |
| --- | --- | --- |
| `ViewHandleComplianceY` | `Compliance_Contrast`, `Compliance_Math`, `Compliance_Contrast_CIE_Y`, `Compliance_Math_CIE_Y` | `t_scgd_algorithm_result_detail_compliance_y` |
| `ViewHandleComplianceXYZ` | `Compliance_Contrast_CIE_XYZ`, `Compliance_Math_CIE_XYZ` | `t_scgd_algorithm_result_detail_compliance_xyz` |
| `ViewHandleComplianceJND` | `Compliance_Math_JND` | `t_scgd_algorithm_result_detail_compliance_jnd` |

## 데이터 모델

`ComplianceYModel` 은 단일값 휘도 또는 대비 결과를 저장하며 `pid`, `name`, `data_type`, `data_value`, `validate_result` 를 가집니다.

`ComplianceXYZModel` 은 `data_value_x/y/z`, `data_value_u/v`, `data_value_cct`, `data_value_wave` 같은 색/광학 성분과 `validate_result` 를 저장합니다.

`ComplianceJNDModel` 은 `data_val_h`, `data_val_v`, `validate_result` 를 저장합니다.

## 판정 로직

세 모델의 `Validate` 로직은 같습니다.

1. `ValidateResult` 가 비어 있으면 `false` 입니다.
2. JSON 은 `ObservableCollection<ValidateRuleResult>` 로 역직렬화됩니다.
3. 모든 `Result` 가 `ValidateRuleResultType.M` 일 때만 통과입니다.
4. 하나라도 `M` 이 아니면 실패입니다.

Compliance 페이지는 임계값을 다시 계산하지 않고, 상위 알고리즘 서비스나 프로젝트 흐름이 기록한 판정 JSON 을 해석합니다.

## 표시 흐름

1. 결과 페이지가 `ViewResultAlgType` 으로 `ViewHandleCompliance*` 를 선택합니다.
2. `ResultImagFile` 이 있으면 먼저 이미지를 엽니다.
3. handler 는 마스터 결과 `id` 로 상세 테이블을 조회합니다.
4. 행을 `IViewResult` 로 변환해 ListView 에 바인딩합니다.
5. 표에는 이름, 값, 판정 상태, 판정 JSON 이 표시됩니다.

현재 `ViewHandleComplianceXYZ` 는 `DataValue` 컬럼을 바인딩하지만 모델은 주로 `DataValuex/y/z/u/v/...` 성분 필드를 노출합니다. XYZ 값이 비어 있으면 바인딩 이름과 모델 속성을 먼저 확인하세요.

## 다른 모듈과의 관계

| 모듈 | 판정 흐름에서의 역할 |
| --- | --- |
| [Validate 판정 규칙 템플릿](./validate-rules.md) | 필드, 임계값, 비교 방식을 정의합니다. |
| [BuzProduct 제품 업무 파라미터 템플릿](./buz-product-template.md) | `val_rule_temp_id` 로 규칙 템플릿을 선택합니다. |
| Compliance 결과 | `ValidateResult` 를 읽고 통과/실패를 표시합니다. |
| 프로젝트 패키지 | Compliance/JND/POI 결과를 집계하거나 내보낼 수 있습니다. |

## 문제 해결

| 현상 | 먼저 확인할 것 |
| --- | --- |
| 상세가 표시되지 않음 | 결과 타입과 상세 테이블의 `pid` 를 확인합니다. |
| 이미지가 열리지 않음 | `ResultImagFile` 과 이전 후 경로를 확인합니다. |
| `Validate` 가 실패 | `validate_result` 가 비어 있거나 비 `M` 규칙이 있는지 확인합니다. |
| XYZ 값이 비어 있음 | ListView 바인딩 이름과 `ComplianceXYZModel` 을 확인합니다. |
| 프로젝트 보고서와 다름 | 프로젝트 측 필터, 정렬, 집계를 확인합니다. |

## 인수인계 체크리스트

- Compliance 는 결과 표시/해석 계층이며 규칙 편집 계층이 아님을 설명합니다.
- 결과 타입을 추가할 때 handler, DAO, 테이블, 문서를 함께 갱신합니다.
- `ValidateResult` JSON 을 변경하면 Y, XYZ, JND 세 계열을 검증합니다.
- 검수 시 마스터 결과, 상세 행, 이미지 경로, Validate 템플릿, 프로젝트 출력 파일을 보관합니다.

## 더 읽기

- [Validate 판정 규칙 템플릿](./validate-rules.md)
- [BuzProduct 제품 업무 파라미터 템플릿](./buz-product-template.md)
- [JND 템플릿](./jnd-template.md)
- [Engine 결과 표시와 프로젝트 인수인계 체인](../../engine-components/result-handoff-chain.md)
- [현재 알고리즘 템플릿 커버리지](../current-algorithm-template-coverage.md)
