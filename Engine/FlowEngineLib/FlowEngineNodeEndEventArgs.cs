using System;

namespace FlowEngineLib;

public class FlowEngineNodeEndEventArgs : EventArgs
{
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
}
