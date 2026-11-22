using ColorVision.Engine.Diagnostics.HostBenchmark;
using ColorVision.UI.Marketplace;
using System.IO;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class HostBenchmarkTests
{
    private static readonly DateTimeOffset MeasuredAt = new(2026, 11, 21, 1, 23, 45, TimeSpan.Zero);

    [Fact]
    public void ScoresNormalizeToTheReferenceAndUseAnEqualWeightedGeometricMean()
    {
        HostBenchmarkRun reference = CreateRun(MeasuredAt, 10, 20, 40, 80);
        HostBenchmarkRun measured = CreateRun(MeasuredAt.AddMinutes(1), 5, 10, 40, 160);

        var self = HostBenchmarkScoring.Compare(reference, reference);
        Assert.NotNull(self);
        Assert.Equal(100, self.OverallScore, precision: 8);

        var comparison = HostBenchmarkScoring.Compare(measured, reference);
        Assert.NotNull(comparison);
        Assert.Equal(100 * Math.Pow(2, 0.25), comparison.OverallScore, precision: 8);
        Assert.Collection(comparison.Metrics,
            metric => Assert.Equal(("memory-copy", 10.0, 200.0, 0.5), (metric.Id, metric.ReferenceMilliseconds, metric.Score, metric.TimeRatio)),
            metric => Assert.Equal(("channel-swap", 20.0, 200.0, 0.5), (metric.Id, metric.ReferenceMilliseconds, metric.Score, metric.TimeRatio)),
            metric => Assert.Equal(("bayer-rg16", 40.0, 100.0, 1.0), (metric.Id, metric.ReferenceMilliseconds, metric.Score, metric.TimeRatio)),
            metric => Assert.Equal(("pixel-compute", 80.0, 50.0, 2.0), (metric.Id, metric.ReferenceMilliseconds, metric.Score, metric.TimeRatio)));
    }

    [Fact]
    public void TheFirstRunUsesTheLeaderboardReferenceForItsDisplayedScore()
    {
        HostBenchmarkRun run = CreateRun(MeasuredAt, 17.180, 25.654, 17.757, 23.731) with
        {
            WorkloadVersion = HostBenchmarkWorkloads.Version
        };

        var score = HostBenchmarkScoring.CompareToStandard(run);

        Assert.NotNull(score);
        Assert.Equal(98.2, score.OverallScore, precision: 1);
        Assert.Null(HostBenchmarkScoring.CompareToStandard(run with { FrameWidth = 1 }));

        using var files = new BenchmarkFiles();
        var store = new HostBenchmarkStore(files.HistoryPath);
        store.Add(run);
        store.Export(files.ExportPath, run.Id);
        using JsonDocument export = JsonDocument.Parse(File.ReadAllText(files.ExportPath));
        Assert.Equal(HostBenchmarkScoring.StandardVersion, export.RootElement.GetProperty("scoringVersion").GetString());
        Assert.Equal(98.2, export.RootElement.GetProperty("standardComparison").GetProperty("overallScore").GetDouble(), precision: 1);
    }

    [Fact]
    public void HistoryKeepsTheFirstReferenceAndPersistsAnExplicitChange()
    {
        using var files = new BenchmarkFiles();
        var store = new HostBenchmarkStore(files.HistoryPath);
        HostBenchmarkRun first = CreateRun(MeasuredAt, 10, 20, 40, 80);
        HostBenchmarkRun latest = CreateRun(MeasuredAt.AddMinutes(2), 5, 10, 20, 40);
        HostBenchmarkRun middle = CreateRun(MeasuredAt.AddMinutes(1), 8, 16, 32, 64);

        store.Add(first);
        store.Add(latest);
        store.Add(middle);

        var reopened = new HostBenchmarkStore(files.HistoryPath);
        var history = reopened.Load();
        Assert.Equal(first.Id, history.ReferenceId);
        Assert.Equal(new[] { latest.Id, middle.Id, first.Id }, history.Runs.Select(run => run.Id));

        reopened.SetReference(middle.Id);
        Assert.Equal(middle.Id, new HostBenchmarkStore(files.HistoryPath).Load().ReferenceId);
    }

    [Fact]
    public void PublicShareReceiptPersistsOnOnlyTheSharedResult()
    {
        using var files = new BenchmarkFiles();
        var store = new HostBenchmarkStore(files.HistoryPath);
        HostBenchmarkRun first = CreateRun(MeasuredAt, 10, 20, 40, 80);
        HostBenchmarkRun second = CreateRun(MeasuredAt.AddMinutes(1), 8, 15, 34, 65);
        store.Add(first);
        store.Add(second);

        var reopened = new HostBenchmarkStore(files.HistoryPath);
        Assert.All(reopened.Load().Runs, run => Assert.False(run.ShareAttempted));
        store.MarkShareAttempted(second.Id);
        store.MarkShareAttempted(second.Id);
        var waitingToRetry = reopened.Load();
        Assert.Equal(2, waitingToRetry.Runs.Length);
        Assert.True(waitingToRetry.Runs[0].ShareAttempted);
        Assert.Null(waitingToRetry.Runs[0].PublicShare);
        Assert.False(waitingToRetry.Runs[1].ShareAttempted);

        var receipt = new HostBenchmarkPublicShare(107.5, HostBenchmarkScoring.StandardVersion);
        store.MarkPublicShare(second.Id, receipt);
        store.MarkPublicShare(second.Id, receipt);
        var history = new HostBenchmarkStore(files.HistoryPath).Load();

        Assert.Equal(first.Id, history.ReferenceId);
        Assert.Equal(receipt, history.Runs[0].PublicShare);
        Assert.Null(history.Runs[1].PublicShare);
    }

    [Fact]
    public void DefaultMarketplaceShareLinkPinsThePublishedResult()
    {
        Guid runId = Guid.NewGuid();
        var profile = new HostBenchmarkShareProfile(Guid.Empty, MarketplaceConfig.ServiceBaseUrl, string.Empty, null);
        Uri expected = new UriBuilder(MarketplaceConfig.ServiceBaseUrl)
        {
            Path = "/benchmarks",
            Fragment = $"run={runId:D}"
        }.Uri;

        Assert.Equal(expected, HostBenchmarkSharing.GetComparisonPageUri(profile, runId));
    }

    [Fact]
    public void ExportIncludesRawSamplesAndTheSelectedRunsReferenceComparison()
    {
        using var files = new BenchmarkFiles();
        var store = new HostBenchmarkStore(files.HistoryPath);
        HostBenchmarkRun reference = CreateRun(MeasuredAt, 10, 20, 40, 80);
        HostBenchmarkRun measured = CreateRun(MeasuredAt.AddMinutes(1), 5, 10, 20, 40);
        store.Add(reference);
        store.Add(measured);

        store.Export(files.ExportPath, measured.Id);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(files.ExportPath));
        JsonElement export = document.RootElement;
        Assert.Equal(1, export.GetProperty("schemaVersion").GetInt32());
        JsonElement run = export.GetProperty("run");
        Assert.Equal(measured.Id, run.GetProperty("id").GetGuid());
        Assert.Equal(measured.MeasuredAtUtc, run.GetProperty("measuredAtUtc").GetDateTimeOffset());
        Assert.Equal(reference.Id, export.GetProperty("reference").GetProperty("id").GetGuid());

        JsonElement.ArrayEnumerator metrics = run.GetProperty("metrics").EnumerateArray();
        JsonElement memoryCopy = Assert.Single(metrics, metric => metric.GetProperty("id").GetString() == "memory-copy");
        Assert.Equal(new[] { 4.0, 5.0, 6.0 }, memoryCopy.GetProperty("samplesMilliseconds").EnumerateArray().Select(sample => sample.GetDouble()));

        JsonElement comparison = export.GetProperty("comparison");
        Assert.Equal(200, comparison.GetProperty("overallScore").GetDouble(), precision: 8);
        Assert.All(comparison.GetProperty("metrics").EnumerateArray(), metric => Assert.Equal(0.5, metric.GetProperty("timeRatio").GetDouble()));
    }

    [Fact]
    public void ComparisonRejectsIncompatibleWorkloadsAndMalformedMetrics()
    {
        HostBenchmarkRun reference = CreateRun(MeasuredAt, 10, 20, 40, 80);
        HostBenchmarkRun measured = CreateRun(MeasuredAt.AddMinutes(1), 5, 10, 20, 40);
        HostBenchmarkRun changedVersion = measured with { WorkloadVersion = "future-workload" };
        HostBenchmarkRun changedMetric = measured with
        {
            Metrics = [.. measured.Metrics[..^1], measured.Metrics[^1] with { Id = "unknown" }]
        };
        HostBenchmarkRun duplicateMetric = measured with
        {
            Metrics = [.. measured.Metrics[..^1], measured.Metrics[0]]
        };
        HostBenchmarkRun invalidTiming = measured with
        {
            Metrics = [.. measured.Metrics[..^1], measured.Metrics[^1] with { MedianMilliseconds = 0 }]
        };

        Assert.Null(HostBenchmarkScoring.Compare(changedVersion, reference));
        Assert.Null(HostBenchmarkScoring.Compare(measured with { FrameWidth = measured.FrameWidth / 2 }, reference));
        Assert.Null(HostBenchmarkScoring.Compare(changedMetric, reference));
        Assert.Null(HostBenchmarkScoring.Compare(measured, duplicateMetric));
        Assert.Null(HostBenchmarkScoring.Compare(invalidTiming, reference));
    }

    private static HostBenchmarkRun CreateRun(DateTimeOffset measuredAt, double copy, double swap, double bayer, double compute) =>
        new(Guid.NewGuid(), "test-v1", measuredAt, "TestHost", measuredAt.AddMinutes(-12), measuredAt.AddHours(-7), 8,
            [CreateMetric("memory-copy", copy), CreateMetric("channel-swap", swap), CreateMetric("bayer-rg16", bayer), CreateMetric("pixel-compute", compute)]);

    private static HostBenchmarkMetric CreateMetric(string id, double median) =>
        new(id, median, median + 1, [median - 1, median, median + 1]);

    private sealed class BenchmarkFiles : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-HostBenchmark-");

        public string HistoryPath => Path.Combine(directory.FullName, "history.json");
        public string ExportPath => Path.Combine(directory.FullName, "export.json");

        public void Dispose()
        {
            string parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            Assert.Equal(parent, directory.Parent?.FullName, ignoreCase: true);
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }
}
