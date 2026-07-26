using System;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    [Obsolete("Use FlowExecutionAnalysisWindow. This compatibility type will be removed in a future version.")]
    public class FlowNodeAnalysisWindow : FlowExecutionAnalysisWindow
    {
        public FlowNodeAnalysisWindow()
        {
        }

        public FlowNodeAnalysisWindow(MeasureBatchModel batch)
            : base(batch)
        {
        }
    }
}
