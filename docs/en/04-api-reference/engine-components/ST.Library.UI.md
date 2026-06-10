# ST.Library.UI

This page only describes the `ST.Library.UI` module actually available in the current repository, no longer maintaining the old "complete UI platform manual + massive examples + unified extension framework" draft.

## What This Module Is Now

Based on current source code status, `ST.Library.UI` is a relatively low-level WinForms node editor library. Its clearest role is not as an independent application shell, but providing for Flow-related functionality:

- Node canvas and interactive editor
- Node base class and port connection model
- Property editing panel
- Node tree and node panel composite control

Therefore, it is closer to "node editor infrastructure" rather than the ColorVision business layer itself.

## Most Critical Files

- `Engine/ST.Library.UI/NodeEditor/STNodeEditor.cs`
- `Engine/ST.Library.UI/NodeEditor/STNode.cs`
- `Engine/ST.Library.UI/NodeEditor/STNodeOption.cs`
- `Engine/ST.Library.UI/NodeEditor/STNodePropertyGrid.cs`
- `Engine/ST.Library.UI/NodeEditor/STNodeTreeView.cs`
- `Engine/ST.Library.UI/NodeEditor/STNodeEditorPannel.cs`
- `Engine/ST.Library.UI/FrmSTNodePropertyInput.cs`

If you just want to understand what this library actually does in the current repository, these files already cover the main body.

## How the Current Control Surface Is Partitioned

### Canvas Control

`STNodeEditor` is the central control of the entire library. Based on current implementation, it handles:

- Maintaining `Nodes`
- Maintaining canvas offset and zoom
- Managing node selection, hover, and active states
- Handling node connections, disconnections, and canvas interaction
- Triggering node and canvas related events

This shows that the control logic of the current node editor is concentrated in a single WinForms `Control`, rather than split into a bunch of independent MVVM services.

### Node Object Model

`STNode` is the common base class for all current nodes, handling:

- Title, dimensions, position
- Input and output option collections
- Node embedded control collections
- Selected state and active state
- Auto-sizing and repainting

And `STNodeOption` takes on the port model, currently providing:

- Port text and data type
- Single/multi-connection limits
- Connection count and connected port collections
- Connection, disconnection, and data transfer events

Therefore, the foundational mental model of this library is not "nodes are just a graph," but a composite object of "node + ports + controls + events."

### Property Panel

`STNodePropertyGrid` is currently a control specifically designed for node properties, not a direct reuse of the .NET standard PropertyGrid. It revolves around the current `STNode`:

- Reading property descriptors
- Rendering items, descriptions, and error areas
- Highlighting based on node title color or custom colors
- Handling read-only and edit mode switching

`FrmSTNodePropertyInput` is the companion lightweight input form for editing individual property values.

### Node Tree and Composite Panel

`STNodeTreeView` currently handles:

- Organizing the node type tree
- Maintaining search and grouped display
- Linking with the editor and property panel

`STNodeEditorPannel` combines:

- `STNodeEditor`
- `STNodeTreeView`
- `STNodePropertyGrid`

into a directly usable integrated panel, supplemented with split lines, zoom hints, and connection status hints.

This shows that `ST.Library.UI` currently does not just provide a single editor control, but also a fairly complete set of composite host panels.

## Current Relationship with ColorVision

In this repository, `ST.Library.UI` is more used as infrastructure by `FlowEngineLib` and its host layer. The current business layer typically:

- Inherits `STNode` to create its own node types
- Uses `STNodeEditor` as the flow canvas
- Uses `STNodePropertyGrid` to expose node properties
- Uses `STNodeTreeView` to manage node categories and drag-and-drop creation

So documentation should not write it as a "flow system" at the same layer as business logic — it is the UI foundation library beneath the flow system.

## Handoff Acceptance

When taking over this module, the goal is not to validate a specific business flow, but to confirm that node-editor infrastructure still works:

