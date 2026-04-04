using System;

namespace FlowEngineLib;

public class FlowEngineNodeRunEventArgs : EventArgs
{
    /// <summary>
    /// 发送的MQTT消息Topic
    /// </summary>
    public string SendTopic { get; set; }

    /// <summary>
    /// 发送的MQTT消息ID
    /// </summary>
    public string SendMsgId { get; set; }

    /// <summary>
    /// 发送的MQTT EventName
    /// </summary>
    public string SendEventName { get; set; }

    /// <summary>
    /// 发送的MQTT消息内容(JSON)
    /// </summary>
    public string SendPayload { get; set; }
}
