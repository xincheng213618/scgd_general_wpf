using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Diagnostics;

internal sealed record FlowNodeComparisonHistory(
    IReadOnlyList<FlowNodeRecord> Records,
    IReadOnlyList<FlowNodeMessage> Messages,
    bool HasFlowIdentity)
{
    internal static FlowNodeComparisonHistory ForRuns(string nodeId, IEnumerable<FlowRunRecord> runs,
        IEnumerable<FlowNodeRecord> records, IEnumerable<FlowNodeMessage> messages)
    {
        var runKeys = runs.Where(run => run.BatchId.HasValue)
            .Select(run => (run.BatchId!.Value, run.SerialNumber ?? string.Empty)).ToHashSet();
        var legacySerials = runs.Where(run => !run.BatchId.HasValue && !string.IsNullOrWhiteSpace(run.SerialNumber))
            .Select(run => run.SerialNumber!).ToHashSet(StringComparer.Ordinal);
        var selectedRecords = records.Where(record => record.NodeId == nodeId
                && (runKeys.Contains((record.BatchId, record.SerialNumber ?? string.Empty))
                    || legacySerials.Contains(record.SerialNumber ?? string.Empty)))
            .DistinctBy(record => (record.Id, record.BatchId, record.SerialNumber, record.StartTime))
            .OrderByDescending(record => record.StartTime).ThenByDescending(record => record.Id).ToArray();
        var selectedKeys = selectedRecords.Select(record => (record.BatchId, record.SerialNumber ?? string.Empty)).ToHashSet();
        var selectedMessages = messages.Where(message => message.NodeId == nodeId
                && selectedKeys.Contains((message.BatchId, message.SerialNumber ?? string.Empty)))
            .DistinctBy(message => (message.Id, message.BatchId, message.SerialNumber, message.MsgId, message.SendTime))
            .ToArray();
        return new(selectedRecords, selectedMessages, true);
    }

    internal IReadOnlyList<FlowNodeComparisonRow> BuildRows()
    {
        var rows = new List<FlowNodeComparisonRow>();
        foreach (FlowNodeRecord record in Records)
        {
            IReadOnlyList<FlowNodeMessage> messages = FlowExecutionAnalysisPresentation.GetMessagesForNodeExecution(record, Messages, Records);
            if (messages.Count == 0)
                rows.Add(new(record, null));
            else
                rows.AddRange(messages.Select(message => new FlowNodeComparisonRow(record, message)));
        }
        return rows;
    }
}

internal sealed record FlowNodeComparisonRow(FlowNodeRecord Record, FlowNodeMessage? Message)
{
    public int BatchId => Record.BatchId;
    public string SerialNumber => Record.SerialNumber ?? string.Empty;
    public string TimeText => (Message?.SendTime ?? Record.StartTime).ToString("yyyy/MM/dd HH:mm:ss.fff");
    public string EventName => Message?.EventName ?? "—";
    public string MessageId => Message?.MsgId ?? "—";
    public string DurationText => Record.EndTime.HasValue
        && Message?.State != FlowMessageState.Timeout && Message?.StatusCode != -2
        ? FlowExecutionAnalysisPresentation.FormatDuration(Record.ElapsedMs) : "—";
    public string StateText => Message == null ? EngineLocalization.Get("无消息记录")
        : EngineLocalization.Get(Message.State switch
        {
            FlowMessageState.Success => "成功",
            FlowMessageState.Fail => "失败",
            FlowMessageState.Timeout => "超时",
            FlowMessageState.Canceled => "已取消",
            FlowMessageState.Sent => "已发送",
            _ => "等待"
        });
    public string StatusText => Message?.StatusMessage ?? string.Empty;
    public string Label => $"Batch {BatchId} · {TimeText} · {EventName} · SN {SerialNumber}";
}
