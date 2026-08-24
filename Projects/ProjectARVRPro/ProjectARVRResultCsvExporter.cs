using ProjectARVRPro.Process;
using System.IO;

namespace ProjectARVRPro;

public static class ProjectARVRResultCsvExporter
{
    public const string Header = "Test_Screen,Test_item,Test_Value,unit,lower_limit,upper_limit,Test_Result";

    public static IReadOnlyList<ObjectiveTestCsvRow> CollectRows(IEnumerable<ProjectARVRReuslt> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        ProcessManager processManager = ProcessManager.GetInstance();
        var rows = new List<ObjectiveTestCsvRow>();
        foreach (ProjectARVRReuslt result in results.OrderBy(item => item.Id))
        {
            // Exact replay requires the process and config snapshots captured with the row.
            // Older rows fall back to the aggregate ObjectiveTestResult exporter at the caller.
            if (string.IsNullOrWhiteSpace(result.ViewResultJson) ||
                string.IsNullOrWhiteSpace(result.ProcessTypeFullName) ||
                string.IsNullOrWhiteSpace(result.ProcessConfigJson))
                continue;

            IProcess? process = ResultProcessResolver.Resolve(
                result,
                processManager.Processes,
                processManager.GetResultProcessMappings());
            if (process == null)
                continue;

            rows.AddRange(process.GetObjectiveCsvRows(result));
        }

        return rows;
    }

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
    {
        var metrics = new List<ObjectiveTestResultMetric>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ObjectiveTestCsvRow row in CollectRows(results))
        {
            if (keys.Add(row.MetricKey))
                metrics.Add(new ObjectiveTestResultMetric(row.MetricKey, row.MetricHeader, row.MetricValue));
        }
        return metrics;
    }
}
