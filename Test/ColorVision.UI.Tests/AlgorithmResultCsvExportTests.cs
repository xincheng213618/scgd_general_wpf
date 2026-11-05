using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using Microsoft.VisualBasic.FileIO;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmResultCsvExportTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("ColorVision-CsvExport-").FullName;

    [Fact]
    public async Task BundlePreservesCsvFieldsLargeUnicodeValuesAndArtifactPayloads()
    {
        // More than one writer buffer, with surrogate pairs and quoted multiline fields.
        string text = string.Concat(Enumerable.Repeat("😀x", 40_000)) + ",\"\r\n中文";
        const string heading = "Text,\"quoted\"\r\n列";
        using AlgorithmResult result = Result(
            new AlgorithmMeasurementArtifact("metrics", [new AlgorithmMeasurement("mean,\"Y\"", 12.5, "cd/m²", 2, 0.75,
                new Dictionary<string, string> { ["note"] = text })]),
            new AlgorithmTableArtifact("samples",
                [new(heading, "string"), new("Number", "number"), new("Missing", "string"), new("Null", "string")],
                [new Dictionary<string, JsonElement>
                {
                    [heading] = JsonSerializer.SerializeToElement(text),
                    ["Number"] = JsonSerializer.SerializeToElement(1.25),
                    ["Null"] = JsonSerializer.SerializeToElement<string?>(null),
                }]),
            new AlgorithmGeometryArtifact("geometry", AlgorithmCoordinateSpace.Pixel,
                [new AlgorithmGeometry("circle,1", AlgorithmGeometryKind.Circle, [new AlgorithmPoint(1.25, 2.5)],
                    Radius: 3.5, Confidence: 0.8, FilterReason: "kept\r\n\"yes\"",
                    Measurements: new Dictionary<string, double> { ["area"] = 38.5 })]),
            new AlgorithmStructuredDataArtifact("structured", "schema,\"v1\"",
                JsonSerializer.SerializeToElement(new { text, number = 2.75 })));
        List<AlgorithmProgress> progress = [];
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        IReadOnlyList<string> paths;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            paths = await AlgorithmResultExporter.ExportCsvBundleAsync(result, Path.Combine(_directory, "result.csv"),
                progress: new InlineProgress(progress.Add));
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }

        Assert.Equal(4, paths.Count);
        foreach (string path in paths)
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, File.ReadAllBytes(path)[..3]);

        string[] measurement = ReadRows(paths[0])[1];
        Assert.Equal(new[] { "metrics", "mean,\"Y\"", "12.5", "cd/m²", "2", "0.75" }, measurement[..6]);
        using (JsonDocument qualifiers = JsonDocument.Parse(measurement[6]))
            Assert.Equal(text, qualifiers.RootElement.GetProperty("note").GetString());

        string[][] table = ReadRows(paths[1]);
        Assert.Equal(new[] { heading, "Number", "Missing", "Null" }, table[0]);
        Assert.Equal(new[] { text, "1.25", "", "" }, table[1]);

        string[] geometry = ReadRows(paths[2])[1];
        Assert.Equal(new[] { "geometry", "Pixel", "circle,1", "Circle" }, geometry[..4]);
        Assert.Equal(new[] { "3.5", "", "", "0.8", "kept\r\n\"yes\"" }, geometry[5..10]);
        Assert.Equal(new[] { new AlgorithmPoint(1.25, 2.5) },
            JsonSerializer.Deserialize<AlgorithmPoint[]>(geometry[4], AlgorithmJson.Options));
        using (JsonDocument measurements = JsonDocument.Parse(geometry[10]))
            Assert.Equal(38.5, measurements.RootElement.GetProperty("area").GetDouble());

        string[] structured = ReadRows(paths[3])[1];
        Assert.Equal(new[] { "structured", "schema,\"v1\"" }, structured[..2]);
        using (JsonDocument data = JsonDocument.Parse(structured[2]))
        {
            Assert.Equal(text, data.RootElement.GetProperty("text").GetString());
            Assert.Equal(2.75, data.RootElement.GetProperty("number").GetDouble());
        }
        Assert.Equal(new[] { "measurements", "samples", "geometry", "structured", "complete" }, progress.Select(value => value.Message));
        Assert.All(progress, value => Assert.Equal("export-csv", value.Stage));
        Assert.Equal(1, progress[^1].Fraction);
        Assert.Equal(paths.Order(), Directory.GetFiles(_directory).Order());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancellationPreservesEveryExistingTargetAndRemovesStagedFiles(bool cancelBeforeWriting)
    {
        using AlgorithmResult result = SampleTableResult();
        string primary = Path.Combine(_directory, "result.csv");
        string companion = Path.Combine(_directory, "result_samples.csv");
        File.WriteAllText(primary, "previous measurements");
        File.WriteAllText(companion, "previous samples");
        using CancellationTokenSource cancellation = new();
        if (cancelBeforeWriting) cancellation.Cancel();
        InlineProgress progress = new(value =>
        {
            if (value.Message == "samples") cancellation.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AlgorithmResultExporter.ExportCsvBundleAsync(
            result, primary, overwrite: true, cancellation.Token, progress));

        Assert.Equal("previous measurements", File.ReadAllText(primary));
        Assert.Equal("previous samples", File.ReadAllText(companion));
        Assert.Equal(new[] { primary, companion }.Order(), Directory.GetFiles(_directory).Order());
    }

    [Fact]
    public async Task FailureCommittingCompanionRestoresAlreadyReplacedPrimary()
    {
        using AlgorithmResult result = SampleTableResult();
        string primary = Path.Combine(_directory, "result.csv");
        string companion = Path.Combine(_directory, "result_samples.csv");
        File.WriteAllText(primary, "previous measurements");
        Directory.CreateDirectory(companion);

        Exception? failure = await Record.ExceptionAsync(() => AlgorithmResultExporter.ExportCsvBundleAsync(result, primary, overwrite: true));

        Assert.True(failure is IOException or UnauthorizedAccessException, failure?.ToString() ?? "The companion commit should fail.");
        Assert.Equal("previous measurements", File.ReadAllText(primary));
        Assert.True(Directory.Exists(companion));
        Assert.Equal(new[] { primary }, Directory.GetFiles(_directory));
        Assert.Empty(Directory.GetFileSystemEntries(companion));
    }

    private static AlgorithmResult SampleTableResult() => Result(new AlgorithmTableArtifact("samples",
        [new("Value", "number")],
        [new Dictionary<string, JsonElement> { ["Value"] = JsonSerializer.SerializeToElement(1.25) }]));

    private static AlgorithmResult Result(params AlgorithmArtifact[] artifacts) => new()
    {
        InvocationId = Guid.NewGuid(),
        AlgorithmId = new AlgorithmId("csv-export-test"),
        AlgorithmVersion = new AlgorithmVersion(1, 0, 0),
        Status = AlgorithmResultStatus.Succeeded,
        Artifacts = artifacts,
    };

    private static string[][] ReadRows(string path)
    {
        using TextFieldParser parser = new(path, Encoding.UTF8)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false,
        };
        parser.SetDelimiters(",");
        List<string[]> rows = [];
        while (!parser.EndOfData) rows.Add(parser.ReadFields()!);
        return rows.ToArray();
    }

    private sealed class InlineProgress(Action<AlgorithmProgress> report) : IProgress<AlgorithmProgress>
    {
        public void Report(AlgorithmProgress value) => report(value);
    }

    public void Dispose()
    {
        string resolved = Path.GetFullPath(_directory);
        Assert.StartsWith(Path.GetTempPath(), resolved, StringComparison.OrdinalIgnoreCase);
        Directory.Delete(resolved, recursive: true);
    }
}
