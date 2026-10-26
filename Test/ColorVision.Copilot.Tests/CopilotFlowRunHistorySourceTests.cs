using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.Services.FlowDiagnostics;
using Microsoft.Data.Sqlite;
using SqlSugar;
using System.IO;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowRunHistorySourceTests
{
    private static readonly DateTime Timestamp = new(2026, 10, 27, 1, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RealHistoryReaderAndServiceUseOnlyQualifiedDeliveredRunsAndKeepRepeatedNodeAttempts()
    {
        using var fixture = new DatabaseFixture();
        const string nodeId = "recorded-node-A";
        FlowRunRecord successful;
        FlowRunRecord warning;
        FlowRunRecord legacy;
        FlowRunRecord failed;
        FlowRunRecord differentContent;
        FlowRunRecord anchor;
        using (var db = fixture.CreateDatabase(includeAttempts: true))
        {
            successful = InsertRun(db, elapsedMs: 1000);
            warning = InsertRun(db, finalOutcome: FlowFinalOutcome.SucceededWithWarnings, elapsedMs: 3000);
            legacy = InsertRun(db, finalOutcome: null, elapsedMs: 200);
            failed = InsertRun(db, status: FlowStatus.Failed, finalOutcome: FlowFinalOutcome.Failed, elapsedMs: 50);
            differentContent = InsertRun(db, contentHash: new string('b', 64), elapsedMs: 9000);
            anchor = InsertRun(db, elapsedMs: 4000);
            InsertAttempt(db, successful.Id, nodeId, "Succeeded", 100);
            InsertAttempt(db, successful.Id, nodeId, "Succeeded", 200);
            InsertAttempt(db, warning.Id, nodeId, "Succeeded", 500);
            InsertAttempt(db, legacy.Id, nodeId, "Succeeded", 10);
            InsertAttempt(db, failed.Id, nodeId);
            InsertAttempt(db, differentContent.Id, nodeId, "Succeeded", 900);
            InsertAttempt(db, anchor.Id, nodeId, "Succeeded", 300);
            InsertAttempt(db, anchor.Id, nodeId, "Succeeded", 500);
        }
        var before = File.ReadAllBytes(fixture.DatabasePath);
        var service = new CopilotFlowRunHistoryInspectionService(fixture.Reader.ReadAsync);

        var result = await service.InspectAsync(anchor.Id, nodeId, 10, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        var evidence = document.RootElement;
        Assert.Equal(anchor.Id, evidence.GetProperty("anchor").GetProperty("run_record_id").GetInt32());
        var previous = evidence.GetProperty("previous_runs").EnumerateArray().ToArray();
        Assert.Equal(new[] { differentContent.Id, failed.Id, legacy.Id, warning.Id, successful.Id },
            previous.Select(item => item.GetProperty("run_record_id").GetInt32()));
        Assert.Equal(2, previous.Single(item => item.GetProperty("run_record_id").GetInt32() == successful.Id)
            .GetProperty("node_attempts").GetArrayLength());
        var runReference = evidence.GetProperty("references").GetProperty("run_duration");
        Assert.Equal(new[] { warning.Id, successful.Id }, runReference.GetProperty("run_record_ids").EnumerateArray().Select(item => item.GetInt32()));
        Assert.Equal(2, runReference.GetProperty("sample_count").GetInt32());
        Assert.Equal(2000, runReference.GetProperty("mean_ms").GetDouble());
        var nodeReference = evidence.GetProperty("references").GetProperty("node_duration");
        Assert.Equal(new[] { warning.Id, successful.Id }, nodeReference.GetProperty("run_record_ids").EnumerateArray().Select(item => item.GetInt32()));
        Assert.Equal(2, nodeReference.GetProperty("sample_count").GetInt32());
        Assert.Equal(400, nodeReference.GetProperty("mean_ms").GetDouble());
        Assert.Equal(800, nodeReference.GetProperty("target_duration_ms").GetDouble());
        Assert.True(evidence.GetProperty("coverage").GetProperty("node_attempts_available").GetBoolean());
        Assert.False(evidence.GetProperty("coverage").GetProperty("history_truncated").GetBoolean());
        Assert.DoesNotContain("private-", result.Content, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
        Assert.Equal(new[] { "history.db" }, Directory.GetFiles(fixture.Root).Select(Path.GetFileName));
    }

    [Fact]
    public async Task StableFlowKeyNeverBorrowsLegacyOrOtherKeysSharingTemplateNameSerialOrId()
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord previous;
        FlowRunRecord anchor;
        using (var db = fixture.CreateDatabase())
        {
            previous = InsertRun(db, templateId: 7);
            InsertRun(db, flowKey: null);
            InsertRun(db, flowKey: "other-flow");
            InsertRun(db, flowKey: "FLOW-A");
            anchor = InsertRun(db);
            InsertRun(db);
        }

        var result = Assert.IsType<FlowRunHistoryReadResult>(await fixture.Reader.ReadAsync(anchor.Id, null, 50, CancellationToken.None));

        Assert.Equal(anchor.Id, result.Anchor.Run.Id);
        Assert.Equal(previous.Id, Assert.Single(result.PreviousRuns).Run.Id);
        Assert.Equal("flow_key", result.IdentityBasis);
        Assert.False(result.HistoryTruncated);
        Assert.False(result.NodeAttemptsAvailable);
    }

    [Fact]
    public async Task LegacyIdentityRequiresBothKeysMissingAndTheSamePositiveTemplateId()
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord first;
        FlowRunRecord unicodeMissing;
        FlowRunRecord anchor;
        using (var db = fixture.CreateDatabase())
        {
            first = InsertRun(db, flowKey: null);
            InsertRun(db, flowKey: "assigned-key");
            unicodeMissing = InsertRun(db, flowKey: new string('\u2003', 300));
            InsertRun(db, flowKey: null, templateId: 43);
            anchor = InsertRun(db, flowKey: "\t\u00a0");
        }

        var result = Assert.IsType<FlowRunHistoryReadResult>(await fixture.Reader.ReadAsync(anchor.Id, null, 10, CancellationToken.None));

        Assert.Equal("legacy_template_id", result.IdentityBasis);
        Assert.Equal(new[] { unicodeMissing.Id, first.Id }, result.PreviousRuns.Select(item => item.Run.Id));
        Assert.True(result.TextWasTruncated);
    }

    [Fact]
    public async Task FullNormalizedFlowKeysControlIdentityBeyondTheBoundedDisplayPrefix()
    {
        using var fixture = new DatabaseFixture();
        var key = new string('k', 300);
        FlowRunRecord previous;
        FlowRunRecord anchor;
        using (var db = fixture.CreateDatabase())
        {
            previous = InsertRun(db, flowKey: "\u2003" + key + "one\t", templateId: 43);
            InsertRun(db, flowKey: key + "two");
            anchor = InsertRun(db, flowKey: key + "one");
        }

        var result = Assert.IsType<FlowRunHistoryReadResult>(await fixture.Reader.ReadAsync(anchor.Id, null, 10, CancellationToken.None));

        Assert.Equal(previous.Id, Assert.Single(result.PreviousRuns).Run.Id);
        Assert.Equal("flow_key", result.IdentityBasis);
        Assert.True(result.TextWasTruncated);
    }

    [Fact]
    public async Task UnavailableIdentityReturnsOnlyTheAnchorAndNeverGuessesByNameOrSerial()
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord anchor;
        using (var db = fixture.CreateDatabase())
        {
            InsertRun(db, flowKey: null, templateId: 0);
            anchor = InsertRun(db, flowKey: null, templateId: -7);
        }

        var result = Assert.IsType<FlowRunHistoryReadResult>(await fixture.Reader.ReadAsync(anchor.Id, null, 10, CancellationToken.None));

        Assert.Equal("unavailable", result.IdentityBasis);
        Assert.Equal(anchor.Id, result.Anchor.Run.Id);
        Assert.Empty(result.PreviousRuns);
        Assert.False(result.HistoryTruncated);
        Assert.Null(await fixture.Reader.ReadAsync(anchor.Id + 100, null, 10, CancellationToken.None));
    }

    [Fact]
    public async Task EarlierHistoryIsAnIdWindowRatherThanAnInventedChronologicalOrSuccessfulRunFilter()
    {
        using var fixture = new DatabaseFixture();
        var runs = new List<FlowRunRecord>();
        using (var db = fixture.CreateDatabase())
        {
            for (var index = 0; index < 7; index++)
                runs.Add(InsertRun(db, status: index % 2 == 0 ? FlowStatus.Completed : FlowStatus.Runing,
                    finalOutcome: index % 2 == 0 ? FlowFinalOutcome.Failed : null, timestamp: Timestamp.AddDays(-index)));
        }

        var result = Assert.IsType<FlowRunHistoryReadResult>(await fixture.Reader.ReadAsync(runs[5].Id, null, 2, CancellationToken.None));

        Assert.Equal(new[] { runs[4].Id, runs[3].Id }, result.PreviousRuns.Select(item => item.Run.Id));
        Assert.True(result.HistoryTruncated);
        Assert.Equal(FlowFinalOutcome.Failed, result.PreviousRuns[0].Run.FinalOutcome);
        Assert.Equal(FlowStatus.Runing, result.PreviousRuns[1].Run.Status);
        Assert.Equal(runs[4].StartedTimeUtc, result.PreviousRuns[0].Run.StartedTimeUtc);
        Assert.Null(result.PreviousRuns[1].Run.CompletedTimeUtc);
    }

    [Fact]
    public async Task ExactNodeAttemptsKeepLoopsPerRunAndReturnTheNewestTenInAscendingIdOrder()
    {
        using var fixture = new DatabaseFixture();
        const string nodeId = "node-A ";
        FlowRunRecord previous;
        FlowRunRecord anchor;
        var previousAttempts = new List<long>();
        var anchorAttempts = new List<long>();
        using (var db = fixture.CreateDatabase(includeAttempts: true))
        {
            previous = InsertRun(db);
            var other = InsertRun(db, flowKey: "other-flow");
            anchor = InsertRun(db);
            for (var index = 0; index < 12; index++)
            {
                previousAttempts.Add(InsertAttempt(db, previous.Id, nodeId).Id);
                anchorAttempts.Add(InsertAttempt(db, anchor.Id, nodeId).Id);
            }
            InsertAttempt(db, previous.Id, "node-A");
            InsertAttempt(db, previous.Id, "NODE-A ");
            InsertAttempt(db, other.Id, nodeId);
        }

        var result = Assert.IsType<FlowRunHistoryReadResult>(await fixture.Reader.ReadAsync(anchor.Id, nodeId, 10, CancellationToken.None));

        Assert.True(result.NodeAttemptsAvailable);
        var history = Assert.Single(result.PreviousRuns);
        Assert.Equal(previous.Id, history.Run.Id);
        Assert.Equal(previousAttempts.Skip(2), history.Attempts.Select(item => item.Id));
        Assert.Equal(anchorAttempts.Skip(2), result.Anchor.Attempts.Select(item => item.Id));
        Assert.True(history.AttemptsTruncated);
        Assert.True(result.Anchor.AttemptsTruncated);
        Assert.All(history.Attempts.Concat(result.Anchor.Attempts), attempt =>
        {
            Assert.Equal(nodeId, attempt.NodeId);
            Assert.Equal(1, attempt.AttemptNo);
            Assert.Null(attempt.ErrorMessage);
            Assert.Empty(attempt.InvocationId);
        });
    }

    [Theory]
    [InlineData("missing_table", false)]
    [InlineData("missing_column", false)]
    [InlineData("empty_table", true)]
    public async Task RequestedNodeSchemaAvailabilityIsDistinctFromAnEmptyRetainedAttemptWindow(string scenario, bool available)
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord anchor;
        using (var db = fixture.CreateDatabase(includeAttempts: scenario != "missing_table"))
        {
            InsertRun(db);
            anchor = InsertRun(db);
            if (scenario == "missing_column")
                db.Ado.ExecuteCommand("ALTER TABLE FlowNodeAttempt DROP COLUMN error_code");
        }

        var result = Assert.IsType<FlowRunHistoryReadResult>(await fixture.Reader.ReadAsync(anchor.Id, "arbitrary-recorded-node", 10, CancellationToken.None));

        Assert.Equal(available, result.NodeAttemptsAvailable);
        Assert.Single(result.PreviousRuns);
        Assert.Empty(result.Anchor.Attempts);
        Assert.Empty(result.PreviousRuns[0].Attempts);
    }

    [Fact]
    public async Task SafeMetadataIsUnicodeBoundedAndNoPrivateValuesOrLegacyLocalTimestampAreRead()
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord anchor;
        var prefix = new string('名', 255);
        using (var db = fixture.CreateDatabase())
        {
            anchor = InsertRun(db);
            db.Ado.ExecuteCommand("UPDATE FlowRunRecord SET flow_name=@name,run_key=@name", new SugarParameter("@name", prefix + "😀tail"));
            db.Ado.ExecuteCommand("ALTER TABLE FlowRunRecord DROP COLUMN serial_number");
            db.Ado.ExecuteCommand("ALTER TABLE FlowRunRecord DROP COLUMN recovery_reason");
        }
        var before = File.ReadAllBytes(fixture.DatabasePath);

        var result = Assert.IsType<FlowRunHistoryReadResult>(await fixture.Reader.ReadAsync(anchor.Id, null, 10, CancellationToken.None));

        Assert.True(result.TextWasTruncated);
        Assert.Equal(prefix, result.Anchor.Run.FlowName);
        Assert.Equal(prefix, result.Anchor.Run.RunKey);
        Assert.Null(result.Anchor.Run.SerialNumber);
        Assert.Null(result.Anchor.Run.BatchId);
        Assert.Null(result.Anchor.Run.OwnerMachine);
        Assert.Null(result.Anchor.Run.RecoveryReason);
        Assert.Equal(default, result.Anchor.Run.CompletedTime);
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
        Assert.Equal(new[] { "history.db" }, Directory.GetFiles(fixture.Root).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("😀suffix")]
    [InlineData("\0suffix")]
    public async Task OverlongHashNeverBecomesAValidComparablePrefix(string suffix)
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord anchor;
        using (var db = fixture.CreateDatabase())
        {
            InsertRun(db, contentHash: new string('a', 64) + suffix);
            anchor = InsertRun(db);
        }

        var result = Assert.IsType<FlowRunHistoryReadResult>(await fixture.Reader.ReadAsync(anchor.Id, null, 10, CancellationToken.None));

        var hash = Assert.Single(result.PreviousRuns).Run.ContentHash;
        Assert.False(hash is { Length: 64 } && hash.All(char.IsAsciiHexDigit));
        Assert.True(result.TextWasTruncated);
    }

    [Fact]
    public async Task ValidHashesRemainComparableInAUtf16EncodedHistoryDatabase()
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord previous;
        FlowRunRecord anchor;
        using (var db = fixture.CreateDatabase(utf16: true))
        {
            previous = InsertRun(db);
            anchor = InsertRun(db);
            Assert.Equal(128L, Convert.ToInt64(db.Ado.GetScalar("SELECT length(CAST(content_hash AS BLOB)) FROM FlowRunRecord LIMIT 1")));
        }
        var before = File.ReadAllBytes(fixture.DatabasePath);

        var result = Assert.IsType<FlowRunHistoryReadResult>(await fixture.Reader.ReadAsync(anchor.Id, null, 10, CancellationToken.None));

        Assert.Equal(anchor.ContentHash, result.Anchor.Run.ContentHash);
        Assert.Equal(previous.ContentHash, Assert.Single(result.PreviousRuns).Run.ContentHash);
        Assert.False(result.TextWasTruncated);
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Theory]
    [InlineData("missing_file")]
    [InlineData("missing_table")]
    [InlineData("missing_column")]
    public async Task MissingRunStorageIsExplicitWithoutCreatingOrMigratingIt(string scenario)
    {
        using var fixture = new DatabaseFixture();
        if (scenario != "missing_file")
        {
            using var db = fixture.CreateDatabase();
            db.Ado.ExecuteCommand(scenario == "missing_table" ? "DROP TABLE FlowRunRecord" : "ALTER TABLE FlowRunRecord DROP COLUMN elapsed_ms");
        }
        var before = File.Exists(fixture.DatabasePath) ? File.ReadAllBytes(fixture.DatabasePath) : null;

        var failure = await Record.ExceptionAsync(() => fixture.Reader.ReadAsync(1, null, 10, CancellationToken.None));

        if (scenario == "missing_file")
        {
            Assert.IsType<FileNotFoundException>(failure);
            Assert.Empty(Directory.GetFiles(fixture.Root));
        }
        else
        {
            Assert.IsType<InvalidDataException>(failure);
            Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
        }
    }

    [Fact]
    public async Task InvalidArgumentsAndPreCancellationDoNotOpenStorage()
    {
        using var fixture = new DatabaseFixture();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Reader.ReadAsync(0, null, 10, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Reader.ReadAsync(1, null, 51, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Reader.ReadAsync(1, "\u2003\t", 10, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Reader.ReadAsync(1, new string('n', 129), 10, CancellationToken.None));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Reader.ReadAsync(1, null, 10, cancellation.Token));
        Assert.Empty(Directory.GetFiles(fixture.Root));
    }

    private static FlowRunRecord InsertRun(SqlSugarClient db, string? flowKey = "flow-a", int templateId = 42,
        FlowStatus status = FlowStatus.Completed, FlowFinalOutcome? finalOutcome = FlowFinalOutcome.Succeeded, DateTime? timestamp = null,
        long elapsedMs = 100, string? contentHash = null)
    {
        var run = new FlowRunRecord
        {
            FlowKey = flowKey, TemplateId = templateId, FlowName = "Shared name", RunKey = Guid.NewGuid().ToString("N"),
            Status = status, FinalOutcome = finalOutcome, ElapsedMs = elapsedMs, StartedTimeUtc = timestamp ?? Timestamp,
            CompletedTimeUtc = status == FlowStatus.Completed ? (timestamp ?? Timestamp).AddSeconds(1) : null,
            CompletedTime = Timestamp.ToLocalTime(), TemplateRevision = 7, ContentHash = contentHash ?? new string('a', 64),
            SerialNumber = "private-shared-SN", BatchId = 777, OwnerMachine = "private-owner", RecoveryReason = "private-recovery",
        };
        run.Id = db.Insertable(run).ExecuteReturnIdentity();
        return run;
    }

    private static FlowNodeAttempt InsertAttempt(SqlSugarClient db, int runId, string nodeId, string outcome = "Failed", int durationMs = 20)
    {
        var attempt = new FlowNodeAttempt
        {
            RunRecordId = runId, NodeId = nodeId, AttemptNo = 1, InvocationId = Guid.NewGuid().ToString("N"),
            StartedTimeUtc = Timestamp, CompletedTimeUtc = Timestamp.AddMilliseconds(durationMs), Outcome = outcome,
            ErrorCode = "failure-code", ErrorMessage = "private-payload-error",
        };
        attempt.Id = db.Insertable(attempt).ExecuteReturnBigIdentity();
        return attempt;
    }

    private sealed class DatabaseFixture : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("CopilotFlowRunHistory-").FullName;
        public string DatabasePath => Path.Combine(Root, "history.db");
        public FlowRunHistoryReader Reader => new(DatabasePath);

        public SqlSugarClient CreateDatabase(bool includeAttempts = false, bool utf16 = false)
        {
            var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString(),
                DbType = SqlSugar.DbType.Sqlite, IsAutoCloseConnection = !utf16, InitKeyType = InitKeyType.Attribute,
            });
            if (utf16)
                db.Ado.ExecuteCommand("PRAGMA encoding='UTF-16'");
            db.CodeFirst.InitTables<FlowRunRecord>();
            if (includeAttempts)
                db.CodeFirst.InitTables<FlowNodeAttempt>();
            if (utf16)
            {
                Assert.StartsWith("UTF-16", Convert.ToString(db.Ado.GetScalar("PRAGMA encoding")), StringComparison.OrdinalIgnoreCase);
                db.Ado.Close();
                db.CurrentConnectionConfig.IsAutoCloseConnection = true;
            }
            return db;
        }

        public void Dispose()
        {
            var fullRoot = Path.GetFullPath(Root);
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), Path.GetDirectoryName(fullRoot), ignoreCase: true);
            Assert.StartsWith("CopilotFlowRunHistory-", Path.GetFileName(fullRoot), StringComparison.Ordinal);
            Directory.Delete(fullRoot, recursive: true);
        }
    }
}
