using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using ProjectARVRPro.Offline;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.OpticCenter;
using ProjectARVRPro.Process.POI;
using SqlSugar;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using ColorVision.Themes;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ProjectARVRPro.Tests;

public sealed class OfflineDataSourceTests
{
    private static readonly DateTime Day = new(2026, 9, 16);
    private static ResultStatisticsQuery All => new() { From = Day, ToExclusive = Day.AddDays(1) };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OfflineWindowsLoadScopedDataAndHideDestructiveActionsAtMinimumSize(bool dark)
    {
        using var fixture = new Files();
        fixture.CreateProject("{}");
        fixture.CreateFlow("local-node", true);
        string sourcePath = Environment.GetEnvironmentVariable("COLORVISION_OFFLINE_FEEDBACK_ROOT") ?? fixture.Root;
        var source = ArvrOfflineDataSource.Open(sourcePath, fixture.Cache);
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            Theme previousTheme = ThemeManager.Current.CurrentUITheme;
            ThemeManager.Current.ApplyTheme(Application.Current, dark ? Theme.Dark : Theme.Light);
            var window = new CycleTimeStatisticsWindow(source);
            try
            {
                RunUiTask(window, "RefreshHomeAsync");
                ResultStatistics homeSummary = source.Statistics.QueryDashboard(new()
                {
                    From = source.LatestDate, ToExclusive = source.LatestDate.AddDays(1),
                }, ResultStatisticsPeriodMode.Day, source.LatestDate).Summary;
                Assert.Equal(homeSummary.AverageFlowRunTimeText, ((TextBlock)window.FindName("AverageFlowRunTimeText")).Text);
                ((CheckBox)window.FindName("EnableCombinedStatisticsCheckBox")).IsChecked = true;
                RunUiTask(window, "RefreshHomeAsync");
                ResultStatistics combinedSummary = source.Statistics.QueryCombinedDashboard(new()
                {
                    From = source.LatestDate, ToExclusive = source.LatestDate.AddDays(1),
                }, ResultStatisticsPeriodMode.Day, source.LatestDate).Summary;
                Assert.Equal(combinedSummary.AverageFlowRunTimeText, ((TextBlock)window.FindName("CombinedAverageFlowRunTimeText")).Text);
                var tabs = (TabControl)window.FindName("StatisticsTabs");
                tabs.SelectedIndex = 0;
                SavePreview(window, dark, "statistics-home", 1100, 640);
                tabs.SelectedIndex = 1;
                RunUiTask(window, "RefreshRecordsAsync", 1);
                var grid = (DataGrid)window.FindName("RecordDataGrid");
                Assert.NotEmpty(grid.Items.Cast<object>());
                var record = Assert.IsType<ResultStatisticsRecordRow>(grid.SelectedItem);
                RunUiTask(window, "LoadFlowDetailsAsync", record);
                var details = (DataGrid)window.FindName("DetailList");
                Assert.NotEmpty(details.Items.Cast<object>());
                Assert.Contains("现场只读", ((TextBlock)window.FindName("DataSourceText")).Text);
                Assert.Equal(source.LatestDate, ((DatePicker)window.FindName("RecordAnchorDatePicker")).SelectedDate);
                SavePreview(window, dark, "statistics", 1100, 640);
                var messages = new OfflineMessagesWindow(source, record);
                try
                {
                    RunUiTask(messages, "ReloadAsync");
                    Assert.DoesNotContain("读取失败", ((TextBlock)messages.FindName("StatusText")).Text);
                    var messageGrid = (DataGrid)messages.FindName("MessagesGrid");
                    if (messageGrid.Items.Count > 0)
                    {
                        var payload = (TextBox)messages.FindName("PayloadText");
                        PumpUntil(() => !payload.Text.Contains("正在读取正文", StringComparison.Ordinal));
                        Assert.DoesNotContain("正文读取失败", payload.Text);
                    }
                    SavePreview(messages, dark, "messages", 800, 470);
                }
                finally { messages.Close(); }

                var detail = details.Items.Cast<ProjectARVRReuslt>().Last();
                var analysis = new FlowExecutionAnalysisWindow(source.FlowDatabasePath!, source.Label, detail.BatchId, source.ResolveFlowSerialNumber(detail));
                try
                {
                    PumpUntil(() => typeof(FlowExecutionAnalysisWindow).GetField("_session", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(analysis) != null);
                    Assert.Equal(Visibility.Collapsed, ((Button)analysis.FindName("ClearAllRecordsButton")).Visibility);
                    Assert.Contains(source.Label, ((TextBlock)analysis.FindName("HeaderSubtitleText")).Text);
                    SavePreview(analysis, dark, "nodes", 1100, 640);
                    var overview = Assert.IsType<FlowExecutionOverviewPage>(((Frame)analysis.FindName("AnalysisFrame")).Content);
                    Assert.Equal(Visibility.Collapsed, ((Button)overview.FindName("ClearCurrentFlowButton")).Visibility);
                    var expectedColor = ((SolidColorBrush)overview.FindResource("GlobalTextBrush")).Color;
                    var texts = Descendants<TextBlock>((ListBox)overview.FindName("DurationListBox")).ToArray();
                    Assert.Contains(texts, text => text.Text == "local-node" || text.Text == "L/BV相机");
                    foreach (var text in texts.Where(text => text.FontWeight == FontWeights.SemiBold))
                        Assert.Equal(expectedColor, Assert.IsType<SolidColorBrush>(text.Foreground).Color);
                }
                finally { analysis.Close(); }
            }
            finally { window.Close(); ThemeManager.Current.ApplyTheme(Application.Current, previousTheme); }
        });
    }

    [Fact]
    public void BothFlowDetailMenusUseTheirOwnSelectionAndRightClickSelectsTheTargetRow()
    {
        using var fixture = new Files();
        fixture.CreateProject("{}");
        var source = ArvrOfflineDataSource.Open(fixture.Root, fixture.Cache);
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
        {
            var window = new CycleTimeStatisticsWindow(source);
            try
            {
                var detailGrid = (DataGrid)window.FindName("DetailList");
                var combinedGrid = (DataGrid)window.FindName("CombinedDetailList");
                ProjectARVRReuslt valid = new() { Id = 2, BatchId = 20, SN = "RIGHT", Model = "right-flow" };
                ProjectARVRReuslt unavailable = new() { SN = "LEFT", Model = "no-details" };
                ProjectARVRReuslt[] flows = [valid, unavailable];
                detailGrid.ItemsSource = flows;
                combinedGrid.ItemsSource = flows;
                ContextMenu singleMenu = Assert.IsType<ContextMenu>(detailGrid.ContextMenu);
                ContextMenu combinedMenu = Assert.IsType<ContextMenu>(combinedGrid.ContextMenu);
                Assert.NotSame(singleMenu, combinedMenu);
                Assert.Equal(singleMenu.Items.OfType<MenuItem>().Select(item => item.Header), combinedMenu.Items.OfType<MenuItem>().Select(item => item.Header));

                foreach (DataGrid target in new[] { detailGrid, combinedGrid })
                {
                    DataGrid other = target == detailGrid ? combinedGrid : detailGrid;
                    MenuItem[] menu = target.ContextMenu.Items.OfType<MenuItem>().ToArray();
                    // Optional history tools may grow; the offline access boundary must remain closed.
                    MenuItem evidence = Assert.Single(menu, item => Equals(item.Header, DisplayText.Get("算法实证")));
                    Assert.Same(target, menu[0].CommandTarget);
                    target.SelectedItem = valid;
                    other.SelectedItem = unavailable;
                    Assert.False(menu[1].Command.CanExecute(null)); // Offline file location.
                    Assert.False(menu[2].Command.CanExecute(null)); // Offline MySQL history.
                    Assert.True(menu[3].Command.CanExecute(null));
                    Assert.True(menu[4].Command.CanExecute(null));
                    Assert.False(evidence.Command.CanExecute(null));
                    target.SelectedItem = unavailable;
                    other.SelectedItem = valid;
                    Assert.False(menu[3].Command.CanExecute(null));
                    Assert.False(menu[4].Command.CanExecute(null));
                    Assert.False(evidence.Command.CanExecute(null));

                    // Materialize the detail row without opening a real application window.
                    target.Measure(new Size(900, 300));
                    target.Arrange(new Rect(0, 0, 900, 300));
                    target.UpdateLayout();
                    DataGridRow row = Assert.IsType<DataGridRow>(target.ItemContainerGenerator.ContainerFromItem(valid));
                    row.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
                    {
                        RoutedEvent = Mouse.PreviewMouseDownEvent,
                    });
                    Assert.Same(valid, target.SelectedItem);
                    Assert.True(menu[3].Command.CanExecute(null));
                    Assert.True(menu[4].Command.CanExecute(null));
                }
            }
            finally { window.Close(); }
        });
    }

    private static void RunUiTask(object target, string method, params object[] args)
    {
        Task task = (Task)target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args)!;
        PumpUntil(() => task.IsCompleted);
        task.GetAwaiter().GetResult();
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T value) yield return value;
            foreach (T nested in Descendants<T>(child)) yield return nested;
        }
    }
    private static void PumpUntil(Func<bool> complete)
    {
        var frame = new DispatcherFrame();
        var deadline = DateTime.UtcNow.AddSeconds(20);
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) => { if (complete() || DateTime.UtcNow >= deadline) frame.Continue = false; };
        timer.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); }
        Assert.True(complete(), "Offline window did not finish loading.");
    }
    private static void SavePreview(Window window, bool dark, string name, int width, int height)
    {
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(width, height)); content.Arrange(new Rect(0, 0, width, height)); content.UpdateLayout();
        bool layoutCompleted = false;
        content.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => layoutCompleted = true);
        PumpUntil(() => layoutCompleted);
        content.Measure(new Size(width, height)); content.Arrange(new Rect(0, 0, width, height)); content.UpdateLayout();
        Assert.True(content.ActualWidth > 0);
        string? directory = Environment.GetEnvironmentVariable("COLORVISION_OFFLINE_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var drawing = background.RenderOpen()) drawing.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
        image.Render(background); image.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(Path.Combine(directory, $"offline-{name}-{(dark ? "dark" : "light")}.png")); encoder.Save(file);
    }

    [Fact]
    public void LegacySchemaIsReadableWithoutMigrationAndRepeatedSnStaysSeparated()
    {
        using var fixture = new Files();
        string path = fixture.CreateProject("site-a");
        string hash = Hash(path);
        var store = new ResultStatisticsDataStore(path, readOnly: true);
        store.InitializeSchema();
        Assert.Equal(2, store.QueryRecordCount(All));
        var rows = store.QueryRecords(All);
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { 2, 1 }, rows.Select(x => x.ExecutionIndex));
        Assert.Equal(new[] { 2, 1 }, rows.Select(x => Assert.Single(store.QueryFlowDetails(x)).Id));
        Assert.Equal(2, store.QueryDashboard(All, ResultStatisticsPeriodMode.Day, Day.AddHours(12)).Summary.TotalCount);
        Assert.Equal(2, store.QueryDashboard(All, ResultStatisticsPeriodMode.All, Day.AddHours(12)).Summary.TotalCount);
        Assert.Equal(2, store.QueryStatistics(All, Day).TotalCount);
        Assert.Equal(2, store.QueryFlowExecutionCount(new() { From = Day, ToExclusive = Day.AddDays(1) }));
        Assert.Equal(2, store.QueryFlowExecutions(new() { From = Day, ToExclusive = Day.AddDays(1) }).Count);
        Assert.Equal("PG", Assert.Single(store.QueryFlowNames()));
        Assert.Equal(2, Assert.Single(store.QuerySnSummaries()).TotalCount);
        Assert.Equal("site-a", store.GetRecord(1)!.ObjectiveTestResultJson);
        Assert.All(store.GetRecords([1, 2]), x => Assert.Equal("site-a", x.ObjectiveTestResultJson));
        Assert.Equal("site-a", Assert.Single(store.QueryFlowDetailsForExport(rows[0])).ViewResultJson);
        Assert.Null(Assert.Single(store.QueryFlowDetails(rows[0])).FlowStartedAt);
        Assert.Equal(hash, Hash(path));
        Assert.False(new ReadOnlySqliteDatabase(path).HasColumn("ObjectiveTestResultRecord", "IsFinalized"));
    }

    [Fact]
    public void BulkReadOnlyResultsKeepCompressedAndLegacyPayloadsAcrossQueryBatches()
    {
        using var fixture = new Files();
        string path = fixture.CreateProject("legacy");
        using (var db = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            db.Open();
            Execute(db, """
                ALTER TABLE ObjectiveTestResultRecord ADD COLUMN ObjectiveTestResultJsonGzip BLOB;
                ALTER TABLE ARVRReuslt ADD COLUMN ViewResultJsonGzip BLOB;
                WITH RECURSIVE ids(Id) AS (SELECT 3 UNION ALL SELECT Id + 1 FROM ids WHERE Id < 501)
                INSERT INTO ObjectiveTestResultRecord(Id, SN, ResultId, BatchId, CreateTime, UpdateTime, TotalResult, ObjectiveTestResultJson)
                SELECT Id, 'sameSN', Id, Id, '2026-09-16 12:00:00', '2026-09-16 12:01:00', 1, CASE WHEN Id <> 3 THEN 'legacy' END FROM ids;
                WITH RECURSIVE ids(Id) AS (SELECT 3 UNION ALL SELECT Id + 1 FROM ids WHERE Id < 501)
                INSERT INTO ARVRReuslt(Id, SN, BatchId, Model, CreateTime, RunTime, ViewResultJson, Result)
                SELECT Id, 'sameSN', Id, 'PG', '2026-09-16 12:00:00', 1000, CASE WHEN Id <> 3 THEN 'legacy' END, 1 FROM ids;
                INSERT INTO ARVRReuslt(Id, SN, BatchId, Model, CreateTime, RunTime, ViewResultJson, Result)
                VALUES (0, 'legacyZero', 0, 'PG', '2026-09-16 12:00:00', 1000, '旧库零 ID 载荷', 1);
                """);
            using var update = db.CreateCommand();
            update.CommandText = """
                UPDATE ObjectiveTestResultRecord SET ObjectiveTestResultJsonGzip = CASE Id WHEN 1 THEN @first WHEN 2 THEN X'' WHEN 501 THEN @last END;
                UPDATE ARVRReuslt SET ViewResultJsonGzip = CASE Id WHEN 1 THEN @first WHEN 2 THEN X'' WHEN 501 THEN @last END;
                """;
            update.Parameters.AddWithValue("@first", Compress("首条"));
            update.Parameters.AddWithValue("@last", Compress("末条🧪"));
            update.ExecuteNonQuery();
        }

        var store = new ResultStatisticsDataStore(path, readOnly: true);
        string hash = Hash(path);
        var objectives = store.GetRecords(Enumerable.Range(1, 501)).ToDictionary(item => item.Id);
        Assert.Equal(501, objectives.Count);
        Assert.Equal("首条", objectives[1].ObjectiveTestResultJson);
        Assert.Equal("legacy", objectives[2].ObjectiveTestResultJson);
        Assert.Equal(string.Empty, objectives[3].ObjectiveTestResultJson);
        Assert.Equal("末条🧪", objectives[501].ObjectiveTestResultJson);

        var flows = store.QueryFlowDetailsForExport(new ResultStatisticsRecordRow { SN = "sameSN", ResultId = 501 });
        Assert.Equal(501, flows.Count);
        Assert.Equal("首条", flows[0].ViewResultJson);
        Assert.Equal("legacy", flows[1].ViewResultJson);
        Assert.Null(flows[2].ViewResultJson);
        Assert.Equal("末条🧪", flows[^1].ViewResultJson);

        var legacyRow = new ResultStatisticsRecordRow
        {
            Id = 502,
            SN = "legacyZero",
            StartTime = Day.AddHours(11),
            EndTime = Day.AddHours(13),
        };
        var legacyFlow = Assert.Single(store.QueryFlowDetailsForExport(legacyRow));
        Assert.Equal(0, legacyFlow.Id);
        Assert.Equal("旧库零 ID 载荷", legacyFlow.ViewResultJson);

        var requests = Enumerable.Range(1, 501)
            .Select(id => new ResultStatisticsRecordRow { Id = id, SN = "sameSN", PreviousResultId = id - 1, ResultId = id })
            .Concat([legacyRow, new() { Id = 503, SN = "anotherSN", ResultId = 1 }, new() { Id = 504, SN = "sameSN", ResultId = 2 }])
            .ToArray();
        var batch = store.QueryFlowDetailsForBatchExport(requests);
        Assert.Equal(requests.Length, batch.Count);
        foreach (var flow in flows)
        {
            var item = Assert.Single(batch[flow.Id]);
            Assert.Equal(flow.Id, item.Id);
            Assert.Equal(flow.ViewResultJson, item.ViewResultJson);
        }
        Assert.Equal(legacyFlow.ViewResultJson, Assert.Single(batch[502]).ViewResultJson);
        Assert.Empty(batch[503]);
        Assert.Equal(new[] { 1, 2 }, batch[504].Select(item => item.Id));
        Assert.Equal(new[] { "首条", "legacy" }, batch[504].Select(item => item.ViewResultJson));
        Assert.Equal(hash, Hash(path));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BatchCsvReadsSummaryOnlyForRecordsWithoutFlowMetrics(bool readOnly)
    {
        var legacy = new ObjectiveTestResult
        {
            OpticCenterTestResult = new() { OptCenterRotation = new() { Name = "OptCenterRotation", Value = 17 } }
        };
        var flow = new OpticCenterTestResult { OptCenterRotation = new() { Name = "OptCenterRotation", Value = 23 } };
        var snapshot = new ProjectARVRReuslt();
        ResultProcessResolver.Capture(snapshot, new OpticCenterProcess());

        using var fixture = new Files();
        string summaryJson = JsonConvert.SerializeObject(legacy);
        string databasePath = readOnly ? fixture.CreateProject(summaryJson) : Path.Combine(fixture.Root, "ProjectARVRPro.db");
        var store = new ResultStatisticsDataStore(databasePath, readOnly);
        store.InitializeSchema();
        if (!readOnly)
        {
            using var writer = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={databasePath};Pooling=False",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
            });
            writer.Insertable(Enumerable.Range(1, 2).Select(id => new ObjectiveTestResultRecord
            {
                SN = "sameSN", ResultId = id, IsFinalized = true, TotalResult = true,
                CreateTime = Day.AddHours(12).AddMinutes(id), UpdateTime = Day.AddHours(12).AddMinutes(id).AddSeconds(1),
            }).ToArray()).ExecuteCommand();
            writer.Insertable(Enumerable.Range(1, 2).Select(id => new ProjectARVRReuslt
            {
                SN = "sameSN", Model = "PG", CreateTime = Day.AddHours(12).AddMinutes(id),
            }).ToArray()).ExecuteCommand();
        }
        using (var db = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
        {
            db.Open();
            if (readOnly)
            {
                Execute(db, """
                    ALTER TABLE ObjectiveTestResultRecord ADD COLUMN ObjectiveTestResultJsonGzip BLOB;
                    ALTER TABLE ARVRReuslt ADD COLUMN ViewResultJsonGzip BLOB;
                    ALTER TABLE ARVRReuslt ADD COLUMN ProcessTypeFullName TEXT;
                    ALTER TABLE ARVRReuslt ADD COLUMN ProcessConfigJson TEXT;
                    """);
                store = new ResultStatisticsDataStore(databasePath, readOnly: true);
            }

            using var command = db.CreateCommand();
            command.CommandText = """
                UPDATE ObjectiveTestResultRecord SET ObjectiveTestResultJsonGzip = CASE Id WHEN 1 THEN @summary ELSE X'01020304' END;
                UPDATE ARVRReuslt SET ProcessTypeFullName = @type, ProcessConfigJson = @config,
                    ViewResultJsonGzip = CASE Id WHEN 1 THEN @empty ELSE @flow END;
                """;
            command.Parameters.AddWithValue("@summary", readOnly ? Array.Empty<byte>() : Compress(summaryJson));
            command.Parameters.AddWithValue("@type", snapshot.ProcessTypeFullName!);
            command.Parameters.AddWithValue("@config", snapshot.ProcessConfigJson!);
            command.Parameters.AddWithValue("@empty", Compress("{}"));
            command.Parameters.AddWithValue("@flow", Compress(JsonConvert.SerializeObject(flow)));
            command.ExecuteNonQuery();
        }

        string hash = Hash(databasePath);
        string csvPath = Path.Combine(fixture.Root, "batch.csv");
        var rows = store.QueryRecords(All).OrderBy(row => row.Id).ToArray();
        Assert.Equal(2, store.ExportBatchCsv(rows, csvPath));
        string[] lines = File.ReadAllLines(csvPath);
        Assert.Equal(3, lines.Length);
        Assert.EndsWith(",Optical_Center_OptCenterRotation", lines[0]);
        Assert.EndsWith(",17", lines[1]);
        Assert.EndsWith(",23", lines[2]);
        if (readOnly)
            Assert.Equal(hash, Hash(databasePath));

        // If the flow has no usable metrics, the damaged summary is needed and the old CSV stays intact.
        using (var db = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
        {
            db.Open();
            Execute(db, "UPDATE ARVRReuslt SET ProcessConfigJson = NULL WHERE Id = 2;");
        }
        Assert.Throws<InvalidDataException>(() => store.ExportBatchCsv(rows, csvPath));
        Assert.Equal(lines, File.ReadAllLines(csvPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BatchCsvKeepsPreviousFileWhenLaterFlowHasCorruptGzip(bool configureLaterFlow)
    {
        using var fixture = new Files();
        string databasePath = fixture.CreateProject("{}");
        string processType = typeof(PoiDynamicProcess).FullName!;
        const string config = """{"Name":"Screen"}""";
        Assert.NotNull(ProjectARVRResultCsvExporter.CreateReaderForTransientPayloads(
            [new ProjectARVRReuslt { ProcessTypeFullName = processType, ProcessConfigJson = config }]));
        Assert.Null(ProjectARVRResultCsvExporter.CreateReaderForTransientPayloads(
            [new ProjectARVRReuslt { ProcessTypeFullName = typeof(OpticCenterProcess).FullName, ProcessConfigJson = "{}" }]));
        using (var db = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
        {
            db.Open();
            Execute(db, """
                ALTER TABLE ARVRReuslt ADD COLUMN ViewResultJsonGzip BLOB;
                ALTER TABLE ARVRReuslt ADD COLUMN ProcessTypeFullName TEXT;
                ALTER TABLE ARVRReuslt ADD COLUMN ProcessConfigJson TEXT;
                """);
            using var command = db.CreateCommand();
            command.CommandText = """
                UPDATE ARVRReuslt SET ProcessTypeFullName = @type, ProcessConfigJson = @config, ViewResultJsonGzip = @payload WHERE Id = 1;
                UPDATE ARVRReuslt SET ProcessTypeFullName = @laterType, ProcessConfigJson = @laterConfig, ViewResultJsonGzip = @damaged WHERE Id = 2;
                """;
            command.Parameters.AddWithValue("@type", processType);
            command.Parameters.AddWithValue("@config", config);
            command.Parameters.AddWithValue("@payload", Compress("""{"PoixyuvDatas":[{"Name":"P","Y":42}]}"""));
            byte[] damaged = configureLaterFlow ? Compress("!" + new string(' ', 80 * 1024)) : [0, 1, 2, 3];
            if (configureLaterFlow)
                damaged[^8] ^= 0x80;
            command.Parameters.AddWithValue("@laterType", configureLaterFlow ? processType : DBNull.Value);
            command.Parameters.AddWithValue("@laterConfig", configureLaterFlow ? config : DBNull.Value);
            command.Parameters.AddWithValue("@damaged", damaged);
            command.ExecuteNonQuery();
        }

        string csvPath = Path.Combine(fixture.Root, "batch.csv");
        byte[] original = Encoding.UTF8.GetBytes("previous export");
        File.WriteAllBytes(csvPath, original);
        var store = new ResultStatisticsDataStore(databasePath, readOnly: true);
        var rows = store.QueryRecords(All).OrderBy(row => row.Id).ToArray();
        Assert.Throws<InvalidDataException>(() => store.ExportBatchCsv(rows, csvPath));
        Assert.Equal(original, File.ReadAllBytes(csvPath));
        Assert.Empty(Directory.EnumerateFiles(fixture.Root, "*.arvr.tmp"));
    }

    [Fact]
    public void BatchCsvKeepsFirstMetricAfterMetadataOnlyRowsAndPreservesOldFileOnLaterCorruption()
    {
        using var fixture = new Files();
        string path = fixture.CreateProject("legacy");
        using (var db = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            db.Open();
            Execute(db, """
                ALTER TABLE ObjectiveTestResultRecord ADD COLUMN ObjectiveTestResultJsonGzip BLOB;
                ALTER TABLE ARVRReuslt ADD COLUMN ViewResultJsonGzip BLOB;
                UPDATE ObjectiveTestResultRecord SET ObjectiveTestResultJson = NULL;
                WITH RECURSIVE ids(Id) AS (SELECT 3 UNION ALL SELECT Id + 1 FROM ids WHERE Id < 130)
                INSERT INTO ObjectiveTestResultRecord(Id, SN, ResultId, BatchId, CreateTime, UpdateTime, TotalResult)
                SELECT Id, 'metadata-' || Id, 0, Id, '2026-09-16 12:00:00', '2026-09-16 12:01:00', 1 FROM ids;
                """);
            using var command = db.CreateCommand();
            command.CommandText = """
                UPDATE ObjectiveTestResultRecord SET ObjectiveTestResultJsonGzip=@first WHERE Id=64;
                UPDATE ObjectiveTestResultRecord SET ObjectiveTestResultJsonGzip=X'', ObjectiveTestResultJson=@second WHERE Id=63;
                """;
            command.Parameters.AddWithValue("@first", Compress("""{"DynamicTestResults":{"Csv":[{"Name":"Value","TestValue":"first"}]}}"""));
            command.Parameters.AddWithValue("@second", """{"DynamicTestResults":{"Csv":[{"Name":"Value","TestValue":"second"}]}}""");
            command.ExecuteNonQuery();
        }

        var store = new ResultStatisticsDataStore(path, readOnly: true);
        var query = new ResultStatisticsQuery { From = Day, ToExclusive = Day.AddDays(1), PageSize = 150 };
        var rows = store.QueryRecords(query);
        Assert.Equal(130, rows.Count);
        string csvPath = Path.Combine(fixture.Root, "batch.csv");
        string referencePath = Path.Combine(fixture.Root, "reference.csv");
        string sourceHash = Hash(path);

        var records = store.GetRecords(rows.Select(row => row.Id)).ToDictionary(record => record.Id);
        ObjectiveTestResultBatchCsvExporter.ExportToCsv(rows.Select(row => records[row.Id]),
            store.QueryFlowDetailsForBatchExport(rows), referencePath);
        Assert.Equal(130, store.ExportBatchCsv(rows, csvPath));
        Assert.Equal(File.ReadAllBytes(referencePath), File.ReadAllBytes(csvPath));
        string[] lines = File.ReadAllLines(csvPath);
        Assert.EndsWith(",Csv_Value", lines[0], StringComparison.Ordinal);
        Assert.EndsWith(",", lines[1], StringComparison.Ordinal);
        Assert.EndsWith(",first", lines[67], StringComparison.Ordinal);
        Assert.EndsWith(",second", lines[68], StringComparison.Ordinal);
        Assert.Equal(sourceHash, Hash(path));

        byte[] previous = File.ReadAllBytes(csvPath);
        using (var db = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            db.Open();
            Execute(db, "UPDATE ARVRReuslt SET ViewResultJsonGzip=X'00010203' WHERE Id=2");
        }

        Assert.Throws<InvalidDataException>(() => store.ExportBatchCsv(rows, csvPath));
        Assert.Equal(previous, File.ReadAllBytes(csvPath));
        Assert.Empty(Directory.GetFiles(fixture.Root, "*.arvr.tmp"));
    }


    [Fact]
    public void ReadOnlyConnectionRejectsWritesAndMissingFileDoesNotCreateDatabase()
    {
        using var fixture = new Files();
        string path = fixture.CreateProject("a");
        using (var database = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            database.Open();
            Execute(database, "CREATE TABLE pragma_table_info(user_value TEXT);");
        }
        var reader = new ReadOnlySqliteDatabase(path);
        Assert.True(reader.HasColumn("pragma_table_info", "user_value"));
        using var client = reader.OpenClient();
        Assert.ThrowsAny<Exception>(() => client.Ado.ExecuteCommand("DELETE FROM ARVRReuslt"));
        Assert.Equal(2, reader.Query<ProjectARVRReuslt>(client).Count());
        string missing = Path.Combine(fixture.Root, "missing.db");
        Assert.Throws<FileNotFoundException>(() => new ReadOnlySqliteDatabase(missing));
        Assert.False(File.Exists(missing));
    }

    [Fact]
    public void ExplicitFileSelectionUsesThatFileEvenWhenItsNameWasChanged()
    {
        using var first = new Files();
        using var second = new Files();
        first.CreateProject("neighbor");
        string renamed = Path.Combine(first.Root, "selected-site.db");
        File.Copy(second.CreateProject("selected"), renamed);
        var source = ArvrOfflineDataSource.Open(renamed, first.Cache);
        Assert.Equal("selected", source.Statistics.GetRecord(1)!.ObjectiveTestResultJson);
        Assert.Contains("selected-site.db", source.Label);
    }

    [Fact]
    public void ZipAndFolderSourcesKeepOverlappingIdsAndPayloadsIndependent()
    {
        using var first = new Files();
        using var second = new Files();
        string original = first.CreateProject("first");
        second.CreateProject("second");
        first.CreateFlow("first-node", compressed: true);
        second.CreateFlow("second-node", compressed: false);
        string zip = Path.Combine(first.Root, "feedback.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(original, "Database/ProjectARVRPro.db");
            archive.CreateEntryFromFile(Path.Combine(first.Root, "FlowNodeRecords.db"), "Database/FlowNodeRecords.db");
            using var writer = new StreamWriter(archive.CreateEntry("../not-a-database.txt").Open());
            writer.Write("must not be extracted");
        }
        string hash = Hash(zip);
        var a = ArvrOfflineDataSource.Open(zip, first.Cache);
        var b = ArvrOfflineDataSource.Open(second.Root, second.Cache);
        Assert.NotEqual(a.DirectoryPath, b.DirectoryPath);
        Assert.Equal("first", a.Statistics.GetRecord(1)!.ObjectiveTestResultJson);
        Assert.Equal("second", b.Statistics.GetRecord(1)!.ObjectiveTestResultJson);
        Assert.Equal(Day, a.LatestDate);
        Assert.Null(a.SocketDatabasePath);
        Assert.Equal(2, Directory.GetFiles(a.DirectoryPath).Length);
        Assert.Equal(hash, Hash(zip));
        Assert.Equal("sameSN_20260916T120000", a.ResolveFlowSerialNumber(new() { SN = "sameSN", BatchId = 20 }));
        var flowA = new FlowAnalysisDataSource(a.FlowDatabasePath, "a");
        var flowB = new FlowAnalysisDataSource(b.FlowDatabasePath, "b");
        Assert.True(flowA.FlushPendingWrites(TimeSpan.Zero));
        Assert.Equal("first-node", Assert.Single(flowA.GetByRun(20, "sameSN_20260916T120000")).NodeName);
        Assert.Equal("second-node", flowB.GetLatestRecord()!.NodeName);
        Assert.Equal("first-node", flowA.GetLastByNodeId("node")!.NodeName);
        Assert.Equal("first-node", flowA.GetMessagePayloads(1).SendPayload);
        Assert.Equal("second-node", flowB.GetMessagePayloads(1).SendPayload);
        Assert.Equal(20, Assert.Single(flowA.GetRecentRunSummaries(5, default)).BatchId);
        Assert.Single(flowA.GetRunSummariesBySerialNumbers(["sameSN_20260916T120000"], default));
        Assert.Single(flowA.GetMessagesByRun(20, "sameSN_20260916T120000"));
        Assert.Single(flowA.GetHistoryMessagesByNodeId("node", [20]));
        var session = new FlowExecutionAnalysisSession(20, "sameSN_20260916T120000", null, null, [], [], [], Day, 0, flowA);
        Assert.Empty(flowA.GetSameFlowRuns(session));
        Assert.Null(flowA.GetFlowRun(20, "sameSN_20260916T120000"));
        Assert.Empty(flowA.GetExecutionEvents(1));
    }

    [Fact]
    public void ImportCapturesCommittedWalAndDoesNotFallBackToAnUnrelatedRun()
    {
        using var fixture = new Files();
        string path = fixture.CreateProject("wal");
        fixture.CreateFlow("node", false);
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        Execute(connection, "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; UPDATE ObjectiveTestResultRecord SET Msg='committed WAL';");
        Assert.True(File.Exists(path + "-wal"));
        string hash = Hash(path), walHash = Hash(path + "-wal");
        var source = ArvrOfflineDataSource.Open(path, fixture.Cache);
        Assert.Equal("committed WAL", source.Statistics.GetRecord(1)!.Msg);
        Assert.Equal(hash, Hash(path));
        Assert.Equal(walHash, Hash(path + "-wal"));
        Assert.Throws<InvalidDataException>(() => source.ResolveFlowSerialNumber(new() { SN = "other", BatchId = 20 }));
        using var flow = new SqliteConnection($"Data Source={source.FlowDatabasePath};Pooling=False");
        flow.Open();
        Execute(flow, "INSERT INTO FlowNodeRecord VALUES(2,20,'sameSN_another','node','other','2026-09-16 12:00:00',3)");
        Assert.Throws<InvalidDataException>(() => source.ResolveFlowSerialNumber(new() { SN = "sameSN", BatchId = 20 }));
    }

    [Fact]
    public void FlowHistoryUsesTheStoredCodeWhenTheBatchIdHasBeenReused()
    {
        using var fixture = new Files();
        fixture.CreateProject("{}");
        fixture.CreateFlow("new-node", false);
        string path = Path.Combine(fixture.Root, "FlowNodeRecords.db");
        using (var db = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            db.Open();
            Execute(db, """
                INSERT INTO FlowNodeRecord VALUES(2,20,'2026_09_16 11_58_00','node','old-node','2026-09-16 11:58:00',3);
                INSERT INTO FlowNodeRecord VALUES(3,21,'SN__20260916T120000','node','legacy-node','2026-09-16 12:00:00',3);
                """);
        }

        var source = ArvrOfflineDataSource.Open(fixture.Root, fixture.Cache);
        var result = new ProjectARVRReuslt { BatchId = 20, Code = "2026_09_16 11_58_00", SN = "sameSN" };
        string code = ArvrFlowAnalysisHistory.ResolveSerialNumber(path, result);
        Assert.Equal(result.Code, code);
        Assert.Equal(code, source.ResolveFlowSerialNumber(result));
        Assert.Equal("old-node", Assert.Single(new FlowAnalysisDataSource(path).GetByRun(result.BatchId, code)).NodeName);
        Assert.Throws<InvalidDataException>(() => source.ResolveFlowSerialNumber(new() { BatchId = 21, Code = result.Code, SN = "sameSN" }));
        Assert.Throws<InvalidDataException>(() => source.ResolveFlowSerialNumber(new() { BatchId = 20, Code = "missing", SN = "sameSN" }));
        Assert.Equal("SN__20260916T120000", source.ResolveFlowSerialNumber(new() { BatchId = 21, SN = "SN?" }));
    }

    [Fact]
    public void FlowHistoryFindsJournalOnlyRunsAndRejectsDuplicateRunIdentities()
    {
        using var fixture = new Files();
        string path = Path.Combine(fixture.Root, "FlowNodeRecords.db");
        using var db = new SqliteConnection($"Data Source={path};Pooling=False");
        db.Open();
        Execute(db, """
            CREATE TABLE FlowRunRecord(id INTEGER PRIMARY KEY,batch_id INTEGER,serial_number TEXT);
            INSERT INTO FlowRunRecord VALUES(1,20,'2026_09_16 12_00_00_a1b2c3d4e5f60718');
            """);
        var result = new ProjectARVRReuslt { BatchId = 20, Code = "2026_09_16 12_00_00_a1b2c3d4e5f60718" };

        Assert.Equal(result.Code, ArvrFlowAnalysisHistory.ResolveSerialNumber(path, result));
        Execute(db, "INSERT INTO FlowRunRecord VALUES(2,20,'2026_09_16 12_00_00_a1b2c3d4e5f60718');");
        Assert.Contains("多个流程运行", Assert.Throws<InvalidDataException>(() => ArvrFlowAnalysisHistory.ResolveSerialNumber(path, result)).Message);
    }

    [Fact]
    public void CompressedMessagesLoadOnlyFromTheirSourceAndRespectTimeWindow()
    {
        using var fixture = new Files();
        string path = Path.Combine(fixture.Root, "SocketMessages.db");
        using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            Execute(connection, "CREATE TABLE SocketMessage(id INTEGER PRIMARY KEY, MessageTime TEXT, Direction INTEGER, EventName TEXT, MsgID TEXT, ClientEndPoint TEXT, Content TEXT, ContentGzip BLOB)");
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO SocketMessage VALUES(1,'2026-09-16 12:00:00',0,'RunAll','id','endpoint',NULL,@gzip),(2,'2026-09-16 10:00:00',1,'Other','id','endpoint','excluded',NULL)";
            command.Parameters.AddWithValue("@gzip", Compress("{\"source\":\"site\"}"));
            command.ExecuteNonQuery();
        }
        string hash = Hash(path);
        var store = new OfflineMessageStore(path, true);
        var result = store.Query(Day.AddHours(11), Day.AddHours(13), 1);
        Assert.Equal(1, result.Count);
        Assert.Equal("接收", Assert.Single(result.Rows).Direction);
        Assert.Equal("{\"source\":\"site\"}", store.LoadPayload(1));
        Assert.Equal(hash, Hash(path));
    }

    [Fact]
    public void InvalidArchiveAndMissingCoreColumnsProduceActionableErrors()
    {
        using var fixture = new Files();
        string zip = Path.Combine(fixture.Root, "empty.zip");
        using (ZipFile.Open(zip, ZipArchiveMode.Create)) { }
        Assert.Contains("ProjectARVRPro.db", Assert.Throws<InvalidDataException>(() => ArvrOfflineDataSource.Open(zip, fixture.Cache)).Message);
        string path = fixture.CreateProject("a");
        using (var db = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            db.Open(); Execute(db, "ALTER TABLE ARVRReuslt DROP COLUMN SN");
        }
        Assert.Contains("SN", Assert.Throws<InvalidDataException>(() => new ResultStatisticsDataStore(path, true)).Message);
    }

    // Opt-in field snapshot verification. Normal CI/tests do not require private diagnostic files.
    [Fact]
    public void FieldSnapshotQueriesWholeRunPgNodesHistoryAndMessagesWithoutChangingSources()
    {
        string? root = Environment.GetEnvironmentVariable("COLORVISION_OFFLINE_FEEDBACK_ROOT");
        if (string.IsNullOrWhiteSpace(root)) return;
        using var fixture = new Files();
        string[] paths = Directory.GetFiles(root, "*.db");
        var hashes = paths.ToDictionary(x => x, Hash);
        var source = ArvrOfflineDataSource.Open(root, fixture.Cache);
        var query = new ResultStatisticsQuery { From = DateTime.MinValue, ToExclusive = DateTime.MaxValue };
        Assert.True(source.Statistics.QueryRecordCount(query) > 0);
        var record = source.Statistics.QueryRecords(query)[0];
        Assert.True(source.Statistics.QueryDashboard(query, ResultStatisticsPeriodMode.All, source.LatestDate).Summary.TotalCount > 0);
        var details = source.Statistics.QueryFlowDetails(record);
        Assert.NotEmpty(details);
        Assert.False(string.IsNullOrWhiteSpace(source.Statistics.GetRecord(record.Id)!.ObjectiveTestResultJson));
        var flow = new FlowAnalysisDataSource(source.FlowDatabasePath, source.Label);
        foreach (var detail in details)
        {
            string serial = source.ResolveFlowSerialNumber(detail);
            var nodes = flow.GetByRun(detail.BatchId, serial);
            Assert.NotEmpty(nodes);
            Assert.NotEmpty(flow.GetByNodeId(nodes[0].NodeId, 20));
            var messages = flow.GetMessagesByRun(detail.BatchId, serial);
            foreach (var message in messages.Take(1)) Assert.NotNull(flow.GetMessagePayloads(message.Id).SendPayload);
            var run = flow.GetFlowRun(detail.BatchId, serial);
            if (run != null)
            {
                Assert.Equal(serial, run.SerialNumber);
                var events = flow.GetExecutionEvents(run.Id);
                var session = new FlowExecutionAnalysisSession(detail.BatchId, serial, null, run, nodes, messages, events, record.StartTime, 0, flow);
                Assert.NotEmpty(flow.GetSameFlowRuns(session));
            }
            source.Statistics.LoadViewResultJson(detail);
        }
        foreach (var pair in new[] { (source.SocketDatabasePath, true), (source.MqttDatabasePath, false) })
        {
            if (pair.Item1 == null) continue;
            var store = new OfflineMessageStore(pair.Item1, pair.Item2);
            var messages = store.Query(record.StartTime.AddSeconds(-1), record.EndTime.AddSeconds(1), 1);
            foreach (var row in messages.Rows.Take(1)) Assert.NotNull(store.LoadPayload(row.Id));
        }
        foreach (string path in paths) Assert.Equal(hashes[path], Hash(path));
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery();
    }
    private static string Hash(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(file));
    }
    private static byte[] Compress(string text)
    {
        using var output = new MemoryStream();
        using (var stream = new GZipStream(output, CompressionMode.Compress, leaveOpen: true)) stream.Write(Encoding.UTF8.GetBytes(text));
        return output.ToArray();
    }
    private sealed class Files : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "ColorVision.OfflineTests", Guid.NewGuid().ToString("N"));
        public string Cache => Path.Combine(Root, "cache");
        public Files() => Directory.CreateDirectory(Root);
        public string CreateProject(string payload)
        {
            string path = Path.Combine(Root, "ProjectARVRPro.db");
            using var db = new SqliteConnection($"Data Source={path};Pooling=False"); db.Open();
            Execute(db, """
                CREATE TABLE ObjectiveTestResultRecord(Id INTEGER PRIMARY KEY,SN TEXT,ResultId INTEGER,BatchId INTEGER,CreateTime TEXT,UpdateTime TEXT,TotalResult INTEGER,LastModel TEXT,Msg TEXT,ObjectiveTestResultJson TEXT);
                CREATE TABLE ARVRReuslt(Id INTEGER PRIMARY KEY,SN TEXT,BatchId INTEGER,Model TEXT,CreateTime TEXT,RunTime INTEGER,ViewResultJson TEXT,Result INTEGER);
                INSERT INTO ObjectiveTestResultRecord VALUES(1,'sameSN',1,10,'2026-09-16 11:59:50','2026-09-16 12:00:00',1,'PG','',NULL),(2,'sameSN',2,20,'2026-09-16 12:01:00','2026-09-16 12:01:10',1,'PG','',NULL);
                INSERT INTO ARVRReuslt VALUES(1,'sameSN',10,'PG','2026-09-16 12:00:00',1000,NULL,1),(2,'sameSN',20,'PG','2026-09-16 12:01:10',1100,NULL,1);
                """);
            using var command = db.CreateCommand();
            command.CommandText = "UPDATE ObjectiveTestResultRecord SET ObjectiveTestResultJson=@payload; UPDATE ARVRReuslt SET ViewResultJson=@payload";
            command.Parameters.AddWithValue("@payload", payload); command.ExecuteNonQuery();
            return path;
        }
        public void CreateFlow(string payload, bool compressed)
        {
            using var db = new SqliteConnection($"Data Source={Path.Combine(Root, "FlowNodeRecords.db")};Pooling=False"); db.Open();
            Execute(db, """
                CREATE TABLE FlowNodeRecord(id INTEGER PRIMARY KEY,batch_id INTEGER,serial_number TEXT,node_id TEXT,node_name TEXT,start_time TEXT,elapsed_ms INTEGER);
                CREATE TABLE FlowNodeMessage(id INTEGER PRIMARY KEY,batch_id INTEGER,serial_number TEXT,node_id TEXT,send_time TEXT,send_payload TEXT,send_payload_gzip BLOB);
                INSERT INTO FlowNodeRecord VALUES(1,20,'sameSN_20260916T120000','node',NULL,'2026-09-16 12:00:00',3);
                INSERT INTO FlowNodeMessage VALUES(1,20,'sameSN_20260916T120000','node','2026-09-16 12:00:00',NULL,NULL);
                """);
            using var command = db.CreateCommand();
            command.CommandText = "UPDATE FlowNodeRecord SET node_name=@payload; UPDATE FlowNodeMessage SET send_payload=@legacy,send_payload_gzip=@gzip";
            command.Parameters.AddWithValue("@payload", payload);
            command.Parameters.AddWithValue("@legacy", compressed ? DBNull.Value : payload);
            command.Parameters.AddWithValue("@gzip", compressed ? Compress(payload) : DBNull.Value);
            command.ExecuteNonQuery();
        }
        public void Dispose()
        {
            using var projectPool = new SqliteConnection($"Data Source={Path.Combine(Root, "ProjectARVRPro.db")};Default Timeout=5");
            SqliteConnection.ClearPool(projectPool);
            // Only delete the unique directory created by this fixture.
            string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ColorVision.OfflineTests")) + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(Root).StartsWith(parent, StringComparison.OrdinalIgnoreCase)) Directory.Delete(Root, recursive: true);
        }
    }
}
