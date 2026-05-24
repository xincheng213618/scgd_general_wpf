# Templates Module Analysis

This page only analyzes the current status of the Templates system in the repository, no longer retaining now-invalid summary content like "how many documents were supplemented" or "how many optimization plans were planned."

## First, Understand What This Module Actually Is

`Engine/ColorVision.Engine/Templates/` is not a single algorithm directory, but a set of code that simultaneously handles these types of responsibilities:

- Template abstraction and registration
- Template management and editing windows
- Template implementations for different business domains
- Flow template-related capabilities
- Template search, creation, sample saving, and other auxiliary tools

Therefore, it contains both pure model code and obvious WPF window and editor code — do not mistakenly classify it as "only algorithm parameter definitions" when reading.

## The Most Worthwhile Files to Get to Know First

### Core Entry Points

- `ITemplate.cs`: Main entry for template abstraction capabilities
- `ModelBase.cs`, `ParamModBase.cs`: Base types for template models and parameter models
- `TemplateContorl.cs`: Template registry center and loading entry

### Management and Editing UI

- `TemplateManagerWindow.xaml(.cs)`: Template management window
- `TemplateEditorWindow.xaml(.cs)`: Template editing window
- `TemplateCreate.xaml(.cs)`: Template creation entry
- `TemplateSettingEdit.xaml(.cs)`: Template configuration editing entry

### Search and Samples

- `TemplateSearchProvider.cs`
- `TemplateSampleLibrary.cs`
- `TemplateSampleSaveWindow.xaml(.cs)`

If you just want to understand "how templates are discovered, how they are opened, how they are edited," looking at these files first is usually more effective than diving directly into a specific algorithm subdirectory.

## How the Current Directory Is Divided

From the repository's current state, this directory can be divided into at least several categories:

### 1. Core Framework Layer

This part handles template abstraction, registration, base models, and common UI.

Typical files include:

- `ITemplate.cs`
- `ModelBase.cs`
- `ParamModBase.cs`
- `TemplateContorl.cs`
- `TemplatesExtension.cs`

### 2. Flow Template Layer

Under `Flow/` handles flow templates and flow editing/running related capabilities. It exists in the same large system as general algorithm templates but has distinctly different usage scenarios.

### 3. Business Template Families

Multiple groups of business template directories can still be directly seen in the current repository, for example:

- `ARVR/`
- `POI/`
- `Compliance/`
- `JND/`
- `Matching/`
- `FindLightArea/`
- `FocusPoints/`
- `ImageCropping/`
- `LedCheck/`
- `LEDStripDetection/`
- `Validate/`
- `DataLoad/`

These directories are not entirely divided by uniform rules. Some are grouped by algorithm domain, some by processing stage, and some are closer to functional packages left from historical evolution.

### 4. JSON Template Family

`Jsons/` is one of the areas most easily misread currently. It typically handles a batch of template implementations centered on JSON configuration, coexisting with traditional directory-style templates.

If you see template implementations with similar names but different directories, do not first assume they are duplicate code — they are more likely different historical versions, configuration approaches, or business integration methods.

## How Templates Are Connected to the System at Runtime

The most important runtime chain for the current template system is very direct:

1. The main application and plugins first load assemblies.
2. `TemplateContorl` scans currently loaded assemblies after the database connection becomes available.
3. It looks for non-abstract types that implement `IITemplateLoad`.
4. These types register templates into `ITemplateNames` via `Load()`.
5. Template management windows, editing windows, flow windows, and business functions then consume these registered templates.

This chain indicates two practical constraints:

- Templates are not hand-declared in a static master list.
- Whether templates appear is jointly affected by assembly loading and database availability state.

## Most Common Misconceptions When Reading This Module

### Misconception 1: Treating It as a Pure Algorithm Layer

This simultaneously contains windows, editors, search, sample saving, template creation, and flow-related UI — it is not a simple algorithm parameter definition repository.

### Misconception 2: Assuming All Directories Were Designed in the Same Period by the Same Rules

Not so. The current directory structure has clear evolutionary traces; do not expect it to naturally satisfy a completely consistent layered model.

### Misconception 3: Checking the Editor First When Templates Are Missing

Many template problems occur earlier in the registration chain: assemblies not loaded, database not connected, `IITemplateLoad` not executed, or template names duplicated and overwritten.

## If You Want to Continue Reading Now

Recommended order:

1. First look at `TemplateContorl.cs` to understand how templates are discovered and registered.
2. Then look at `ITemplate.cs`, `ModelBase.cs`, `ParamModBase.cs` to understand what the template objects themselves look like.
3. Then look at `TemplateManagerWindow` and `TemplateEditorWindow` to understand how users operate templates.
4. Finally, enter specific business directories like `ARVR/`, `POI/`, or `Flow/`.

This is easier for building an overall understanding than diving into a specific template subdirectory from the start.

## What This Page No Longer Does

This page no longer maintains these contents:

- Document completion achievement statistics
- Overly specific but unimplemented refactoring roadmaps
- Unified layered models disconnected from current repository state

If a real refactoring proposal needs to be made later, it should be separately argued in an independent design page rather than mixed into "status analysis."

## Continue Reading

- [Templates Architecture Design](./design.md)
- [Component Interactions](../../overview/component-interactions.md)
- [Architecture Runtime](../../overview/runtime.md)