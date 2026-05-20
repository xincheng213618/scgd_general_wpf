namespace ProjectARVRPro.Exports
{
    public static class CustomTestResultExportService
    {
        private static readonly List<ITestResultCustomExporter> Exporters =
        [
            new JinxingInspectionXlsxExporter(),
        ];

        public static string Export(ObjectiveTestResultExportContext context, string profileName)
        {
            ITestResultCustomExporter exporter = Exporters.FirstOrDefault(item =>
                string.Equals(item.ProfileName, profileName, StringComparison.OrdinalIgnoreCase))
                ?? Exporters[0];

            return exporter.Export(context);
        }
    }
}
