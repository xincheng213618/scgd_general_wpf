using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    internal enum FlowAnalysisPageKind
    {
        Overview,
        Node,
        Messages,
        Comparison
    }

    internal readonly record struct FlowAnalysisNavigationState(
        FlowAnalysisPageKind PageKind,
        int BatchId,
        string SerialNumber,
        int? RecordId = null,
        int? MessageId = null);

    internal sealed class FlowExecutionAnalysisSession
    {
        private readonly FlowExecutionAnalysisPresentation.NodeMessageLookup _messagesByExecution;

        internal FlowExecutionAnalysisSession(
            int batchId,
            string? serialNumber,
            MeasureBatchModel? batch,
            FlowRunRecord? run,
            IReadOnlyList<FlowNodeRecord> records,
            IReadOnlyList<FlowNodeMessage> messages,
            IReadOnlyList<FlowExecutionEvent> events,
            DateTime capturedAt,
            long slowNodeThresholdMs,
            FlowAnalysisDataSource? dataSource = null)
        {
            DataSource = dataSource ?? new FlowAnalysisDataSource();
            BatchId = batchId;
            SerialNumber = serialNumber ?? string.Empty;
            Batch = batch;
            Run = run;
            Records = SnapshotInTimeOrder(records, static item => item.StartTime, static item => item.Id);
            Messages = SnapshotInTimeOrder(messages, static item => item.SendTime, static item => item.Id);
            _messagesByExecution = new FlowExecutionAnalysisPresentation.NodeMessageLookup(Records, Messages, messagesOrdered: true);
            Events = SnapshotInTimeOrder(events, static item => item.OccurredTimeUtc, static item => item.SequenceNo);
            CapturedAt = capturedAt;
            (DurationItems, Summary) = FlowExecutionAnalysisPresentation.BuildOverviewFromOrderedRecords(
                Records,
                capturedAt,
                slowNodeThresholdMs,
                _messagesByExecution);
            PhaseSummary =
                FlowExecutionAnalysisPresentation.BuildPhaseSummaryFromOrderedEvents(Events);
        }

        public int BatchId { get; }
        internal FlowAnalysisDataSource DataSource { get; }

        public string SerialNumber { get; }

        public MeasureBatchModel? Batch { get; }

        public FlowRunRecord? Run { get; }

        public IReadOnlyList<FlowNodeRecord> Records { get; }

        public IReadOnlyList<FlowNodeMessage> Messages { get; }

        public IReadOnlyList<FlowExecutionEvent> Events { get; }

        public IReadOnlyList<FlowNodeDurationAnalysis> DurationItems { get; }

        public FlowExecutionAnalysisSummary Summary { get; }

        public FlowExecutionPhaseSummary PhaseSummary { get; }

        public DateTime CapturedAt { get; }

        public long WallClockMs => Batch?.TotalTime > 0
            ? Batch.TotalTime
            : Run?.ElapsedMs > 0
                ? Run.ElapsedMs
                : Summary.WallClockMs;

        public long NodeInactiveMs => Math.Max(0, WallClockMs - Summary.ActiveMs);

        public long OtherExecutionMs => Math.Max(
            0,
            NodeInactiveMs - (PhaseSummary.PreProcessMs ?? 0));

        public FlowNodeRecord? FindRecord(int? recordId)
        {
            return recordId.HasValue
                ? Records.FirstOrDefault(item => item.Id == recordId.Value)
                : null;
        }

        public FlowNodeDurationAnalysis? FindDuration(FlowNodeRecord record)
        {
            return DurationItems.FirstOrDefault(item => IsSameRecord(item.Record, record));
        }

        public IReadOnlyList<FlowNodeMessage> GetMessages(FlowNodeRecord record)
        {
            return _messagesByExecution.GetMessages(record);
        }

        public FlowNodeRecord? GetAdjacentRecord(FlowNodeRecord record, int offset)
        {
            if (Records.Count == 0)
                return null;

            int currentIndex = Records
                .Select((item, index) => (item, index))
                .Where(pair => IsSameRecord(pair.item, record))
                .Select(pair => pair.index)
                .DefaultIfEmpty(-1)
                .First();
            if (currentIndex < 0)
                return null;

            int targetIndex = (currentIndex + offset + Records.Count) % Records.Count;
            return Records[targetIndex];
        }

        private static List<T> SnapshotInTimeOrder<T>(IReadOnlyList<T> source, Func<T, DateTime> time, Func<T, long> tie)
        {
            var snapshot = source.ToList();
            // Keep database rows in their existing order when their times and tie breaks already match.
            for (int index = 1; index < snapshot.Count; index++)
            {
                T previous = snapshot[index - 1];
                T current = snapshot[index];
                int order = time(previous).CompareTo(time(current));
                if (order > 0 || (order == 0 && tie(previous) > tie(current)))
                    return snapshot.OrderBy(time).ThenBy(tie).ToList();
            }
            return snapshot;
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
