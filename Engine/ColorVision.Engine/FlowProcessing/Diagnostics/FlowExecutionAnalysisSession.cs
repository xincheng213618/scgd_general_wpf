using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    internal enum FlowAnalysisPageKind
    {
        Overview,
        Node,
        Messages
    }

    internal readonly record struct FlowAnalysisNavigationState(
        FlowAnalysisPageKind PageKind,
        int BatchId,
        string SerialNumber,
        int? RecordId = null,
        int? MessageId = null);

    internal sealed class FlowExecutionAnalysisSession
    {
        internal FlowExecutionAnalysisSession(
            int batchId,
            string? serialNumber,
            MeasureBatchModel? batch,
            FlowRunRecord? run,
            IReadOnlyList<FlowNodeRecord> records,
            IReadOnlyList<FlowNodeMessage> messages,
            IReadOnlyList<FlowExecutionEvent> events,
            DateTime capturedAt,
            long slowNodeThresholdMs)
        {
            BatchId = batchId;
            SerialNumber = serialNumber ?? string.Empty;
            Batch = batch;
            Run = run;
            Records = records
                .OrderBy(item => item.StartTime)
                .ThenBy(item => item.Id)
                .ToList();
            Messages = messages
                .OrderBy(item => item.SendTime)
                .ThenBy(item => item.Id)
                .ToList();
            Events = events
                .OrderBy(item => item.OccurredTimeUtc)
                .ThenBy(item => item.SequenceNo)
                .ToList();
            CapturedAt = capturedAt;
            DurationItems = FlowExecutionAnalysisPresentation.BuildDurationItems(
                Records,
                capturedAt,
                slowNodeThresholdMs,
                Messages);
            Summary = FlowExecutionAnalysisPresentation.BuildSummary(
                Records,
                DurationItems,
                capturedAt);
            PhaseSummary =
                FlowExecutionAnalysisPresentation.BuildPhaseSummary(Events);
        }

        public int BatchId { get; }

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
            return FlowExecutionAnalysisPresentation.GetMessagesForNodeExecution(record, Messages, Records);
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
