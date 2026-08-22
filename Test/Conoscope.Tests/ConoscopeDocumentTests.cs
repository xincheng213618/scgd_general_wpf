using ColorVision.FileIO;
using Conoscope.ApplicationServices.Preprocess;
using Conoscope.Core;
using Conoscope.Processing.Preprocess;
using log4net;
using System.Diagnostics;
using System.IO;
using Xunit.Abstractions;

namespace Conoscope.Tests;

public sealed class ConoscopeDocumentTests
{
    private readonly ITestOutputHelper output;

    public ConoscopeDocumentTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    private static readonly ConoscopePreprocessOptions NoPreprocess = new(
        ClampNonPositiveXyz: false,
        PositiveFloor: 0.000001f,
        DustRemovalEnabled: false,
        new DustRemovalOptions(DustRemovalMode.DarkSpot, 12, 1, 500, 3),
        new ImageFilterOptions(ImageFilterType.None, 1, 1, 1, 1, 1));

    [Fact]
    public async Task PublishesYBeforeDeferredXzAndOwnsTheFinalMats()
    {
        string directory = CreateSampleDirectory();
        string filePath = Path.Combine(directory, "staged.cvcie");
        WriteSample(filePath, offset: 0);

        try
        {
            using ConoscopeDocument document = new(LogManager.GetLogger(typeof(ConoscopeDocumentTests)));
            List<(ConoscopeDocumentChangeKind Kind, bool HasY, bool HasXyz)> changes = [];
            document.Changed += (_, args) => changes.Add((args.Kind, document.HasDisplayData, document.HasXyzData));

            await document.OpenAsync(filePath, exposureSummary: null, NoPreprocess, applyPreprocess: false);

            Assert.Equal(
                [
                    (ConoscopeDocumentChangeKind.InitialDisplayReady, true, false),
                    (ConoscopeDocumentChangeKind.DeferredChannelsReady, true, true)
                ],
                changes);
            Assert.True(document.HasXyzData);
            Assert.Equal(filePath, document.FileName);
            Assert.Equal(100f, document.X!.At<float>(0, 0));
            Assert.Equal(200f, document.Y!.At<float>(0, 0));
            Assert.Equal(300f, document.Z!.At<float>(0, 0));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task NewerOpenRequestWinsAndCanceledDataIsNeverPublished()
    {
        string directory = CreateSampleDirectory();
        string firstPath = Path.Combine(directory, "first.cvcie");
        string secondPath = Path.Combine(directory, "second.cvcie");
        WriteSample(firstPath, offset: 0);
        WriteSample(secondPath, offset: 1000);

        try
        {
            using ConoscopeDocument document = new(LogManager.GetLogger(typeof(ConoscopeDocumentTests)));
            List<string> publishedFiles = [];
            document.Changed += (_, _) => publishedFiles.Add(document.FileName);

            Task first = document.OpenAsync(firstPath, exposureSummary: null, NoPreprocess, applyPreprocess: false);
            Task second = document.OpenAsync(secondPath, exposureSummary: null, NoPreprocess, applyPreprocess: false);
            await Task.WhenAll(first, second);

            Assert.NotEmpty(publishedFiles);
            Assert.All(publishedFiles, path => Assert.Equal(secondPath, path));
            Assert.Equal(1100f, document.X!.At<float>(0, 0));
            Assert.Equal(1200f, document.Y!.At<float>(0, 0));
            Assert.Equal(1300f, document.Z!.At<float>(0, 0));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ObserverFailureDoesNotInterruptStagedLoading()
    {
        string directory = CreateSampleDirectory();
        string filePath = Path.Combine(directory, "observer.cvcie");
        WriteSample(filePath, offset: 0);

        try
        {
            using ConoscopeDocument document = new(LogManager.GetLogger(typeof(ConoscopeDocumentTests)));
            document.Changed += (_, _) => throw new InvalidOperationException("simulated presentation failure");

            await document.OpenAsync(filePath, exposureSummary: null, NoPreprocess, applyPreprocess: false);

            Assert.True(document.HasXyzData);
            Assert.Equal(filePath, document.FileName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FailedOpenDoesNotPublishCandidateMetadataOrData()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.cvcie");
        using ConoscopeDocument document = new(LogManager.GetLogger(typeof(ConoscopeDocumentTests)));
        Exception? reportedException = null;
        document.LoadFailed += (_, args) => reportedException = args.Exception;

        await document.OpenAsync(missingPath, "candidate exposure", NoPreprocess, applyPreprocess: false);

        Assert.NotNull(reportedException);
        Assert.Equal(string.Empty, document.FileName);
        Assert.Null(document.ExposureSummary);
        Assert.False(document.HasDisplayData);
        Assert.False(document.HasXyzData);
    }

    [Fact]
    public async Task OpensConfiguredRealWorldSampleThroughStagedDocumentOwner()
    {
        string? filePath = Environment.GetEnvironmentVariable("CONOSCOPE_REAL_SAMPLE");
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        Assert.True(File.Exists(filePath), $"Configured CVCIE sample does not exist: {filePath}");
        using ConoscopeDocument document = new(LogManager.GetLogger(typeof(ConoscopeDocumentTests)));
        Stopwatch stopwatch = Stopwatch.StartNew();
        double initialDisplayMilliseconds = 0;
        List<ConoscopeDocumentChangeKind> changes = [];
        document.Changed += (_, args) =>
        {
            changes.Add(args.Kind);
            if (args.Kind == ConoscopeDocumentChangeKind.InitialDisplayReady)
            {
                initialDisplayMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            }
        };

        await document.OpenAsync(filePath, exposureSummary: null, NoPreprocess, applyPreprocess: false);

        Assert.Equal(
            [ConoscopeDocumentChangeKind.InitialDisplayReady, ConoscopeDocumentChangeKind.DeferredChannelsReady],
            changes);
        Assert.True(document.HasXyzData);
        Assert.Equal(document.X!.Size(), document.Y!.Size());
        Assert.Equal(document.Y.Size(), document.Z!.Size());
        output.WriteLine(
            "Staged document: {0}x{1}, Y-ready={2:F0} ms, XYZ-ready={3:F0} ms, peak working set={4:N0} bytes",
            document.Y.Cols,
            document.Y.Rows,
            initialDisplayMilliseconds,
            stopwatch.Elapsed.TotalMilliseconds,
            Process.GetCurrentProcess().PeakWorkingSet64);
    }

    private static string CreateSampleDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"conoscope-document-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void WriteSample(string filePath, float offset)
    {
        float[] values = Enumerable.Range(0, 12)
            .Select(index => offset + (index / 4 + 1) * 100 + index % 4)
            .ToArray();
        using CVCIEFile source = new()
        {
            Version = 2,
            FileExtType = CVType.CIE,
            Rows = 2,
            Cols = 2,
            Bpp = 32,
            Channels = 3,
            Exp = [10, 20, 30],
            Data = values.SelectMany(BitConverter.GetBytes).ToArray()
        };

        Assert.True(CVFileUtil.WriteCIEFile(filePath, source));
    }
}
