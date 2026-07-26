using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    internal sealed class FlowNodeDurationAnalysis
    {
        internal FlowNodeDurationAnalysis(
            IReadOnlyList<FlowNodeRecord> records,
            long averageElapsedMs,
            long minimumElapsedMs,
            long maximumElapsedMs,
            bool isRunning,
            bool isWarning)
        {
            Records = records;
            Record = records.OrderByDescending(item => item.StartTime).First();
            AverageElapsedMs = averageElapsedMs;
            MinimumElapsedMs = minimumElapsedMs;
            MaximumElapsedMs = maximumElapsedMs;
            IsRunning = isRunning;
            IsWarning = isWarning;
        }

        public IReadOnlyList<FlowNodeRecord> Records { get; }

        public FlowNodeRecord Record { get; }

        public int Rank { get; internal set; }

        public string NodeName => string.IsNullOrWhiteSpace(Record.NodeName) ? "Unknown" : Record.NodeName;

        public string NodeType => Record.NodeType ?? string.Empty;

        public string NodeId => Record.NodeId ?? string.Empty;

        public int SampleCount => Records.Count;

        public long AverageElapsedMs { get; }

        public long MinimumElapsedMs { get; }

        public long MaximumElapsedMs { get; }

        public double RelativeToSlowestPercent { get; internal set; }

        public double ShareOfNodeWorkPercent { get; internal set; }

        public bool IsRunning { get; }

        public bool IsWarning { get; }

        public bool IsComparison => Records.Select(item => item.BatchId).Distinct().Skip(1).Any();

        public string DurationText => $"{AverageElapsedMs:N0} ms";

        public string ShareText => $"{ShareOfNodeWorkPercent:N1}%";

        public string SampleText => IsComparison
            ? $"{SampleCount} 次 · {MinimumElapsedMs:N0}–{MaximumElapsedMs:N0} ms"
            : $"{NodeType}";
    }

    internal readonly record struct FlowExecutionAnalysisSummary(
        long AverageWallClockMs,
        long AverageActiveMs,
        long AverageIdleMs,
        long AverageOverlapMs,
        long AverageNodeWorkMs,
        int NodeCount,
        int RunningCount,
        int WarningCount,
        string SlowestNodeName,
        long SlowestNodeElapsedMs);

    internal static class FlowExecutionAnalysisPresentation
    {
        internal static IReadOnlyList<FlowNodeDurationAnalysis> BuildDurationItems(
            IEnumerable<FlowNodeRecord> source,
            DateTime now,
            long warningThresholdMs)
        {
            List<FlowNodeRecord> records = source?.OrderBy(item => item.StartTime).ToList()
                ?? new List<FlowNodeRecord>();
            bool isSingleBatch = records.Select(item => item.BatchId).Distinct().Take(2).Count() <= 1;

            IEnumerable<IGrouping<string, FlowNodeRecord>> groups = isSingleBatch
                ? records.GroupBy(item => $"record:{item.Id}:{item.StartTime.Ticks}")
                : records.GroupBy(GetStableNodeKey);

            List<FlowNodeDurationAnalysis> items = groups
                .Select(group => CreateDurationItem(group.ToList(), now, warningThresholdMs))
                .OrderByDescending(item => item.AverageElapsedMs)
                .ThenBy(item => item.NodeName, StringComparer.CurrentCulture)
                .ToList();

            long totalNodeWorkMs = items.Sum(item => item.AverageElapsedMs);
            long slowestMs = items.FirstOrDefault()?.AverageElapsedMs ?? 0;
            for (int index = 0; index < items.Count; index++)
            {
                FlowNodeDurationAnalysis item = items[index];
                item.Rank = index + 1;
                item.RelativeToSlowestPercent = slowestMs > 0
                    ? item.AverageElapsedMs * 100d / slowestMs
                    : 0;
                item.ShareOfNodeWorkPercent = totalNodeWorkMs > 0
                    ? item.AverageElapsedMs * 100d / totalNodeWorkMs
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
            var batchTimings = records
                .GroupBy(item => item.BatchId)
                .Select(group =>
                {
                    DateTime start = group.Min(item => item.StartTime);
                    DateTime end = group.Max(item => item.EndTime ?? now);
                    long wallClockMs = Math.Max(0, (long)(end - start).TotalMilliseconds);
                    long activeMs = CalculateActiveTimeMs(group, now);
                    long nodeWorkMs = group.Sum(item => GetEffectiveElapsedMs(item, now));
                    return (
                        WallClockMs: wallClockMs,
                        ActiveMs: activeMs,
                        IdleMs: Math.Max(0, wallClockMs - activeMs),
                        OverlapMs: Math.Max(0, nodeWorkMs - activeMs));
                })
                .ToList();

            long averageWallClockMs = batchTimings.Count == 0
                ? 0
                : Convert.ToInt64(batchTimings.Average(item => item.WallClockMs));
            long averageActiveMs = batchTimings.Count == 0
                ? 0
                : Convert.ToInt64(batchTimings.Average(item => item.ActiveMs));
            long averageIdleMs = batchTimings.Count == 0
                ? 0
                : Convert.ToInt64(batchTimings.Average(item => item.IdleMs));
            long averageOverlapMs = batchTimings.Count == 0
                ? 0
                : Convert.ToInt64(batchTimings.Average(item => item.OverlapMs));
            long averageNodeWorkMs = durationItems.Sum(item => item.AverageElapsedMs);
            FlowNodeDurationAnalysis? slowest = durationItems.Count > 0 ? durationItems[0] : null;

            return new FlowExecutionAnalysisSummary(
                averageWallClockMs,
                averageActiveMs,
                averageIdleMs,
                averageOverlapMs,
                averageNodeWorkMs,
                durationItems.Count,
                durationItems.Count(item => item.IsRunning),
                durationItems.Count(item => item.IsWarning),
                slowest?.NodeName ?? "—",
                slowest?.AverageElapsedMs ?? 0);
        }

        internal static long GetEffectiveElapsedMs(FlowNodeRecord record, DateTime now)
        {
            if (record.EndTime.HasValue)
                return Math.Max(0, record.ElapsedMs);

            return Math.Max(0, (long)(now - record.StartTime).TotalMilliseconds);
        }

        private static FlowNodeDurationAnalysis CreateDurationItem(
            IReadOnlyList<FlowNodeRecord> records,
            DateTime now,
            long warningThresholdMs)
        {
            long[] elapsedValues = records.Select(item => GetEffectiveElapsedMs(item, now)).ToArray();
            long averageElapsedMs = elapsedValues.Length == 0
                ? 0
                : Convert.ToInt64(elapsedValues.Average());
            bool isRunning = records.Any(item => !item.EndTime.HasValue);
            bool isWarning = elapsedValues.Any(value => value > warningThresholdMs);
            return new FlowNodeDurationAnalysis(
                records,
                averageElapsedMs,
                elapsedValues.DefaultIfEmpty().Min(),
                elapsedValues.DefaultIfEmpty().Max(),
                isRunning,
                isWarning);
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

        private static string GetStableNodeKey(FlowNodeRecord record)
        {
            if (!string.IsNullOrWhiteSpace(record.NodeId))
                return $"id:{record.NodeId}";

            return $"name:{record.NodeName}|type:{record.NodeType}";
        }
    }
}
