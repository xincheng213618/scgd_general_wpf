using Newtonsoft.Json;
using ProjectARVRPro.Process;
using System.IO;

namespace ProjectARVRPro;

public static class ProjectARVRResultCsvExporter
{
    public const string Header = "Test_Screen,Test_item,Test_Value,unit,lower_limit,upper_limit,Test_Result";

    internal static string BuildFileName(string? serialNumber, DateTime timestamp) =>
        ColorVision.Common.Utilities.Tool.SanitizeFileName($"TestResults_{serialNumber}_{timestamp:yyyyMMdd_HHmmss}_.csv");

    public static IReadOnlyList<ObjectiveTestCsvRow> CollectRows(IEnumerable<ProjectARVRReuslt> results)
        => new FlowCsvReader().CollectRows(results);

    public static void ExportToCsv(IEnumerable<ProjectARVRReuslt> results, string filePath)
    {
        ExportRows(CollectRows(results), filePath);
    }

    public static void ExportRows(IEnumerable<ObjectiveTestCsvRow> rows, string filePath)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        File.WriteAllLines(filePath, new[] { Header }.Concat(rows.Select(row => row.ToCsvLine())));
    }

    public static IReadOnlyList<ObjectiveTestResultMetric> CollectMetrics(IEnumerable<ProjectARVRReuslt> results)
        => new FlowCsvReader().CollectMetrics(results);

    internal static FlowCsvReader? CreateReaderForTransientPayloads(IEnumerable<ProjectARVRReuslt> results)
    {
        if (JsonConvert.DefaultSettings != null)
            return null;

        // Resolve against the same templates used to check whether an extractor only borrows the result.
        IProcess[] templates = ProcessManager.GetInstance().Processes.ToArray();
        var supportedTypes = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (IProcess template in templates)
        {
            Type type = template.GetType();
            if (type.FullName is string name)
                supportedTypes.TryAdd(name, ResultProcessResolver.IsReadOnlyCsvExtractor(type));
        }

        return results.All(result => string.IsNullOrWhiteSpace(result.ProcessTypeFullName)
            || string.IsNullOrWhiteSpace(result.ProcessConfigJson) || supportedTypes.GetValueOrDefault(result.ProcessTypeFullName))
            ? new FlowCsvReader(templates) : null;
    }

    internal sealed class FlowCsvReader(IProcess[]? templates = null)
    {
        private readonly Dictionary<(string ProcessType, string? ConfigJson), IProcess> snapshots = [];

        internal IReadOnlyList<ObjectiveTestCsvRow> CollectRows(IEnumerable<ProjectARVRReuslt> results)
        {
            ArgumentNullException.ThrowIfNull(results);

            ProcessManager processManager = ProcessManager.GetInstance();
            IEnumerable<IProcess> processTemplates = templates ?? (IEnumerable<IProcess>)processManager.Processes;
            var rows = new List<ObjectiveTestCsvRow>();
            foreach (ProjectARVRReuslt result in results.OrderBy(item => item.Id))
            {
                // Exact replay requires the process and config snapshots captured with the row.
                // Older rows fall back to the aggregate ObjectiveTestResult exporter at the caller.
                if ((result.CsvJsonReader == null && string.IsNullOrWhiteSpace(result.ViewResultJson)) ||
                    string.IsNullOrWhiteSpace(result.ProcessTypeFullName) ||
                    string.IsNullOrWhiteSpace(result.ProcessConfigJson))
                    continue;

                IProcess? process = ResultProcessResolver.Resolve(result, processTemplates, processManager.GetResultProcessMappings(), snapshots);
                if (process != null)
                    rows.AddRange(process.GetObjectiveCsvRows(result));
            }

            return rows;
        }

        internal IReadOnlyList<ObjectiveTestResultMetric> CollectMetrics(IEnumerable<ProjectARVRReuslt> results)
        {
            var metrics = new List<ObjectiveTestResultMetric>();
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectiveTestCsvRow row in CollectRows(results))
            {
                string key = row.MetricKey;
                if (keys.Add(key))
                    metrics.Add(new ObjectiveTestResultMetric(key, row.MetricHeader, row.MetricValue));
            }
            return metrics;
        }

        internal void CollectMetricValues(IEnumerable<ProjectARVRReuslt> results, Dictionary<string, string> values)
        {
            foreach (ObjectiveTestCsvRow row in CollectRows(results))
                values.TryAdd(row.MetricKey, row.MetricValue);
        }
    }
}
