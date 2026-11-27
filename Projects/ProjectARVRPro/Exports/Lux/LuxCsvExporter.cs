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
        IEnumerable<string> lines = LuxCsvProjector.CollectRows(context.Result, format, context.FlowResults, context.LuxMtfFormat, context.LuxLuminanceUniformityFormat)
            .Select(static row => row.ToCsvLine()).Prepend(ProjectARVRResultCsvExporter.Header);
        AtomicResultFile.Write(path, stagingPath => File.WriteAllLines(stagingPath, lines));
        return path;
    }
}
