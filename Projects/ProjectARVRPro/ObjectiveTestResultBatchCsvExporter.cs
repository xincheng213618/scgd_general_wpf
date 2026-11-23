using Newtonsoft.Json;
using System.Globalization;
using System.IO;
using System.Text;

namespace ProjectARVRPro
{
    public static class ObjectiveTestResultBatchCsvExporter
    {
        private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
        private static readonly string[] FixedHeaders = ["SN", "开始时间", "结束时间"];

        public static void ExportToCsv(IEnumerable<ObjectiveTestResultRecord> records, string filePath)
            => ExportToCsv(records, new Dictionary<int, IReadOnlyList<ProjectARVRReuslt>>(), filePath);

        public static void ExportToCsv(
            IEnumerable<ObjectiveTestResultRecord> records,
            IReadOnlyDictionary<int, IReadOnlyList<ProjectARVRReuslt>> flowResultsByRecordId,
            string filePath)
        {
            ArgumentNullException.ThrowIfNull(records);
            ArgumentNullException.ThrowIfNull(flowResultsByRecordId);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            List<ObjectiveTestResultRecord> recordList = records.ToList();
            AtomicResultFile.Write(filePath, path => WriteCsv(recordList,
                index => new FlowRow(flowResultsByRecordId.GetValueOrDefault(recordList[index].Id), null, null), path));
        }

        internal readonly record struct FlowRow(
            IReadOnlyList<ProjectARVRReuslt>? Results,
            ProjectARVRResultCsvExporter.FlowCsvReader? Reader,
            ResultJsonPayloadStorage.BatchViewPayloads? Payloads);

        internal static void WriteCsv(
            IReadOnlyList<ObjectiveTestResultRecord> records,
            Func<int, FlowRow> getFlowRow,
            string filePath,
            Func<ObjectiveTestResultRecord, string?>? getFallbackSummary = null)
        {
            var fallbackReader = new ProjectARVRResultCsvExporter.FlowCsvReader();
            IReadOnlyList<ObjectiveTestResultMetric> columns = Array.Empty<ObjectiveTestResultMetric>();
            int headerSourceIndex = 0;
            for (; headerSourceIndex < records.Count; headerSourceIndex++)
            {
                columns = CollectMetrics(records[headerSourceIndex], getFlowRow(headerSourceIndex), fallbackReader, getFallbackSummary);
                if (columns.Count > 0)
                    break;
            }

            using var writer = new StreamWriter(filePath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            writer.NewLine = "\r\n";
            WriteRow(writer, FixedHeaders.Concat(columns.Select(column => column.Header)));

            Dictionary<string, string>? values = null;
            for (int index = 0; index < records.Count; index++)
            {
                ObjectiveTestResultRecord record = records[index];
                var row = new List<string>(3 + columns.Count)
                {
                    record.SN ?? string.Empty,
                    record.CreateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture),
                    record.UpdateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture),
                };

                if (index < headerSourceIndex)
                {
                    row.AddRange(Enumerable.Repeat(string.Empty, columns.Count));
                }
                else if (index == headerSourceIndex)
                {
                    row.AddRange(columns.Select(column => column.Value));
                }
                else
                {
                    values ??= new Dictionary<string, string>(columns.Count, StringComparer.OrdinalIgnoreCase);
                    values.Clear();
                    CollectMetricValues(record, getFlowRow(index), fallbackReader, getFallbackSummary, values);
                    foreach (ObjectiveTestResultMetric column in columns)
                        row.Add(values.TryGetValue(column.Key, out string? value) ? value : string.Empty);
                }

                WriteRow(writer, row);
            }
        }

        private static IReadOnlyList<ObjectiveTestResultMetric> CollectMetrics(
            ObjectiveTestResultRecord record,
            FlowRow flowRow,
            ProjectARVRResultCsvExporter.FlowCsvReader fallbackReader,
            Func<ObjectiveTestResultRecord, string?>? getFallbackSummary)
        {
            if (flowRow.Results is { } flowResults)
            {
                try
                {
                    flowRow.Payloads?.Decode(flowResults);
                    IReadOnlyList<ObjectiveTestResultMetric> flowMetrics = (flowRow.Reader ?? fallbackReader).CollectMetrics(flowResults);
                    if (flowMetrics.Count > 0)
                        return flowMetrics;
                }
                finally
                {
                    flowRow.Payloads?.Release(flowResults);
                }
            }

            return CollectSummaryMetrics(record, getFallbackSummary);
        }

        private static void CollectMetricValues(
            ObjectiveTestResultRecord record,
            FlowRow flowRow,
            ProjectARVRResultCsvExporter.FlowCsvReader fallbackReader,
            Func<ObjectiveTestResultRecord, string?>? getFallbackSummary,
            Dictionary<string, string> values)
        {
            if (flowRow.Results is { } flowResults)
            {
                try
                {
                    flowRow.Payloads?.Decode(flowResults);
                    (flowRow.Reader ?? fallbackReader).CollectMetricValues(flowResults, values);
                    if (values.Count > 0)
                        return;
                }
                finally
                {
                    flowRow.Payloads?.Release(flowResults);
                }
            }

            foreach (ObjectiveTestResultMetric metric in CollectSummaryMetrics(record, getFallbackSummary))
                values.Add(metric.Key, metric.Value);
        }

        private static IReadOnlyList<ObjectiveTestResultMetric> CollectSummaryMetrics(
            ObjectiveTestResultRecord record, Func<ObjectiveTestResultRecord, string?>? getFallbackSummary)
        {
            string? json = getFallbackSummary == null ? record.ObjectiveTestResultJson : getFallbackSummary(record);
            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<ObjectiveTestResultMetric>();

            try
            {
                ObjectiveTestResult? result = JsonConvert.DeserializeObject<ObjectiveTestResult>(json);
                return result == null
                    ? Array.Empty<ObjectiveTestResultMetric>()
                    : ObjectiveTestResultMetricCollector.Collect(result);
            }
            catch (JsonException)
            {
                return Array.Empty<ObjectiveTestResultMetric>();
            }
        }

        private static void WriteRow(TextWriter writer, IEnumerable<string> fields)
        {
            writer.WriteLine(string.Join(",", fields.Select(ObjectiveTestCsvRow.EscapeCsvField)));
        }
    }
}
