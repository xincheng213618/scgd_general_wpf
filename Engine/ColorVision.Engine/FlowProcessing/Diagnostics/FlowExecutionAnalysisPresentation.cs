using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;

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

        public string StatusText => IsTimedOut ? EngineLocalization.Get("超时") : Outcome switch
        {
            FlowNodeExecutionOutcome.Succeeded => EngineLocalization.Get("成功"),
            FlowNodeExecutionOutcome.Failed => EngineLocalization.Get("失败"),
            FlowNodeExecutionOutcome.Canceled => EngineLocalization.Get("已取消"),
            _ => EngineLocalization.Get("未判定")
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
                return IsTimedOut ? EngineLocalization.Format($"{detail} · 超时") : detail;
            }
        }
    }

    internal readonly record struct FlowExecutionAnalysisSummary(
        long WallClockMs,
        long ActiveMs,
        long NodeInactiveMs,
        long OverlapMs,
        long NodeWorkMs,
        int NodeCount,
        int TimeoutCount,
        int WarningCount,
        string SlowestNodeName,
        long SlowestNodeElapsedMs);

    internal readonly record struct FlowExecutionPhaseSummary(
        long? PreProcessMs,
        long? PostProcessMs);

    internal static class FlowExecutionAnalysisPresentation
    {
        private static readonly TimeSpan LegacyMessageMatchTolerance = TimeSpan.FromMilliseconds(250);

        internal static (IReadOnlyList<FlowNodeDurationAnalysis> Durations, FlowExecutionAnalysisSummary Summary) BuildOverviewFromOrderedRecords(
            IReadOnlyList<FlowNodeRecord> records,
            DateTime now,
            long warningThresholdMs,
            NodeMessageLookup messages)
        {
            if (records.Count == 0)
                return ([], new(0, 0, 0, 0, 0, 0, 0, 0, "—", 0));

            var items = new List<FlowNodeDurationAnalysis>(records.Count);
            DateTime wallStart = records[0].StartTime;
            DateTime wallEnd = records[0].EndTime ?? wallStart;
            DateTime? activeStart = null;
            DateTime activeEnd = default;
            long activeMs = 0;
            long nodeWorkMs = 0;
            int timeoutCount = 0, warningCount = 0;
            foreach (FlowNodeRecord record in records)
            {
                DateTime end = record.EndTime ?? record.StartTime;
                if (end > wallEnd) wallEnd = end;
                bool hasTimeoutMessage = messages.HasTimeout(record);
                bool isTimedOut = !record.EndTime.HasValue || hasTimeoutMessage;
                long elapsedMs = isTimedOut ? 0 : GetEffectiveElapsedMs(record, now);
                bool isWarning = !isTimedOut && elapsedMs > warningThresholdMs;
                items.Add(new(record, elapsedMs, isTimedOut, isWarning));
                nodeWorkMs = checked(nodeWorkMs + elapsedMs);
                if (isTimedOut) timeoutCount++;
                if (isWarning) warningCount++;

                if (isTimedOut || end < record.StartTime) continue;
                // Records arrive in start-time order, so overlapping intervals can be joined as we read them.
                if (activeStart.HasValue && record.StartTime > activeEnd)
                {
                    activeMs += (long)(activeEnd - activeStart.Value).TotalMilliseconds;
                    activeStart = null;
                }
                if (!activeStart.HasValue || end > activeEnd) activeEnd = end;
                activeStart ??= record.StartTime;
            }
            if (activeStart.HasValue) activeMs += (long)(activeEnd - activeStart.Value).TotalMilliseconds;

            // Stable ordering retains the snapshot's start-time and record-ID tie breaks.
            List<FlowNodeDurationAnalysis> ordered = items.OrderByDescending(item => item.ElapsedMs).ToList();
            FlowNodeDurationAnalysis? slowest = null;
            long slowestMs = ordered[0].ElapsedMs;
            foreach (FlowNodeDurationAnalysis item in ordered)
            {
                if (!item.IsTimedOut) slowest ??= item;
                item.RelativeToSlowestPercent = slowestMs > 0
                    ? item.ElapsedMs * 100d / slowestMs
                    : 0;
                item.ShareOfNodeWorkPercent = nodeWorkMs > 0
                    ? item.ElapsedMs * 100d / nodeWorkMs
                    : 0;
            }

            long wallClockMs = Math.Max(0, (long)(wallEnd - wallStart).TotalMilliseconds);
            return (ordered, new(wallClockMs, activeMs, Math.Max(0, wallClockMs - activeMs),
                Math.Max(0, nodeWorkMs - activeMs), nodeWorkMs, ordered.Count, timeoutCount, warningCount,
                slowest?.NodeName ?? "—", slowest?.ElapsedMs ?? 0));
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
            var messageLookup = new NodeMessageLookup(records, messages);

            return records
                .Select(record =>
                {
                    IReadOnlyList<FlowNodeMessage> executionMessages = messageLookup.GetMessages(record);
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

        internal static FlowExecutionPhaseSummary BuildPhaseSummaryFromOrderedEvents(
            IReadOnlyList<FlowExecutionEvent> events)
        {
            return new FlowExecutionPhaseSummary(
                GetPhaseDurationMs(
                    events,
                    "PreProcessStarted",
                    "PreProcessCompleted"),
                GetPhaseDurationMs(
                    events,
                    "PostProcessStarted",
                    "PostProcessCompleted"));
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

        private static long? GetPhaseDurationMs(
            IReadOnlyList<FlowExecutionEvent> events,
            string startedEventType,
            string completedEventType)
        {
            FlowExecutionEvent? completed = events
                .LastOrDefault(item => string.Equals(
                    item.EventType,
                    completedEventType,
                    StringComparison.Ordinal));
            if (completed == null)
                return null;
            if (TryGetElapsedMilliseconds(completed.DataJson, out long elapsedMs))
                return elapsedMs;

            FlowExecutionEvent? started = events
                .LastOrDefault(item =>
                    item.OccurredTimeUtc <= completed.OccurredTimeUtc
                    && string.Equals(
                        item.EventType,
                        startedEventType,
                        StringComparison.Ordinal));
            return started == null
                ? null
                : Math.Max(
                    0,
                    (long)(completed.OccurredTimeUtc - started.OccurredTimeUtc)
                        .TotalMilliseconds);
        }

        private static bool TryGetElapsedMilliseconds(
            string? dataJson,
            out long elapsedMs)
        {
            elapsedMs = 0;
            if (string.IsNullOrWhiteSpace(dataJson))
                return false;

            try
            {
                using JsonDocument document = JsonDocument.Parse(dataJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return false;

                foreach (JsonProperty property in document.RootElement
                    .EnumerateObject())
                {
                    if (string.Equals(
                            property.Name,
                            "elapsedMs",
                            StringComparison.OrdinalIgnoreCase)
                        && property.Value.TryGetInt64(out long value))
                    {
                        elapsedMs = Math.Max(0, value);
                        return true;
                    }
                }
            }
            catch (JsonException)
            {
            }
            return false;
        }

        internal sealed class NodeMessageLookup
        {
            private readonly ILookup<(int BatchId, int? NodeRecordId), FlowNodeMessage> _byExecution;
            private readonly Lazy<LegacyMessageOwners> _legacyOwners;

            internal NodeMessageLookup(IReadOnlyList<FlowNodeRecord> records, IEnumerable<FlowNodeMessage>? messages, bool messagesOrdered = false)
            {
                IEnumerable<FlowNodeMessage> source = messages ?? [];
                _byExecution = (messagesOrdered ? source : source.OrderBy(message => message.SendTime))
                    .ToLookup(message => (message.BatchId, message.NodeRecordId));
                _legacyOwners = new(() => new LegacyMessageOwners(records));
            }

            internal bool HasTimeout(FlowNodeRecord record)
            {
                if (record.Id > 0)
                {
                    bool foundExact = false;
                    foreach (FlowNodeMessage message in _byExecution[(record.BatchId, record.Id)])
                    {
                        if (!IsSameRun(record, message)) continue;
                        if (IsTimeout(message)) return true;
                        foundExact = true;
                    }
                    if (foundExact) return false;
                }
                return GetLegacyMessages(record).Any(IsTimeout);

                static bool IsTimeout(FlowNodeMessage message) => message.State == FlowMessageState.Timeout || message.StatusCode == -2;
            }

            internal IReadOnlyList<FlowNodeMessage> GetMessages(FlowNodeRecord record)
            {
                if (record.Id > 0)
                {
                    List<FlowNodeMessage> exact = _byExecution[(record.BatchId, record.Id)]
                        .Where(message => IsSameRun(record, message))
                        .ToList();
                    if (exact.Count > 0)
                        return exact;
                }
                return GetLegacyMessages(record);
            }

            private IReadOnlyList<FlowNodeMessage> GetLegacyMessages(FlowNodeRecord record)
            {
                // Rows predating node_record_id still use the original matching window.
                var legacyKey = (record.BatchId, (int?)null);
                if (!_byExecution.Contains(legacyKey))
                    return [];

                LegacyMessageOwners legacyOwners = _legacyOwners.Value;
                IEnumerable<FlowNodeRecord> candidates = legacyOwners.RecordsByBatch[record.BatchId];
                IEnumerable<FlowNodeMessage> legacyMessages = _byExecution[legacyKey];
                LegacyBatchWindows? windows = legacyOwners.GetBatchWindows(record.BatchId, candidates, legacyMessages);
                FlowNodeRecord? snapshotRecord = null;
                bool inSnapshot = windows != null && record.Id > 0
                    ? windows.RecordsById.TryGetValue(record.Id, out snapshotRecord)
                    : candidates.Any(candidate => IsSameRecord(candidate, record));
                if (!inSnapshot)
                    candidates = candidates.Append(record);

                // A caller can pass the same ID with different timestamps. Use the range only for a snapshot row.
                if (windows != null && ReferenceEquals(snapshotRecord, record))
                    legacyMessages = windows.MessagesInWindow(record);

                List<FlowNodeMessage>? matches = null;
                foreach (FlowNodeMessage message in legacyMessages)
                {
                    if (!IsSameRun(record, message) || !IsSameNode(record, message))
                        continue;

                    // A legacy message has the same owner across lookups of the same snapshot.
                    FlowNodeRecord? owner = inSnapshot
                        ? legacyOwners.ByMessage.GetOrAdd(message,
                            static (item, state) => FindLegacyMessageOwner(item, state.Windows?.CandidateRecords(item.SendTime) ?? state.Candidates),
                            (Windows: windows, Candidates: candidates))
                        : FindLegacyMessageOwner(message, candidates);
                    if (owner != null && IsSameRecord(owner, record))
                        (matches ??= []).Add(message);
                }
                return matches ?? [];
            }

            private sealed class LegacyMessageOwners(IReadOnlyList<FlowNodeRecord> records)
            {
                private const int MinimumIndexedRecords = 16;
                private ConcurrentDictionary<int, LegacyBatchWindows?>? _batchWindows;

                internal ILookup<int, FlowNodeRecord> RecordsByBatch { get; } = records.ToLookup(record => record.BatchId);
                internal ConcurrentDictionary<FlowNodeMessage, FlowNodeRecord?> ByMessage { get; } =
                    new(ReferenceEqualityComparer.Instance);

                internal LegacyBatchWindows? GetBatchWindows(int batchId, IEnumerable<FlowNodeRecord> candidates,
                    IEnumerable<FlowNodeMessage> messages)
                {
                    if (!candidates.TryGetNonEnumeratedCount(out int count) || count < MinimumIndexedRecords)
                        return null;

                    return LazyInitializer.EnsureInitialized(ref _batchWindows).GetOrAdd(batchId,
                        static (_, state) => LegacyBatchWindows.Create(state.Candidates, state.Messages, state.Count),
                        (Candidates: candidates, Messages: messages, Count: count));
                }
            }

            private sealed class LegacyBatchWindows(
                Dictionary<int, FlowNodeRecord> recordsById,
                FlowNodeRecord[] completed,
                List<FlowNodeRecord> open,
                FlowNodeMessage[] messages,
                long maximumDurationTicks)
            {
                private static readonly long ToleranceTicks = LegacyMessageMatchTolerance.Ticks;
                internal Dictionary<int, FlowNodeRecord> RecordsById { get; } = recordsById;

                internal static LegacyBatchWindows? Create(IEnumerable<FlowNodeRecord> records,
                    IEnumerable<FlowNodeMessage> messages, int count)
                {
                    var byId = new Dictionary<int, FlowNodeRecord>(count);
                    var completed = new List<FlowNodeRecord>(count);
                    var open = new List<FlowNodeRecord>();
                    long maximumDuration = 0;
                    foreach (FlowNodeRecord record in records)
                    {
                        long start = record.StartTime.Ticks;
                        long end = (record.EndTime ?? DateTime.MaxValue).Ticks;
                        // The fast path needs unambiguous record identity and valid DateTime arithmetic.
                        if (record.Id <= 0 || !byId.TryAdd(record.Id, record) || start < ToleranceTicks
                            || (end != DateTime.MaxValue.Ticks && end > DateTime.MaxValue.Ticks - ToleranceTicks))
                            return null;

                        if (end == DateTime.MaxValue.Ticks)
                            open.Add(record);
                        else
                        {
                            completed.Add(record);
                            maximumDuration = Math.Max(maximumDuration, end - start);
                        }
                    }

                    completed.Sort(static (left, right) => left.StartTime.CompareTo(right.StartTime));
                    return new(byId, completed.ToArray(), open, messages.ToArray(), maximumDuration);
                }

                internal IEnumerable<FlowNodeRecord> CandidateRecords(DateTime sendTime)
                {
                    // A completed row can start at most its duration plus the tolerance before the message.
                    long start = sendTime.Ticks - maximumDurationTicks - ToleranceTicks;
                    long end = sendTime.Ticks + ToleranceTicks;
                    int first = LowerBound(completed, start, static record => record.StartTime.Ticks);
                    int stop = LowerBound(completed, end + 1, static record => record.StartTime.Ticks);
                    for (int index = first; index < stop; index++)
                        yield return completed[index];
                    foreach (FlowNodeRecord record in open)
                        yield return record;
                }

                internal IEnumerable<FlowNodeMessage> MessagesInWindow(FlowNodeRecord record)
                {
                    long start = record.StartTime.Ticks - ToleranceTicks;
                    long end = (record.EndTime ?? DateTime.MaxValue).Ticks;
                    if (end != DateTime.MaxValue.Ticks)
                        end += ToleranceTicks;
                    int first = LowerBound(messages, start, static message => message.SendTime.Ticks);
                    int stop = LowerBound(messages, end + 1, static message => message.SendTime.Ticks);
                    return new ArraySegment<FlowNodeMessage>(messages, first, Math.Max(0, stop - first));
                }

                private static int LowerBound<T>(T[] items, long ticks, Func<T, long> getTicks)
                {
                    int first = 0;
                    int last = items.Length;
                    while (first < last)
                    {
                        int middle = first + (last - first) / 2;
                        if (getTicks(items[middle]) < ticks)
                            first = middle + 1;
                        else
                            last = middle;
                    }
                    return first;
                }
            }
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

        private static bool IsSameRun(FlowNodeRecord record, FlowNodeMessage message)
        {
            if (string.IsNullOrWhiteSpace(record.SerialNumber)
                || string.IsNullOrWhiteSpace(message.SerialNumber))
            {
                return true;
            }

            return string.Equals(record.SerialNumber, message.SerialNumber, StringComparison.Ordinal);
        }

        private static FlowNodeRecord? FindLegacyMessageOwner(
            FlowNodeMessage message,
            IEnumerable<FlowNodeRecord> candidates)
        {
            FlowNodeRecord? owner = null;
            (long Distance, DateTime Start, int Id) closest = default;
            foreach (FlowNodeRecord candidate in candidates)
            {
                if (!IsSameRun(candidate, message) || !IsSameNode(candidate, message)
                    || !IsWithinLegacyMatchWindow(candidate, message.SendTime))
                    continue;

                var order = (Math.Abs((message.SendTime - candidate.StartTime).Ticks), candidate.StartTime, candidate.Id);
                if (owner != null && order.CompareTo(closest) >= 0)
                    continue;
                owner = candidate;
                closest = order;
            }
            return owner;
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

    }
}
