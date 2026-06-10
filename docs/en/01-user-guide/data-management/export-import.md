# Data Export & Import

There is no single unified "import/export everything from here" control center in the current repository. The reality is closer to: different objects use different entry points, and settings, workflow templates, and specific result data each have their own import/export methods.

## How to Understand This Page First

If you need to do import/export, first answer three questions:

1. Are you dealing with settings, workflow templates, or data from a specific result module?
2. Do you need global configuration migration, or export of a single business object?
3. Does this functionality inherently belong to a specific window rather than a data management center?

## Currently Verifiable Entry Points

### Settings Import/Export

There is a clear menu entry:

- Tools → Import/Export Settings

This includes at least two types of actions:

- Export settings to `.cvsettings`
- Import settings from `.cvsettings`

If your goal is to migrate software configuration rather than result data, start here first.

### Workflow Template Import/Export

Workflow template import/export is not handled uniformly on the data management page, but is provided separately within the workflow designer:

- Export current workflow
- Import workflow
- Import module

If you need to migrate workflow content, go to [Workflow Design](../workflow/design.md) first.

### In-Module Result Export

Some business windows provide their own export functionality, for example:

- The workflow node analysis window can export CSV
- Some plugins or image/measurement windows have their own CSV or image export entry points

This type of export is typically tightly coupled to specific business objects and should no longer be described as a unified global "data export center."

## Object and Entry Mapping

| Deliverable | Preferred entry | Confirm before handoff |
| --- | --- | --- |
| Software settings | Tools -> Import/Export Settings | `.cvsettings` imports and key settings persist after restart |
| Workflow template | Workflow designer import/export | start node, device binding, and template parameters are correct after import |
| Database record | Database browser or business result page | same run can be queried by SN, time, or batch |
| CSV/Excel | owning business window or plugin export | field order, units, PASS/FAIL, and encoding match customer requirements |
| PDF/report | project window or plugin report entry | header, customer mark, result image, and judgement items are correct |
| Image/overlay | image editor or result window | original image, ROI/POI, annotation coordinates, and file name match |
| Socket/MES response | project window, SocketProtocol, or integration tool | request/response samples are saved and status/Data fields are correct |

## Export Acceptance Before Handoff

Export issues often look like "the button works, but the delivered file is wrong." Run one end-to-end check before handoff.

| Step | Action | Pass standard |
| --- | --- | --- |
| 1 | Run a minimal workflow with a known SN or test batch | all later query/export/response checks use the same identifier |
| 2 | Confirm source data in database or result window | source is not empty and is not an old batch |
| 3 | Export from the target window | file is generated and path/name are explainable |
| 4 | Open the exported file and verify fields | field order, unit, judgement, time, and SN match customer format |
| 5 | Save one sample file and screenshot | future upgrades can be retested against the sample |

## Export Failure Triage

| Symptom | Check first | Then check |
| --- | --- | --- |
| Cannot find export button | object belongs to settings, workflow, or business window | plugin/project docs declare export support |
| File generated but empty | source data exists, batch/SN selected correctly | export filters and field mapping |
| Missing or reordered fields | exporting the correct object/window | project exporter and customer format version |
| Image or overlay misaligned | result image and original image are from the same run | ROI/POI coordinates, scaling, template version |
| External system receives nothing | ColorVision completed and generated result | protocol, port, project handler, response fields |
| Behavior changed after migration | exported settings only, not result data | old config, workflow template, database backup synchronized |

## Handoff Record Template

```text
export object:
source window:
source SN/batch/time:
database evidence:
export file path:
file format/version:
required fields:
sample screenshot:
external response sample:
known limitations:
owner/date:
```

## Common Usage Order

1. First confirm exactly what object you want to export.
2. If it is global settings, use "Import/Export Settings."
3. If it is workflow content, use the import/export in [Workflow Design](../workflow/design.md).
4. If it is result data or business data, first go back to the corresponding module window and find its own export entry point.
5. When it truly involves in-database data, use [Database Operations](./database.md) to confirm the source data scope.

## What This Page No Longer Promises

The following capabilities are no longer claimed as universally available by the current user guide page, unless you have actually seen them in a specific module window:

- Unified Excel export center
- Unified JSON export center
- Unified XML export center
- Unified PDF report export center
- Universal column mapping import wizard
- Universal batch folder import wizard

If a specific plugin or window does support these formats, it should be documented on that module's own page, not described generically here.

## Common Issues

### I Want to Export Data, but Can't Find a Unified Entry Point

- Do not keep searching the top-level menu
- First confirm whether the object belongs to settings, workflows, or a specific business result window
- Then go back to the corresponding module page to find the export entry point

### Exported Settings, but Business Results Did Not Migrate Along

- `.cvsettings` is primarily used for configuration migration, not equivalent to database result migration
- Actual result data must be handled in conjunction with [Database Operations](./database.md) or the corresponding business module separately

### Workflow Import Succeeded, but Execution Results Are Wrong

- First confirm whether the correct workflow version was imported
- Then go back to [Workflow Execution & Debugging](../workflow/execution.md) to verify dependent devices and templates
- Recheck module imports and workflow parameters if necessary

## Continue Reading

- [Data Management Overview](./README.md)
- [Database Operations](./database.md)
- [Workflow Design](../workflow/design.md)
- [Common Issues](../troubleshooting/common-issues.md)
- [Field Operation Acceptance Checklist](../field-operation-acceptance.md)

## Notes

- This page only describes currently verifiable import/export paths and no longer maintains generalized Excel/JSON/XML/PDF unified wizard documentation.
- Settings import/export implementation is primarily located in `UI/ColorVision.UI.Desktop/Settings/ExportAndImport/`.
