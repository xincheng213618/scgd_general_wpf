using System;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    [Obsolete("Use FlowMessageTraceWindow. This compatibility type will be removed in a future version.")]
    public class FlowMessageListWindow : FlowMessageTraceWindow
    {
        public FlowMessageListWindow()
        {
        }

        public FlowMessageListWindow(string? nodeId, string? nodeName)
            : base(nodeId, nodeName)
        {
        }
    }
}
