namespace ProjectARVRPro.Exports
{
    public interface ITestResultCustomExporter
    {
        CustomTestResultOutputProfile Profile { get; }

        string FileSuffix { get; }

        string Export(ObjectiveTestResultExportContext context);
    }
}
