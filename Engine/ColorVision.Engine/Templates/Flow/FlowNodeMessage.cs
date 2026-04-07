using SqlSugar;
using System;

namespace ColorVision.Engine.Templates.Flow
{
    /// <summary>
    /// 消息状态
    /// </summary>
    public enum FlowMessageState
    {
        Initial = 0,
        Sended = 1,
        Success = 2,
        Fail = 3,
        Timeout = 4
    }

    /// <summary>
    /// 流程节点MQTT消息记录 - 一条记录包含发送和接收
    /// </summary>
    [SugarTable("FlowNodeMessage")]
    public class FlowNodeMessage
    {
        [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "batch_id")]
        public int BatchId { get; set; }

        [SugarColumn(ColumnName = "serial_number", IsNullable = true)]
        public string SerialNumber { get; set; }

        [SugarColumn(ColumnName = "node_id", IsNullable = true)]
        public string NodeId { get; set; }

        [SugarColumn(ColumnName = "node_name", IsNullable = true)]
        public string NodeName { get; set; }

        [SugarColumn(ColumnName = "msg_id", IsNullable = true)]
        public string MsgId { get; set; }

        [SugarColumn(ColumnName = "event_name", IsNullable = true)]
        public string EventName { get; set; }

        [SugarColumn(ColumnName = "send_topic", IsNullable = true)]
        public string SendTopic { get; set; }

        [SugarColumn(ColumnName = "send_payload", IsNullable = true, Length = 8000)]
        public string SendPayload { get; set; }

        [SugarColumn(ColumnName = "send_time")]
        public DateTime SendTime { get; set; }

        [SugarColumn(ColumnName = "recv_topic", IsNullable = true)]
        public string RecvTopic { get; set; }

        [SugarColumn(ColumnName = "recv_payload", IsNullable = true, Length = 8000)]
        public string RecvPayload { get; set; }

        [SugarColumn(ColumnName = "recv_time", IsNullable = true)]
        public DateTime? RecvTime { get; set; }

        [SugarColumn(ColumnName = "status_code", IsNullable = true)]
        public int? StatusCode { get; set; }

        [SugarColumn(ColumnName = "status_message", IsNullable = true)]
        public string StatusMessage { get; set; }

        [SugarColumn(ColumnName = "state")]
        public FlowMessageState State { get; set; }

        /// <summary>
        /// 耗时(ms)，接收时间 - 发送时间
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public long ElapsedMs => RecvTime.HasValue ? (long)(RecvTime.Value - SendTime).TotalMilliseconds : -1;

        [SugarColumn(IsIgnore = true)]
        public bool IsRecived => State == FlowMessageState.Success || State == FlowMessageState.Fail;
    }
}
