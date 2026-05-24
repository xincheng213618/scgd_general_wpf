# Template Management

This page only describes the template host chain actually available in the current repository, no longer maintaining the old "unified framework blueprint + idealized MVVM layers + large pseudo-examples" draft.

## What This Page Now Covers

Based on current source code status, template management is not a separate backend service, but a host chain pieced together from the `ITemplate` base class, global registry, management window, editing window, and creation window. It currently handles:

- Scanning and registering concrete template types after startup.
- Organizing template entry points by namespace in the main program.
- Providing general-purpose editing, creation, import/export, copy, and rename windows.
- Allowing JSON templates, flow templates, POI templates, dictionary templates, etc. to share a common host interface.
- Providing SQLite sample library and global search integration.

So what this page really covers is not "template theory," but how the main program currently hosts various template types.

## Most Critical Files

- `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
- `Engine/ColorVision.Engine/Templates/ITemplate.cs`
- `Engine/ColorVision.Engine/Templates/TemplateManagerWindow.xaml.cs`
- `Engine/ColorVision.Engine/Templates/TemplateEditorWindow.xaml.cs`
- `Engine/ColorVision.Engine/Templates/TemplateCreate.xaml.cs`
- `Engine/ColorVision.Engine/Templates/TemplateSearchProvider.cs`
- `Engine/ColorVision.Engine/Templates/TemplateSampleLibrary.cs`
- `Engine/ColorVision.Engine/Templates/TemplateSampleSaveWindow.xaml.cs`

Reading just these files is already sufficient to establish the main mental model of the current template system.

## How the Current Main Chain Runs

### Initialization and Registration

After startup, `TemplateInitializer` triggers `TemplateControl.GetInstance()`; `TemplateControl` then scans all `IITemplateLoad` implementations in the assemblies and executes `Load()`.

On the other hand, the `ITemplate` constructor itself also asynchronously registers template instances into `TemplateControl.ITemplateNames`. Therefore, current template discovery works through two parallel mechanisms:

- Template objects register into the global registry upon construction.
- Concrete template loaders refresh content after MySQL becomes available.

This is why many template pages cannot be understood in isolation from initialization and database prerequisites.

### Template Management Window

`MenuTemplateManagerWindow` opens `TemplateManagerWindow`. This window is currently not a simple list, but:

- Reads `TemplateControl.ITemplateNames`
- Groups by type namespace
- Supports search and filtering
- Supports displaying templates as cards
- Directly opens the corresponding editor after selecting a template

Therefore, it plays the role of "template entry point aggregator," not just a menu popup.

### Template Editing Window

`TemplateEditorWindow` is the current most general-purpose template host window. It first calls `template.Load()`, then follows two paths based on template type:

- Regular templates: Right side uses `PropertyGrid`
- Custom templates: Calls `GetUserControl()` and lets the template take over the right-side area

The window also uniformly handles:

- Create, copy, save, delete commands
- `SetSaveIndex(...)` on selected item change
- `SetUserControlDataContext(...)` or `GetParamValue(...)`
- Column sorting, search, and double-click behavior

This is why various templates, despite significant UI differences, can still share the same host shell.

### Template Creation Window

`TemplateCreate` is no longer a window that "just provides a name input box." Based on current implementation, it provides multiple sources for new templates:

- System default template
- Current copy (temporarily saved template content after copy)
- Historical samples from the SQLite sample library

These sources are rendered as cards and filtered by group. Finally, `ApplyTemplateSource(...)` injects the selected source into the template to be created.

This shows that the current template creation chain is no longer just "CreateDefault() + manually fill parameters."

### Search and Sample Library

`TemplateSearchProvider` registers all template names into the global search entry point; `TemplateSampleLibrary` stores template samples in a SQLite database under the user documents directory:

- `.../Templates/TemplateSamples.db`

It currently stores:

- Template code and template type
- Group name and sample name
- Description text
- Serialized template content

So template management now has a local sample reuse chain in addition to MySQL primary storage.

## Most Common Mistakes to Avoid

### It Is Not a Pure Service Layer System

Many key logics are currently written directly in WPF windows like `TemplateManagerWindow`, `TemplateEditorWindow`, `TemplateCreate`. Continuing to describe it as "the host only binds ViewModel, logic is all in the service layer" does not match the real code.

### Different Templates Have Inconsistent Persistence Approaches

Some templates primarily depend on MySQL, some support file import/export, and some additionally use the SQLite sample library. Documentation can no longer assume all templates share the same storage model.

### `IsUserControl` and `IsSideHide` Significantly Change Behavior

The current template host is not a fixed layout. `IsUserControl` hands the right side over to the template's custom control, while `IsSideHide` even changes window layout and double-click behavior. Ignoring these two switches makes many template pages inexplicable.

### Template Registration and Database Connection Remain Coupled

Although `ITemplate` construction registers instances, many concrete template contents still need to wait for MySQL connection before they can actually load. Writing the template system as "purely local static registration" would miss critical prerequisites.

## Recommended Reading Order

1. `Engine/ColorVision.Engine/Templates/ITemplate.cs`
2. `Engine/ColorVision.Engine/Templates/TemplateContorl.cs`
3. `Engine/ColorVision.Engine/Templates/TemplateManagerWindow.xaml.cs`
4. `Engine/ColorVision.Engine/Templates/TemplateEditorWindow.xaml.cs`
5. `Engine/ColorVision.Engine/Templates/TemplateCreate.xaml.cs`
6. `Engine/ColorVision.Engine/Templates/TemplateSearchProvider.cs`
7. `Engine/ColorVision.Engine/Templates/TemplateSampleLibrary.cs`

## Continue Reading

- [JSON Templates](./json-templates.md)
- [Flow Engine](./flow-engine.md)
- [Templates Analysis Summary](../../../03-architecture/components/templates/analysis.md)