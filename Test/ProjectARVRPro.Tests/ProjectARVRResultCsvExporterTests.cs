using System.IO;

namespace ProjectARVRPro.Tests;

public sealed class ProjectARVRResultCsvExporterTests
{
    [Theory]
    [InlineData("SN-123", "TestResults_SN-123_20261023_190236_.csv")]
    [InlineData("P5QD0L??GLJ002Q", "TestResults_P5QD0L__GLJ002Q_20261023_190236_.csv")]
    public void ProductSerialNumberIsFormattedOnlyForTheOutputFile(string serialNumber, string expected)
    {
        string directory = Path.Combine(Path.GetTempPath(), "ArvrSerialNumberExport", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string name = ProjectARVRResultCsvExporter.BuildFileName(serialNumber, new DateTime(2026, 10, 23, 19, 2, 36));
            string path = Path.Combine(directory, name);
            var row = new ObjectiveTestCsvRow(serialNumber, "Luminance", "1", "1", "nit", "0", "0", "PASS");

            ProjectARVRResultCsvExporter.ExportRows([row], path);

            Assert.Equal(expected, name);
            Assert.True(File.Exists(path));
            Assert.Contains(serialNumber, File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
}
