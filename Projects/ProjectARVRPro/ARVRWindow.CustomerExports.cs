using ProjectARVRPro.Exports;
using ProjectARVRPro.ImageExport;

namespace ProjectARVRPro;

public partial class ARVRWindow
{
    private async Task<bool> ExportCustomerReportAsync(CustomTestResultExportOptions options, string serialNumber, DateTime exportTime, bool luxFlow)
    {
        CustomTestResultOutputProfile profile = options.Profile;
        if (!options.Enabled || (luxFlow && !CustomTestResultExportService.IsLuxProfile(profile)))
            return false;

        string directory = options.Directory;
        var context = new ObjectiveTestResultExportContext
        {
            Result = ObjectiveTestResult,
            SerialNumber = serialNumber,
            OutputDirectory = directory,
            BaseFileName = BuildDailyCustomXlsxBaseFileName(exportTime, options.ProjectName),
            ExportTime = exportTime,
            LuxMtfFormat = options.LuxMtfFormat,
            LuxLuminanceUniformityFormat = options.LuxLuminanceUniformityFormat,
            FlowResults = CustomTestResultExportService.IsLuxProfile(profile)
                ? ViewResultManager.GetObjectiveTestFlowResults(ObjectiveTestResultRecordId)
                : Array.Empty<ProjectARVRReuslt>()
        };
        string outputPath = CustomTestResultExportService.GetOutputPath(context, profile);
        string currentSnDirectory = ProjectImageExportService.BuildOutputDirectory(options.CsvDirectory, options.SaveByDate, exportTime, serialNumber);
        using var write = ResultStorageSpaceManager.Instance.BeginWrite(directory, options.AutoCleanupEnabled, options.MinimumFreeSpaceGB, currentSnDirectory, outputPath);
        await write.EnsureSpaceAsync();
        string savedPath = CustomTestResultExportService.Export(context, profile);
        log.Info($"客户报表导出完成:{savedPath}");
        return true;
    }
}
