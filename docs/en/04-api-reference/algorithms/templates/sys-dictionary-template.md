# SysDictionary Template

This page explains `Engine/ColorVision.Engine/Templates/SysDictionary/`. It maintains default algorithm dictionaries stored in `t_scgd_sys_dictionary_mod_master` and `t_scgd_sys_dictionary_mod_item`. `TemplateModParam` currently loads `mod_type = 7`.

## Scope

| Item | Current implementation |
| --- | --- |
| Template class | `TemplateModParam : ITemplate<DicModParam>` |
| Parameter class | `DicModParam : ParamModBase` |
| Editor | `EditDictionaryMode.xaml(.cs)` |
| Create master window | `CreateDicTemplate.xaml(.cs)` |
| Create detail window | `CreateDicModeDetail.xaml(.cs)` |
| Menu entry | `MenuDefalutDicAlg` |
| Master table | `t_scgd_sys_dictionary_mod_master` |
| Detail table | `t_scgd_sys_dictionary_mod_item` |
| Current scope | `tenant_id = 0`, `mod_type = 7` |

## Data Model

`SysDictionaryModModel` stores dictionary masters: `code`, `name`, `pid`, `p_type`, `mod_type`, `cfg_json`, `version`, `is_enable`, `is_delete`, and `tenant_id`.

`SysDictionaryModDetaiModel` stores dictionary items: `pid`, `address_code`, `symbol`, `name`, `default_val`, `val_type`, `is_enable`, and `is_delete`. `val_type` can be `Integer`, `Float`, `Bool`, `String`, or `Enum`.

## Lifecycle

1. `MenuDefalutDicAlg` opens `TemplateEditorWindow(new TemplateModParam())`.
2. `TemplateModParam.Load()` reads `tenant_id = 0`, `mod_type = 7` masters.
3. Details are loaded by `SysDictionaryModDetailDao.GetAllByPid(model.Id)`.
4. `EditDictionaryMode` displays details and allows default-value and enable-state editing.
5. `CreateDicTemplate` creates a master with `ModType = 7`.
6. `CreateDicModeDetail` creates a detail with `ValueType = String` and `IsEnable = true`.
7. `Save()` persists detail rows.

Currently `Save()` persists details only, not master fields. The delete path calls `SysResourceModel`; if deletion does not affect the expected dictionary table, check the DAO/table mapping first.

## Relationships

| Module | Relationship |
| --- | --- |
| Strongly typed templates | Use `TemplateDicId` to read default dictionary items. |
| JSON templates | Many JSON template masters are also `mod_type = 7`, with content in `cfg_json`. |
| Flow templates | Flow construction reads dictionary details for node/template parameters. |
| Validate | Validate dictionaries use `mod_type = 110/111/120`; do not mix them with algorithm dictionaries. |

## Troubleshooting

| Symptom | Check first |
| --- | --- |
| Template fields missing | `TemplateDicId` and dictionary master existence. |
| New field not used | Detail `pid`, `symbol`, `address_code`, and `is_enable`. |
| Default value ignored | `default_val` type versus `val_type`. |
| Menu entry missing | `MenuDefalutDicAlg` scanning and permissions. |
| Deleted field still visible | Delete table, cache, and menu/template refresh. |

## Handoff Checklist

- Confirm whether a new template needs a new dictionary master and items.
- Check template parameter names, import/export, Flow packages, and historical templates when changing `symbol`.
- Migrate master and detail tables together.
- Keep `mod_type = 7` algorithm dictionaries separate from Validate dictionaries.

## Further Reading

- [Template Management](./template-management.md)
- [Templates API Reference](./api-reference.md)
- [Validate Rule Templates](./validate-rules.md)
- [Engine Template And Flow Chain](../../engine-components/template-flow-chain.md)
- [Current Algorithm Template Coverage](../current-algorithm-template-coverage.md)
