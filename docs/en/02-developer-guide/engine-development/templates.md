# Engine Template System Development Handoff

This page documents the real template model under `Engine/ColorVision.Engine/Templates/`. Templates own parameters, editing, persistence, import/export, and algorithm command parameters. Customer judgement and report formats belong to project packages.

Read [Engine Templates And Flow Chain](../../04-api-reference/engine-components/template-flow-chain.md) first, then use this page for code changes.

## Runtime Chain

| Stage | Key Object | Purpose |
| --- | --- | --- |
| Template registration | `ITemplate` constructor, `TemplateControl.AddITemplateInstance` | Adds template instances to the global template table |
| Discovery | `IITemplateLoad`, `TemplateControl` | Scans and instantiates loadable templates |
| Parameter list | `TemplateModel<T>`, `TemplateParams` | Real collection used by combo boxes and editors |
| MySQL templates | `ITemplate<T>`, `ParamModBase`, `ModMasterModel`, `ModDetailModel` | Reads dictionary-backed template rows by `TemplateDicId` |
| JSON templates | `ITemplateJson<T>`, `TemplateJsonParam` | Stores complex algorithm parameters as JSON |
| Editing | `TemplateEditorWindow`, `EditTemplateJson`, specific editors | Create, copy, edit, import, export |
| Flow binding | `Templates/Flow/`, `NodeConfigurator` | Reads templates and writes node parameters |
| Result display | `ViewHandle*`, `IResultHandleBase` | Result parsing and overlay, separate from template persistence |

## Choose a Template Model

| Scenario | Model | Examples |
| --- | --- | --- |
| Stable dictionary-backed parameters | `ITemplate<T>` + `ParamModBase` | `TemplatePoi`, `TemplateSFR`, `TemplateImageCropping` |
| Complex algorithm parameters | `ITemplateJson<T>` + `TemplateJsonParam` | `TemplateSFR2`, `TemplateOLEDAOI`, `TemplateKB` |
| Device runtime parameters | Device-folder `Templates/` | Camera, PG, Sensor, SMU templates |
| Flow template | `Templates/Flow/TemplateFlow` | Flow groups and node configuration |
| Customer output format | Project `Process` / exporter | Do not put this in the common template layer |

## Add a Template

1. Decide whether the parameter belongs to a common algorithm, device runtime, Flow node, or project rule.
2. Add the parameter class. MySQL templates inherit `ParamModBase`; JSON templates inherit `TemplateJsonParam`.
3. Add `Template*`, inheriting `ITemplate<T>` or `ITemplateJson<T>`, and implement `IITemplateLoad` when it should auto-load.
4. Provide a static `Params` collection and assign it to `TemplateParams`.
5. Implement `GetMysqlCommand()` when database restore is needed, and verify `TemplateDicId`.
6. Add an editor path: PropertyGrid/specific editor for normal parameters, `EditTemplateJson` or a specific page for JSON.
7. If an algorithm references the template, write template id/name and related POI template into `CVTemplateParam`.
8. If a Flow node selects the template, add the combo box and edit button in `NodeConfigurator` or the node property panel.
9. If a new result type is produced, add `ViewHandle*` and update result handoff docs.

## Persistence and Compatibility

- `ITemplate<T>.Load()` queries `ModMasterModel` and `ModDetailModel` by `TemplateDicId`.
- `SaveIndex` controls which templates are saved after editing.
- Use `NewCreateFileName()` and `TemplateControl.ExitsTemplateName()` to avoid duplicate names.
- JSON field renames affect old template deserialization; new fields need defaults.
- Template names and ids are often used by project exports and result display, so UI-only validation is too weak.

## Common Failures

| Symptom | Check First |
| --- | --- |
| Template missing from combo box | `IITemplateLoad`, `TemplateParams`, `TemplateControl` discovery |
| Changes lost after restart | `GetMysqlCommand()`, `TemplateDicId`, `SaveIndex`, MySQL connection |
| Flow node shows stale values | Node storage fields, template id, template name |
| Algorithm misses parameters | `Algorithm*` writing `TemplateParam` / `POITemplateParam` |
| Result page fails | `ViewHandle*` and DAO chain, not only template code |

## Acceptance Checklist

| Item | Validation |
| --- | --- |
| Template manager | Create, copy, rename, import, export, delete |
| Save/restore | Restart the host and verify all fields |
| Flow | Run a minimal flow with the new template |
| Algorithm request | Log or message record contains the expected template id/name |
| Result display | History, overlay, table, and project export read the new result |
| Old templates | Existing templates still run with sane defaults |

## Related Documents

- [Engine Templates And Flow Chain](../../04-api-reference/engine-components/template-flow-chain.md)
- [Engine Result Display And Project Handoff](../../04-api-reference/engine-components/result-handoff-chain.md)
- [FlowEngineLib](../../04-api-reference/engine-components/FlowEngineLib.md)
- [Testing and Validation Handoff](../testing.md)
