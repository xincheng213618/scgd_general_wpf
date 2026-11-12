using ProjectARVRPro.Exports.Lux;
using System.IO;

namespace ProjectARVRPro.Exports
{
    public static class CustomTestResultExportService
    {
        private static readonly List<ITestResultCustomExporter> Exporters =
        [
            new JinxingInspectionXlsxExporter(),
            new LuxCsvExporter(LuxCsvFormat.Current),
            new LuxCsvExporter(LuxCsvFormat.Legacy),
        ];

        public static string Export(ObjectiveTestResultExportContext context, CustomTestResultOutputProfile profile)
        {
            return GetExporter(profile).Export(context);
        }

        public static string GetOutputPath(ObjectiveTestResultExportContext context, CustomTestResultOutputProfile profile) =>
            Path.Combine(context.OutputDirectory, GetExporter(profile).GetFileName(context));

        public static bool IsLuxProfile(CustomTestResultOutputProfile profile) =>
            profile is CustomTestResultOutputProfile.LUX_Current_CSV or CustomTestResultOutputProfile.LUX_Legacy_CSV;

        private static ITestResultCustomExporter GetExporter(CustomTestResultOutputProfile profile) =>
            Exporters.FirstOrDefault(item => item.Profile == profile) ?? Exporters[0];
    }
}
