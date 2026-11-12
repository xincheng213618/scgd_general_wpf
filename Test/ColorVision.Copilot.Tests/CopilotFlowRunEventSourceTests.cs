using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Services.FlowDiagnostics;
using Microsoft.Data.Sqlite;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowRunEventSourceTests
{
    [Fact]
    public async Task EarlierWindowsTraverseOneExactRunsEventsAndSequenceTiesWithoutLosingOrRepeatingRows()
    {
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabase();
        fixture.AddRun(1);
        fixture.AddRun(2);
        var expected = new List<FlowExecutionEvent>();
        for (var number = 160; number >= 1; number--)
        {
            expected.Add(fixture.AddEvent(1, (number + 1) / 2));
            fixture.AddEvent(2, (number + 1) / 2, "Different run with the same sequence.");
        }
        var before = File.ReadAllBytes(fixture.DatabasePath);
        var remaining = expected.OrderByDescending(item => item.SequenceNo).ThenByDescending(item => item.Id).ToList();
        var delivered = new List<long>();
        long? anchor = null;
        long? anchorSequence = null;
        do
        {
            var page = Assert.IsType<FlowRunEventPage>(await fixture.Reader.ReadPageAsync(1, 23, anchor, CancellationToken.None));
            var window = remaining.Take(23).OrderBy(item => item.SequenceNo).ThenBy(item => item.Id).ToArray();
            Assert.Equal(1, page.RunRecordId);
            Assert.Equal(anchorSequence, page.AnchorSequenceNo);
            Assert.Equal(window.Select(item => item.Id), page.Events.Select(item => item.Id));
            Assert.All(page.Events, item =>
            {
                Assert.Equal(1, item.RunRecordId);
                Assert.Null(item.EventKey);
                Assert.Null(item.DataJson);
            });
            Assert.False(page.TextWasTruncated);
            delivered.AddRange(page.Events.Select(item => item.Id));
            remaining.RemoveRange(0, window.Length);
            Assert.Equal(remaining.Count > 0, page.HasMore);
            anchor = page.Events[0].Id;
            anchorSequence = page.Events[0].SequenceNo;
        } while (remaining.Count > 0);
        Assert.Equal(160, delivered.Distinct().Count());
        Assert.Equal(expected.Select(item => item.Id).Order(), delivered.Order());
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
        Assert.Equal(new[] { "events.db" }, Directory.GetFiles(fixture.Root).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("deleted")]
    [InlineData("other-run")]
    [InlineData("sequence-changed")]
    public async Task ContinuationsRequireTheExactSameRunAnchorAndReportItsCurrentSequence(string change)
    {
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabase();
        fixture.AddRun(1);
        fixture.AddRun(2);
        fixture.AddEvent(1, 1);
        var anchor = fixture.AddEvent(1, 2);
        fixture.AddEvent(1, 3);
        var first = Assert.IsType<FlowRunEventPage>(await fixture.Reader.ReadPageAsync(1, 2, null, CancellationToken.None));
        Assert.Equal(anchor.Id, first.Events[0].Id);
        fixture.Execute(change switch
        {
            "deleted" => "DELETE FROM FlowExecutionEvent WHERE id=$id",
            "other-run" => "UPDATE FlowExecutionEvent SET run_record_id=2 WHERE id=$id",
            _ => "UPDATE FlowExecutionEvent SET sequence_no=77 WHERE id=$id",
        }, ("$id", anchor.Id));
        var before = File.ReadAllBytes(fixture.DatabasePath);

        if (change == "sequence-changed")
        {
            var page = Assert.IsType<FlowRunEventPage>(await fixture.Reader.ReadPageAsync(1, 10, anchor.Id, CancellationToken.None));
            Assert.Equal(77L, page.AnchorSequenceNo); // The service compares this stamp to the previous cursor, rather than silently moving it.
            Assert.Equal(new long[] { 1, 3 }, page.Events.Select(item => item.SequenceNo));
            Assert.False(page.HasMore);
        }
        else
            await Assert.ThrowsAsync<ArgumentException>(() => fixture.Reader.ReadPageAsync(1, 10, anchor.Id, CancellationToken.None));
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Fact]
    public async Task MissingRunsRemainMissingAndEmptyRunsAreDifferentFromMissingEventSchema()
    {
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabase(includeEvents: false);
        fixture.AddRun(1);
        Assert.Null(await fixture.Reader.ReadPageAsync(999, 10, null, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Reader.ReadPageAsync(1, 10, null, CancellationToken.None));
        fixture.CreateEventTable();
        fixture.AddEvent(999, 1); // An orphan must not manufacture a retained run or fall back by other metadata.
        var before = File.ReadAllBytes(fixture.DatabasePath);

        var empty = Assert.IsType<FlowRunEventPage>(await fixture.Reader.ReadPageAsync(1, 10, null, CancellationToken.None));

        Assert.Empty(empty.Events);
        Assert.False(empty.HasMore);
        Assert.Null(empty.AnchorSequenceNo);
        Assert.Null(await fixture.Reader.ReadPageAsync(999, 10, null, CancellationToken.None));
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Fact]
    public async Task SafeProjectionNeedsOnlyRunIdAndEventEvidenceColumnsAndBindsTheNormalizedSourcePath()
    {
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabase();
        fixture.AddRun(1);
        var saved = fixture.AddEvent(1, 1, attemptId: 123);
        fixture.Execute("ALTER TABLE FlowExecutionEvent DROP COLUMN event_key");
        fixture.Execute("ALTER TABLE FlowExecutionEvent DROP COLUMN data_json");
        var before = File.ReadAllBytes(fixture.DatabasePath);

        var page = Assert.IsType<FlowRunEventPage>(await fixture.Reader.ReadPageAsync(1, 10, null, CancellationToken.None));
        string normalized = Path.Combine(fixture.Root, ".", "events.db").Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
        var alternate = Assert.IsType<FlowRunEventPage>(await new FlowRunEventReader(normalized).ReadPageAsync(1, 10, null, CancellationToken.None));

        var item = Assert.Single(page.Events);
        Assert.Equal(saved.Id, item.Id);
        Assert.Equal(123L, item.AttemptId);
        Assert.Null(item.EventKey);
        Assert.Null(item.DataJson);
        Assert.Equal(page.SourceKey, alternate.SourceKey);
        Assert.Equal(64, page.SourceKey.Length);
        Assert.All(page.SourceKey, character => Assert.True(char.IsAsciiHexDigit(character)));
        Assert.DoesNotContain(fixture.Root, page.SourceKey, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SqlPreprojectionAndUtf16SafeBoundsApplyOnlyToDeliveredRows(bool utf16)
    {
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabase(utf16: utf16);
        fixture.AddRun(1);
        var identifier = new string('i', 255) + "😀tail";
        var diagnostic = new string('d', 2047) + "😀tail";
        var old = fixture.AddEvent(1, 1, diagnostic, identifier, identifier, identifier);
        var recent = fixture.AddEvent(1, 2);
        if (utf16) Assert.StartsWith("UTF-16", Convert.ToString(fixture.Scalar("PRAGMA encoding")), StringComparison.OrdinalIgnoreCase);
        var before = File.ReadAllBytes(fixture.DatabasePath);

        var first = Assert.IsType<FlowRunEventPage>(await fixture.Reader.ReadPageAsync(1, 1, null, CancellationToken.None));
        var second = Assert.IsType<FlowRunEventPage>(await fixture.Reader.ReadPageAsync(1, 1, recent.Id, CancellationToken.None));

        Assert.Equal(recent.Id, Assert.Single(first.Events).Id);
        Assert.True(first.HasMore);
        Assert.False(first.TextWasTruncated); // The oversized look-ahead row was not delivered in this window.
        var row = Assert.Single(second.Events);
        Assert.Equal(old.Id, row.Id);
        Assert.Equal(new string('i', 255), row.EventType);
        Assert.Equal(new string('i', 255), row.NodeId);
        Assert.Equal(new string('i', 255), row.Code);
        Assert.Equal(new string('d', 2047), row.Message);
        Assert.True(second.TextWasTruncated);
        Assert.False(second.HasMore);
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Theory]
    [InlineData(2.5)]
    [InlineData("not-an-integer")]
    [InlineData(null)]
    public async Task NonIntegerSequenceKeysAreNeverTruncatedIntoValidPageOrAnchorKeys(object? sequence)
    {
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabase();
        fixture.AddRun(1);
        var malformed = fixture.AddEvent(1, 2);
        fixture.Execute("UPDATE FlowExecutionEvent SET sequence_no=$value WHERE id=$id", ("$value", sequence), ("$id", malformed.Id));
        var before = File.ReadAllBytes(fixture.DatabasePath);

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Reader.ReadPageAsync(1, 10, null, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Reader.ReadPageAsync(1, 10, malformed.Id, CancellationToken.None));

        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Fact]
    public async Task NullSequenceKeysCannotDisappearBehindAnEarlierWindowBoundary()
    {
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabase();
        fixture.AddRun(1);
        var malformed = fixture.AddEvent(1, 0);
        fixture.Execute("UPDATE FlowExecutionEvent SET sequence_no=NULL WHERE id=$id", ("$id", malformed.Id));
        for (var sequence = 1; sequence <= 101; sequence++) fixture.AddEvent(1, sequence);
        var before = File.ReadAllBytes(fixture.DatabasePath);
        var first = Assert.IsType<FlowRunEventPage>(await fixture.Reader.ReadPageAsync(1, 100, null, CancellationToken.None));
        Assert.True(first.HasMore);
        Assert.Equal(2L, first.Events[0].SequenceNo);

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Reader.ReadPageAsync(1, 100, first.Events[0].Id, CancellationToken.None));

        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Fact]
    public async Task EmbeddedNulTextReturnsOnlyItsSafePrefixAndExplicitlyReportsTruncationInUtf16Storage()
    {
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabase(utf16: true);
        fixture.AddRun(1);
        const string identifier = "visible-id\0PRIVATE_HIDDEN_TAIL";
        const string diagnostic = "visible diagnostic\0PRIVATE_HIDDEN_TAIL";
        fixture.AddEvent(1, 1, diagnostic, identifier, identifier, identifier);
        Assert.True(Convert.ToInt64(fixture.Scalar("SELECT length(CAST(message AS BLOB)) FROM FlowExecutionEvent")) > "visible diagnostic".Length * 2);
        var before = File.ReadAllBytes(fixture.DatabasePath);

        var page = Assert.IsType<FlowRunEventPage>(await fixture.Reader.ReadPageAsync(1, 10, null, CancellationToken.None));

        var item = Assert.Single(page.Events);
        Assert.Equal("visible-id", item.EventType);
        Assert.Equal("visible-id", item.NodeId);
        Assert.Equal("visible-id", item.Code);
        Assert.Equal("visible diagnostic", item.Message);
        Assert.True(page.TextWasTruncated);
        Assert.False(page.HasMore);
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Fact]
    public async Task SequenceKeyValidationIncludesLookAheadAndRetainsFullInt64Precision()
    {
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabase();
        fixture.AddRun(1);
        var earlier = fixture.AddEvent(1, long.MaxValue - 1);
        var later = fixture.AddEvent(1, long.MaxValue);
        var first = Assert.IsType<FlowRunEventPage>(await fixture.Reader.ReadPageAsync(1, 1, null, CancellationToken.None));
        var second = Assert.IsType<FlowRunEventPage>(await fixture.Reader.ReadPageAsync(1, 1, later.Id, CancellationToken.None));
        Assert.Equal(later.Id, Assert.Single(first.Events).Id);
        Assert.Equal(earlier.Id, Assert.Single(second.Events).Id);
        Assert.Equal(long.MaxValue, second.AnchorSequenceNo);
        fixture.Execute("UPDATE FlowExecutionEvent SET sequence_no=0.5 WHERE id=$id", ("$id", earlier.Id));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Reader.ReadPageAsync(1, 1, null, CancellationToken.None));
    }

    [Theory]
    [InlineData("missing-file")]
    [InlineData("run-table")]
    [InlineData("run-id")]
    [InlineData("event-column")]
    public async Task MissingFilesAndRequiredSchemaFailWithoutCreatingOrMigratingStorage(string boundary)
    {
        using var fixture = new DatabaseFixture();
        if (boundary != "missing-file")
        {
            fixture.CreateDatabase();
            fixture.AddRun(1);
            fixture.Execute(boundary switch
            {
                "run-table" => "DROP TABLE FlowRunRecord",
                "run-id" => "ALTER TABLE FlowRunRecord RENAME COLUMN id TO unexpected_id",
                _ => "ALTER TABLE FlowExecutionEvent DROP COLUMN message",
            });
        }
        var before = File.Exists(fixture.DatabasePath) ? File.ReadAllBytes(fixture.DatabasePath) : null;

        var failure = await Record.ExceptionAsync(() => fixture.Reader.ReadPageAsync(1, 10, null, CancellationToken.None));

        if (boundary == "missing-file")
        {
            Assert.IsType<FileNotFoundException>(failure);
            Assert.Empty(Directory.GetFiles(fixture.Root));
        }
        else
        {
            Assert.IsType<InvalidDataException>(failure);
            Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
            Assert.Equal(new[] { "events.db" }, Directory.GetFiles(fixture.Root).Select(Path.GetFileName));
        }
    }

    [Fact]
    public async Task InvalidArgumentsAndPreCancellationDoNotOpenOrCreateStorage()
    {
        using var fixture = new DatabaseFixture();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Reader.ReadPageAsync(0, 10, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Reader.ReadPageAsync(1, 0, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Reader.ReadPageAsync(1, 101, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Reader.ReadPageAsync(1, 10, 0, CancellationToken.None));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Reader.ReadPageAsync(1, 10, null, cancellation.Token));
        Assert.Empty(Directory.GetFiles(fixture.Root));
    }

    internal sealed class DatabaseFixture : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("CopilotFlowRunEvents-").FullName;
        public string DatabasePath => Path.Combine(Root, "events.db");
        public FlowRunEventReader Reader => new(DatabasePath);
        private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString();
        private const string EventSchema = """
            CREATE TABLE FlowExecutionEvent(id INTEGER PRIMARY KEY AUTOINCREMENT,run_record_id INTEGER,sequence_no INTEGER,
                event_type TEXT,occurred_time_utc TEXT,node_id TEXT,attempt_id INTEGER,code TEXT,message TEXT,event_key TEXT,data_json TEXT)
            """;

        public void CreateDatabase(bool includeEvents = true, bool utf16 = false)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            if (utf16)
            {
                command.CommandText = "PRAGMA encoding='UTF-16'";
                command.ExecuteNonQuery();
            }
            command.CommandText = "CREATE TABLE FlowRunRecord(id INTEGER PRIMARY KEY)";
            command.ExecuteNonQuery();
            if (includeEvents)
            {
                command.CommandText = EventSchema;
                command.ExecuteNonQuery();
            }
        }

        public void CreateEventTable() => Execute(EventSchema);
        public void AddRun(int runRecordId) => Execute("INSERT INTO FlowRunRecord(id) VALUES($id)", ("$id", runRecordId));

        public FlowExecutionEvent AddEvent(int runRecordId, long sequence, string message = "Recorded event.",
            string? eventType = null, string? nodeId = null, string? code = null, long? attemptId = null)
        {
            var item = new FlowExecutionEvent
            {
                RunRecordId = runRecordId, SequenceNo = sequence, EventType = eventType ?? "NodeCompleted", NodeId = nodeId ?? "node-A",
                Code = code, AttemptId = attemptId, Message = message, OccurredTimeUtc = new(2026, 10, 27, 1, 0, 0, DateTimeKind.Utc),
            };
            item.Id = Convert.ToInt64(Scalar("""
                INSERT INTO FlowExecutionEvent(run_record_id,sequence_no,event_type,occurred_time_utc,node_id,attempt_id,code,message,event_key,data_json)
                VALUES($run,$sequence,$type,$time,$node,$attempt,$code,$message,'PRIVATE_EVENT_KEY','PRIVATE_RAW_JSON') RETURNING id
                """, ("$run", runRecordId), ("$sequence", sequence), ("$type", item.EventType), ("$time", item.OccurredTimeUtc),
                ("$node", item.NodeId), ("$attempt", attemptId), ("$code", code), ("$message", message)));
            return item;
        }

        public void Execute(string sql, params (string Name, object? Value)[] values)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var command = CreateCommand(connection, sql, values);
            command.ExecuteNonQuery();
        }

        public object? Scalar(string sql, params (string Name, object? Value)[] values)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var command = CreateCommand(connection, sql, values);
            return command.ExecuteScalar();
        }

        private static SqliteCommand CreateCommand(SqliteConnection connection, string sql, (string Name, object? Value)[] values)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            return command;
        }

        public void Dispose()
        {
            string root = Path.GetFullPath(Root);
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), Path.GetDirectoryName(root), ignoreCase: true);
            Assert.StartsWith("CopilotFlowRunEvents-", Path.GetFileName(root), StringComparison.Ordinal);
            Directory.Delete(root, recursive: true);
        }
    }
}
