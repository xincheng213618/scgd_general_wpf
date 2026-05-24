# FlowEngineLib Architecture

This page only describes the flow editing and execution chain actually running in the current repository, no longer continuing to maintain old drafts that described FlowEngineLib as an independent layered framework.

## First, See Its Position in the System

FlowEngine-related capabilities do not only exist in `Engine/FlowEngineLib/`. The current actual usage chain spans two layers:

- `FlowEngineLib/` provides execution control, start nodes, end nodes, and service node base capabilities in the node editor
- `Engine/ColorVision.Engine/Templates/Flow/` is responsible for connecting flow templates, editing windows, runtime windows, and batch processing to the main application

Therefore, when discussing the FlowEngine architecture, writing only about the library would miss half of the actual runtime.

## The Most Critical Objects Currently

### `FlowEngineControl`

`FlowEngineControl` is the central object for execution control. It currently handles:

- Binding `STNodeEditor`
- Identifying start nodes and service nodes when nodes are added to the editor
- Maintaining `startNodeNames` and `services`
- Loading canvases from files or Base64
- Selecting start nodes and starting/stopping flows
- Throwing `Finished` outwards when a start node completes

This means it is not an abstract scheduling interface, but a runtime controller directly bound to the node editor instance, node objects, and service collection.

### `BaseStartNode`

The start node encapsulates one flow execution as `CVStartCFC` and dispatches start, stop, and completion events along the flow graph.

Current key points:

- `Start(serialNumber)` creates `CVStartCFC`
- `DoDispatch(...)` passes actions downstream
- `FireFinished(...)` is where the flow completion event is actually emitted

Therefore, "flow completion" is not inferred by the controller itself, but ultimately emitted by the start node.

### `CVBaseServerNode`

Most device nodes and algorithm nodes fall under the `CVBaseServerNode` system. They handle:

- Sending and waiting for runtime actions
- Handling timeouts, failures, and return data
- Passing node results onward to downstream
- Reporting individual node end status via `nodeEndEvent`

The `nodeEndEvent` here is important, but it only indicates node-level completion, not that the entire flow has ended.

### `CVEndNode`

The end node is the last hop in the flow completion chain. In the current implementation, the end node calls `startAction.FireFinished()` during end processing, thereby marking the entire flow as complete.

This is why "a certain node has finished executing" and "the entire flow is finished" are two different things in the system.

## How the Flow Actually Runs

The current main chain is roughly:

1. `TemplateFlow` or `FlowEngineToolWindow` prepares flow data.
2. `FlowEngineToolWindow` loads `FlowEngineLib.dll` into the node editor.
3. `FlowEngineControl` binds `STNodeEditor`, identifying start nodes and service nodes when nodes are added.
4. `LoadFromBase64(...)` or `Load(...)` loads the flow graph into the canvas.
5. `StartNode(...)` selects the specified start node, or defaults to the first start node.
6. `BaseStartNode` creates `CVStartCFC` and dispatches to downstream nodes.
7. Each `CVBaseServerNode` derived node handles its own execution, timeout, and data passing.
8. `CVEndNode` calls `startAction.FireFinished()` on completion.
9. `BaseStartNode.Finished` is triggered.
10. `FlowEngineControl.Start_Finished(...)` converts it into its own `Finished` event.

This completion chain is stricter than the old documentation's "a node ending means the flow ends" and is closer to the current code.

## How the Engine Layer Connects It to the Main Application

### Flow Templates

`TemplateFlow` allows flow graphs to exist as templates in the system, supporting:

- Template list management
- Double-click to open flow editor directly
- `.stn` / `.cvflow` import
- Handling associated templates during flow package import

### Editing Window

`FlowEngineToolWindow` is the standalone flow editing surface. It handles:

- Hosting `STNodeEditor`
- Loading `FlowEngineLib.dll`
- Connecting undo, redo, copy, paste, zoom, and auto-alignment
- Connecting property panel and node tree via `STNodeEditorHelper`

So the current editing experience is not FlowEngineLib's built-in UI, but rather the Engine layer wrapping a WPF window to connect it.

### Runtime Window

What actually falls into daily use in the main application is the `DisplayFlow` and `FlowControl` line.

`DisplayFlow` currently handles:

- Refreshing current flow template
- Executing preprocessing before startup
- Listening for flow completion
- Writing runtime logs, batch info, and progress
- Triggering custom batch processing after flow completion

This shows that flow execution in the main application is not just "run through the graph," but also tied to batch records, log text, and post-processing extensions.

## Current Error-Prone Boundaries

### `nodeEndEvent` Is Not a Flow Completion Event

`nodeEndEvent` on `CVCommonNode` is only for node-level feedback. The true flow completion chain is:

- EndNode calls `startAction.FireFinished()`
- `CVStartCFC.FireFinished()` returns to the start node
- `BaseStartNode.Finished` is triggered
- `FlowEngineControl.Finished` is thrown outward

If these two events are conflated, failure propagation, progress updates, and final completion judgment will all be written incorrectly.

### Start Nodes Are Not Arbitrary Nodes

`FlowEngineControl` only collects `BaseStartNode` into `startNodeNames` when nodes are added. When starting a flow, if no name is specified, it defaults to the first start node.

So whether a flow can be started is directly related to whether the start node exists and is ready.

### Failure Propagation Depends on Node Type

Flow failure is not uniformly judged by the controller as a fallback. Many failures, timeouts, or cancellations are generated inside nodes and then continue to propagate along connections. Especially for multi-input nodes, failure propagation behavior depends on specific node implementations, not just the controller's surface state.

## Where Extensions Typically Land

### New Nodes

If adding new flow nodes, the focus is typically on the node implementation itself in `FlowEngineLib`, and how it connects to the start, end, or service node chain.

### New Template-Type Flows

If adding a new type of editable flow template, the focus is typically on template management adjacent to `TemplateFlow`, import/export, and editing window connection.

### Node Property Panel Extension

If adding new flow node configuration UI, it typically falls into the `STNodeEditorHelper` or `NodeConfigurator` area, rather than just modifying the node class itself.

## Recommended Reading Order

Recommended reading order:

1. `Engine/FlowEngineLib/FlowEngineControl.cs`
2. `Engine/FlowEngineLib/Start/BaseStartNode.cs`
3. `Engine/FlowEngineLib/End/CVEndNode.cs`
4. `Engine/FlowEngineLib/Base/CVBaseServerNode.cs`
5. `Engine/ColorVision.Engine/Templates/Flow/TemplateFlow.cs`
6. `Engine/ColorVision.Engine/Templates/Flow/FlowEngineToolWindow.xaml.cs`
7. `Engine/ColorVision.Engine/Templates/Flow/DisplayFlow.xaml.cs`

This way, you can first build the execution main chain, then return to editing and main application integration.

## What This Page No Longer Does

This page no longer maintains these high-risk contents:

- Describing FlowEngineLib as a standard layered framework disconnected from current implementation
- Covering all node behaviors with a set of abstract design patterns
- Wrapping MQTT, logging, serialization, and other peripherals as independent infrastructure layer promises

If future refactoring directions need to be discussed, they should start from the specific execution chain and actual node system.

## Continue Reading

- [Component Interactions](../../overview/component-interactions.md)
- [Architecture Runtime](../../overview/runtime.md)
- [Templates Architecture Design](../templates/design.md)