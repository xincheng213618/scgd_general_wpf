namespace ProjectARVRPro
{
    public enum ResultTimelinePhaseKind
    {
        ExternalPictureSwitch,
        DevicePictureSwitch,
        FlowPreparation,
        PreProcessing,
        FlowExecution,
        ResultProcessing,
        Unattributed,
    }

    public sealed class ResultTimelineSegment
    {
        public ResultTimelinePhaseKind Kind { get; init; }
        public double StartUnits { get; init; }
        public double WidthUnits { get; init; }
        public double DurationMilliseconds { get; init; }
        public string ToolTip { get; init; } = string.Empty;
    }

    public sealed class ResultTimelineFlowRow
    {
        public int FlowSequence { get; init; }
        public string FlowName { get; init; } = string.Empty;
        public string Label => $"{FlowSequence:00}  {FlowName}";
        public string ExecutionTimeText { get; init; } = string.Empty;
        public IReadOnlyList<ResultTimelineSegment> Segments { get; init; } = [];
    }

    public sealed class ResultTimelinePresentation
    {
        public IReadOnlyList<ResultTimelineFlowRow> Rows { get; init; } = [];
        public string SummaryText { get; init; } = string.Empty;
        public string NoteText { get; init; } = string.Empty;
        public string StartTimeText { get; init; } = string.Empty;
        public string MiddleTimeText { get; init; } = string.Empty;
        public string EndTimeText { get; init; } = string.Empty;
        public bool HasMeasuredPhases { get; init; }
    }

    public static class ResultTimelineBuilder
    {
        private const double TimelineUnits = 1000;

        public static ResultTimelinePresentation Build(
            ResultStatisticsRecordRow record,
            IEnumerable<ProjectARVRReuslt> flowDetails)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(flowDetails);

            DateTime startTime = record.StartTime;
            DateTime endTime = record.EndTime >= startTime ? record.EndTime : startTime;
            double totalMilliseconds = (endTime - startTime).TotalMilliseconds;
            if (totalMilliseconds <= 0)
            {
                return new ResultTimelinePresentation
                {
                    SummaryText = "CT 0.000 s",
                    NoteText = "当前记录没有可绘制的时间范围。",
                    StartTimeText = startTime.ToString("HH:mm:ss.fff"),
                    MiddleTimeText = startTime.ToString("HH:mm:ss.fff"),
                    EndTimeText = endTime.ToString("HH:mm:ss.fff"),
                };
            }

            List<ProjectARVRReuslt> details = flowDetails.OrderBy(item => item.Id).ThenBy(item => item.CreateTime).ToList();
            bool hasMeasuredPhases = details.Any(HasMeasuredPhase);
            List<ResultTimelineFlowRow> rows = hasMeasuredPhases
                ? BuildMeasuredRows(startTime, endTime, details)
                : BuildLegacyRows(startTime, endTime, details);

            List<ResultTimelineSegment> segments = rows.SelectMany(item => item.Segments).ToList();
            double flowMilliseconds = SumDuration(segments, ResultTimelinePhaseKind.FlowExecution);
            double switchMilliseconds = SumDuration(
                segments,
                ResultTimelinePhaseKind.ExternalPictureSwitch,
                ResultTimelinePhaseKind.DevicePictureSwitch);
            double knownNonFlowMilliseconds = SumDuration(
                segments,
                ResultTimelinePhaseKind.FlowPreparation,
                ResultTimelinePhaseKind.PreProcessing,
                ResultTimelinePhaseKind.ResultProcessing);
            double unattributedMilliseconds = SumDuration(segments, ResultTimelinePhaseKind.Unattributed);
            double pgCycleMilliseconds = 0;
            int measuredPgCycleCount = 0;
            foreach (ProjectARVRReuslt detail in details)
            {
                if (TryGetDuration(detail.SwitchRequestedAt, detail.FlowCompletedAt, out double durationMilliseconds))
                {
                    pgCycleMilliseconds += durationMilliseconds;
                    measuredPgCycleCount++;
                }
            }

            bool hasCompletePgCycles = details.Count > 0 && measuredPgCycleCount == details.Count;
            string summary = hasMeasuredPhases && hasCompletePgCycles
                ? $"CT {Format(totalMilliseconds)} · PG→执行结束 {Format(pgCycleMilliseconds)}（含执行 {Format(flowMilliseconds)}） · 执行后/空档 {Format(Math.Max(0, totalMilliseconds - pgCycleMilliseconds))}"
                : hasMeasuredPhases
                    ? $"CT {Format(totalMilliseconds)} · 流程 {Format(flowMilliseconds)} · PG应答/切图 {Format(switchMilliseconds)} · 其余处理/空档 {Format(knownNonFlowMilliseconds + unattributedMilliseconds)}"
                    : $"CT {Format(totalMilliseconds)} · 流程 {Format(flowMilliseconds)} · 非流程间隔 {Format(Math.Max(0, totalMilliseconds - flowMilliseconds))}";

            return new ResultTimelinePresentation
            {
                Rows = rows,
                SummaryText = summary,
                NoteText = hasMeasuredPhases
                    ? "PG 周期按发送到该流程执行结束统计；蓝、黄、绿分别显示其中的应答/切图、准备和执行子段，紫色是执行结束后的处理与保存。"
                    : "旧记录仅能由流程结束时间和耗时推算绿色执行段；灰色包含切图、预处理、结果处理及等待，不能再细分。",
                StartTimeText = startTime.ToString("HH:mm:ss.fff"),
                MiddleTimeText = startTime.AddMilliseconds(totalMilliseconds / 2).ToString("HH:mm:ss.fff"),
                EndTimeText = endTime.ToString("HH:mm:ss.fff"),
                HasMeasuredPhases = hasMeasuredPhases,
            };
        }

        private static List<ResultTimelineFlowRow> BuildMeasuredRows(
            DateTime batchStart,
            DateTime batchEnd,
            List<ProjectARVRReuslt> flowDetails)
        {
            var rows = new List<ResultTimelineFlowRow>();
            DateTime cursor = batchStart;
            for (int index = 0; index < flowDetails.Count; index++)
            {
                ProjectARVRReuslt detail = flowDetails[index];
                int sequence = index + 1;
                string flowName = string.IsNullOrWhiteSpace(detail.Model) ? $"流程 {sequence}" : detail.Model;
                List<(ResultTimelinePhaseKind Kind, DateTime Start, DateTime End)> flowPhases = GetMeasuredPhases(detail)
                    .OrderBy(item => item.Start)
                    .ThenBy(item => item.Kind)
                    .ToList();
                if (flowPhases.Count == 0)
                {
                    DateTime inferredEnd = Clamp(detail.CreateTime, batchStart, batchEnd);
                    DateTime inferredStart = Clamp(inferredEnd.AddMilliseconds(-Math.Max(0, detail.RunTime)), batchStart, batchEnd);
                    if (inferredEnd > inferredStart)
                        flowPhases.Add((ResultTimelinePhaseKind.FlowExecution, inferredStart, inferredEnd));
                }

                var segments = new List<ResultTimelineSegment>();
                DateTime flowCursor = cursor;
                foreach ((ResultTimelinePhaseKind kind, DateTime start, DateTime end) in flowPhases)
                {
                    DateTime phaseStart = Clamp(start, batchStart, batchEnd);
                    DateTime phaseEnd = Clamp(end, batchStart, batchEnd);
                    if (phaseEnd <= phaseStart)
                        continue;

                    if (phaseStart > flowCursor)
                        segments.Add(CreateSegment(ResultTimelinePhaseKind.Unattributed, flowCursor, phaseStart, batchStart, batchEnd));
                    segments.Add(CreateSegment(kind, phaseStart, phaseEnd, batchStart, batchEnd));
                    if (phaseEnd > flowCursor)
                        flowCursor = phaseEnd;
                }

                if (segments.Count == 0)
                    continue;

                cursor = flowCursor > cursor ? flowCursor : cursor;
                double executionMilliseconds = segments
                    .Where(item => item.Kind == ResultTimelinePhaseKind.FlowExecution)
                    .Sum(item => item.DurationMilliseconds);
                rows.Add(new ResultTimelineFlowRow
                {
                    FlowSequence = sequence,
                    FlowName = flowName,
                    ExecutionTimeText = Format(executionMilliseconds),
                    Segments = segments,
                });
            }

            if (cursor < batchEnd)
            {
                ResultTimelineSegment trailing = CreateSegment(ResultTimelinePhaseKind.Unattributed, cursor, batchEnd, batchStart, batchEnd);
                if (rows.Count == 0)
                {
                    rows.Add(new ResultTimelineFlowRow
                    {
                        FlowSequence = 1,
                        FlowName = "未记录流程",
                        ExecutionTimeText = "-",
                        Segments = [trailing],
                    });
                }
                else
                {
                    ResultTimelineFlowRow last = rows[^1];
                    rows[^1] = new ResultTimelineFlowRow
                    {
                        FlowSequence = last.FlowSequence,
                        FlowName = last.FlowName,
                        ExecutionTimeText = last.ExecutionTimeText,
                        Segments = last.Segments.Concat([trailing]).ToList(),
                    };
                }
            }

            return rows;
        }

        private static bool HasMeasuredPhase(ProjectARVRReuslt detail)
        {
            return GetMeasuredPhases(detail).Any();
        }

        private static IEnumerable<(ResultTimelinePhaseKind Kind, DateTime Start, DateTime End)> GetMeasuredPhases(ProjectARVRReuslt detail)
        {
            if (TryCreatePhase(detail.SwitchRequestedAt, detail.SwitchAcknowledgedAt, out DateTime start, out DateTime end))
                yield return (ResultTimelinePhaseKind.ExternalPictureSwitch, start, end);
            if (TryCreatePhase(detail.SwitchAcknowledgedAt, detail.PictureSwitchStartedAt, out start, out end))
                yield return (ResultTimelinePhaseKind.FlowPreparation, start, end);
            if (TryCreatePhase(detail.PictureSwitchStartedAt, detail.PictureSwitchCompletedAt, out start, out end))
                yield return (ResultTimelinePhaseKind.DevicePictureSwitch, start, end);
            if (TryCreatePhase(detail.PictureSwitchCompletedAt, detail.PreProcessingCompletedAt, out start, out end))
                yield return (ResultTimelinePhaseKind.PreProcessing, start, end);
            if (TryCreatePhase(detail.FlowStartedAt, detail.FlowCompletedAt, out start, out end))
                yield return (ResultTimelinePhaseKind.FlowExecution, start, end);
            if (TryCreatePhase(detail.FlowCompletedAt, detail.ResultProcessingCompletedAt, out start, out end))
                yield return (ResultTimelinePhaseKind.ResultProcessing, start, end);
        }

        private static bool TryCreatePhase(DateTime? start, DateTime? end, out DateTime phaseStart, out DateTime phaseEnd)
        {
            phaseStart = start ?? default;
            phaseEnd = end ?? default;
            return start.HasValue && end.HasValue && phaseEnd > phaseStart;
        }

        private static bool TryGetDuration(DateTime? start, DateTime? end, out double durationMilliseconds)
        {
            durationMilliseconds = 0;
            if (!start.HasValue || !end.HasValue || end.Value <= start.Value)
                return false;

            durationMilliseconds = (end.Value - start.Value).TotalMilliseconds;
            return true;
        }

        private static List<ResultTimelineFlowRow> BuildLegacyRows(
            DateTime batchStart,
            DateTime batchEnd,
            IEnumerable<ProjectARVRReuslt> flowDetails)
        {
            var rows = new List<ResultTimelineFlowRow>();
            DateTime cursor = batchStart;
            int sequence = 0;
            foreach (ProjectARVRReuslt detail in flowDetails.OrderBy(item => item.Id).ThenBy(item => item.CreateTime))
            {
                sequence++;
                DateTime flowEnd = Clamp(detail.CreateTime, batchStart, batchEnd);
                DateTime flowStart = Clamp(flowEnd.AddMilliseconds(-Math.Max(0, detail.RunTime)), batchStart, batchEnd);
                var segments = new List<ResultTimelineSegment>();
                if (flowStart > cursor)
                    segments.Add(CreateSegment(ResultTimelinePhaseKind.Unattributed, cursor, flowStart, batchStart, batchEnd));
                if (flowEnd > flowStart)
                    segments.Add(CreateSegment(ResultTimelinePhaseKind.FlowExecution, flowStart, flowEnd, batchStart, batchEnd));
                if (flowEnd > cursor)
                    cursor = flowEnd;

                rows.Add(new ResultTimelineFlowRow
                {
                    FlowSequence = sequence,
                    FlowName = string.IsNullOrWhiteSpace(detail.Model) ? $"流程 {sequence}" : detail.Model,
                    ExecutionTimeText = Format(Math.Max(0, detail.RunTime)),
                    Segments = segments,
                });
            }

            ResultTimelineSegment? trailing = cursor < batchEnd
                ? CreateSegment(ResultTimelinePhaseKind.Unattributed, cursor, batchEnd, batchStart, batchEnd)
                : null;
            if (rows.Count == 0)
            {
                rows.Add(new ResultTimelineFlowRow
                {
                    FlowSequence = 1,
                    FlowName = "未记录流程",
                    ExecutionTimeText = "-",
                    Segments = trailing == null ? [] : [trailing],
                });
            }
            else if (trailing != null)
            {
                ResultTimelineFlowRow last = rows[^1];
                rows[^1] = new ResultTimelineFlowRow
                {
                    FlowSequence = last.FlowSequence,
                    FlowName = last.FlowName,
                    ExecutionTimeText = last.ExecutionTimeText,
                    Segments = last.Segments.Concat([trailing]).ToList(),
                };
            }

            return rows;
        }

        private static ResultTimelineSegment CreateSegment(
            ResultTimelinePhaseKind kind,
            DateTime startTime,
            DateTime endTime,
            DateTime batchStart,
            DateTime batchEnd)
        {
            double totalMilliseconds = (batchEnd - batchStart).TotalMilliseconds;
            double durationMilliseconds = Math.Max(0, (endTime - startTime).TotalMilliseconds);
            double startUnits = Math.Clamp((startTime - batchStart).TotalMilliseconds / totalMilliseconds * TimelineUnits, 0, TimelineUnits);
            double widthUnits = Math.Min(TimelineUnits - startUnits, Math.Max(1.5, durationMilliseconds / totalMilliseconds * TimelineUnits));
            return new ResultTimelineSegment
            {
                Kind = kind,
                StartUnits = startUnits,
                WidthUnits = widthUnits,
                DurationMilliseconds = durationMilliseconds,
                ToolTip = $"{GetPhaseName(kind)} · {startTime:HH:mm:ss.fff} - {endTime:HH:mm:ss.fff} · {Format(durationMilliseconds)}",
            };
        }

        private static double SumDuration(IEnumerable<ResultTimelineSegment> segments, params ResultTimelinePhaseKind[] kinds)
        {
            HashSet<ResultTimelinePhaseKind> included = kinds.ToHashSet();
            return segments.Where(item => included.Contains(item.Kind)).Sum(item => item.DurationMilliseconds);
        }

        private static DateTime Clamp(DateTime value, DateTime minimum, DateTime maximum)
        {
            if (value < minimum)
                return minimum;
            return value > maximum ? maximum : value;
        }

        private static string GetPhaseName(ResultTimelinePhaseKind kind)
        {
            return kind switch
            {
                ResultTimelinePhaseKind.ExternalPictureSwitch => "PG发送→应答",
                ResultTimelinePhaseKind.DevicePictureSwitch => "设备切图",
                ResultTimelinePhaseKind.FlowPreparation => "流程准备",
                ResultTimelinePhaseKind.PreProcessing => "预处理",
                ResultTimelinePhaseKind.FlowExecution => "流程执行",
                ResultTimelinePhaseKind.ResultProcessing => "执行后处理/保存",
                _ => "未归因间隔",
            };
        }

        private static string Format(double milliseconds) => $"{Math.Max(0, milliseconds) / 1000d:F3} s";
    }
}
