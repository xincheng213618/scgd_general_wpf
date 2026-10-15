using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Themes;
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
