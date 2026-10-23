using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Start;

/// <summary>A local start point controlled by the normal workflow Run command.</summary>
[STNode("全局", "手动开始", CategoryOrder = 0, NodeOrder = 1)]
public sealed class ManualFlowStartNode : BaseStartNode
{
    public ManualFlowStartNode() : base("手动开始") => DeviceCode = string.Empty;
}
