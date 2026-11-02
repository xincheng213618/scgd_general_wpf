namespace ProjectARVRPro.Exports;

internal sealed record CustomTestResultExportOptions(
    bool Enabled, CustomTestResultOutputProfile Profile, string Directory, string CsvDirectory,
    string ProjectName, bool SaveByDate, bool AutoCleanupEnabled, int MinimumFreeSpaceGB)
{
    internal static CustomTestResultExportOptions Capture(ViewResultManagerConfig config) => new(
        config.IsSaveCustomXlsx, config.CustomOutputProfile,
        string.IsNullOrWhiteSpace(config.CustomXlsxSavePath) ? config.CsvSavePath : config.CustomXlsxSavePath,
        config.CsvSavePath, config.CustomXlsxProjectName, config.SaveByDate, config.AutoCleanupEnabled, config.MinimumFreeSpaceGB);
}
