using System;
using FlowEngineLib.Runtime;

namespace FlowEngineLib;

public class FlowEngineNodeEndEventArgs : EventArgs
{
    public string SerialNumber { get; set; }

    /// <summary>
    /// 接收的MQTT响应Topic
    /// </summary>
    public string RecvTopic { get; set; }

    /// <summary>
    /// 接收的MQTT消息ID
    /// </summary>
    public string RecvMsgId { get; set; }

    /// <summary>
    /// 接收的MQTT EventName
    /// </summary>
    public string RecvEventName { get; set; }

    /// <summary>
    /// 响应状态码 (0=Finish, 102=Pending, other=Failed)
    /// </summary>
    public int? RecvStatusCode { get; set; }

    /// <summary>
    /// 响应消息
    /// </summary>
    public string RecvStatusMessage { get; set; }

    /// <summary>
    /// 接收的MQTT响应内容(JSON)
    /// </summary>
    public string RecvPayload { get; set; }

    /// <summary>
    /// Runtime failure category, when this completion represents a failure.
    /// </summary>
    public FlowFailureKind? FailureKind { get; set; }

    /// <summary>
    /// True when a runtime-only error route accepted the failure.
    /// </summary>
    public bool FailureHandled { get; set; }

    public string FailureRouteTargetNodeId { get; set; }

    public bool WillRetry { get; set; }

    public int AttemptNumber { get; set; } = 1;

    public int MaxAttempts { get; set; } = 1;

    public int RetryDelayMs { get; set; }
}
