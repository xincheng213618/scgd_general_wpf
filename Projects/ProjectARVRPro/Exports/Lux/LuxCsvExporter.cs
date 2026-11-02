using ColorVision.Common.Utilities;
using System.IO;

namespace ProjectARVRPro.Exports.Lux;

public enum LuxCsvFormat { Current, Legacy }

public sealed class LuxCsvExporter(LuxCsvFormat format) : ITestResultCustomExporter
{
    public CustomTestResultOutputProfile Profile => format == LuxCsvFormat.Legacy
        ? CustomTestResultOutputProfile.LUX_Legacy_CSV : CustomTestResultOutputProfile.LUX_Current_CSV;

    public string FileSuffix => string.Empty;

    public string GetFileName(ObjectiveTestResultExportContext context) =>
        Tool.SanitizeFileName($"C_{context.SerialNumber}.csv");

    public string Export(ObjectiveTestResultExportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Directory.CreateDirectory(context.OutputDirectory);
        string path = Path.Combine(context.OutputDirectory, GetFileName(context));
        var lines = new List<string> { ProjectARVRResultCsvExporter.Header };
        foreach (ObjectiveTestCsvRow row in LuxCsvProjector.CollectRows(context.Result, format, context.FlowResults))
            lines.Add(string.Join(",", new[] { row.TestScreen, row.TestItem, row.TestValue, row.Unit, row.LowerLimit, row.UpperLimit, row.TestResult }.Select(Escape)));
        File.WriteAllLines(path, lines);
        return path;
    }

    private static string Escape(string value) => value.IndexOfAny([',', '"', '\r', '\n']) < 0
        ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
}
