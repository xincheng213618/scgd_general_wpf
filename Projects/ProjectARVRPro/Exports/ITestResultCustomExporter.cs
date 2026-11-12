namespace ProjectARVRPro.Exports
{
    public interface ITestResultCustomExporter
    {
        CustomTestResultOutputProfile Profile { get; }

        string FileSuffix { get; }

        string GetFileName(ObjectiveTestResultExportContext context);

        string Export(ObjectiveTestResultExportContext context);
    }
}
