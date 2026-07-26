using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    internal sealed class FlowNodeDurationAnalysis
    {
        internal FlowNodeDurationAnalysis(
            FlowNodeRecord record,
            long elapsedMs,
            bool isRunning,
            bool isWarning)
        {
            Record = record;
            ElapsedMs = elapsedMs;
            IsRunning = isRunning;
            IsWarning = isWarning;
        }

        public FlowNodeRecord Record { get; }

        public string NodeName => string.IsNullOrWhiteSpace(Record.NodeName) ? "Unknown" : Record.NodeName;

        public string NodeType => Record.NodeType ?? string.Empty;

        public string NodeId => Record.NodeId ?? string.Empty;

        public long ElapsedMs { get; }

        public double RelativeToSlowestPercent { get; internal set; }

        public double ShareOfNodeWorkPercent { get; internal set; }

        public bool IsRunning { get; }

        public bool IsWarning { get; }

        public string DurationText => FlowExecutionAnalysisPresentation.FormatDuration(ElapsedMs);

        public string ShareText => $"{ShareOfNodeWorkPercent:N1}%";

        public string DetailText => string.IsNullOrWhiteSpace(NodeType)
            ? Record.StartTime.ToString("HH:mm:ss.fff")
            : $"{NodeType} · {Record.StartTime:HH:mm:ss.fff}";
    }

    internal readonly record struct FlowExecutionAnalysisSummary(
        long WallClockMs,
        long ActiveMs,
        long IdleMs,
        long OverlapMs,
        long NodeWorkMs,
        int NodeCount,
        int RunningCount,
        int WarningCount,
        string SlowestNodeName,
        long SlowestNodeElapsedMs);

    internal static class FlowExecutionAnalysisPresentation
    {
        private static readonly TimeSpan LegacyMessageMatchTolerance = TimeSpan.FromMilliseconds(250);

        internal static IReadOnlyList<FlowNodeDurationAnalysis> BuildDurationItems(
            IEnumerable<FlowNodeRecord> source,
            DateTime now,
            long warningThresholdMs)
        {
            List<FlowNodeRecord> records = source?
                .OrderBy(item => item.StartTime)
                .ThenBy(item => item.Id)
                .ToList() ?? new List<FlowNodeRecord>();

            List<FlowNodeDurationAnalysis> items = records
                .Select(record =>
                {
                    long elapsedMs = GetEffectiveElapsedMs(record, now);
                    return new FlowNodeDurationAnalysis(
                        record,
                        elapsedMs,
                        !record.EndTime.HasValue,
                        elapsedMs > warningThresholdMs);
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
            DateTime end = records.Max(item => item.EndTime ?? now);
            long wallClockMs = Math.Max(0, (long)(end - start).TotalMilliseconds);
            long activeMs = CalculateActiveTimeMs(records, now);
            long nodeWorkMs = records.Sum(item => GetEffectiveElapsedMs(item, now));
            FlowNodeDurationAnalysis? slowest = durationItems.Count > 0 ? durationItems[0] : null;

            return new FlowExecutionAnalysisSummary(
                wallClockMs,
                activeMs,
                Math.Max(0, wallClockMs - activeMs),
                Math.Max(0, nodeWorkMs - activeMs),
                nodeWorkMs,
                durationItems.Count,
                durationItems.Count(item => item.IsRunning),
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

            return Math.Max(0, (long)(now - record.StartTime).TotalMilliseconds);
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

        private static long CalculateActiveTimeMs(IEnumerable<FlowNodeRecord> source, DateTime now)
        {
            var intervals = source
                .Select(item => (Start: item.StartTime, End: item.EndTime ?? now))
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
