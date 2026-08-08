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
        {
            ArgumentNullException.ThrowIfNull(records);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            List<ObjectiveTestResultRecord> recordList = records.ToList();
            IReadOnlyList<ObjectiveTestResultMetric> columns = recordList.Count == 0
                ? Array.Empty<ObjectiveTestResultMetric>()
                : CollectMetrics(recordList[0]);

            using var writer = new StreamWriter(filePath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            writer.NewLine = "\r\n";
            WriteRow(writer, FixedHeaders.Concat(columns.Select(column => column.Header)));

            foreach (ObjectiveTestResultRecord record in recordList)
            {
                Dictionary<string, string> values = CollectMetrics(record)
                    .ToDictionary(metric => metric.Key, metric => metric.Value, StringComparer.OrdinalIgnoreCase);

                var row = new List<string>(3 + columns.Count)
                {
                    record.SN ?? string.Empty,
                    record.CreateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture),
                    record.UpdateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture),
                };

                foreach (ObjectiveTestResultMetric column in columns)
                    row.Add(values.TryGetValue(column.Key, out string? value) ? value : string.Empty);

                WriteRow(writer, row);
            }
        }

        private static IReadOnlyList<ObjectiveTestResultMetric> CollectMetrics(ObjectiveTestResultRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.ObjectiveTestResultJson))
                return Array.Empty<ObjectiveTestResultMetric>();

            try
            {
                ObjectiveTestResult? result = JsonConvert.DeserializeObject<ObjectiveTestResult>(record.ObjectiveTestResultJson);
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
