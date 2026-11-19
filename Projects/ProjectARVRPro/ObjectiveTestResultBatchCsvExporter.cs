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
            AtomicCsvFile.Write(filePath, path =>
                WriteCsv(recordList, index => flowResultsByRecordId.GetValueOrDefault(recordList[index].Id), path));
        }

        internal static void WriteCsv(
            IReadOnlyList<ObjectiveTestResultRecord> recordList,
            Func<int, IReadOnlyList<ProjectARVRReuslt>?> getFlowResults,
            string filePath,
            Func<ObjectiveTestResultRecord, string?>? getFallbackSummary = null)
        {
            var flowReader = new ProjectARVRResultCsvExporter.FlowCsvReader();
            IReadOnlyList<ObjectiveTestResultMetric> columns = Array.Empty<ObjectiveTestResultMetric>();
            int headerSourceIndex = 0;
            for (; headerSourceIndex < recordList.Count; headerSourceIndex++)
            {
                columns = CollectMetrics(recordList[headerSourceIndex], getFlowResults(headerSourceIndex), flowReader, getFallbackSummary);
                if (columns.Count > 0)
                    break;
            }

            using var writer = new StreamWriter(filePath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            writer.NewLine = "\r\n";
            WriteRow(writer, FixedHeaders.Concat(columns.Select(column => column.Header)));

            Dictionary<string, string>? values = null;
            for (int index = 0; index < recordList.Count; index++)
            {
                ObjectiveTestResultRecord record = recordList[index];
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

                    if (getFlowResults(index) is { } flowResults)
                        flowReader.CollectMetricValues(flowResults, values);
                    if (values.Count == 0)
                    {
                        string? json = getFallbackSummary == null ? record.ObjectiveTestResultJson : getFallbackSummary(record);
                        foreach (ObjectiveTestResultMetric metric in CollectSummaryMetrics(json))
                            values.Add(metric.Key, metric.Value);
                    }
                    foreach (ObjectiveTestResultMetric column in columns)
                        row.Add(values.TryGetValue(column.Key, out string? value) ? value : string.Empty);
                }

                WriteRow(writer, row);
            }
        }

        private static IReadOnlyList<ObjectiveTestResultMetric> CollectMetrics(
            ObjectiveTestResultRecord record,
            IReadOnlyList<ProjectARVRReuslt>? flowResults,
            ProjectARVRResultCsvExporter.FlowCsvReader flowReader,
            Func<ObjectiveTestResultRecord, string?>? getFallbackSummary)
        {
            if (flowResults != null)
            {
                IReadOnlyList<ObjectiveTestResultMetric> flowMetrics = flowReader.CollectMetrics(flowResults);
                if (flowMetrics.Count > 0)
                    return flowMetrics;
            }

            string? json = getFallbackSummary == null ? record.ObjectiveTestResultJson : getFallbackSummary(record);
            return CollectSummaryMetrics(json);
        }

        private static IReadOnlyList<ObjectiveTestResultMetric> CollectSummaryMetrics(string? json)
        {
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
            writer.WriteLine(string.Join(",", fields.Select(EscapeCsv)));
        }

        private static string EscapeCsv(string? field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            if (!field.Contains(',') && !field.Contains('"') && !field.Contains('\r') && !field.Contains('\n'))
                return field;

            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
    }
}
