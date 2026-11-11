using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using OpenCvSharp;
using System.Collections;
using System.IO;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class BatchAlgorithmAnalysisTests
{
    private static readonly AlgorithmId AnalysisId = new("test.batch-analysis");
    private static readonly HashSet<AlgorithmImageFormat> Formats = [AlgorithmImageFormat.Gray8];
    private const AlgorithmHostCapabilities Capabilities = AlgorithmHostCapabilities.Batch
        | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local;

    [Theory]
    [InlineData(BatchAnalysisExportFormat.Json)]
    [InlineData(BatchAnalysisExportFormat.CsvBundle)]
    public async Task CancelingWhileWritingTheLastAnalysisPreservesExistingReports(BatchAnalysisExportFormat format)
    {
        using var cancellation = new CancellationTokenSource();
        using var scheduler = new AlgorithmExecutionScheduler(cpuConcurrency: 1);
        var processor = CreateProcessor(scheduler, cancellation.Cancel);
        string directory = Directory.CreateTempSubdirectory("ColorVision-BatchAnalysis-").FullName;
        string sourcePath = Path.Combine(directory, "sample.fixture");
        string primary = Path.Combine(directory, format == BatchAnalysisExportFormat.Json ? "sample_analysis.json" : "sample_analysis.csv");
        string companion = Path.Combine(directory, "sample_analysis_samples.csv");
        string[] expected = format == BatchAnalysisExportFormat.Json ? [primary] : [primary, companion];
        try
        {
            foreach (string path in expected) File.WriteAllText(path, $"prior {Path.GetFileName(path)}");
            BatchAlgorithmAnalysisResult result = await processor.ProcessAsync(new BatchAlgorithmAnalysisRequest
            {
                Items = [new BatchImageItem(sourcePath)],
                Invocation = AlgorithmInvocation.Create(AnalysisId, new NoAlgorithmParameters()),
                ExportFormat = format,
                AvoidOverwrite = false,
            }, cancellationToken: cancellation.Token);

            Assert.True(cancellation.IsCancellationRequested, "The batch must reach table serialization.");
            Assert.Equal(expected.Order(), Directory.GetFiles(directory).Order());
            foreach (string path in expected) Assert.Equal($"prior {Path.GetFileName(path)}", File.ReadAllText(path));
            Assert.True(result.Cancelled);
            BatchAlgorithmAnalysisFileResult canceled = Assert.Single(result.Files);
            Assert.Equal(AlgorithmResultStatus.Cancelled, canceled.Status);
            Assert.Empty(canceled.OutputPaths);
        }
        finally
        {
            foreach (string path in Directory.EnumerateFiles(directory)) File.Delete(path);
            Directory.Delete(directory);
        }
    }

    [Theory]
    [InlineData(BatchAnalysisExportFormat.Json, "other/sample.fixture", "sample.json", "sample_2.json")]
    [InlineData(BatchAnalysisExportFormat.CsvBundle, "sample_samples.fixture", "sample.csv", "sample_samples_2.csv")]
    [InlineData(BatchAnalysisExportFormat.CsvBundle, "sample_samples.csv", "sample_2.csv", "sample_samples_2.csv")]
    public async Task BatchKeepsDistinctReportsAndProtectsSelectedSources(BatchAnalysisExportFormat format,
        string secondSourceName, string firstReportName, string secondReportName)
    {
        using var scheduler = new AlgorithmExecutionScheduler(cpuConcurrency: 1);
        var processor = CreateProcessor(scheduler, static () => { });
        string directory = Directory.CreateTempSubdirectory("ColorVision-BatchAnalysisPaths-").FullName;
        string firstSource = Path.Combine(directory, "sample.fixture");
        string secondSource = Path.Combine(directory, secondSourceName);
        bool secondSourceIsCsv = Path.GetExtension(secondSource) == ".csv";
        try
        {
            if (secondSourceIsCsv) File.WriteAllText(secondSource, "selected source contents");
            BatchAlgorithmAnalysisResult result = await processor.ProcessAsync(new BatchAlgorithmAnalysisRequest
            {
                Items = [new BatchImageItem(firstSource), new BatchImageItem(secondSource)],
                Invocation = AlgorithmInvocation.Create(AnalysisId, new NoAlgorithmParameters()),
                OutputDirectory = directory,
                PreserveFolderStructure = false,
                Suffix = string.Empty,
                ExportFormat = format,
                AvoidOverwrite = false,
            });

            Assert.Equal(2, result.Succeeded);
            if (secondSourceIsCsv) Assert.Equal("selected source contents", File.ReadAllText(secondSource));
            string[] paths = result.Files.SelectMany(file => file.OutputPaths).ToArray();
            Assert.Equal(paths.Length, paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal(firstReportName, Path.GetFileName(result.Files[0].OutputPaths[0]));
            Assert.Equal(secondReportName, Path.GetFileName(result.Files[1].OutputPaths[0]));
            Assert.All(paths, path => Assert.True(File.Exists(path)));
        }
        finally
        {
            foreach (string path in Directory.EnumerateFiles(directory)) File.Delete(path);
            Directory.Delete(directory);
        }
    }

    private static BatchAlgorithmAnalysisProcessor CreateProcessor(AlgorithmExecutionScheduler scheduler, Action onReadRow)
    {
        var catalog = new AlgorithmCatalog();
        catalog.Register(new AlgorithmDescriptor(AnalysisId, new AlgorithmVersion(1, 0, 0), "Test", "Test",
            "Produces a table for batch export.", typeof(NoAlgorithmParameters),
            new AlgorithmParameterSchema(1, [], AlgorithmJson.ToElement(new NoAlgorithmParameters())),
            Formats, Capabilities));
        var runtime = new AlgorithmRuntime(catalog, [new TableProvider(onReadRow)], scheduler);
        return new BatchAlgorithmAnalysisProcessor([new GrayImageLoader()], runtime);
    }

    private sealed class GrayImageLoader : IBatchImageLoader
    {
        public IReadOnlyCollection<string> Extensions => [".fixture", ".csv"];
        public Mat Load(string filePath) => new(1, 1, MatType.CV_8UC1, Scalar.All(42));
    }

    private sealed class TableProvider(Action onReadRow) : IImageAlgorithmProvider
    {
        public AlgorithmProviderMetadata Metadata { get; } = new("batch-table", "Batch table", AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local, 1, Capabilities, Formats);

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            reason = null;
            return descriptor.Id == AnalysisId;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(new AlgorithmResult
            {
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts = [new AlgorithmTableArtifact("samples", [new("Value", "number")], new ObservedRows(onReadRow))],
            });
    }

    private sealed class ObservedRows(Action onRead) : IReadOnlyList<IReadOnlyDictionary<string, JsonElement>>
    {
        public int Count => 1;
        public IReadOnlyDictionary<string, JsonElement> this[int index]
        {
            get
            {
                if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
                onRead();
                return new Dictionary<string, JsonElement> { ["Value"] = JsonSerializer.SerializeToElement(42) };
            }
        }

        public IEnumerator<IReadOnlyDictionary<string, JsonElement>> GetEnumerator() { yield return this[0]; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
