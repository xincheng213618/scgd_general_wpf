using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using Microsoft.Data.Sqlite;
using ProjectARVRPro.Offline;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using ColorVision.Themes;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
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
    public void ReadOnlyConnectionRejectsWritesAndMissingFileDoesNotCreateDatabase()
    {
        using var fixture = new Files();
        string path = fixture.CreateProject("a");
        var reader = new ReadOnlySqliteDatabase(path);
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
        Assert.Single(flowA.GetByBatchIds([20]));
        Assert.Single(flowA.GetBySerialNumbers(["sameSN_20260916T120000"]));
        Assert.Equal(20, Assert.Single(flowA.GetDistinctBatchIds(5)));
        Assert.Single(flowA.GetMessagesByRun(20, "sameSN_20260916T120000"));
        Assert.Single(flowA.GetHistoryMessagesByNodeId("node", [20]));
        Assert.Empty(flowA.GetSameFlowRuns("sameSN_20260916T120000"));
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
                flow.GetExecutionEvents(run.Id);
                Assert.NotEmpty(flow.GetSameFlowRuns(serial));
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
            // Only delete the unique directory created by this fixture.
            string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ColorVision.OfflineTests")) + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(Root).StartsWith(parent, StringComparison.OrdinalIgnoreCase)) Directory.Delete(Root, recursive: true);
        }
    }
}
