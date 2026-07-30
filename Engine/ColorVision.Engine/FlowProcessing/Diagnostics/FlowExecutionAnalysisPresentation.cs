using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    internal enum FlowNodeExecutionOutcome
    {
        Succeeded,
        Failed,
        Canceled,
        Completed
    }

    internal sealed class FlowNodeHistoryAnalysis
    {
        internal FlowNodeHistoryAnalysis(
            FlowNodeRecord record,
            FlowNodeExecutionOutcome outcome,
            long? elapsedMs,
            bool isTimedOut)
        {
            Record = record;
            Outcome = outcome;
            ElapsedMs = elapsedMs;
            IsTimedOut = isTimedOut;
        }

        public FlowNodeRecord Record { get; }

        public int BatchId => Record.BatchId;

        public DateTime StartTime => Record.StartTime;

        public long? ElapsedMs { get; }

        public string ElapsedText => ElapsedMs.HasValue
            ? $"{ElapsedMs.Value:N0}"
            : "—";

        public FlowNodeExecutionOutcome Outcome { get; }

        public string StatusText => IsTimedOut ? "超时" : Outcome switch
        {
            FlowNodeExecutionOutcome.Succeeded => "成功",
            FlowNodeExecutionOutcome.Failed => "失败",
            FlowNodeExecutionOutcome.Canceled => "已取消",
            _ => "未判定"
        };

        public bool IsTimedOut { get; }

        public bool IsSucceeded => Outcome == FlowNodeExecutionOutcome.Succeeded;

        public bool IsFailed => Outcome == FlowNodeExecutionOutcome.Failed;

        public bool IsCanceled => Outcome == FlowNodeExecutionOutcome.Canceled;
    }

    internal readonly record struct FlowNodeHistorySummary(
        int TotalCount,
        int SuccessCount,
        int FailureCount,
        int TimeoutCount,
        int CompletedCount,
        long? SuccessAverageMs,
        long? SuccessP95Ms,
        long? FailureAverageMs,
        long? FailureP95Ms)
    {
        public double? SuccessRatePercent
        {
            get
            {
                int classifiedCount = SuccessCount + FailureCount;
                return classifiedCount == 0
                    ? null
                    : SuccessCount * 100d / classifiedCount;
            }
        }
    }

    internal sealed class FlowNodeDurationAnalysis
    {
        internal FlowNodeDurationAnalysis(
            FlowNodeRecord record,
            long elapsedMs,
            bool isTimedOut,
            bool isWarning)
        {
            Record = record;
            ElapsedMs = elapsedMs;
            IsTimedOut = isTimedOut;
            IsWarning = isWarning;
        }

        public FlowNodeRecord Record { get; }

        public string NodeName => string.IsNullOrWhiteSpace(Record.NodeName) ? "Unknown" : Record.NodeName;

        public string NodeType => Record.NodeType ?? string.Empty;

        public string NodeId => Record.NodeId ?? string.Empty;

        public long ElapsedMs { get; }

        public double RelativeToSlowestPercent { get; internal set; }

        public double ShareOfNodeWorkPercent { get; internal set; }

        public bool IsTimedOut { get; }

        public bool IsWarning { get; }

        public string DurationText => IsTimedOut
            ? "—"
            : FlowExecutionAnalysisPresentation.FormatDuration(ElapsedMs);

        public string ShareText => IsTimedOut
            ? "—"
            : $"{ShareOfNodeWorkPercent:N1}%";

        public string DetailText
        {
            get
            {
                string detail = string.IsNullOrWhiteSpace(NodeType)
                    ? Record.StartTime.ToString("HH:mm:ss.fff")
                    : $"{NodeType} · {Record.StartTime:HH:mm:ss.fff}";
                return IsTimedOut ? $"{detail} · 超时" : detail;
            }
        }
    }

    internal readonly record struct FlowExecutionAnalysisSummary(
        long WallClockMs,
        long ActiveMs,
        long IdleMs,
        long OverlapMs,
        long NodeWorkMs,
        int NodeCount,
        int TimeoutCount,
        int WarningCount,
        string SlowestNodeName,
        long SlowestNodeElapsedMs);

    internal static class FlowExecutionAnalysisPresentation
    {
        private static readonly TimeSpan LegacyMessageMatchTolerance = TimeSpan.FromMilliseconds(250);

        internal static IReadOnlyList<FlowNodeDurationAnalysis> BuildDurationItems(
            IEnumerable<FlowNodeRecord> source,
            DateTime now,
            long warningThresholdMs,
            IEnumerable<FlowNodeMessage>? messages = null)
        {
            List<FlowNodeRecord> records = source?
                .OrderBy(item => item.StartTime)
                .ThenBy(item => item.Id)
                .ToList() ?? new List<FlowNodeRecord>();
            List<FlowNodeMessage> messageList = messages?.ToList()
                ?? new List<FlowNodeMessage>();

            List<FlowNodeDurationAnalysis> items = records
                .Select(record =>
                {
                    IReadOnlyList<FlowNodeMessage> executionMessages =
                        GetMessagesForNodeExecution(record, messageList, records);
                    bool isTimedOut = !record.EndTime.HasValue
                        || executionMessages.Any(message =>
                            message.State == FlowMessageState.Timeout
                            || message.StatusCode == -2);
                    long elapsedMs = isTimedOut
                        ? 0
                        : GetEffectiveElapsedMs(record, now);
                    return new FlowNodeDurationAnalysis(
                        record,
                        elapsedMs,
                        isTimedOut,
                        !isTimedOut && elapsedMs > warningThresholdMs);
                })
                .OrderByDescending(item => item.ElapsedMs)
                .ThenBy(item => item.Record.StartTime)
                .ThenBy(item => item.Record.Id)
                .ToList();

            long totalNodeWorkMs = items.Sum(item => item.ElapsedMs);
            long slowestMs = items.FirstOrDefault()?.ElapsedMs ?? 0;
            foreach (FlowNodeDurationAnalysis item in items)
            {
                item.RelativeToSlowestPercent = slowestMs > 0
                    ? item.ElapsedMs * 100d / slowestMs
                    : 0;
                item.ShareOfNodeWorkPercent = totalNodeWorkMs > 0
                    ? item.ElapsedMs * 100d / totalNodeWorkMs
                    : 0;
            }

            return items;
        }

        internal static IReadOnlyList<FlowNodeHistoryAnalysis> BuildNodeHistoryItems(
            IEnumerable<FlowNodeRecord> source,
            IEnumerable<FlowNodeMessage> messages,
            DateTime now)
        {
            List<FlowNodeRecord> records = source?
                .OrderByDescending(item => item.StartTime)
                .ThenByDescending(item => item.Id)
                .ToList() ?? new List<FlowNodeRecord>();
            List<FlowNodeMessage> messageList = messages?.ToList() ?? new List<FlowNodeMessage>();

            return records
                .Select(record =>
                {
                    IReadOnlyList<FlowNodeMessage> executionMessages =
                        GetMessagesForNodeExecution(record, messageList, records);
                    FlowNodeExecutionOutcome outcome =
                        GetNodeExecutionOutcome(record, executionMessages);
                    bool isTimedOut =
                        !record.EndTime.HasValue
                        || executionMessages.Any(message =>
                            message.State == FlowMessageState.Timeout
                            || message.StatusCode == -2);
                    return new FlowNodeHistoryAnalysis(
                        record,
                        outcome,
                        record.EndTime.HasValue && !isTimedOut
                            ? Math.Max(0, record.ElapsedMs)
                            : null,
                        isTimedOut);
                })
                .ToList();
        }

        internal static FlowNodeHistorySummary BuildNodeHistorySummary(
            IEnumerable<FlowNodeHistoryAnalysis> source)
        {
            List<FlowNodeHistoryAnalysis> items = source?.ToList()
                ?? new List<FlowNodeHistoryAnalysis>();
            long[] successfulElapsed = items
                .Where(item => item.Outcome == FlowNodeExecutionOutcome.Succeeded
                    && item.ElapsedMs.HasValue)
                .Select(item => item.ElapsedMs!.Value)
                .OrderBy(item => item)
                .ToArray();
            long[] failedElapsed = items
                .Where(item => item.Outcome == FlowNodeExecutionOutcome.Failed
                    && item.ElapsedMs.HasValue)
                .Select(item => item.ElapsedMs!.Value)
                .OrderBy(item => item)
                .ToArray();

            return new FlowNodeHistorySummary(
                items.Count,
                items.Count(item => item.Outcome == FlowNodeExecutionOutcome.Succeeded),
                items.Count(item => item.Outcome == FlowNodeExecutionOutcome.Failed),
                items.Count(item => item.IsTimedOut),
                items.Count(item =>
                    item.Outcome == FlowNodeExecutionOutcome.Completed
                    || item.Outcome == FlowNodeExecutionOutcome.Canceled),
                CalculateAverage(successfulElapsed),
                CalculateP95(successfulElapsed),
                CalculateAverage(failedElapsed),
                CalculateP95(failedElapsed));
        }

        internal static FlowNodeExecutionOutcome GetNodeExecutionOutcome(
            FlowNodeRecord record,
            IEnumerable<FlowNodeMessage> messages)
        {
            if (!record.EndTime.HasValue)
                return FlowNodeExecutionOutcome.Failed;

            List<FlowNodeMessage> messageList = messages?.ToList()
                ?? new List<FlowNodeMessage>();
            if (messageList.Any(message =>
                message.State == FlowMessageState.Canceled
                || message.StatusCode == -4))
            {
                return FlowNodeExecutionOutcome.Canceled;
            }
            if (messageList.Any(message =>
                message.State == FlowMessageState.Fail
                || message.State == FlowMessageState.Timeout
                || message.StatusCode is int statusCode && statusCode != 0))
            {
                return FlowNodeExecutionOutcome.Failed;
            }

            if (messageList.Count > 0
                && messageList.All(message =>
                    message.State == FlowMessageState.Success
                    || message.StatusCode == 0))
            {
                return FlowNodeExecutionOutcome.Succeeded;
            }

            return FlowNodeExecutionOutcome.Completed;
        }

        internal static FlowExecutionAnalysisSummary BuildSummary(
            IEnumerable<FlowNodeRecord> source,
            IReadOnlyList<FlowNodeDurationAnalysis> durationItems,
            DateTime now)
        {
            List<FlowNodeRecord> records = source?.ToList() ?? new List<FlowNodeRecord>();
            if (records.Count == 0)
            {
                return new FlowExecutionAnalysisSummary(
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    "—",
                    0);
            }

            DateTime start = records.Min(item => item.StartTime);
            DateTime end = records.Max(item => item.EndTime ?? item.StartTime);
            long wallClockMs = Math.Max(0, (long)(end - start).TotalMilliseconds);
            long activeMs = CalculateActiveTimeMs(
                durationItems
                    .Where(item => !item.IsTimedOut)
                    .Select(item => item.Record));
            long nodeWorkMs = durationItems.Sum(item => item.ElapsedMs);
            FlowNodeDurationAnalysis? slowest =
                durationItems.FirstOrDefault(item => !item.IsTimedOut);

            return new FlowExecutionAnalysisSummary(
                wallClockMs,
                activeMs,
                Math.Max(0, wallClockMs - activeMs),
                Math.Max(0, nodeWorkMs - activeMs),
                nodeWorkMs,
                durationItems.Count,
                durationItems.Count(item => item.IsTimedOut),
                durationItems.Count(item => item.IsWarning),
                slowest?.NodeName ?? "—",
                slowest?.ElapsedMs ?? 0);
        }

        internal static IReadOnlyList<FlowNodeMessage> GetMessagesForNodeExecution(
            FlowNodeRecord record,
            IEnumerable<FlowNodeMessage> source,
            IEnumerable<FlowNodeRecord>? runRecords = null)
        {
            List<FlowNodeMessage> runMessages = source?
                .Where(message => message.BatchId == record.BatchId
                    && IsSameRun(record, message))
                .OrderBy(message => message.SendTime)
                .ToList() ?? new List<FlowNodeMessage>();
            if (runMessages.Count == 0)
                return runMessages;

            if (record.Id > 0)
            {
                List<FlowNodeMessage> exact = runMessages
                    .Where(message => message.NodeRecordId == record.Id)
                    .ToList();
                if (exact.Count > 0)
                    return exact;
            }

            List<FlowNodeMessage> legacyMessages = runMessages
                .Where(message => !message.NodeRecordId.HasValue
                    && IsSameNode(record, message))
                .ToList();
            if (legacyMessages.Count == 0)
                return legacyMessages;

            List<FlowNodeRecord> candidateRecords = (runRecords ?? new[] { record })
                .Where(candidate => candidate.BatchId == record.BatchId
                    && IsSameRun(record, candidate)
                    && IsSameNode(record, candidate))
                .OrderBy(candidate => candidate.StartTime)
                .ThenBy(candidate => candidate.Id)
                .ToList();
            if (!candidateRecords.Any(candidate => IsSameRecord(candidate, record)))
                candidateRecords.Add(record);

            return legacyMessages
                .Where(message =>
                {
                    FlowNodeRecord? owner = FindLegacyMessageOwner(message, candidateRecords);
                    return owner != null && IsSameRecord(owner, record);
                })
                .ToList();
        }

        internal static long GetEffectiveElapsedMs(FlowNodeRecord record, DateTime now)
        {
            if (record.EndTime.HasValue)
                return Math.Max(0, record.ElapsedMs);

            return 0;
        }

        internal static string FormatDuration(long milliseconds)
        {
            if (milliseconds < 1000)
                return $"{milliseconds:N0} ms";
            if (milliseconds < 60000)
                return $"{milliseconds / 1000d:N2} s";

            TimeSpan duration = TimeSpan.FromMilliseconds(milliseconds);
            return duration.TotalHours >= 1
                ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
                : $"{duration.Minutes}:{duration.Seconds:00}.{duration.Milliseconds / 100:0}";
        }

        private static long? CalculateAverage(long[] values)
        {
            return values.Length == 0
                ? null
                : Convert.ToInt64(values.Average());
        }

        private static long? CalculateP95(long[] sortedValues)
        {
            if (sortedValues.Length == 0)
                return null;

            int index = Math.Clamp(
                (int)Math.Ceiling(sortedValues.Length * 0.95) - 1,
                0,
                sortedValues.Length - 1);
            return sortedValues[index];
        }

        private static bool IsSameNode(FlowNodeRecord record, FlowNodeMessage message)
        {
            if (!string.IsNullOrWhiteSpace(record.NodeId) && !string.IsNullOrWhiteSpace(message.NodeId))
                return string.Equals(record.NodeId, message.NodeId, StringComparison.Ordinal);

            return string.Equals(record.NodeName, message.NodeName, StringComparison.Ordinal);
        }

        private static bool IsSameNode(FlowNodeRecord left, FlowNodeRecord right)
        {
            if (!string.IsNullOrWhiteSpace(left.NodeId) && !string.IsNullOrWhiteSpace(right.NodeId))
                return string.Equals(left.NodeId, right.NodeId, StringComparison.Ordinal);

            return string.Equals(left.NodeName, right.NodeName, StringComparison.Ordinal);
        }

        private static bool IsSameRun(FlowNodeRecord record, FlowNodeMessage message)
        {
            if (string.IsNullOrWhiteSpace(record.SerialNumber)
                || string.IsNullOrWhiteSpace(message.SerialNumber))
            {
                return true;
            }

            return string.Equals(record.SerialNumber, message.SerialNumber, StringComparison.Ordinal);
        }

        private static bool IsSameRun(FlowNodeRecord left, FlowNodeRecord right)
        {
            if (string.IsNullOrWhiteSpace(left.SerialNumber)
                || string.IsNullOrWhiteSpace(right.SerialNumber))
            {
                return true;
            }

            return string.Equals(left.SerialNumber, right.SerialNumber, StringComparison.Ordinal);
        }

        private static FlowNodeRecord? FindLegacyMessageOwner(
            FlowNodeMessage message,
            IEnumerable<FlowNodeRecord> candidates)
        {
            return candidates
                .Where(candidate => IsWithinLegacyMatchWindow(candidate, message.SendTime))
                .OrderBy(candidate => Math.Abs((message.SendTime - candidate.StartTime).Ticks))
                .ThenBy(candidate => candidate.StartTime)
                .ThenBy(candidate => candidate.Id)
                .FirstOrDefault();
        }

        private static bool IsWithinLegacyMatchWindow(FlowNodeRecord record, DateTime sendTime)
        {
            DateTime lowerBound = record.StartTime - LegacyMessageMatchTolerance;
            DateTime upperBound = record.EndTime ?? DateTime.MaxValue;
            if (upperBound != DateTime.MaxValue)
                upperBound += LegacyMessageMatchTolerance;

            return sendTime >= lowerBound && sendTime <= upperBound;
        }

        private static bool IsSameRecord(FlowNodeRecord left, FlowNodeRecord right)
        {
            if (left.Id > 0 && right.Id > 0)
                return left.Id == right.Id;

            return left.BatchId == right.BatchId
                && left.StartTime == right.StartTime
                && string.Equals(left.NodeId, right.NodeId, StringComparison.Ordinal)
                && string.Equals(left.SerialNumber, right.SerialNumber, StringComparison.Ordinal);
        }

        private static long CalculateActiveTimeMs(IEnumerable<FlowNodeRecord> source)
        {
            var intervals = source
                .Select(item => (Start: item.StartTime, End: item.EndTime ?? item.StartTime))
                .Where(item => item.End >= item.Start)
                .OrderBy(item => item.Start)
                .ToList();
            if (intervals.Count == 0)
                return 0;

            DateTime currentStart = intervals[0].Start;
            DateTime currentEnd = intervals[0].End;
            long activeMs = 0;
            for (int index = 1; index < intervals.Count; index++)
            {
                (DateTime start, DateTime end) = intervals[index];
                if (start <= currentEnd)
                {
                    if (end > currentEnd)
                        currentEnd = end;
                    continue;
                }

                activeMs += Math.Max(0, (long)(currentEnd - currentStart).TotalMilliseconds);
                currentStart = start;
                currentEnd = end;
            }

            activeMs += Math.Max(0, (long)(currentEnd - currentStart).TotalMilliseconds);
            return activeMs;
        }
    }
}
