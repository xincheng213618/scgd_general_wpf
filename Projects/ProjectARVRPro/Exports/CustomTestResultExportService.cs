namespace ProjectARVRPro.Exports
{
    public static class CustomTestResultExportService
    {
        private static readonly List<ITestResultCustomExporter> Exporters =
        [
            new JinxingInspectionXlsxExporter(),
        ];

        public static string Export(ObjectiveTestResultExportContext context, CustomTestResultOutputProfile profile)
        {
            ITestResultCustomExporter exporter = Exporters.FirstOrDefault(item =>
                item.Profile == profile)
                ?? Exporters[0];

            return exporter.Export(context);
        }
    }
}