| Check | Where to Look | Passing Standard |
| --- | --- | --- |
| Node type loading | `STNodeTypeRegistry`, `CVNodeContainer.LoadAssembly(...)` | Current and external node assemblies can be registered, and the node tree shows the expected types |
| Canvas loading | `STNodeEditor.LoadCanvas(...)`, `CVNodeContainer.LoadCanvas(...)` | File, byte[], and Stream entry points restore nodes, positions, properties, and connections |
| Missing-type handling | `CVNodeContainer.LoadCanvas(...)` | Missing node types or assemblies produce clear exceptions/prompts instead of silently creating a broken canvas |
| Node editing | `STNodeEditor`, `STNode` | Add, move, select, delete, active-state switching, and repainting work correctly |
| Port connections | `STNodeOption`, `OptionConnecting`, `OptionConnected`, `OptionDisConnecting`, `OptionDisConnected` | Connection limits, disconnects, event order, and data transfer remain correct |
| Property editing | `STNodePropertyGrid`, `FrmSTNodePropertyInput`, `FrmSTNodePropertySelect` | Text, enum, boolean, and read-only properties edit or disable according to node definitions |
| Canvas interaction | `CanvasMoved`, `CanvasScaled` | Canvas dragging, zooming, zoom hints, and connection status hints work |
| WPF host embedding | Upper-level Flow editor windows | Mouse, keyboard, focus, and zoom remain usable after embedding WinForms controls inside WPF |

## Change Boundary

| Change Type | Should This Module Change | Notes |
| --- | --- | --- |
| Node canvas, port connection, node tree, or node property panel interaction changes | Yes | This is the foundation responsibility of `ST.Library.UI` |
| Business execution logic of flow nodes changes | Usually no | Start with `FlowEngineLib`, `NodeConfigurator`, Engine templates, or project-specific nodes |
| Outer WPF page layout changes | Usually no | Start with the UI host page; this module should only handle behavior inside the WinForms controls |
| Node configuration save format changes | Maybe | If `LoadCanvas(...)` and connection restoration are affected, update this module; if only business fields change, start in upper-level nodes |
| New business node types are added | Usually no | Implement and register them in upper-level node assemblies instead of placing customer business logic in the foundation UI library |

## First Checks

| Symptom | First Check |
| --- | --- |
| Nodes disappear after opening a canvas | Check whether `STNodeTypeRegistry` loaded the corresponding assembly and whether `CVNodeContainer` reports missing types |
| Connections are not restored | Check saved input/output port keys, `LoadCanvas(...)` connection restore order, and whether events are intercepted |
| Property panel edits do not affect the node | Check `STNodePropertyGrid` property descriptors, read-only markers, and input-form writeback |
| Mouse or keyboard behaves oddly inside WPF | Check WinFormsHost embedding, focus transfer, and shortcut handling before changing business nodes |
| Dragging or zooming is slow | Start with `STNodeEditor` repainting, cached images, node count, and connection count |

## Most Common Mistakes to Avoid

### It Is a WinForms Library, Not a WPF Flow Framework

Although the upper-level main program heavily uses WPF, the current core controls of `ST.Library.UI` are still WinForms `Control`. This boundary is important for understanding host embedding approaches.

### This Library Provides More Than Just an Editor Control

Beyond `STNodeEditor`, there are currently node object model, port model, property grid, node tree, and composite panel. Abbreviating it as "a canvas control" would underestimate the actual scope.

### Property Editing Is a Custom Implementation, Not Directly Using System PropertyGrid

`STNodePropertyGrid` and `FrmSTNodePropertyInput` are the library's own node property editing chain. Continuing to describe it as a generic reflection panel as in old documentation would blur the current dedicated implementation.

### It Is Primarily Consumed by the Upper-Level Node System

The current real usage is that the upper layer defines node types and then hands them to the editor, tree, and property panel here for hosting, rather than directly writing business node logic inside `ST.Library.UI`.

## Recommended Reading Order

1. `Engine/ST.Library.UI/NodeEditor/STNodeEditor.cs`
2. `Engine/ST.Library.UI/NodeEditor/STNode.cs`
3. `Engine/ST.Library.UI/NodeEditor/STNodeOption.cs`
4. `Engine/ST.Library.UI/NodeEditor/STNodePropertyGrid.cs`
5. `Engine/ST.Library.UI/NodeEditor/STNodeTreeView.cs`
6. `Engine/ST.Library.UI/NodeEditor/STNodeEditorPannel.cs`

This allows building the canvas and node model first, then understanding how the property panel and node library are attached.

## Continue Reading

- [docs/04-api-reference/engine-components/FlowEngineLib.md](./FlowEngineLib.md)
- [docs/04-api-reference/extensions/flow-node.md](../extensions/flow-node.md)
- [docs/03-architecture/components/engine/flow-engine.md](../../03-architecture/components/engine/flow-engine.md)
