# FlowEngineLib

This page only describes the FlowEngineLib implementation actually available in the current repository, no longer maintaining the old "class diagram + idealized data flow + pseudo API table" draft.

## What This Module Is Now

Based on current source code status, FlowEngineLib is not an abstract flow design concept, but a runtime execution core built directly on top of a node editor. It currently handles at least four types of things:

- Hosting the flow canvas and node objects.
- Managing start nodes, service nodes, and loaded canvases.
- Adding nodes to `FlowNodeManager`'s device view.
- Closing the completion event of the entire flow between start node and end node.

Therefore, it is closer to a "node execution kernel," rather than the generic DSL platform that exists independently of the host as described in old documentation.

## Most Critical Files

- `Engine/FlowEngineLib/FlowEngineControl.cs`
- `Engine/FlowEngineLib/CVFlowContainer.cs`
- `Engine/FlowEngineLib/Base/CVCommonNode.cs`
- `Engine/FlowEngineLib/Base/CVBaseServerNode.cs`
- `Engine/FlowEngineLib/Start/BaseStartNode.cs`
- `Engine/FlowEngineLib/End/CVEndNode.cs`
- `Engine/FlowEngineLib/Algorithm/AlgorithmNode.cs`
- `Engine/FlowEngineLib/Base/CVStartCFC.cs`

If you just want to understand how flows are loaded, started, forwarded, and ended, these files already cover the main chain.

## How the Current Control Surface Is Layered

### Flow Controller

`FlowEngineControl` is the current most core runtime controller. Based on implementation, it handles:

- Attaching `STNodeEditor`
- Tracking the start node dictionary `startNodeNames`
- Tracking the service node dictionary `services`
- Caching loaded canvas `loadedCanvas`
- Triggering the flow completion event `Finished`

After nodes enter the editor, `FlowEngineControl` splits them into two categories in the `NodeAdded` event:

- `BaseStartNode` enters the start node dictionary and subscribes to `Finished`
- `CVBaseServerNode` enters the service node collection and syncs to `FlowNodeManager`

This is closer to the real implementation than the old documentation's "execute directly after loading the graph."

### Multi-Flow Container

`CVFlowContainer` is another control line adjacent to `FlowEngineControl`. It retains:

- Mapping of multiple start nodes
- `startNodesFlowMap`
- append / load / start combination capability

This shows that FlowEngineLib currently serves not just a single fixed canvas, but also considers scenarios of appending and starting flows by key.

## What the Current Node System Actually Looks Like

### `CVCommonNode`

This is the common base class for all core nodes, currently providing:

- `NodeName`
- `NodeType`
- `DeviceCode`
- `NodeID`
- `ZIndex`
- `nodeEvent`
- `nodeRunEvent`
- `nodeEndEvent`

Additionally, it unifies control creation helper methods and registers type colors to the node editor on `OnOwnerChanged()`.

### `BaseStartNode`

The start node currently handles:

- Creating `OUT_START` and multiple `OUT_LOOP` outputs
- Maintaining `Ready`, `Running`, and `startActions`
- Distributing `CVStartCFC` to the first batch of connected nodes
- Throwing `Finished` after flow completion

So flow "start" is not completed by an external controller alone, but materialized inside the start node.

### `CVBaseServerNode`

This is the current most common execution node base class. Based on implementation, it handles:

- Establishing `IN` / `OUT` and other node ports
- Maintaining template ID, template name, image filename, Token, and timeout configuration
- Assembling base request data
- Receiving server responses and continuing to pass them along the flow

The `DoServerWork` that consistently appeared in old documentation is not the extension surface that should be emphasized now; the more realistic focus points are `OnCreate()`, request parameter construction, response handling, and reset logic.

### `CVEndNode`

The end node currently does something very clear:

- Receives `CVStartCFC` or loop next-step input
- Calls `startAction.DoFinishing()`
- Finally calls `startAction.FireFinished()`

This is the true closure position of the entire flow's finished state.

### `AlgorithmNode`

`AlgorithmNode` is a very typical sample for understanding service nodes. It currently will:

- Maintain operator type, template, POI template, color, and cache length
- Create in-node editing controls in `OnCreate()`
- Package template, image, color, and SMU data into algorithm request parameters in `getBaseEventData(...)`

This again shows that the core work of current FlowEngineLib nodes is "constructing and forwarding execution parameters," not running complete algorithms locally within the node.

## How the Flow Completion Chain Closes

`CVStartCFC` is currently the key object for passing flow state between nodes. It records:

- Start and end times
- Flow status
- Serial number
- Data dictionary
- Corresponding start node

When the flow ends, `CVEndNode` calls `DoFinishing()` and `FireFinished()`, which goes back to `BaseStartNode`'s `Finished` event, and finally `FlowEngineControl` throws `FlowEngineEventArgs` externally.

If this chain is not viewed as connected, it is easy to conflate "node end" and "flow end" as the same thing.

## Current Boundary with Host Code

FlowEngineLib itself only handles the node execution kernel. What actually connects it into the ColorVision main program is the `Engine/ColorVision.Engine/Templates/Flow/` layer, for example:

- `FlowEngineManager.cs`
- `DisplayFlow.xaml.cs`
- `TemplateFlow.cs`

That layer handles:

- Refreshing flow canvas combined with MQTT RC service tokens
- Loading flow templates from Base64 into the controller
- Selecting, editing, and running flows in the UI

Therefore, if you only read FlowEngineLib without looking at the template layer, you would know "how to run" but not "who triggers it to run in the main program."

## Most Common Mistakes to Avoid

### It Is Not the Complete Code for a Host-Level Workflow System

FlowEngineLib only implements the node execution kernel. Template management, window interaction, and data loading after entering the main program are still in the `ColorVision.Engine/Templates/Flow/` layer.

### "Node Completion" Is Not Equal to "Flow Completion"

What currently makes flow completion land is the chain `CVEndNode -> CVStartCFC.FireFinished() -> BaseStartNode.Finished -> FlowEngineControl.Finished`, not any arbitrary node emitting `nodeEndEvent`.

### Service Node Extension Points Should No Longer Be Understood Through Old Documentation

The current real extension path is closer to:

- `OnCreate()`
- Parameter assembly
- Response handling
- `Reset()`

Continuing to search for a unified "local execution business function" in old documentation would distort understanding of the node model.

### `loadedCanvas` Is Not a Cosmetic Cache

Both `FlowEngineControl` and `CVFlowContainer` use canvas content hashing to avoid duplicate loading. This detail affects your understanding of "why the same flow is not repeatedly rebuilt."

## Recommended Reading Order

1. `Engine/FlowEngineLib/FlowEngineControl.cs`
2. `Engine/FlowEngineLib/Base/CVCommonNode.cs`
3. `Engine/FlowEngineLib/Start/BaseStartNode.cs`
4. `Engine/FlowEngineLib/Base/CVBaseServerNode.cs`
5. `Engine/FlowEngineLib/End/CVEndNode.cs`
6. `Engine/FlowEngineLib/Algorithm/AlgorithmNode.cs`
7. `Engine/FlowEngineLib/Base/CVStartCFC.cs`
8. `Engine/ColorVision.Engine/Templates/Flow/DisplayFlow.xaml.cs`

This allows building kernel awareness first, then connecting it to the host-side UI trigger chain.

## Continue Reading

- [docs/04-api-reference/extensions/flow-node.md](../extensions/flow-node.md)
- [docs/03-architecture/components/engine/flow-engine.md](../../03-architecture/components/engine/flow-engine.md)
- [docs/04-api-reference/engine-components/ColorVision.Engine.md](./ColorVision.Engine.md)