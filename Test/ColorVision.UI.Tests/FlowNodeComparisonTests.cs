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
            db.Ado.ExecuteCommand("""
                WITH RECURSIVE history(batch_id) AS (
                    VALUES (100) UNION ALL SELECT batch_id + 1 FROM history WHERE batch_id < 200
                )
                INSERT INTO FlowNodeRecord (batch_id, serial_number, node_id, start_time, elapsed_ms)
                SELECT batch_id, 'other-history', 'fov', @time, 20 FROM history;
                """, new SugarParameter("@time", _time));
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
        Assert.Equal(new[] { "previous", "current" }, history.Records.Select(item => item.SerialNumber));
        Assert.Equal(2, history.Messages.Count);
        Assert.All(history.Messages, message => { Assert.Null(message.SendPayload); Assert.Null(message.RecvPayload); });
        Assert.Equal("a-current", source.GetMessagePayloads(currentMessage.Id).RecvPayload);
        Assert.Equal("b-current", new FlowAnalysisDataSource(otherPath).GetMessagePayloads(currentMessage.Id).RecvPayload);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void OfflineRunHistoryKeepsLocalKeysAndPositiveLegacyFallbackSeparate()
    {
        string path = Path.Combine(_directory, "run-identities.db");
        const string flowKey = "local-flow:known";
        int local;
        int sameKey;
        int missingKey;
        int emptyKey;
        using (var db = CreateDatabase(path))
        {
            local = AddHistoryRun(db, -7, flowKey, 10);
            emptyKey = AddHistoryRun(db, 42, "", 15);
            missingKey = AddHistoryRun(db, 42, null, 20);
            sameKey = AddHistoryRun(db, 77, flowKey, 30);
            AddHistoryRun(db, -7, null, 40);
            AddHistoryRun(db, 42, "LOCAL-FLOW:KNOWN", 45);
            AddHistoryRun(db, 4, null, 55);
        }
        byte[] before = File.ReadAllBytes(path);
        var source = new FlowAnalysisDataSource(path);

        Assert.Equal(new[] { sameKey, local },
            source.GetFlowRuns(new FlowIdentity(-7, flowKey, "Shared")).Select(run => run.Id));
        Assert.Equal(new[] { sameKey, missingKey, emptyKey, local },
            source.GetFlowRuns(new FlowIdentity(42, flowKey, "Shared")).Select(run => run.Id));
        Assert.Equal(before, File.ReadAllBytes(path));

        int AddHistoryRun(SqlSugarClient db, int templateId, string? key, int offsetMs) => db.Insertable(new FlowRunRecord
        {
            TemplateId = templateId,
            FlowKey = key,
            FlowName = "Shared",
            RunKey = Guid.NewGuid().ToString("N"),
            CompletedTime = _time.AddMilliseconds(offsetMs),
        }).ExecuteReturnIdentity();
    }

    [Fact]
    public void OfflineMessageDetailReadsBothPayloadsFromPartiallyMigratedDatabase()
    {
        string path = Path.Combine(_directory, "partial-payloads.db");
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE FlowNodeRecord (id INTEGER, batch_id INTEGER, serial_number TEXT, node_id TEXT, start_time TEXT, elapsed_ms INTEGER);
                CREATE TABLE FlowNodeMessage (id INTEGER PRIMARY KEY, send_payload TEXT, recv_payload_gzip BLOB);
                INSERT INTO FlowNodeMessage VALUES (1, 'legacy send', @recv);
                """;
            command.Parameters.AddWithValue("@recv", ColorVision.Database.GzipTextPayloadCodec.Encode("压缩接收正文").CompressedBytes!);
            command.ExecuteNonQuery();
        }

        var source = new FlowAnalysisDataSource(path);
        Assert.Equal(new FlowNodeMessagePayloads("legacy send", "压缩接收正文"), source.GetMessagePayloads(1));
        Assert.Equal(default, source.GetMessagePayloads(2));
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
    public void LegacyRunLookupRequiresOneExactSerialWithoutABatch()
    {
        string path = Path.Combine(_directory, "legacy-serial.db");
        int legacyId;
        using (var db = CreateDatabase(path))
        {
            legacyId = AddRun(db, null, "Exact ", "legacy-flow").Id;
            AddRun(db, 100, "Exact ", "modern-flow");
            AddRun(db, null, "Repeated", "ambiguous-first");
            AddRun(db, null, "Repeated", "ambiguous-second");
        }
        byte[] before = File.ReadAllBytes(path);
        var source = new FlowAnalysisDataSource(path);

        Assert.Equal(legacyId, Assert.Single(Lookup("Exact ")).Id);
        Assert.Empty(Lookup("Exact"));
        Assert.Empty(Lookup("exact "));
        Assert.Empty(Lookup("Repeated"));
        Assert.Equal(before, File.ReadAllBytes(path));

        List<FlowRunRecord> Lookup(string serial) => source.GetSameFlowRuns(
            new FlowExecutionAnalysisSession(100, serial, null, null, [], [], [], _time, 0, source));
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

    [Theory]
    [InlineData(null)]
    [InlineData(" \t\u3000")]
    public void HistoryAssignsAnUnlinkedLegacyTimeoutToOnlyTheClosestExecution(string? messageSerial)
    {
        FlowNodeRecord first = new() { Id = 1, BatchId = 0, SerialNumber = "first", NodeId = "fov", StartTime = _time, EndTime = _time.AddSeconds(2), ElapsedMs = 2000 };
        FlowNodeRecord second = new() { Id = 2, BatchId = 0, SerialNumber = "second", NodeId = "fov", StartTime = _time.AddMilliseconds(500), EndTime = _time.AddSeconds(2), ElapsedMs = 1500 };
        FlowNodeMessage timeout = new() { Id = 1, BatchId = 0, SerialNumber = messageSerial!, NodeId = "fov", SendTime = _time.AddMilliseconds(550), State = FlowMessageState.Timeout };

        var history = FlowExecutionAnalysisPresentation.BuildNodeHistoryItems([first, second], [timeout], _time.AddSeconds(3));

        var firstItem = Assert.Single(history, item => item.Record.Id == first.Id);
        Assert.Equal(FlowNodeExecutionOutcome.Completed, firstItem.Outcome);
        Assert.False(firstItem.IsTimedOut);
        var secondItem = Assert.Single(history, item => item.Record.Id == second.Id);
        Assert.Equal(FlowNodeExecutionOutcome.Failed, secondItem.Outcome);
        Assert.True(secondItem.IsTimedOut);
    }

    [Fact]
    public void HistoryAssignsEquidistantLegacyTimeoutToEarliestStartThenLowestId()
    {
        FlowNodeRecord first = new() { Id = 8, BatchId = 1, SerialNumber = "sn", NodeId = "fov", StartTime = _time, EndTime = _time.AddSeconds(1) };
        FlowNodeRecord second = new() { Id = 9, BatchId = 1, SerialNumber = "sn", NodeId = "fov", StartTime = _time, EndTime = _time.AddSeconds(1) };
        FlowNodeRecord later = new() { Id = 1, BatchId = 1, SerialNumber = "sn", NodeId = "fov", StartTime = _time.AddMilliseconds(500), EndTime = _time.AddSeconds(1) };
        FlowNodeMessage timeout = new() { Id = 1, BatchId = 1, SerialNumber = "sn", NodeId = "fov", SendTime = _time.AddMilliseconds(250), State = FlowMessageState.Timeout };

        var history = FlowExecutionAnalysisPresentation.BuildNodeHistoryItems([later, second, first], [timeout], _time.AddSeconds(2));

        Assert.Same(first, Assert.Single(history, item => item.IsTimedOut).Record);
    }

    [Fact]
    public void LinkedMessagesKeepBatchAndSerialRulesAndTakePrecedenceOverLegacyMatches()
    {
        var record = new FlowNodeRecord
        {
            Id = 5, BatchId = 3, SerialNumber = "sn", NodeId = "fov", StartTime = _time,
            EndTime = _time.AddSeconds(1), ElapsedMs = 1000,
        };
        var matching = new FlowNodeMessage { Id = 1, BatchId = 3, SerialNumber = "sn", NodeRecordId = 5, SendTime = _time.AddMilliseconds(2) };
        var blankSerial = new FlowNodeMessage { Id = 2, BatchId = 3, SerialNumber = " ", NodeRecordId = 5, SendTime = _time };
        var otherSerial = new FlowNodeMessage { Id = 3, BatchId = 3, SerialNumber = "other", NodeRecordId = 5, SendTime = _time, State = FlowMessageState.Timeout };
        var otherBatch = new FlowNodeMessage { Id = 4, BatchId = 4, SerialNumber = "sn", NodeRecordId = 5, SendTime = _time, State = FlowMessageState.Timeout };
        var legacy = new FlowNodeMessage { Id = 5, BatchId = 3, SerialNumber = "sn", NodeId = "fov", SendTime = _time, State = FlowMessageState.Timeout };
        var session = new FlowExecutionAnalysisSession(3, "sn", null, null, [record],
            [matching, blankSerial, otherSerial, otherBatch, legacy], [], _time, 30_000);

        Assert.Equal(new[] { blankSerial, matching }, session.GetMessages(record));
        Assert.False(Assert.Single(session.DurationItems).IsTimedOut);
    }

    [Fact]
    public void LegacyMessageOwnershipStaysScopedToTheRecordSnapshot()
    {
        var first = new FlowNodeRecord
        {
            Id = 1, BatchId = 3, SerialNumber = "one", NodeId = "fov", NodeName = "FOV",
            StartTime = _time, EndTime = _time.AddSeconds(1),
        };
        var second = new FlowNodeRecord
        {
            Id = 2, BatchId = 3, SerialNumber = "two", NodeId = "fov", NodeName = "FOV",
            StartTime = _time.AddMilliseconds(500), EndTime = _time.AddSeconds(1),
        };
        var legacy = new FlowNodeMessage
        {
            Id = 1, BatchId = 3, NodeName = "FOV",
            SendTime = _time.AddMilliseconds(550), State = FlowMessageState.Timeout,
        };
        var unmatched = new FlowNodeMessage
        {
            Id = 2, BatchId = 3, NodeName = "FOV",
            SendTime = _time.AddSeconds(2), State = FlowMessageState.Timeout,
        };
        var lookup = new FlowExecutionAnalysisPresentation.NodeMessageLookup([first], [unmatched, legacy]);
        var largerLookup = new FlowExecutionAnalysisPresentation.NodeMessageLookup([first, second], [legacy, unmatched]);

        Assert.Same(legacy, Assert.Single(lookup.GetMessages(second)));
        Assert.Same(legacy, Assert.Single(lookup.GetMessages(first)));
        Assert.Empty(largerLookup.GetMessages(first));
        Assert.Same(legacy, Assert.Single(largerLookup.GetMessages(second)));
        Assert.Same(legacy, Assert.Single(lookup.GetMessages(first)));
    }

    [Fact]
    public void LargeLegacyBatchKeepsWindowBoundariesAndSnapshotOwnership()
    {
        const int batchId = 3;
        FlowNodeRecord first = new()
        {
            Id = 1, BatchId = batchId, SerialNumber = "sn", NodeId = "fov", NodeName = "FOV",
            StartTime = _time, EndTime = _time.AddMilliseconds(100),
        };
        FlowNodeRecord open = new()
        {
            Id = 2, BatchId = batchId, SerialNumber = "sn", NodeId = "open", NodeName = "Open",
            StartTime = _time.AddSeconds(-2),
        };
        FlowNodeRecord sameStartHigherId = new()
        {
            Id = 3, BatchId = batchId, SerialNumber = first.SerialNumber, NodeId = first.NodeId, NodeName = first.NodeName,
            StartTime = first.StartTime, EndTime = first.EndTime,
        };
        FlowNodeRecord[] fillers = Enumerable.Range(0, 20).Select(index => new FlowNodeRecord
        {
            Id = index + 4, BatchId = batchId, SerialNumber = "sn", NodeId = "fov", NodeName = "FOV",
            StartTime = _time.AddSeconds(index + 10), EndTime = _time.AddSeconds(index + 11),
        }).ToArray();
        FlowNodeMessage lower = new() { Id = 2, BatchId = batchId, NodeName = "FOV", SendTime = _time.AddMilliseconds(-250) };
        FlowNodeMessage upper = new() { Id = 3, BatchId = batchId, NodeId = "fov", SendTime = _time.AddMilliseconds(350) };
        FlowNodeMessage equalUpper = new() { Id = 4, BatchId = batchId, NodeId = "fov", SendTime = upper.SendTime };
        FlowNodeMessage tooEarly = new() { Id = 1, BatchId = batchId, NodeId = "fov", SendTime = lower.SendTime.AddTicks(-1) };
        FlowNodeMessage tooLate = new() { Id = 5, BatchId = batchId, NodeId = "fov", SendTime = upper.SendTime.AddTicks(1) };
        FlowNodeMessage lateOpen = new() { Id = 6, BatchId = batchId, NodeId = "open", SendTime = _time.AddDays(1) };
        var lookup = new FlowExecutionAnalysisPresentation.NodeMessageLookup([open, sameStartHigherId, .. fillers, first],
            [tooLate, equalUpper, lower, tooEarly, upper, lateOpen]);

        Assert.Equal(new[] { lower, equalUpper, upper }, lookup.GetMessages(first));
        Assert.Empty(lookup.GetMessages(sameStartHigherId));
        Assert.Same(lateOpen, Assert.Single(lookup.GetMessages(open)));

        FlowNodeRecord copy = new()
        {
            Id = first.Id, BatchId = batchId, SerialNumber = first.SerialNumber, NodeId = first.NodeId, NodeName = first.NodeName,
            StartTime = _time.AddDays(1), EndTime = _time.AddDays(1),
        };
        Assert.Equal(lookup.GetMessages(first), lookup.GetMessages(copy));

        FlowNodeRecord external = new()
        {
            Id = 100, BatchId = batchId, SerialNumber = first.SerialNumber, NodeId = first.NodeId,
            StartTime = upper.SendTime, EndTime = upper.SendTime,
        };
        Assert.Equal(new[] { equalUpper, upper, tooLate }, lookup.GetMessages(external));
        Assert.Equal(new[] { lower, equalUpper, upper }, lookup.GetMessages(first));
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
            SynchronizationContext? previousContext = SynchronizationContext.Current;
            var operations = new AsyncVoidDrainContext(previousContext ?? new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            SynchronizationContext.SetSynchronizationContext(operations);
            try
            {
                page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                Assert.True(operations.ObservedOperation);
                PumpUntil(() => baseline.Text.Contains("40") && selected.Text.Contains("40") && list.Items.Count == 2);
                list.SelectedItem = list.Items.Cast<FlowNodeComparisonRow>().Single(row => row.BatchId == 11);
                PumpUntil(() => selected.Text.Contains("42"));
                Assert.Contains("40", baseline.Text);
                search.Text = "previous";
                Assert.Single(list.Items.Cast<FlowNodeComparisonRow>());
                PumpUntil(() => selected.Text.Contains("42"));
                Assert.Contains("40", baseline.Text);
                ((Button)page.FindName("BaselineButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                PumpUntil(() => baseline.Text.Contains("42"));
                search.Text = "no-such-run";
                Assert.Empty(list.Items.Cast<FlowNodeComparisonRow>());
                Assert.Contains("42", baseline.Text);
                Assert.False(((Button)page.FindName("BaselineButton")).IsEnabled);
            }
            finally
            {
                try
                {
                    page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                    // Unloading suppresses obsolete UI results. Wait for their database reads before fixture disposal.
                    PumpUntil(() => !operations.HasPendingOperations);
                }
                finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
            }
        });
    }

    private sealed class AsyncVoidDrainContext(SynchronizationContext dispatcher) : SynchronizationContext
    {
        private int _pending;
        public bool HasPendingOperations => Volatile.Read(ref _pending) != 0;
        public bool ObservedOperation { get; private set; }

        public override void OperationStarted()
        {
            ObservedOperation = true;
            Interlocked.Increment(ref _pending);
        }
        public override void OperationCompleted() => Interlocked.Decrement(ref _pending);
        public override SynchronizationContext CreateCopy() => this;
        public override void Post(SendOrPostCallback callback, object? state) => dispatcher.Post(value => Invoke(callback, value), state);
        public override void Send(SendOrPostCallback callback, object? state) => dispatcher.Send(value => Invoke(callback, value), state);

        private void Invoke(SendOrPostCallback callback, object? state)
        {
            SynchronizationContext? previous = Current;
            SetSynchronizationContext(this);
            try { callback(state); }
            finally { SetSynchronizationContext(previous); }
        }
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
        // The analysis data source under test opens pooled connections to ui.db.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(target, recursive: true);
    }
}
