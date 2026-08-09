using SqlSugar;
using System;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    /// <summary>
    /// 消息状态
    /// </summary>
    public enum FlowMessageState
    {
        Initial = 0,
        Sent = 1,
        [Obsolete("Use Sent.")]
        Sended = Sent,
        Success = 2,
        Fail = 3,
        Timeout = 4,
        Canceled = 5
    }

    /// <summary>
    /// 流程节点MQTT消息记录 - 一条记录包含发送和接收
    /// </summary>
    [SugarTable("FlowNodeMessage")]
    [SugarIndex(
        "idx_flow_node_message_batch_time",
        nameof(BatchId),
        OrderByType.Asc,
        nameof(SendTime),
        OrderByType.Desc)]
    [SugarIndex(
        "idx_flow_node_message_send_time",
        nameof(SendTime),
        OrderByType.Desc)]
    public class FlowNodeMessage
    {
        [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "batch_id")]
        public int BatchId { get; set; }

        [SugarColumn(ColumnName = "serial_number", IsNullable = true)]
        public string SerialNumber { get; set; }

        [SugarColumn(ColumnName = "node_record_id", IsNullable = true)]
        public int? NodeRecordId { get; set; }

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

        /// <summary>
        /// Runtime-only payload. SQLite stores this value as a compressed BLOB
        /// which is loaded explicitly for the selected message.
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public string? SendPayload { get; set; }

        [SugarColumn(ColumnName = "send_time")]
        public DateTime SendTime { get; set; }

        [SugarColumn(ColumnName = "recv_topic", IsNullable = true)]
        public string RecvTopic { get; set; }

        /// <summary>
        /// Runtime-only payload. SQLite stores this value as a compressed BLOB
        /// which is loaded explicitly for the selected message.
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        public string? RecvPayload { get; set; }

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
        public bool IsReceived => State == FlowMessageState.Success || State == FlowMessageState.Fail;

        [Obsolete("Use IsReceived.")]
        [SugarColumn(IsIgnore = true)]
        public bool IsRecived => IsReceived;
    }
}
