using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Themes;
using ColorVision.UI;
using SqlSugar;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class FlowAnalysisLoadingTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("ColorVision-AnalysisLoading-").FullName;

    [Fact]
    public void CurrentPageLoadsBeforeHistoryAndChartAndCanNavigateWhenReady()
    {
        string path = CreateDatabase();
        byte[] before = File.ReadAllBytes(path);
        WithWindow(path, window =>
        {
            bool currentLoaded = false, navigationPending = false, chartPending = false;
            var frame = (Frame)window.FindName("AnalysisFrame");
            frame.Navigated += (_, e) =>
            {
                if (e.Content is FlowExecutionOverviewPage page)
                    page.Loaded += (_, _) =>
                    {
                        if (currentLoaded) return;
                        currentLoaded = true;
                        navigationPending = Field<bool>(window, "_isNavigationLoading");
                        chartPending = ((ContentControl)page.FindName("TimelineHost")).Content == null;
                    };
            };
            window.Show();
            PumpUntil(() => currentLoaded && !Field<bool>(window, "_isNavigationLoading"));
            Assert.True(navigationPending);
            Assert.True(chartPending);
            Assert.Equal("second", Field<FlowExecutionAnalysisSession>(window, "_session").SerialNumber);
            Assert.Single(Field<FlowExecutionAnalysisSession>(window, "_session").Records);
            var page = Assert.IsType<FlowExecutionOverviewPage>(frame.Content);
            PumpUntil(() => ((ContentControl)page.FindName("TimelineHost")).Content is ScottPlot.WPF.WpfPlot);
            var plot = (ScottPlot.WPF.WpfPlot)((ContentControl)page.FindName("TimelineHost")).Content;
            Assert.True(plot.ActualWidth > 0 && plot.ActualHeight > 0);
            var previous = (Button)window.FindName("PreviousSameFlowRunButton");
            Assert.True(previous.IsEnabled);
            previous.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            PumpUntil(() => Field<FlowExecutionAnalysisSession>(window, "_session").SerialNumber == "first"
                && !Field<bool>(window, "_isNavigationLoading"));
            Assert.Equal(100, Field<FlowExecutionAnalysisSession>(window, "_session").BatchId);
            Assert.True(((Button)window.FindName("NextSameFlowRunButton")).IsEnabled);
        });
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void SameFlowNavigationKeepsTheOpenedTemplateWhenAnotherTemplateReusesItsSerial()
    {
        string path = CreateDatabase();
        using (var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source={path};Pooling=False", DbType = DbType.Sqlite }))
        {
            DateTime time = new(2026, 10, 3, 8, 2, 0);
            db.Insertable(new FlowRunRecord { BatchId = 102, SerialNumber = "second", TemplateId = 2, FlowKey = "other-flow", RunKey = "other-run", StartedTimeUtc = time, CompletedTime = time.AddSeconds(1) }).ExecuteCommand();
            db.Insertable(new FlowNodeRecord { BatchId = 102, SerialNumber = "second", NodeId = "node", StartTime = time }).ExecuteCommand();
        }
        byte[] before = File.ReadAllBytes(path);

        WithWindow(path, window =>
        {
            window.Show();
            PumpUntil(() => Field<FlowExecutionAnalysisSession>(window, "_session")?.BatchId == 101 && !Field<bool>(window, "_isNavigationLoading"));

            Assert.Equal("flow", Field<FlowExecutionAnalysisSession>(window, "_session").Run?.FlowKey);
            Assert.Equal(new[] { 100, 101 }, Field<IReadOnlyList<FlowRunNavigationItem>>(window, "_sameFlowRuns").Select(run => run.BatchId));
            Assert.True(((Button)window.FindName("PreviousSameFlowRunButton")).IsEnabled);
        });
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void SameFlowNavigationKeepsDistinctBatchesThatReuseTheSameSerial()
    {
        DateTime first = new(2026, 10, 3, 8, 0, 0);
        DateTime second = first.AddMinutes(1);
        var sameFlowRuns = new[]
        {
            new FlowRunRecord { BatchId = 101, SerialNumber = "reused", CompletedTime = first },
            new FlowRunRecord { BatchId = 102, SerialNumber = "reused", CompletedTime = second },
        };
        var nodeRuns = new FlowRunNavigationSummary[]
        {
            new(101, "reused", first.AddSeconds(-1), 1),
            new(102, "reused", second.AddSeconds(-1), 2),
            new(103, "reused", second.AddMinutes(1), 3),
        };

        var navigation = FlowExecutionAnalysisWindow.BuildSameFlowRunOrder(sameFlowRuns, nodeRuns);

        Assert.Equal(new[] { new FlowRunNavigationItem(101, "reused", first), new FlowRunNavigationItem(102, "reused", second) }, navigation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SupersededOrClosedPageCannotBeRepopulatedByDeferredWork(bool close)
    {
        string path = CreateDatabase();
        WithWindow(path, window =>
        {
            FlowExecutionOverviewPage? oldPage = null;
            Task? replacement = null;
            var frame = (Frame)window.FindName("AnalysisFrame");
            frame.Navigated += (_, e) =>
            {
                if (e.Content is FlowExecutionOverviewPage page)
                    page.Loaded += (_, _) =>
                    {
                        if (oldPage != null) return;
                        oldPage = page;
                        if (close) window.Close();
                        else replacement = (Task)typeof(FlowExecutionAnalysisWindow).GetMethod("LoadRunAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                            .Invoke(window, [100, "first", null, false, null])!;
                    };
            };
            window.Show();
            PumpUntil(() => oldPage != null && (close || replacement!.IsCompleted && !Field<bool>(window, "_isNavigationLoading")));
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.Null(((ContentControl)oldPage!.FindName("TimelineHost")).Content);
            if (close)
            {
                Assert.Null(frame.Content);
                Assert.Empty(Field<IReadOnlyList<FlowRunNavigationItem>>(window, "_allRuns"));
            }
            else
            {
                replacement!.GetAwaiter().GetResult();
                Assert.Equal("first", Field<FlowExecutionAnalysisSession>(window, "_session").SerialNumber);
                Assert.False(((Button)window.FindName("PreviousSameFlowRunButton")).IsEnabled);
                Assert.True(((Button)window.FindName("NextSameFlowRunButton")).IsEnabled);
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JournalLookupKeepsReusedBatchSerialsSeparate(bool offline)
    {
        string path = ConfigService.Instance.GetRequiredService<FlowNodeRecordConfig>().SqliteDbPath;
        string tempDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        Assert.StartsWith(tempDirectory, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source={path};Pooling=False", DbType = DbType.Sqlite, IsAutoCloseConnection = true });
        FlowDiagnosticsMaintenanceGate.RunExclusive(() => FlowDiagnosticsSchemaMigrator.EnsureSchema(db));
        const int batchId = int.MaxValue - 763;
        string prefix = Guid.NewGuid().ToString("N");
        DateTime started = new(2026, 10, 3, 8, 0, 0);
        var inserted = new List<int>();
        try
        {
            InsertRun("same", started);
            int matching = InsertRun("same", started);
            int blank = InsertRun("\t\u3000", started.AddMinutes(1));
            InsertRun("other", started.AddMinutes(2));
            var source = new FlowAnalysisDataSource(offline ? path : null);

            Assert.Null(source.GetFlowRun(batchId, "missing"));
            Assert.Null(source.GetFlowRun(batchId, "SAME"));
            Assert.Equal(blank, source.GetFlowRun(batchId, null)?.Id);
            Assert.Equal(blank, source.GetFlowRun(batchId, "")?.Id);
            Assert.Equal(matching, source.GetFlowRun(batchId, "same")?.Id);
        }
        finally
        {
            db.Deleteable<FlowRunRecord>().Where(run => inserted.Contains(run.Id)).ExecuteCommand();
        }

        int InsertRun(string serial, DateTime timestamp)
        {
            int id = db.Insertable(new FlowRunRecord
            {
                BatchId = batchId,
                SerialNumber = serial,
                TemplateId = 1,
                RunKey = prefix + inserted.Count,
                StartedTimeUtc = timestamp,
                CompletedTime = timestamp.AddSeconds(1),
            }).ExecuteReturnIdentity();
            inserted.Add(id);
            return id;
        }
    }

    [Fact]
    public void NavigationSummariesPreserveLegacyTimesAndBatchSerialOrdering()
    {
        string path = Path.Combine(_directory, "navigation.db");
        using (var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source={path};Pooling=False", DbType = DbType.Sqlite, IsAutoCloseConnection = false }))
        {
            db.Ado.ExecuteCommand("""
                CREATE TABLE FlowNodeRecord (id INTEGER PRIMARY KEY, batch_id INTEGER, serial_number TEXT,
                    node_id TEXT, start_time TEXT, elapsed_ms INTEGER);
                INSERT INTO FlowNodeRecord VALUES
                    (1, 100, 'sn', 'node', '2026-10-03 08:00:00', 0),
                    (2, 101, 'sn', 'node', '2026-10-03 08:00:00.0000002', 0),
                    (3, 101, 'sn', 'node', '2026-10-03 08:00:00.0000001', 0),
                    (4, 101, 'SN', 'node', '2026-10-03 08:00:00.0000001', 0),
                    (5, 101, NULL, 'node', '2026-10-03 08:01:00', 0),
                    (6, 101, '　', 'node', '2026-10-03 08:00:00', 0),
                    (7, 102, 'sn', 'node', '2026-10-03 09:00:00', 0),
                    (8, 102, 'sn', 'node', '2026-10-03T09:01:00+14:00', 0),
                    (9, 102, 'quoted''sn', 'node', '2026-10-03 09:02:00', 0);
                """);
        }

        byte[] before = File.ReadAllBytes(path);
        var source = new FlowAnalysisDataSource(path);
        var recent = FlowExecutionAnalysisWindow.BuildFlowRunOrder(source.GetRecentRunSummaries(2, default));
        var rows = source.GetByRun(101, null).Concat(source.GetByRun(102, null)).ToArray();
        var expected = rows.GroupBy(row => (row.BatchId, Serial: string.IsNullOrWhiteSpace(row.SerialNumber) ? "" : row.SerialNumber))
            .OrderBy(run => run.Min(row => row.StartTime)).ThenBy(run => run.Min(row => row.Id))
            .Select(run => new FlowRunNavigationItem(run.Key.BatchId, run.Key.Serial, run.Min(row => row.StartTime)));

        Assert.Equal(expected, recent);
        var bySerial = source.GetRunSummariesBySerialNumbers(["sn", "sn", "quoted'sn", "　"], default);
        Assert.Equal(new[] { "quoted'sn", "sn" }, bySerial.Select(row => row.SerialNumber).Distinct().Order(StringComparer.Ordinal));
        var sameFlow = FlowExecutionAnalysisWindow.BuildSameFlowRunOrder(
            [new FlowRunRecord { SerialNumber = "sn", CompletedTime = new DateTime(2026, 10, 4) }], bySerial);
        var latest = expected.Where(run => run.SerialNumber == "sn").OrderByDescending(run => run.ExecutedTime).First();
        Assert.Equal(latest.BatchId, Assert.Single(sameFlow).BatchId);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    private string CreateDatabase()
    {
        string path = Path.Combine(_directory, "analysis.db");
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source={path};Pooling=False", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        FlowDiagnosticsSchemaMigrator.EnsureSchema(db);
        DateTime time = new(2026, 10, 3, 8, 0, 0);
        for (int i = 0; i < 2; i++)
        {
            string serial = i == 0 ? "first" : "second";
            DateTime start = time.AddMinutes(i);
            db.Insertable(new FlowRunRecord { BatchId = i == 0 ? null : 101, SerialNumber = serial, TemplateId = 1, FlowKey = "flow", FlowName = "Test flow", RunKey = serial, StartedTimeUtc = start, CompletedTime = start.AddSeconds(1) }).ExecuteCommand();
            db.Insertable(new FlowNodeRecord { BatchId = 100 + i, SerialNumber = serial, NodeId = "node", NodeName = "Camera", NodeType = "Camera", StartTime = start, EndTime = start.AddSeconds(1), ElapsedMs = 1000 }).ExecuteCommand();
        }
        return path;
    }

    private static void WithWindow(string path, Action<FlowExecutionAnalysisWindow> action) => WpfTestHost.Invoke(() =>
    {
        var resources = ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase)
            .Select(uri => new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) }).ToList();
        foreach (var resource in resources) Application.Current.Resources.MergedDictionaries.Add(resource);
        var window = new FlowExecutionAnalysisWindow(path, "Read-only test", 101, "second") { ShowInTaskbar = false, ShowActivated = false, Left = -20000 };
        try { action(window); }
        finally
        {
            window.Close();
            foreach (var resource in resources) Application.Current.Resources.MergedDictionaries.Remove(resource);
        }
    });

    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static void PumpUntil(Func<bool> complete)
    {
        var frame = new DispatcherFrame();
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.ApplicationIdle,
            (_, _) => { if (complete() || DateTime.UtcNow >= deadline) frame.Continue = false; }, Dispatcher.CurrentDispatcher);
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); }
        Assert.True(complete(), "Analysis loading did not complete.");
    }

    public void Dispose()
    {
        string path = Path.GetFullPath(_directory);
        if (!path.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unexpected test directory.");
        Directory.Delete(path, recursive: true);
    }
}
