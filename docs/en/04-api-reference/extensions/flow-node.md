# FlowEngineLib Node Extensions

This page only describes the Flow node extension paths actually available in the current repository, no longer maintaining the old "development guide" based on illustrative APIs.

## What the Node System Actually Looks Like

From the current code, Flow node extensions primarily revolve around these categories of base classes:

- `CVCommonNode`: Common base class for all nodes, providing public capabilities such as `NodeName`, `NodeType`, `DeviceCode`, `NodeID`, `ZIndex`, and `nodeEvent` / `nodeRunEvent` / `nodeEndEvent`.
- `BaseStartNode`: Flow start node, responsible for creating `CVStartCFC`, maintaining running `startActions`, and throwing `Finished` when the flow completes.
- `CVBaseServerNode`: The most common base class for service/algorithm type nodes, responsible for input/output, MQTT request assembly, timeout handling, and node-level completion callback.
- `CVEndNode`: Flow end node, ultimately calling `startAction.FireFinished()` to mark the entire flow as complete.

This means current node extensions are not a lightweight plugin model of "just implement the interface," but are directly built upon `STNode` and a set of concrete base classes.

## Code Anchors Most Worth Looking at First

If you need to add or understand a node, prioritize these files:

- `Engine/FlowEngineLib/Base/CVCommonNode.cs`
- `Engine/FlowEngineLib/Base/CVBaseServerNode.cs`
- `Engine/FlowEngineLib/Start/BaseStartNode.cs`
- `Engine/FlowEngineLib/End/CVEndNode.cs`
- `Engine/FlowEngineLib/Algorithm/AlgorithmNode.cs`

Among them, `AlgorithmNode` is a very typical real-world example: it does not directly compute images inside the node, but collects parameters such as template, color, image path, then assembles the request data actually sent to the server.

## How Service Nodes Are Typically Extended

From the implementation of `CVBaseServerNode`, the most common extension approach is currently:

1. Inherit `CVBaseServerNode`.
2. In the constructor, determine the title, `NodeType`, service name, and device code, and set node behavior fields such as `operatorCode`.
3. Add inputs/outputs or editing controls in `OnCreate()`.
4. Assemble the parameter object actually sent to the execution side by overriding `getBaseEventData(CVStartCFC start)`.
5. Override `OnServerResponse(...)`, `Reset(...)`, or connection-related virtual methods as needed to supplement response handling and cleanup logic.

The old documentation's statement that "override `DoServerWork` to complete node development" is not consistent with the current real implementation of `CVBaseServerNode`.

## A Skeleton Closer to Current Reality

```csharp
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using ST.Library.UI.NodeEditor;

[STNode("/Custom/MyNode")]
public class MyNode : CVBaseServerNode
{
    public MyNode()
        : base("Custom Node", "Algorithm", "SVR.Custom", "DEV.Custom")
    {
        operatorCode = "CustomEvent";
    }

    protected override void OnCreate()
    {
        base.OnCreate();
        CreateTempControl(m_custom_item);
    }

    protected override object getBaseEventData(CVStartCFC start)
    {
        var param = new AlgorithmParam();
        BuildTemp(param);
        BuildImageParam(param);
        return param;
    }

    protected override void OnServerResponse(CVServerResponse resp, CVStartCFC startCFC)
    {
        base.OnServerResponse(resp, startCFC);
        // Handle returned data as needed
    }
}
```

This skeleton is closer to the current code: the core of a node is typically "how to build request data and integrate into the existing execution chain," rather than completing the entire business computation within the node itself.

## What Start Nodes and End Nodes Each Control

### `BaseStartNode`

The start node currently handles:

- Creating and saving `CVStartCFC`
- Distributing start actions via `m_op_start` and multiple `m_op_loop`
- Managing `Ready`, `Running`, and in-progress `startActions`
- Throwing `Finished` externally after the flow truly completes

Therefore, if you are extending a flow entry node, your focus will not be template parameters, but start, loop output, and flow state management.

### `CVEndNode`

The end node currently handles:

- Receiving `CVStartCFC` or loop continuation actions
- Calling `startAction.DoFinishing()` in `DoNodeEnded(...)`
- Finally calling `startAction.FireFinished()`

This is also the true exit point for "entire flow finished" in the current code.

## Most Common Mistakes to Avoid

### `nodeEndEvent` Is Not Equal to Flow Completion

`CVCommonNode.nodeEndEvent` only represents node-level end feedback. The entire flow completion must reach `CVEndNode`, then be triggered by `startAction.FireFinished()`.

### Do Not Design New Nodes Around the Non-Existent `DoServerWork`

The current real extension points of `CVBaseServerNode` are closer to:

- `OnCreate()`
- `getBaseEventData(...)`
- `OnServerResponse(...)`
- `Reset(...)`

If you search for `DoServerWork` following old documentation, you will directly misunderstand the extension path.

### Node and Service Topics Are Not Auto-Inferred Universal Matches

`CVBaseServerNode` currently cooperates with the message chain through `GetSendTopic()`, `GetRecvTopic()`, `operatorCode`, and `FlowServiceManager`. If these fields do not match the server-side conventions, the node will appear as timeout or receive no response.

### Category Paths Have No Single Fixed Convention

The current `[STNode("...")]` path strings are part of the actual tree structure, but existing nodes in the repository already mix styles like `/00 Global`, `/03_2 Algorithm`, etc. When extending, you should follow the existing grouping of neighboring nodes rather than copying the assumed category table from old documentation.

## Recommended Reading Order

1. `CVCommonNode`: First understand common properties, events, and control helper methods.
2. `CVBaseServerNode`: Then see how typical service nodes initiate requests, wait for responses, and handle timeouts.
3. `BaseStartNode`: Understand flow startup, loop output, and `Finished` event source.
4. `CVEndNode`: Confirm where the flow completion chain closes.
5. `AlgorithmNode` or other neighboring real nodes: Finally, extend by following existing nodes rather than starting from old tutorial templates.

## Continue Reading

- [FlowEngineLib Architecture](../../03-architecture/components/engine/flow-engine.md)
- [Engine Components Overview](../engine-components/README.md)
- [Algorithm System Overview](../algorithms/overview.md)