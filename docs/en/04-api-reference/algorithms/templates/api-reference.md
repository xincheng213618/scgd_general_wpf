# Templates API Reference

This page only retains the relatively stable template host entry points in the current source code, no longer attempting to maintain a "complete signature manual." The reason is straightforward: many template behaviors depend on specific subclass overrides, database state, and user control attachments, making old-style API tables prone to drift.

## Which Entry Points Are Most Worth Knowing First

Based on current code, the most stable and highest-priority types to understand in the template system are:

- `ITemplate`
- `ITemplate<T>`
- `ITemplateJson<T>`
- `TemplateControl` / `IITemplateLoad`
- `ParamBase` / `ModelBase` / `ParamModBase`
- `TemplateModel<T>`
- `TemplateEditorWindow` / `TemplateCreate`

This page focuses on explaining what responsibilities each of these entry points currently carries in the real implementation.

## Core Host Types

### ITemplate

`ITemplate` is the host base class for all templates. Its current most important responsibilities include:

- Registering itself in `TemplateControl.ITemplateNames` upon construction
- Providing lifecycle hooks such as `Load()`, `Save()`, `Import()`, `Export()`, `Delete()`, `Create()`
- Exposing `ItemsSource`, `Count`, `GetValue(...)`, `GetParamValue(...)`
- Controlling host window behavior, such as `IsSideHide`, `IsUserControl`
- Providing creation window source capabilities: `HasCreateTemplateSource`, `ImportName`, `CreateDefault()`

Note: `ITemplate` is currently a concrete base class, not just an interface definition.

### `ITemplate<T>`

`ITemplate<T>` is the most common generic base class for regular parameter templates, where `T : ParamModBase, new()`. It currently primarily unifies:

- `ObservableCollection<TemplateModel<T>> TemplateParams`
- `ItemsSource`
- `Count`
- `GetTemplateNames()`
- `GetTemplateIndex(...)`
- `GetParamValue(...)`

these common list behaviors.

Additionally, it is responsible for generating default parameter objects from dictionary templates based on `TemplateDicId`, so this layer is not just a simple collection wrapper.

### `ITemplateJson<T>`

`ITemplateJson<T>` is the host base class for the JSON template branch, where `T : TemplateJsonParam, new()`. Its main differences from `ITemplate<T>` are:

- Data source is `ModMasterModel.JsonVal`
- Default value creation goes through `SysDictionaryModModel.JsonVal`
- Import/export revolves around `.cfg` and ZIP
- Copy logic is based on JSON serialized copies

If the template content is inherently JSON text, this layer is usually closer to the real implementation than `ITemplate<T>`.

## Registration and Discovery Entry Points

### TemplateControl

`TemplateControl` is the current template registry. It primarily maintains:

- `ITemplateNames`
- `AddITemplateInstance(...)`
- `ExitsTemplateName(...)`
- `FindDuplicateTemplate(...)`

and scans all `IITemplateLoad` implementations during initialization, so that concrete template types load their own content.

### IITemplateLoad

`IITemplateLoad` is the template loading extension point. Currently many template classes implement it so they can execute their own `Load()` when `TemplateControl.Init()` scans.

This is also one of the important reasons the current template system is coupled to the application startup sequence.

## Parameter and Model Base Classes

### ParamBase

`ParamBase` is the thinnest layer, only providing:

- `Id`
- `Name`

It is suitable as the common parent class for all template parameter objects.

### ModelBase

`ModelBase` has a more specific value in the current implementation than its name suggests. It maps `ModDetailModel` lists into parameter dictionaries indexed by symbol name, and provides:

- `GetValue<T>(...)`
- `SetProperty(...)`
- `GetParameter(...)`
- `GetDetail(...)`
- `StringToDoubleArray(...)`
- `DoubleArrayToString(...)`

In other words, the reason many template parameter properties can be written like normal C# properties is because this layer handles dictionary mapping and type conversion underneath.

### ParamModBase

`ParamModBase` goes further up, combining the template master record and parameter detail records, and is the direct base class for most database-driven template parameter objects.

## UI Host Related Types

### `TemplateModel<T>`

`TemplateModel<T>` is the current list item wrapper object. Beyond `Value`, it also handles:

- `Key`
- `IsSelected`
- `IsEditMode`
- Context menu
- Rename and copy name commands

Therefore, the "template items" users see in the list are not bare parameter objects, but this wrapper layer with UI state.

### TemplateEditorWindow

`TemplateEditorWindow` is the most general-purpose template editing host. It decides the right-side display based on whether the template has `IsUserControl`:

- `PropertyGrid`
- Template custom `UserControl`

Meanwhile, it uniformly handles create, copy, save, delete, rename, search, sort, and selection switching.

### TemplateCreate

`TemplateCreate` is currently responsible for template creation source selection. Beyond default templates, it also supports:

- Current copy
- Samples from the SQLite sample library

So it is no longer just a small dialog for entering a template name.

## Most Common Mistakes to Avoid

### `ITemplate` Is Not a Pure Interface

Many default behaviors are currently written directly in the `ITemplate` base class, including registration, window creation, and various lifecycle methods. Writing it as a pure abstract contract would mislead readers.

### Many Behaviors Only Hold After Concrete Template Overrides

For example, methods like `Import()`, `Export()`, `CreateDefault()`, `GetUserControl()` may not have complete implementations in the base class. The base class method table cannot be treated directly as a "feature list fully supported by all templates."

### Data Model and UI Model Are Mixed

Types like `TemplateModel<T>`, `TemplateEditorWindow`, `TemplateCreate` show that the current template system has not completely separated UI state out. API explanations must preserve this real boundary.

### JSON Templates and Regular Parameter Templates Are Two Host Branches

Although they both fall under Templates, the default persistence, creation, and import/export paths of `ITemplate<T>` and `ITemplateJson<T>` are not the same.

## Recommended Reading Order

1. `Engine/ColorVision.Engine/Templates/ITemplate.cs`
2. `Engine/ColorVision.Engine/Templates/Jsons/ITemplateJson.cs`
3. `Engine/ColorVision.Engine/Templates/ModelBase.cs`
4. `Engine/ColorVision.Engine/Templates/ParamModBase.cs`
5. `Engine/ColorVision.Engine/Templates/TemplateModel.cs`
6. `Engine/ColorVision.Engine/Templates/TemplateEditorWindow.xaml.cs`
7. `Engine/ColorVision.Engine/Templates/TemplateCreate.xaml.cs`

## Continue Reading

- [Template Management](./template-management.md)
- [JSON Templates](./json-templates.md)
- [Flow Engine](./flow-engine.md)