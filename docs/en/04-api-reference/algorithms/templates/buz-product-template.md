# BuzProduct Business Template

This page explains `Engine/ColorVision.Engine/Templates/BuzProduct/`. `BuzProduct` is not an algorithm execution entry. It is a product/business template that combines product configuration, POI references, and Validate rule template references for project packages and field reuse.

## Scope

| Item | Current implementation |
| --- | --- |
| Template code | `BuzProduc`, kept as-is because this is the spelling in source |
| Template class | `TemplateBuzProduc : ITemplateBuzProduc<TemplateBuzProductParam>, IITemplateLoad` |
| Parameter class | `TemplateBuzProductParam` |
| Editor | `EditTemplateBuzProduct.xaml(.cs)` |
| MySQL recovery | `MysqlBuzProduct` |
| Master table | `t_scgd_buz_product_master` |
| Detail table | `t_scgd_buz_product_detail` |
| Key dependencies | `TemplateComplyParam.CIEParams`, POI templates, Validate rule templates |

## Source Entries

| File | Handoff purpose |
| --- | --- |
| `TemplateBuzProduc.cs` | Registers title, code, editor control, and MySQL recovery command. |
| `ITemplateBuzProduc.cs` | Implements load, save, create, copy, import, export, and delete. |
| `TemplateBuzProductParam.cs` | Exposes the master model, detail collection, and add-detail command to the editor. |
| `BuzProductMasterDao.cs` | SqlSugar model and DAO for `t_scgd_buz_product_master`. |
| `BuzProductDetailDao.cs` | SqlSugar model and DAO for `t_scgd_buz_product_detail`. |
| `EditTemplateBuzProduct.xaml(.cs)` | Edits product business items and loads Validate options from `CIEParams`. |
| `MysqlBuzProduct.cs` | Recreates the master/detail table structure. |

## Data Tables

`t_scgd_buz_product_master` stores the product or business profile. Important fields are `code`, `name`, `buz_type`, `cfg_json`, `img_file`, `is_enable`, `is_delete`, `tenant_id`, and `remark`.

`t_scgd_buz_product_detail` stores the business items under the master profile. Important fields are `code`, `name`, `pid`, `poi_id`, `order_index`, `cfg_json`, and `val_rule_temp_id`.

`val_rule_temp_id` is the key handoff field. It points to a Validate rule template, so changing it can change the final compliance result for a product item.

## Lifecycle

1. The template system discovers `TemplateBuzProduc` and calls `Load()`.
2. `Load()` reads master rows where `is_delete = 0`.
3. Each master row loads details through `BuzProductDetailDao.GetAllByPid(...)`.
4. The editor binds `TemplateBuzProductParam.BuzProductDetailModels`.
5. `Save()` persists the master name and each detail row.
6. `Create()` creates a new master and copies imported/copied details with IDs reset before insert.
7. `Delete()` removes the master and its detail rows.

## Validate Link

`EditTemplateBuzProduct` initializes the rule selector from:

```csharp
TemplateComplyParam.CIEParams.SelectMany(kvp => kvp.Value).ToList()
```

For handoff, explain the relationship clearly:

| BuzProduct detail | Validate rule | Result impact |
| --- | --- | --- |
| `poi_id` | Selects the detection point or area. | Decides which business point receives a result. |
| `val_rule_temp_id` | Selects the rule template. | Affects Compliance or project-level OK/NG. |
| `cfg_json` | Stores item-specific configuration. | May be interpreted by a project package. |

## Import And Export

Single templates are exported as `.cfg`; multiple selected templates are exported as `.zip`. Import/copy recreates rows and generates new database IDs. When moving templates to another machine, also verify the POI templates, Validate dictionaries, and Validate rule templates.

## Troubleshooting

| Symptom | Check first |
| --- | --- |
| Template cannot be found | Search for `BuzProduc`, not the corrected spelling. |
| Rule dropdown is empty | Check whether `TemplateComplyParam` has loaded `CIEParams`. |
| Validation does not change | Verify the detail row's `val_rule_temp_id`. |
| Business points are wrong after switching projects | Verify `poi_id` against the current project's POI templates. |
| Imported IDs look wrong | Copy/import resets IDs; verify references in the target database. |

## Handoff Checklist

- Document both the master and detail table usage.
- Record the POI template, Validate rule template, and project package used by each product template.
- Update acceptance samples and project docs when `val_rule_temp_id` changes.
- During migration, check BuzProduct, POI, Validate dictionaries, and Validate rules together.
- Do not rename `BuzProduc` casually; it is a persisted template code boundary.

## Further Reading

- [Validate Rule Templates](./validate-rules.md)
- [Compliance Result Handoff](./compliance-results.md)
- [POI Template](./poi-template.md)
- [Template Management](./template-management.md)
- [Current Algorithm Template Coverage](../current-algorithm-template-coverage.md)
