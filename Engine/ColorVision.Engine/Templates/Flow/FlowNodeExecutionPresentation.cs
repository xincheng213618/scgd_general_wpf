using System;

namespace ColorVision.Engine.Templates.Flow
{
    internal enum FlowNodeExecutionState
    {
        NotStarted,
        Running,
        Complete
    }

    internal readonly record struct FlowNodeExecutionPresentation(FlowNodeExecutionState State, long? ElapsedMs)
    {
        internal static FlowNodeExecutionPresentation FromRecord(FlowNodeRecord? record, DateTime now)
        {
            if (record == null)
                return new FlowNodeExecutionPresentation(FlowNodeExecutionState.NotStarted, null);

            if (record.EndTime.HasValue)
                return new FlowNodeExecutionPresentation(FlowNodeExecutionState.Complete, Math.Max(0, record.ElapsedMs));

            long elapsedMs = Math.Max(0, (long)(now - record.StartTime).TotalMilliseconds);
            return new FlowNodeExecutionPresentation(FlowNodeExecutionState.Running, elapsedMs);
        }
    }
}
