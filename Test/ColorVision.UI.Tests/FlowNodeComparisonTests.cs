using ColorVision.Engine.FlowProcessing.Diagnostics;
using SqlSugar;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class FlowNodeComparisonTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("ColorVision-NodeComparison-").FullName;
    private readonly DateTime _time = new(2026, 10, 2, 7, 0, 0);

    [Fact]
    public void HistoryKeepsFlowNodeAndBatchSerialPairsAndLoadsOnlySelectedSourcePayload()
    {
        string path = Path.Combine(_directory, "a.db");
        string otherPath = Path.Combine(_directory, "b.db");
        FlowNodeRecord current;
        FlowNodeMessage currentMessage;
        FlowRunRecord currentRun;
        using (SqlSugarClient db = CreateDatabase(path))
        {
            currentRun = AddRun(db, 10, "current", "flow-a");
            (current, currentMessage) = AddNode(db, 10, "current", "fov", "a-current");
            AddRun(db, 11, "previous", "flow-a");
            AddNode(db, 11, "previous", "fov", "a-previous");
            AddRun(db, 10, "other-flow", "flow-b"); // Reused batch and template ID, different FlowKey.
            AddNode(db, 10, "other-flow", "fov", "wrong-flow");
            AddRun(db, 12, "current", "flow-b"); // Reused SN under a different flow.
            AddNode(db, 12, "current", "fov", "wrong-same-sn");
            AddNode(db, 11, "wrong-sn", "fov", "wrong-sn");
            AddNode(db, 11, "previous", "different-node", "same-name");
        }
        using (SqlSugarClient db = CreateDatabase(otherPath))
        {
            AddRun(db, 10, "current", "flow-a");
            AddNode(db, 10, "current", "fov", "b-current");
        }
        byte[] before = File.ReadAllBytes(path);
        var source = new FlowAnalysisDataSource(path, "offline-a");
        var session = Session(source, current, currentMessage, currentRun);
        FlowNodeComparisonHistory history = source.GetNodeComparisonHistory(session, current);
        Assert.True(history.HasFlowIdentity);
        Assert.Equal(new[] { "current", "previous" }, history.Records.Select(item => item.SerialNumber).Order().ToArray());
        Assert.Equal(2, history.Messages.Count);
        Assert.All(history.Messages, message => { Assert.Null(message.SendPayload); Assert.Null(message.RecvPayload); });
        Assert.Equal("a-current", source.GetMessagePayloads(currentMessage.Id).RecvPayload);
        Assert.Equal("b-current", new FlowAnalysisDataSource(otherPath).GetMessagePayloads(currentMessage.Id).RecvPayload);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void LegacyRunWithoutBatchIdUsesFullSerialAndRetainsMissingMessageExecutions()
    {
        string path = Path.Combine(_directory, "legacy.db");
        FlowNodeRecord current;
        FlowNodeMessage message;
        using (SqlSugarClient db = CreateDatabase(path))
        {
            AddRun(db, null, "legacy-current", "flow-a");
            (current, message) = AddNode(db, 20, "legacy-current", "fov", "payload");
            AddRun(db, null, "legacy-previous", "flow-a");
            var (previous, previousMessage) = AddNode(db, 21, "legacy-previous", "fov", "payload");
            db.Deleteable<FlowNodeMessage>().Where(item => item.Id == previousMessage.Id).ExecuteCommand();
        }
        var source = new FlowAnalysisDataSource(path);
        FlowNodeComparisonHistory history = source.GetNodeComparisonHistory(Session(source, current, message), current);
        Assert.True(history.HasFlowIdentity);
        Assert.Equal(2, history.Records.Count);
        Assert.Single(history.BuildRows(), row => row.Message == null);
    }

    [Fact]
    public void MissingFlowTableOrNodeIdentityDoesNotGuessOtherBatches()
    {
        string path = Path.Combine(_directory, "old.db");
        FlowNodeRecord current;
        FlowNodeMessage message;
        using (SqlSugarClient db = CreateDatabase(path))
        {
            (current, message) = AddNode(db, 1, "one", "fov", "one");
            AddNode(db, 2, "two", "fov", "two");
            db.Ado.ExecuteCommand("DROP TABLE FlowRunRecord");
        }
        var source = new FlowAnalysisDataSource(path);
        FlowNodeComparisonHistory history = source.GetNodeComparisonHistory(Session(source, current, message), current);
        Assert.False(history.HasFlowIdentity);
        Assert.Equal(current.Id, Assert.Single(history.Records).Id);
        current.NodeId = string.Empty;
        var run = new FlowRunRecord { BatchId = 1, SerialNumber = "one", FlowKey = "known" };
        Assert.False(source.GetNodeComparisonHistory(Session(source, current, message, run), current).HasFlowIdentity);
    }

    [Fact]
    public void RepeatedNodeCallsKeepLegacyAndExplicitMessagesSeparate()
    {
        FlowNodeRecord first = new() { Id = 1, BatchId = 1, SerialNumber = "sn", NodeId = "fov", StartTime = _time, EndTime = _time.AddSeconds(1), ElapsedMs = 1000 };
        FlowNodeRecord second = new() { Id = 2, BatchId = 1, SerialNumber = "sn", NodeId = "fov", StartTime = _time.AddSeconds(2), EndTime = _time.AddSeconds(3), ElapsedMs = 1000 };
        FlowNodeMessage firstMessage = new() { Id = 1, BatchId = 1, SerialNumber = "sn", NodeId = "fov", SendTime = _time.AddMilliseconds(100) };
        FlowNodeMessage secondMessage = new() { Id = 2, BatchId = 1, SerialNumber = "sn", NodeId = "fov", NodeRecordId = 2, SendTime = _time.AddMilliseconds(2100), State = FlowMessageState.Timeout };
        var history = new FlowNodeComparisonHistory([first, second], [firstMessage, secondMessage], true);
        IReadOnlyList<FlowNodeComparisonRow> rows = history.BuildRows();
        Assert.Equal(2, rows.Count);
        Assert.Same(firstMessage, Assert.Single(rows, row => row.Record.Id == 1).Message);
        Assert.Same(secondMessage, Assert.Single(rows, row => row.Record.Id == 2).Message);
        Assert.Equal("—", Assert.Single(rows, row => row.Record.Id == 2).DurationText);
    }

    [Fact]
    public void SelectingAndFilteringBatchesKeepsThePinnedPayload()
    {
        string path = Path.Combine(_directory, "ui.db");
        FlowNodeRecord current;
        FlowNodeMessage currentMessage;
        FlowRunRecord run;
        using (SqlSugarClient db = CreateDatabase(path))
        {
            run = AddRun(db, 10, "current", "flow-a");
            (current, currentMessage) = AddNode(db, 10, "current", "fov", "{\"FOV\":40}");
            AddRun(db, 11, "previous", "flow-a");
            AddNode(db, 11, "previous", "fov", "{\"FOV\":42}");
        }
        WpfTestHost.Invoke(() =>
        {
            var source = new FlowAnalysisDataSource(path);
            var page = new FlowNodeComparisonPage(Session(source, current, currentMessage, run), current, () => { });
            var list = (ListView)page.FindName("HistoryListView");
            var baseline = (TextBox)page.FindName("BaselineReceiveTextBox");
            var selected = (TextBox)page.FindName("SelectedReceiveTextBox");
            var search = (TextBox)page.FindName("SearchTextBox");
            page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            try
            {
                PumpUntil(() => baseline.Text.Contains("40") && list.Items.Count == 2);
                list.SelectedItem = list.Items.Cast<FlowNodeComparisonRow>().Single(row => row.BatchId == 11);
                PumpUntil(() => selected.Text.Contains("42"));
                Assert.Contains("40", baseline.Text);
                search.Text = "previous";
                Assert.Single(list.Items.Cast<FlowNodeComparisonRow>());
                Assert.Contains("40", baseline.Text);
                ((Button)page.FindName("BaselineButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                PumpUntil(() => baseline.Text.Contains("42"));
                search.Text = "no-such-run";
                Assert.Empty(list.Items.Cast<FlowNodeComparisonRow>());
                Assert.Contains("42", baseline.Text);
                Assert.False(((Button)page.FindName("BaselineButton")).IsEnabled);
            }
            finally { page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
        });
    }

    private static void PumpUntil(Func<bool> complete)
    {
        var frame = new DispatcherFrame();
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.Background,
            (_, _) => { if (complete() || DateTime.UtcNow >= deadline) frame.Continue = false; }, Dispatcher.CurrentDispatcher);
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); }
        Assert.True(complete(), "Asynchronous comparison page did not finish loading.");
    }

    private SqlSugarClient CreateDatabase(string path)
    {
        var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source={path};Pooling=False", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        FlowDiagnosticsSchemaMigrator.EnsureSchema(db);
        return db;
    }

    private FlowRunRecord AddRun(SqlSugarClient db, int? batch, string serial, string key)
    {
        var run = new FlowRunRecord { BatchId = batch, SerialNumber = serial, TemplateId = 1, FlowKey = key, FlowName = "same-name", CompletedTime = _time, RunKey = Guid.NewGuid().ToString() };
        run.Id = db.Insertable(run).ExecuteReturnIdentity();
        return run;
    }

    private (FlowNodeRecord, FlowNodeMessage) AddNode(SqlSugarClient db, int batch, string serial, string node, string payload)
    {
        var record = new FlowNodeRecord { BatchId = batch, SerialNumber = serial, NodeId = node, NodeName = "FOV计算", NodeType = "LocalFOV", StartTime = _time, EndTime = _time.AddMilliseconds(6), ElapsedMs = 6 };
        record.Id = db.Insertable(record).ExecuteReturnIdentity();
        var message = new FlowNodeMessage { BatchId = batch, SerialNumber = serial, NodeId = node, NodeName = record.NodeName, NodeRecordId = record.Id, MsgId = Guid.NewGuid().ToString(), EventName = "Complete", SendTime = _time, RecvTime = _time.AddMilliseconds(6), State = FlowMessageState.Success };
        message.Id = db.Insertable(message).ExecuteReturnIdentity();
        FlowNodeMessagePayloadStorage.SaveRecvPayload(db, message.Id, payload);
        return (record, message);
    }

    private FlowExecutionAnalysisSession Session(FlowAnalysisDataSource source, FlowNodeRecord record, FlowNodeMessage message, FlowRunRecord? run = null)
        => new(record.BatchId, record.SerialNumber, null, run, [record], [message], [], _time, 30000, source);

    public void Dispose()
    {
        string target = Path.GetFullPath(_directory);
        if (!target.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unexpected test directory.");
        Directory.Delete(target, recursive: true);
    }
}
