using ProjectARVRPro.Exports.Lux;
using ProjectARVRPro.Process.Uniformity;

namespace ProjectARVRPro.Exports
{
    public sealed class ObjectiveTestResultExportContext
    {
        public required ObjectiveTestResult Result { get; init; }

        public required string SerialNumber { get; init; }

        public required string OutputDirectory { get; init; }

        public required string BaseFileName { get; init; }

        public DateTime ExportTime { get; init; } = DateTime.Now;

        public LuxMtfPointFormat LuxMtfFormat { get; init; } = LuxMtfPointFormat.Mtf058;

        public UniformityDisplayFormat LuxLuminanceUniformityFormat { get; init; } = UniformityDisplayFormat.Percentage;

        public IReadOnlyList<ProjectARVRReuslt> FlowResults { get; init; } = Array.Empty<ProjectARVRReuslt>();
    }
}
