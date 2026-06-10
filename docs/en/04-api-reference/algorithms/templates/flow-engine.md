# Flow Engine

This page only describes the real responsibilities of the `Engine/ColorVision.Engine/Templates/Flow` layer in the current repository, no longer maintaining the old draft that "mixes the entire Flow execution kernel, host bridge, and node library into one page."

## What This Page Now Covers

What this page covers is not the `FlowEngineLib` execution kernel itself, but the host layer surrounding flow templates in the main program. Key focuses include:

- How flow templates are loaded from database and resource tables.
- How the editing window opens after double-clicking a flow template.
- How the editing window hosts `STNodeEditor`, property panel, and node tree.
- How the host layer connects devices, templates, and node configurators into the flow editor.

To see node execution semantics and node base classes, please go to [FlowEngineLib](../../engine-components/FlowEngineLib.md).

## Most Critical Files

- `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
- `Engine/ColorVision.Engine/Templates/Flow/FlowEngineToolWindow.xaml.cs`
- `Engine/ColorVision.Engine/Templates/Flow/STNodeEditorHelper.cs`
- `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/*.cs`

These files together determine how flow templates in the main program are edited, saved, and configured.

## How the Current Main Chain Runs

### Flow Template Entry Point

`MenuTemplateFlow` opens `TemplateEditorWindow(new TemplateFlow())`. `TemplateFlow` itself is a concrete implementation of `ITemplate<FlowParam>`, currently responsible for:

- Reading flow template master table from MySQL
- Retrieving node graph content from `SysResourceModel.Value` as Base64
- Wrapping it into `FlowParam`
- Managing save, delete, import, export, and create

Therefore, current flow templates are not simple disk file lists, but a combination of "database master records + resource table binary content."

### Editing Window After Double-Click

`TemplateFlow.PreviewMouseDoubleClick(...)` directly opens `FlowEngineToolWindow`. This shows that flow templates differ from many regular templates:

- The list window is only an entry point
- Real flow editing happens in a separate window

Inside the window, `STNodeEditorHelper` hosts the node canvas, property panel, node tree, clipboard, and context menu.

### Editor Helper Layer

`STNodeEditorHelper` currently handles many things, far beyond "helping adjust the node tree":

- Compressed serialization for node copy and paste
- Syncing currently selected node with property panel
- Node tree initialization and assembly
- Context menu, delete, select-all, and other commands
- Validity checks and auto-layout
- Host attachment of device and template selection panels

This means that a large amount of flow editing window interaction logic is concentrated in this helper, rather than scattered across individual node controls.

### Node Configurator Bridge

The `NodeConfigurator` directory is currently an important bridge layer between the main program and node library. It connects:

- Device service lists
- Local image path inputs
- Regular template selectors
- JSON template selectors

into node property panels.

For example, POI-related configurators connect `TemplatePoi`, `TemplatePoiFilterParam`, `TemplatePoiReviseParam`, `TemplatePoiOutputParam`, and other templates back to flow nodes. In other words, a node's editable experience in the host is not entirely determined by `FlowEngineLib`.

## Current Storage and Export Boundaries

### Primary Storage Is Still Database

`TemplateFlow.Load()` and `Save2DB(...)` both currently revolve around MySQL master tables, detail tables, and `SysResourceModel`. Base64 node graph content is stored in the resource table and linked back through detail records.

### Export Is Not Just One Format

Current flow template export has at least two actual forms:

- `.stn`: Raw node graph file
- `.cvflow`: Flow package with associated template information

Therefore, simply writing flow templates as "just a node graph file" would miss the current package export capability.

### Saving Has Database and Local-File Paths

`FlowEngineToolWindow.Save()` currently has two paths:

| Scenario | Behavior | Handoff focus |
| --- | --- | --- |
| Local `.stn` file is open | `SaveToFile(FileFlow)` writes the canvas back to that file | It does not update database templates or resource rows. |
| A `FlowParam` template is open | `CheckFlow()`, `GetCanvasData()`, Base64, then `TemplateFlow.Save2DB(...)` | Updates `ModMasterModel.Name`, `SysResourceModel.Value`, and detail resource references. |

`TemplateFlow.Save2DB(...)` stores canvas data in `SysResourceModel` with `Type = 101`, while `ModDetailModel.ValueA` stores the resource id. If a flow appears to save but reopens with old content, check `DataBase64`, `ValueA`, and the resource table value first.

### .cvflow Package Structure

`.cvflow` is a ZIP package, not only a renamed `.stn` file.

| Entry | Role |
| --- | --- |
| `flow.stn` | Binary node-canvas data |
| `manifest.json` | `FlowPackageManifest` with flow name, version, and related templates |

`FlowPackageHelper.CollectTemplatesForExport(...)` scans template-reference properties such as `TempName`, `POITempName`, `SavePOITempName`, `OutputTemplateName`, and `ModelName`. Import then creates related templates, resolves duplicate names, and rewrites template names in STN when needed.

Multi-select export still creates a zip of `.stn` files and does not collect a manifest like single `.cvflow` export. For field migration, validate the single-flow `.cvflow` path first.

## Runtime And Scheduling Chain

| Entry | Current chain | Handoff focus |
| --- | --- | --- |
| Manual UI run | `DisplayFlow.RunFlow()` -> `FlowControl.Start(sn)` | Creates `MeasureBatchModel` and binds `FlowCompleted`. |
| Awaitable run | `DisplayFlow.RunFlowAndWaitAsync()` | Used by scheduling and automation to wait for flow completion. |
| Quartz job | `FlowJob.Execute(...)` -> dispatcher -> `RunFlowAndWaitAsync()` | Switches back to the WPF UI thread before starting the flow. |
| Stop | `DisplayFlow.StopFlow()` -> `FlowControl.Stop()` | Updates the current batch to `Canceled`. |

When `FlowCompleted` fires, the current batch receives `FlowStatus`, `TotalTime`, and `Result`, then project-side processing continues. If a flow completes but the project package has no result, trace `FlowCompleted`, batch update, and project `Processing` in order.

## Most Common Mistakes to Avoid

### This Page Is Not a FlowEngineLib Duplicate

`FlowEngineLib` handles node execution and base class system; this page's layer handles template management, window editing, and host bridging in the main program. Both layers are called "Flow Engine," but have different boundaries.

### Flow Templates Are Not Pure Disk Assets

The current primary path is still database + resource tables, not scanning `.stn` files in a directory. Import/export is only an additional capability.

### Node Property Editing Heavily Depends on Host Code

What actually connects device dropdowns, template dropdowns, and JSON template dropdowns into node property areas is the `NodeConfigurator` and `STNodeEditorHelper` layer, not just the node classes themselves.

### Window Behavior Differs from Regular Template Editors

Regular templates are mostly edited on the right side of `TemplateEditorWindow`; flow templates currently follow a "list window + separate flow editor window" path. Continuing to use the narrative of regular templates would mislead readers.

### Import/Export Has Two Compatibility Paths

`.stn` contains only the node graph; `.cvflow` contains a manifest and related templates. Multi-select zip export is still closer to the old `.stn` semantics. Always confirm the actual format before using a flow package for backup, migration, or handoff.

## Acceptance Checklist

| Scenario | Required check |
| --- | --- |
| Save flow | Add nodes, choose templates, save, close, reopen, and confirm parameters remain |
| Export one flow | Export `.cvflow` and confirm `flow.stn` and `manifest.json` exist |
| Import one flow | Import into an environment with duplicate template names and confirm references are rewritten |
| Export multiple flows | Confirm the zip contains multiple `.stn` files and no related-template manifest |
| Scheduled run | `FlowJob` starts the flow and returns `FlowJobResult` in `context.Result` |
| Project handoff | Batch status, elapsed time, result string, and project processing are all traceable |

## Recommended Reading Order

1. `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
2. `Engine/ColorVision.Engine/Templates/Flow/FlowEngineToolWindow.xaml.cs`
3. `Engine/ColorVision.Engine/Templates/Flow/STNodeEditorHelper.cs`
4. `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator/POINodeConfigurators.cs`
5. Other configurators under `Engine/ColorVision.Engine/Templates/Flow/NodeConfigurator`

## Continue Reading

- [FlowEngineLib](../../engine-components/FlowEngineLib.md)
- [Flow Node Extensions](../../extensions/flow-node.md)
- [ColorVision.Engine](../../engine-components/ColorVision.Engine.md)
