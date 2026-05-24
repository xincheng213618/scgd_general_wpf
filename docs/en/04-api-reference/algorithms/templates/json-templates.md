# JSON Templates

This page only describes the JSON template host chain actually available in the current repository, no longer maintaining the old "general-purpose algorithm DSL platform + cross-project configuration framework" draft.

## What This Module Is Now

Based on current source code status, the JSON template system is not a configuration platform that exists independently of the database, but a specific branch within the `ColorVision.Engine` template system. Its current core goals are:

- Hosting JSON content from `ModMasterModel.JsonVal` as template items.
- Providing both text editing and property editing modes through the universal editor `EditTemplateJson`.
- Allowing concrete template types in the form of `ITemplateJson<T>` to reuse the same set of load, save, import/export logic.
- Providing a unified host for JSON-driven templates like `PoiAnalysis`, `SFRFindROI`.

Therefore, it is more like a "JSON template branch within the database" rather than a completely independent configuration subsystem.

## Most Critical Files

- `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/TemplateJsonParam.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml`
- `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/PoiAnalysis/TemplatePoiAnalysis.cs`
- `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`

If you just want to see "how JSON templates are currently stored, edited, and connected into the template window," these files already cover the main path.

## How the Current Main Chain Runs

### Host Base Class

`ITemplateJson<T>` is the common host for the JSON template branch. It currently handles:

- Reading `ModMasterModel` from MySQL using `TemplateDicId`
- Wrapping each record as `TemplateModel<T>`
- Providing save, delete, copy, import, and export
- Generating initial content from dictionary template default JSON when creating new templates

This means that although JSON templates look like plain text editing, they currently still deeply depend on template dictionaries and database records.

### Parameter Object

`TemplateJsonParam` is currently the most basic JSON template parameter object. It holds:

- `TemplateJsonModel`
- `ResetCommand`
- `CheckCommand`
- `JsonValueChanged` event

The real semantics of `JsonValue` are:

- Formatted using `JsonHelper.BeautifyJson(...)` when reading
- Only written back to `TemplateJsonModel.JsonVal` when JSON is valid

`ResetValue()` returns to the dictionary template record's default JSON, rather than simply clearing local text.

### Editor Control

`EditTemplateJson` is the current real editing entry point. It now simultaneously supports:

- AvalonEdit text mode
- `JsonPropertyEditorControl` property mode
- Description/comment view toggling
- Validation button
- External JSON website auxiliary entry point

The actual behavior of the `json` button in the bottom right is very clear:

- Opens `https://www.json.cn/`
- Copies the current JSON to clipboard

This is the real function of `Button_Click_1` in the current active file, not some other hidden command.

### Mode Switching and Synchronization

`EditTemplateJson` is currently not a simple text box wrapper. It will:

- Sync text changes using a debounce timer
- Refresh the UI in reverse through `IEditTemplateJson.JsonValueChanged`
- Synchronize JSON content when switching between text mode and property mode
- Remember width and default edit mode using `EditTemplateJsonConfig`

Therefore, the complexity here mainly lies in "keeping the same JSON consistent across two editing surfaces," rather than the algorithm itself.

## Most Common Mistakes to Avoid

### It Is Not a General-Purpose File Template Platform

The current JSON template primary storage is MySQL's `ModMasterModel.JsonVal`, not a set of arbitrary JSON files in the repository. Continuing to write it as "reading a disk configuration directory" would deviate from the real implementation.

### Not All JSON Templates Share the Same Business Schema

`ITemplateJson<T>` only provides the host chain; what fields each concrete template actually needs is determined by its own JSON conventions. Documentation can no longer write one type of JSON structure as a unified system-wide specification.

### The Editor Is Already More Than a Text Editor

Current `EditTemplateJson` already supports switching between property mode and description mode. Only describing the AvalonEdit text box would miss half of the functionality users actually see.

### "Validation" Is Currently Mainly Event-Triggered, Not a Complete Compiler

`CheckCommand` triggers the `JsonValueChanged` event chain; how it responds specifically depends on the caller. Do not write it as an independent JSON rule engine.

## Recommended Reading Order

1. `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
2. `Engine/ColorVision.Engine/Templates/Jsons/TemplateJsonParam.cs`
3. `Engine/ColorVision.Engine/Templates/Jsons/EditTemplateJson.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/Jsons/PoiAnalysis/TemplatePoiAnalysis.cs`
5. `Engine/ColorVision.Engine/Templates/Jsons/SFRFindROI/TemplateSFRFindROI.cs`

## Continue Reading

- [Templates API Reference](./api-reference.md)
- [Template Management](./template-management.md)
- [ColorVision.Engine](../../engine-components/ColorVision.Engine.md)