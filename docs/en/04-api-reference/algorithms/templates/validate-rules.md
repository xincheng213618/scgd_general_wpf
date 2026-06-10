# Validate Rule Templates

This page explains the two-layer rule system under `Engine/ColorVision.Engine/Templates/Validate/`. Validate has a default compliance dictionary layer and an actual rule-template layer. BuzProduct, Compliance, and several project packages depend on this data.

## Scope

| Item | Current implementation |
| --- | --- |
| Default dictionary template | `TemplateDicComply : ITemplate<DicComplyParam>` |
| Actual rule template | `TemplateComplyParam : ITemplate<ValidateParam>` |
| Dictionary editor | `DicEditComply.xaml(.cs)` |
| Rule editor | `ValidateControl.xaml(.cs)` |
| Menu providers | `ExportComply.cs`, `ExportDicComply.cs` |
| Rule master table | `t_scgd_rule_validate_template_master` |
| Rule detail table | `t_scgd_rule_validate_template_detail` |
| Runtime cache | `TemplateComplyParam.CIEParams`, `TemplateComplyParam.JNDParams` |

## Two Layers

`TemplateDicComply` maintains the default dictionary and default rule items from `SysDictionaryModMasterDao` and `SysDictionaryModItemValidateDao`.

| Dictionary `mod_type` | Current use |
| --- | --- |
| `110` | Point-level CIE/compliance rule menus. |
| `111` | Point-list compliance rule menus. |
| `120` | JND compliance rule menus. |

`TemplateComplyParam(code, type)` loads actual rule templates by dictionary code. It reads `t_scgd_rule_validate_template_master`, then loads details from `t_scgd_rule_validate_template_detail`.

| Table | Key fields | Purpose |
| --- | --- | --- |
| `t_scgd_rule_validate_template_master` | `dic_pid`, `code`, `name`, `is_enable`, `is_delete`, `tenant_id` | One rule template under a dictionary code. |
| `t_scgd_rule_validate_template_detail` | `dic_pid`, `pid`, `code`, `val_max`, `val_min`, `val_equal`, `val_radix`, `val_type` | Individual rule items and thresholds. |

## Dynamic Menus

`ExportComply.cs` builds menus from dictionary data:

| Source | Menu behavior |
| --- | --- |
| `mod_type = 110` | Opens `TemplateComplyParam(item.Code)` for point-level rules. |
| `mod_type = 111` | Opens `TemplateComplyParam(item.Code)` for point-list rules. |
| `mod_type = 120` | Opens `TemplateComplyParam(item.Code, 1)` for JND rules. |
| `ExportDicComply` | Opens `TemplateDicComply` to maintain the default dictionary. |

## Create And Save

`TemplateDicComply.Create(templateCode, templateName)` creates a `SysDictionaryModModel`. The current default `ModType` is `111`.

`TemplateComplyParam.Create(templateName)` creates a master row, finds the dictionary by `Code`, copies enabled dictionary validation items, and saves detail rows with `ValMax`, `ValMin`, `ValEqual`, `ValRadix`, and `ValType`.

`TemplateComplyParam.Save()` saves the rule-template name and detail rows. `TemplateDicComply.Save()` saves the default dictionary and default validation items.

## Runtime Cache

| Cache | Purpose |
| --- | --- |
| `CIEParams` | CIE/general compliance rule templates. BuzProduct reads this list. |
| `JNDParams` | JND rule templates. |

When `type == 1`, the constructor adds the collection to `JNDParams` and also to `CIEParams`. Document this current behavior during handoff instead of assuming that JND templates only exist in `JNDParams`.

## Import Limitation

`TemplateComplyParam.Import()` currently reports that import is not supported. For field migration, move both the dictionary tables and `t_scgd_rule_validate_template_*` data, or add a dedicated import flow.

## Relationship To Other Modules

| Module | Dependency |
| --- | --- |
| [BuzProduct Business Template](./buz-product-template.md) | Detail `val_rule_temp_id` references a Validate template. |
| [Compliance Result Handoff](./compliance-results.md) | Reads `ValidateResult` and checks for `ValidateRuleResultType.M`. |
| [JND Template](./jnd-template.md) | JND rules come from `mod_type = 120`. |
| Project packages | May use Validate/Compliance data for final reports and OK/NG. |

## Troubleshooting

| Symptom | Check first |
| --- | --- |
| Menu entry is missing | Check `SysDictionaryModMaster` `mod_type` and `is_delete = false`. |
| New template has no details | Check enabled dictionary items under the dictionary `pid`. |
| BuzProduct has no rule options | Check whether `TemplateComplyParam.CIEParams` has loaded the code. |
| JND rule appears in CIE list | This is the current constructor behavior. |
| Import is unavailable | Actual rule templates do not support import yet. |

## Handoff Checklist

- Explain the default dictionary layer and actual rule-template layer separately.
- Update dictionary, rule details, acceptance samples, and result docs when adding a field.
- Validate service-written `ValidateResult` after changing threshold semantics.
- Migrate `SysDictionaryMod*` and `t_scgd_rule_validate_template_*` together.
- Test all `mod_type = 110/111/120` menu paths after menu changes.

## Further Reading

- [BuzProduct Business Template](./buz-product-template.md)
- [Compliance Result Handoff](./compliance-results.md)
- [Template Management](./template-management.md)
- [Engine Result Handoff Chain](../../engine-components/result-handoff-chain.md)
- [Current Algorithm Template Coverage](../current-algorithm-template-coverage.md)
