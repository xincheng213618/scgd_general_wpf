using ProjectARVRPro.Exports.Lux;
using ProjectARVRPro.Process.Uniformity;

namespace ProjectARVRPro.Exports;

internal sealed record CustomTestResultExportOptions(
    bool Enabled, CustomTestResultOutputProfile Profile, string Directory, string CsvDirectory,
    string ProjectName, bool SaveByDate, bool AutoCleanupEnabled, int MinimumFreeSpaceGB,
    LuxMtfPointFormat LuxMtfFormat, UniformityDisplayFormat LuxLuminanceUniformityFormat)
{
    internal static CustomTestResultExportOptions Capture(ViewResultManagerConfig config, bool useResultDirectory = false) => new(
        config.IsSaveCustomXlsx, config.CustomOutputProfile,
        useResultDirectory || string.IsNullOrWhiteSpace(config.CustomXlsxSavePath) ? config.CsvSavePath : config.CustomXlsxSavePath,
        config.CsvSavePath, config.CustomXlsxProjectName, config.SaveByDate, config.AutoCleanupEnabled, config.MinimumFreeSpaceGB,
        config.LuxMtfFormat, config.LuxLuminanceUniformityFormat);
}
