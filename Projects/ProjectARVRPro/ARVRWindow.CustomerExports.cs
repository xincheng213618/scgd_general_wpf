using ProjectARVRPro.Exports;
using ProjectARVRPro.Exports.Lux;
using ProjectARVRPro.ImageExport;
using System.Diagnostics;
using System.IO;

namespace ProjectARVRPro;

public partial class ARVRWindow
{
    private async Task<bool> ExportCustomerReportAsync(CustomTestResultExportOptions options, string serialNumber, DateTime exportTime,
        LuxCsvWriteContext? csvLog = null)
    {
        CustomTestResultOutputProfile profile = options.Profile;
        bool luxProfile = CustomTestResultExportService.IsLuxProfile(profile);
        if (!options.Enabled || (csvLog != null && !luxProfile))
            return false;

        string directory = options.Directory;
        if (luxProfile)
            csvLog ??= new(serialNumber, CurrentFlowResult?.BatchId, ProjectConfig.LuxMachineNo, ProcessManager.ActiveGroup?.Name, null);
        var elapsed = Stopwatch.StartNew();
        string outputPath = directory;
        try
        {
            var context = new ObjectiveTestResultExportContext
            {
                Result = ObjectiveTestResult,
                SerialNumber = serialNumber,
                OutputDirectory = directory,
                BaseFileName = BuildDailyCustomXlsxBaseFileName(exportTime, options.ProjectName),
                ExportTime = exportTime,
                LuxMtfFormat = options.LuxMtfFormat,
                LuxLuminanceUniformityFormat = options.LuxLuminanceUniformityFormat,
                FlowResults = luxProfile
                    ? ViewResultManager.GetObjectiveTestFlowResults(ObjectiveTestResultRecordId)
                    : Array.Empty<ProjectARVRReuslt>()
            };
            outputPath = CustomTestResultExportService.GetOutputPath(context, profile);
            outputPath = Path.GetFullPath(outputPath);
            string currentSnDirectory = ProjectImageExportService.BuildOutputDirectory(options.CsvDirectory, options.SaveByDate, exportTime, serialNumber);
            using var write = ResultStorageSpaceManager.Instance.BeginWrite(directory, options.AutoCleanupEnabled, options.MinimumFreeSpaceGB, currentSnDirectory, outputPath);
            await write.EnsureSpaceAsync();
            string savedPath = CustomTestResultExportService.Export(context, profile);
            outputPath = Path.GetFullPath(savedPath);
        }
        catch (Exception ex)
        {
            csvLog?.LogResult(outputPath, elapsed.ElapsedMilliseconds, ex);
            throw;
        }
        if (csvLog == null) log.Info($"客户报表导出完成:{outputPath}");
        else csvLog.LogResult(outputPath, elapsed.ElapsedMilliseconds);
        return true;
    }
}
