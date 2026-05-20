namespace ProjectARVRPro.Exports
{
    public interface ITestResultCustomExporter
    {
        string ProfileName { get; }

        string FileSuffix { get; }

        string Export(ObjectiveTestResultExportContext context);
    }
}
