using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Services.FlowDiagnostics;
using Microsoft.Data.Sqlite;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowRunAttemptSourceTests
{
    [Fact]
    public async Task EarlierWindowsTraverseOneExactRunsRepeatedAttemptsWithoutLosingOrMergingRows()
    {
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabase();
        fixture.AddRun(1);
        fixture.AddRun(2);
        var expected = new List<long>();
        for (int index = 0; index < 160; index++)
        {
            expected.Add(fixture.AddAttempt(1, "node-A", outcome: index % 2 == 0 ? null : "UnknownRecordedOutcome").Id);
            if (index % 8 == 0) fixture.AddAttempt(2, "node-A");
        }
        var before = File.ReadAllBytes(fixture.DatabasePath);
        var delivered = new List<FlowNodeAttempt>();
        long? anchor = null;
        bool hasMore;
        do
        {
            var page = Assert.IsType<FlowRunAttemptPage>(await fixture.Reader.ReadPageAsync(1, null, 100, anchor, CancellationToken.None));
            Assert.Equal(1, page.RunRecordId);
            Assert.InRange(page.Attempts.Count, 1, 100);
            Assert.Equal(page.Attempts.Select(attempt => attempt.Id).Order(), page.Attempts.Select(attempt => attempt.Id));
            Assert.All(page.Attempts, attempt => Assert.Equal(1, attempt.RunRecordId));
            Assert.False(page.TextWasTruncated);
            Assert.Empty(page.OutcomeTruncatedAttemptIds);
            delivered.AddRange(page.Attempts);
            hasMore = page.HasMore;
            anchor = page.Attempts[0].Id;
        } while (hasMore);

        Assert.Equal(expected, delivered.Select(attempt => attempt.Id).Order());
        Assert.Equal(160, delivered.Select(attempt => attempt.Id).Distinct().Count());
        Assert.All(delivered, attempt => Assert.Equal(1, attempt.AttemptNo));
        Assert.Contains(delivered, attempt => attempt.Outcome == null);
        Assert.Contains(delivered, attempt => attempt.Outcome == "UnknownRecordedOutcome");
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Fact]
    public async Task ExactNodeFiltersAreBinaryUntrimmedAndMatchTheWholeStoredIdentityBeforeProjection()
    {
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabase();
        fixture.AddRun(1);
        var exact = fixture.AddAttempt(1, "node-A");
        var padded = fixture.AddAttempt(1, " node-A ");
        fixture.AddAttempt(1, "NODE-A");
        fixture.AddAttempt(1, "node-A-extra");
        string longKey = new('n', 128);
        var fullLength = fixture.AddAttempt(1, longKey);
        fixture.AddAttempt(1, longKey + "different-suffix");

        var matching = Assert.IsType<FlowRunAttemptPage>(await fixture.Reader.ReadPageAsync(1, "node-A", 100, null, CancellationToken.None));
        var paddedPage = Assert.IsType<FlowRunAttemptPage>(await fixture.Reader.ReadPageAsync(1, " node-A ", 100, null, CancellationToken.None));
        var fullPage = Assert.IsType<FlowRunAttemptPage>(await fixture.Reader.ReadPageAsync(1, longKey, 100, null, CancellationToken.None));

        Assert.Equal(exact.Id, Assert.Single(matching.Attempts).Id);
        Assert.Equal(padded.Id, Assert.Single(paddedPage.Attempts).Id);
        Assert.Equal(" node-A ", Assert.Single(paddedPage.Attempts).NodeId);
        Assert.Equal(fullLength.Id, Assert.Single(fullPage.Attempts).Id);
        Assert.False(fullPage.TextWasTruncated);
    }

    [Theory]
    [InlineData("deleted")]
    [InlineData("other_run")]
    [InlineData("other_node")]
    public async Task ContinuationsRequireTheExactAnchorToRemainInTheRequestedRunAndNodeFilter(string change)
    {
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabase();
        fixture.AddRun(1);
        fixture.AddRun(2);
        fixture.AddAttempt(1, "node-A");
        var anchor = fixture.AddAttempt(1, "node-A");
        var first = Assert.IsType<FlowRunAttemptPage>(await fixture.Reader.ReadPageAsync(1, "node-A", 1, null, CancellationToken.None));
        Assert.Equal(anchor.Id, Assert.Single(first.Attempts).Id);
        fixture.Execute(change switch
        {
            "deleted" => "DELETE FROM FlowNodeAttempt WHERE id=$id",
            "other_run" => "UPDATE FlowNodeAttempt SET run_record_id=2 WHERE id=$id",
            _ => "UPDATE FlowNodeAttempt SET node_id='node-B' WHERE id=$id",
        }, ("$id", anchor.Id));
        var before = File.ReadAllBytes(fixture.DatabasePath);

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Reader.ReadPageAsync(1, "node-A", 1, anchor.Id, CancellationToken.None));

        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Fact]
    public async Task MissingRunsAreDistinctFromAnEmptyAttemptWindowOrUnavailableAttemptSchema()
    {
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabase(includeAttempts: false);

        Assert.Null(await fixture.Reader.ReadPageAsync(1, null, 10, null, CancellationToken.None));
        fixture.AddRun(1);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Reader.ReadPageAsync(1, null, 10, null, CancellationToken.None));
        fixture.CreateAttemptTable();
        var before = File.ReadAllBytes(fixture.DatabasePath);
        var empty = Assert.IsType<FlowRunAttemptPage>(await fixture.Reader.ReadPageAsync(1, null, 10, null, CancellationToken.None));

        Assert.Empty(empty.Attempts);
        Assert.False(empty.HasMore);
        Assert.False(empty.TextWasTruncated);
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Fact]
    public async Task OnlyRequiredEvidenceColumnsAreReadAndTheSourceKeyBindsTheNormalizedConfiguredPath()
    {
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabase();
        fixture.AddRun(1);
        fixture.AddAttempt(1, "node-A");
        var before = File.ReadAllBytes(fixture.DatabasePath);

        var page = Assert.IsType<FlowRunAttemptPage>(await fixture.Reader.ReadPageAsync(1, null, 10, null, CancellationToken.None));
        var same = Assert.IsType<FlowRunAttemptPage>(await new FlowRunAttemptReader(Path.Combine(fixture.Root.ToUpperInvariant(), ".", "attempts.db"))
            .ReadPageAsync(1, null, 10, null, CancellationToken.None));

        var attempt = Assert.Single(page.Attempts);
        Assert.Empty(attempt.InvocationId);
        Assert.Null(attempt.LegacyNodeRecordId);
        Assert.Equal("recorded-error-code", attempt.ErrorCode);
        Assert.Equal("Recorded diagnostic message.", attempt.ErrorMessage);
        Assert.Matches("^[a-f0-9]{64}$", page.SourceKey);
        Assert.Equal(page.SourceKey, same.SourceKey);
        Assert.DoesNotContain(fixture.Root, page.SourceKey, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
        Assert.Equal(new[] { "attempts.db" }, Directory.GetFiles(fixture.Root).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Utf16NulAndSurrogateBoundsReportExactlyWhichDeliveredOutcomesWereTruncated()
    {
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabase(utf16: true);
        fixture.AddRun(1);
        var nulOutcome = fixture.AddAttempt(1, "node-A", outcome: "Succeeded\0unrecognized-tail");
        var wideOutcome = fixture.AddAttempt(1, new string('n', 127) + "😀tail", outcome: new string('o', 255) + "😀tail",
            errorMessage: new string('d', 2047) + "😀tail");
        var intactOutcome = fixture.AddAttempt(1, "node-C", outcome: "Succeeded", errorMessage: "safe-prefix\0hidden-tail");
        var before = File.ReadAllBytes(fixture.DatabasePath);

        var page = Assert.IsType<FlowRunAttemptPage>(await fixture.Reader.ReadPageAsync(1, null, 100, null, CancellationToken.None));

        Assert.True(page.TextWasTruncated);
        Assert.Equal(new[] { nulOutcome.Id, wideOutcome.Id }, page.OutcomeTruncatedAttemptIds);
        Assert.Equal("Succeeded", page.Attempts.Single(attempt => attempt.Id == nulOutcome.Id).Outcome);
        var bounded = page.Attempts.Single(attempt => attempt.Id == wideOutcome.Id);
        Assert.Equal(new string('n', 127), bounded.NodeId);
        Assert.Equal(new string('o', 255), bounded.Outcome);
        Assert.Equal(new string('d', 2047), bounded.ErrorMessage);
        Assert.Equal("Succeeded", page.Attempts.Single(attempt => attempt.Id == intactOutcome.Id).Outcome);
        Assert.Equal("safe-prefix", page.Attempts.Single(attempt => attempt.Id == intactOutcome.Id).ErrorMessage);
        Assert.Throws<NotSupportedException>(() => Assert.IsAssignableFrom<IList<long>>(page.OutcomeTruncatedAttemptIds)[0] = 99);
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Fact]
    public async Task UndeliveredLookaheadDoesNotContributeToCoverageAndAttemptIdsKeepInt64Precision()
    {
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabase();
        fixture.AddRun(1);
        var older = fixture.AddAttempt(1, "node-A", outcome: "Succeeded\0tail", id: long.MaxValue - 1);
        var latest = fixture.AddAttempt(1, "node-A", id: long.MaxValue);

        var first = Assert.IsType<FlowRunAttemptPage>(await fixture.Reader.ReadPageAsync(1, null, 1, null, CancellationToken.None));
        var second = Assert.IsType<FlowRunAttemptPage>(await fixture.Reader.ReadPageAsync(1, null, 1, latest.Id, CancellationToken.None));

        Assert.Equal(long.MaxValue, Assert.Single(first.Attempts).Id);
        Assert.True(first.HasMore);
        Assert.False(first.TextWasTruncated);
        Assert.Empty(first.OutcomeTruncatedAttemptIds);
        Assert.Equal(older.Id, Assert.Single(second.Attempts).Id);
        Assert.False(second.HasMore);
        Assert.True(second.TextWasTruncated);
        Assert.Equal(new[] { older.Id }, second.OutcomeTruncatedAttemptIds);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(1.5)]
    public async Task InvalidAttemptKeysCannotBecomeUsablePositiveIntegerEvidence(object id)
    {
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabase(looseAttemptIds: true);
        fixture.AddRun(1);
        fixture.InsertRawAttemptId(id);

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Reader.ReadPageAsync(1, null, 10, null, CancellationToken.None));
    }

    [Fact]
    public async Task NullAttemptKeysCannotDisappearBehindAnEarlierWindowBoundary()
    {
        using var fixture = new DatabaseFixture();
        fixture.CreateDatabase(looseAttemptIds: true);
        fixture.AddRun(1);
        for (long id = 1; id <= 102; id++) fixture.AddAttempt(1, "node-A", id: id);
        fixture.InsertRawAttemptId(null);
        var first = Assert.IsType<FlowRunAttemptPage>(await fixture.Reader.ReadPageAsync(1, null, 100, null, CancellationToken.None));
        Assert.True(first.HasMore);

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Reader.ReadPageAsync(1, null, 100, first.Attempts[0].Id, CancellationToken.None));
    }

    [Theory]
    [InlineData("missing_file")]
    [InlineData("missing_run_table")]
    [InlineData("missing_attempt_column")]
    public async Task MissingFilesAndRequiredSchemaFailWithoutCreatingOrMigratingStorage(string scenario)
    {
        using var fixture = new DatabaseFixture();
        if (scenario != "missing_file")
        {
            fixture.CreateDatabase();
            fixture.AddRun(1);
            fixture.Execute(scenario == "missing_run_table" ? "DROP TABLE FlowRunRecord" : "ALTER TABLE FlowNodeAttempt DROP COLUMN error_message");
        }
        var before = File.Exists(fixture.DatabasePath) ? File.ReadAllBytes(fixture.DatabasePath) : null;

        var exception = await Record.ExceptionAsync(() => fixture.Reader.ReadPageAsync(1, null, 10, null, CancellationToken.None));

        if (scenario == "missing_file")
        {
            Assert.IsType<FileNotFoundException>(exception);
            Assert.Empty(Directory.GetFiles(fixture.Root));
        }
        else
        {
            Assert.IsType<InvalidDataException>(exception);
            Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
        }
    }

    [Fact]
    public async Task InvalidArgumentsAndPreCancellationNeverCreateStorage()
    {
        using var fixture = new DatabaseFixture();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Reader.ReadPageAsync(0, null, 10, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Reader.ReadPageAsync(1, null, 0, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Reader.ReadPageAsync(1, null, 101, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Reader.ReadPageAsync(1, null, 10, 0, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Reader.ReadPageAsync(1, "\u2003\t", 10, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Reader.ReadPageAsync(1, new string('n', 129), 10, null, CancellationToken.None));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Reader.ReadPageAsync(1, null, 10, null, cancellation.Token));
        Assert.Empty(Directory.GetFiles(fixture.Root));
    }

    private sealed class DatabaseFixture : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("CopilotFlowRunAttempts-").FullName;
        public string DatabasePath => Path.Combine(Root, "attempts.db");
        public FlowRunAttemptReader Reader => new(DatabasePath);
        private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString();

        public void CreateDatabase(bool includeAttempts = true, bool utf16 = false, bool looseAttemptIds = false)
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
            if (includeAttempts) CreateAttemptTable(looseAttemptIds);
            if (utf16)
            {
                command.CommandText = "PRAGMA encoding";
                Assert.StartsWith("UTF-16", Convert.ToString(command.ExecuteScalar()), StringComparison.OrdinalIgnoreCase);
            }
        }

        public void CreateAttemptTable(bool looseAttemptIds = false) => Execute($"""
            CREATE TABLE FlowNodeAttempt(id {(looseAttemptIds ? "NUMERIC" : "INTEGER PRIMARY KEY AUTOINCREMENT")},run_record_id INTEGER,
                node_id TEXT,attempt_no INTEGER,started_time_utc TEXT,completed_time_utc TEXT,outcome TEXT,error_code TEXT,error_message TEXT)
            """);

        public void AddRun(int id) => Execute("INSERT INTO FlowRunRecord(id) VALUES($id)", ("$id", id));

        public FlowNodeAttempt AddAttempt(int runId, string nodeId, string? outcome = "Succeeded",
            string errorMessage = "Recorded diagnostic message.", long? id = null)
        {
            var attempt = new FlowNodeAttempt
            {
                RunRecordId = runId, NodeId = nodeId, AttemptNo = 1, Outcome = outcome, ErrorCode = "recorded-error-code", ErrorMessage = errorMessage,
                StartedTimeUtc = new(2026, 10, 27, 1, 0, 0, DateTimeKind.Utc), CompletedTimeUtc = new(2026, 10, 27, 1, 0, 1, DateTimeKind.Utc),
            };
            attempt.Id = Convert.ToInt64(Scalar("""
                INSERT INTO FlowNodeAttempt(id,run_record_id,node_id,attempt_no,started_time_utc,completed_time_utc,outcome,error_code,error_message)
                VALUES($id,$run,$node,1,$started,$completed,$outcome,$code,$message) RETURNING id
                """, ("$id", id), ("$run", runId), ("$node", nodeId), ("$started", attempt.StartedTimeUtc),
                ("$completed", attempt.CompletedTimeUtc), ("$outcome", outcome), ("$code", attempt.ErrorCode), ("$message", errorMessage)));
            return attempt;
        }

        public void InsertRawAttemptId(object? id) => Execute("""
            INSERT INTO FlowNodeAttempt(id,run_record_id,node_id,attempt_no,started_time_utc,outcome)
            VALUES($id,1,'node-A',1,'2026-10-27T01:00:00','Succeeded')
            """, ("$id", id));

        public void Execute(string sql, params (string Name, object? Value)[] values)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var command = Command(connection, sql, values);
            command.ExecuteNonQuery();
        }

        private object? Scalar(string sql, params (string Name, object? Value)[] values)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var command = Command(connection, sql, values);
            return command.ExecuteScalar();
        }

        private static SqliteCommand Command(SqliteConnection connection, string sql, (string Name, object? Value)[] values)
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
            Assert.StartsWith("CopilotFlowRunAttempts-", Path.GetFileName(root), StringComparison.Ordinal);
            Directory.Delete(root, recursive: true);
        }
    }
}
