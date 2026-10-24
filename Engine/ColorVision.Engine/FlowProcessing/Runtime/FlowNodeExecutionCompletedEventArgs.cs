using System;

namespace ColorVision.Engine.FlowProcessing;

/// <summary>A node completion accepted by the active flow session. Observers must not block execution.</summary>
public sealed class FlowNodeExecutionCompletedEventArgs(int batchId, string serialNumber, string nodeId, string nodeName, int? statusCode) : EventArgs
{
    public int BatchId { get; } = batchId;
    public string SerialNumber { get; } = serialNumber;
    public string NodeId { get; } = nodeId;
    public string NodeName { get; } = nodeName;
    public int? StatusCode { get; } = statusCode;
}
