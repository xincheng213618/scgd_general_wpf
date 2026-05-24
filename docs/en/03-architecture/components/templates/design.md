# Templates Architecture Design

This page only describes the actual design of the Templates system in the current repository, no longer maintaining old drafts based on file counts, directory counts, and idealized layered models.

## First, What to View It As

`Engine/ColorVision.Engine/Templates/` is not a single "algorithm template directory," but a hybrid system that simultaneously contains:

- Template abstract base classes
- Template registration and initialization
- Template management and editing windows
- Business domain template implementations
- Flow templates and flow editor integration
- Embedded template calls in node configuration panels

This determines that its design focus is not pure computation, but "how templates are registered, edited, persisted, and consumed by other features."

## How Core Objects Are Divided

### `ITemplate`

`ITemplate` is the common entry point for all templates, but it is not a thin interface layer — rather a base class with clear runtime responsibilities.

What it currently handles includes:

- Template name, title, code, and other metadata
- List-type access entry points
- Create, delete, save, import, export, and other operation entry points
- Preview double-click behavior
- Creation window and custom `UserControl` support

There is also an implementation detail that is easy to overlook: template objects register themselves to `TemplateControl` via the UI thread during construction. This means template registration is not a purely static configuration process, but directly bound to template instantiation.

### `ITemplate<T>`

`ITemplate<T>` is a generic base class for `ParamModBase`-derived parameter models. It unifies the most common template form in the current system as:

- `ObservableCollection<TemplateModel<T>>`
- Index-based and name-based access
- Default template creation
- Import content staging

In other words, the current template design is more like "a list template base class with UI and persistence semantics," rather than a pure DTO layer that only describes parameter structures.

### `TemplateControl`

`TemplateControl` is the registration center of the current template system. Its key responsibilities are:

- Triggering initialization after database connection becomes available
- Scanning `IITemplateLoad` in loaded assemblies
- Calling `Load()` on each implementation
- Maintaining the global template dictionary `ITemplateNames`
- Handling template name conflict checking

This design indicates that the current template discovery mechanism depends on two prerequisites:

- Assemblies have been loaded
- Database connection is ready

If templates do not appear, check this chain first rather than immediately suspecting the editing window.

## How the Initialization Chain Works

The main chain of the current template system is roughly:

1. The main application and plugins load relevant assemblies into the process.
2. `TemplateInitializer` triggers `TemplateControl.GetInstance()` after MySQL-dependent initialization.
3. `TemplateControl` scans `IITemplateLoad` implementations in loaded assemblies.
4. Each template type loads its currently available template items into memory collections via `Load()`.
5. Template management windows, flow node configurators, and template editing windows then consume these registered instances.

There is no independent DI container or template manifest file to uniformly declare templates here, but rather a combination of "assembly scanning + template object self-registration + specific templates loading data themselves."

## How UI Connects In

### Template Management Window

`TemplateManagerWindow` is currently not a simple list popup. It will:

- Read registered templates from `TemplateControl.ITemplateNames`
- Group by namespace
- Provide search and filtering
- Open the corresponding `TemplateEditorWindow`

This shows that the organizational approach of templates in the UI layer currently relies mainly on "runtime registration results + namespace grouping," rather than an independently maintained template classification table.

### Template Editing Window

`TemplateEditorWindow` is the common operation surface for most templates. Different template types connect editing, import/export, double-click preview, custom editing panels, and other behaviors here through their respective `ITemplate` implementations.

### Template Entry Points in Node Configuration Panels

The template system does not only exist in the "Template Management" menu. `NodePanelBuilder` in flow node property panels also directly opens `TemplateEditorWindow`, allowing node configuration to jump to template editing in-place.

This means Templates is actually a shared subsystem reused across windows, not an isolated module.

## Current Data and Persistence Style

The current template system does not completely isolate persistence into a unified repository layer. The more common current situation is:

- Specific templates directly use SqlSugar
- Directly read and write `ModMasterModel`, `ModDetailModel`
- Sometimes also link `SysResourceModel` or other resource tables

Therefore, it is closer to a "template objects know how to write to and read from the database themselves" design, rather than a strictly layered data access architecture.

This is also why many template pages end up distorted if written as standard three-tier architecture descriptions.

## Why Flow Templates Should Be Viewed Separately

Flow templates are the most special branch within Templates because they are not just template data, but are also directly connected to flow editing and execution capabilities.

Several key points about `TemplateFlow`:

- It implements `IITemplateLoad`
- Template code is fixed as `flow`
- Double-click preview directly opens `FlowEngineToolWindow`
- Supports `.stn` and `.cvflow` import
- Importing flow packages will link and update template references in flows

This shows that Flow is both a template and a flow graph carrier, and cannot be treated exactly like ordinary parameter templates.

## The Most Obvious Characteristics of This Design

### Strong Runtime Orientation

Whether templates can appear, be edited, or be selected in nodes is strongly tied to the runtime loading chain.

### UI and Template Logic Are Very Close

Template abstractions, editing windows, creation windows, and node configurators are not deliberately isolated into distant layers.

### Business Groups Coexist, Not Variants Under a Unified Model

Directories like `ARVR/`, `POI/`, `Jsons/`, `Flow/` clearly carry their own evolutionary histories. It is more appropriate to currently understand them as business template families sharing a common template infrastructure, rather than a single tidy product line.

## Recommended Reading Order for Code

To quickly build real understanding, the recommended order is:

1. First read `ITemplate.cs`.
2. Then read `TemplateContorl.cs` and `TemplateInitializer`.
3. Then read `TemplateManagerWindow.xaml.cs`.
4. Then enter `TemplateFlow.cs` or a specific business template.
5. When needing to understand how flow nodes call templates, read `NodeConfigurator/NodePanelBuilder.cs`.

## What This Page No Longer Does

This page no longer maintains these high-risk contents:

- Static statistics based on file counts and code volume
- Three-tier architecture diagrams that look complete but cannot be mapped item-by-item to the existing implementation
- Unified behavior promises generalized to all template families

If refactoring suggestions need to be written later, they should be in a separate design proposal page rather than mixed in here.

## Continue Reading

- [Templates Module Analysis](./analysis.md)
- [FlowEngineLib Architecture](../engine/flow-engine.md)
- [Component Interactions](../../overview/component-interactions.md)