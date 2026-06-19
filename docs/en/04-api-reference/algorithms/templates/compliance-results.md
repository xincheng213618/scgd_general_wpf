# Compliance Result Handoff

This page explains the result models and handlers under `Engine/ColorVision.Engine/Templates/Compliance/`. This directory does not create rule templates. It reads compliance result rows, displays them, and interprets `ValidateResult`.

## Scope

| Item | Current implementation |
| --- | --- |
| Y result | `ComplianceYModel`, `ComplianceYDao`, `ViewHandleComplianceY` |
| XYZ result | `ComplianceXYZModel`, `ComplianceXYZDao`, `ViewHandleComplianceXYZ` |
| JND result | `ComplianceJNDModel`, `ComplianceJNDDao`, `ViewHandleComplianceJND` |
| Rule source | `ValidateResult` JSON |
| Deserialized type | `ObservableCollection<ValidateRuleResult>` |
| Pass condition | Every rule has `Result == ValidateRuleResultType.M` |
| Runtime entry | `IResultHandleBase` handler |

## Result Type Mapping

| Handler | Result types | Table |
| --- | --- | --- |
| `ViewHandleComplianceY` | `Compliance_Contrast`, `Compliance_Math`, `Compliance_Contrast_CIE_Y`, `Compliance_Math_CIE_Y` | `t_scgd_algorithm_result_detail_compliance_y` |
| `ViewHandleComplianceXYZ` | `Compliance_Contrast_CIE_XYZ`, `Compliance_Math_CIE_XYZ` | `t_scgd_algorithm_result_detail_compliance_xyz` |
| `ViewHandleComplianceJND` | `Compliance_Math_JND` | `t_scgd_algorithm_result_detail_compliance_jnd` |

## Data Models

`ComplianceYModel` stores single-value Y or contrast results with `pid`, `name`, `data_type`, `data_value`, and `validate_result`.

`ComplianceXYZModel` stores color and optical components such as `data_value_x/y/z`, `data_value_u/v`, `data_value_cct`, `data_value_wave`, and `validate_result`.

`ComplianceJNDModel` stores `data_val_h`, `data_val_v`, and `validate_result` for JND compliance.

## Validation Logic

All three models use the same `Validate` logic:

1. Empty `ValidateResult` becomes `null` and returns `false`.
2. Non-empty JSON is deserialized into `ObservableCollection<ValidateRuleResult>`.
3. The result passes only when every rule result equals `ValidateRuleResultType.M`.
4. Any non-`M` item makes the result fail.

Compliance pages do not recompute thresholds. They interpret the validation JSON written by the algorithm service or upstream project flow.

## Display Flow

1. The result page selects a `ViewHandleCompliance*` by `ViewResultAlgType`.
2. If `ResultImagFile` exists, the handler opens the image first.
3. The handler queries the detail table by the master result ID.
4. Rows are converted into `IViewResult` and bound to the ListView.
5. Columns show name, value fields, validation state, and validation JSON.

`ViewHandleComplianceXYZ` currently binds a `DataValue` column while the model exposes component fields such as `DataValuex/y/z`. If XYZ values appear blank, check the binding/model alignment first.

## Relationship To Other Modules

| Module | Role in the validation chain |
| --- | --- |
| [Validate Rule Templates](./validate-rules.md) | Defines fields, thresholds, and comparison behavior. |
| [BuzProduct Business Template](./buz-product-template.md) | Selects a rule template through `val_rule_temp_id`. |
| Compliance results | Reads `ValidateResult` and displays pass/fail. |
| Project packages | May aggregate or export Compliance/JND/POI results into final OK/NG. |

## Troubleshooting

| Symptom | Check first |
| --- | --- |
| No detail rows | Confirm the result type and the detail table `pid`. |
| Image does not open | Check `ResultImagFile`, especially after archive or migration. |
| `Validate` is false | Check for empty JSON or a non-`M` rule result. |
| XYZ value is blank | Check ListView binding names versus `ComplianceXYZModel`. |
| Project report differs from result page | Check project-level filtering, sorting, or aggregation. |

## Handoff Checklist

- Explain that Compliance is a result display/interpretation layer, not a rule editor.
- Add handler, DAO, table, and docs when adding a result type.
- Validate all Y/XYZ/JND models if `ValidateResult` changes.
- Preserve master result, detail rows, image path, Validate template, and project exports for acceptance.

## Further Reading

- [Validate Rule Templates](./validate-rules.md)
- [BuzProduct Business Template](./buz-product-template.md)
- [JND Template](./jnd-template.md)
- [Engine Result Handoff Chain](../../engine-components/result-handoff-chain.md)
- [Current Algorithm Template Coverage](../current-algorithm-template-coverage.md)
