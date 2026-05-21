namespace ProjectARVRPro.Exports
{
    public sealed class ObjectiveTestResultExportContext
    {
        public required ObjectiveTestResult Result { get; init; }

        public required string SerialNumber { get; init; }

        public required string OutputDirectory { get; init; }

        public required string BaseFileName { get; init; }

        public DateTime ExportTime { get; init; } = DateTime.Now;
    }
}
