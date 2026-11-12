using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.Services.FlowDiagnostics;
using Microsoft.Data.Sqlite;
using SqlSugar;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowRunQuerySourceTests
{
    private static readonly DateTime Timestamp = new(2026, 10, 27, 1, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AllRetainedRunsPageByIdWithoutIncidentsOrDateOrderingAndPreserveUnknownOrLegacyStates()
    {
        using var fixture = new DatabaseFixture();
        var runs = new List<FlowRunRecord>();
        using (var db = fixture.CreateDatabase())
        {
            for (int index = 0; index < 121; index++)
                runs.Add(InsertRun(db, flowKey: index % 2 == 0 ? null : "flow-a", timestamp: Timestamp.AddDays(-index)));
            db.Ado.ExecuteCommand("UPDATE FlowRunRecord SET status=7319,final_outcome=7320,elapsed_ms=-1 WHERE id=@id", new SugarParameter("@id", runs[^1].Id));
        }
        var before = File.ReadAllBytes(fixture.DatabasePath);
        var delivered = new List<FlowRunRecord>();
        int? anchor = null;
        bool hasMore;
        do
        {
            var page = await fixture.Reader.ReadPageAsync(null, 100, anchor, CancellationToken.None);
            Assert.InRange(page.Runs.Count, 1, 100);
            Assert.Equal(page.Runs.Select(run => run.Id).OrderDescending(), page.Runs.Select(run => run.Id));
            Assert.False(page.TextWasTruncated);
            delivered.AddRange(page.Runs);
            hasMore = page.HasMore;
            anchor = page.Runs[^1].Id;
        } while (hasMore);

        Assert.Equal(runs.Select(run => run.Id).Reverse(), delivered.Select(run => run.Id));
        Assert.Equal(121, delivered.Select(run => run.Id).Distinct().Count());
        Assert.Contains(delivered, run => run.FlowKey == null);
        Assert.All(delivered, run => Assert.Null(run.RunKey));
        Assert.Equal(7319, (int)delivered[0].Status);
        Assert.Equal(7320, (int)delivered[0].FinalOutcome!.Value);
        Assert.Equal(-1, delivered[0].ElapsedMs);
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
        Assert.Equal(new[] { "runs.db" }, Directory.GetFiles(fixture.Root).Select(Path.GetFileName));
    }

    [Fact]
    public async Task FlowKeyFilterUsesFullUnicodeNormalizedBinaryIdentityWithoutNameTemplateOrPrefixFallbacks()
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord matching;
        FlowRunRecord normalized;
        FlowRunRecord fullLength;
        string key = new('k', 256);
        using (var db = fixture.CreateDatabase())
        {
            matching = InsertRun(db);
            InsertRun(db, flowKey: null);
            InsertRun(db, flowKey: "FLOW-A");
            InsertRun(db, flowKey: "flow-a-suffix");
            InsertRun(db, flowKey: "different-flow");
            normalized = InsertRun(db, flowKey: new string('\u3000', 500) + "flow-a" + new string('\u2003', 500));
            fullLength = InsertRun(db, flowKey: key);
            InsertRun(db, flowKey: key + "different-suffix");
        }
        var before = File.ReadAllBytes(fixture.DatabasePath);

        var page = await fixture.Reader.ReadPageAsync("\u3000flow-a\t", 100, null, CancellationToken.None);
        var fullKeyPage = await fixture.Reader.ReadPageAsync(key, 100, null, CancellationToken.None);

        Assert.Equal(new[] { normalized.Id, matching.Id }, page.Runs.Select(run => run.Id));
        Assert.All(page.Runs, run => Assert.Equal("flow-a", run.FlowKey));
        Assert.False(page.TextWasTruncated);
        Assert.Equal(fullLength.Id, Assert.Single(fullKeyPage.Runs).Id);
        Assert.False(fullKeyPage.TextWasTruncated);
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Fact]
    public async Task ContinuationRequiresTheExactAnchorToStillExistInTheSameFilter()
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord first;
        FlowRunRecord anchor;
        using (var db = fixture.CreateDatabase())
        {
            first = InsertRun(db);
            anchor = InsertRun(db);
        }
        var initial = await fixture.Reader.ReadPageAsync("flow-a", 1, null, CancellationToken.None);
        Assert.Equal(anchor.Id, Assert.Single(initial.Runs).Id);
        Assert.True(initial.HasMore);
        using (var db = fixture.OpenDatabase())
            db.Ado.ExecuteCommand("UPDATE FlowRunRecord SET flow_key='flow-b' WHERE id=@id", new SugarParameter("@id", anchor.Id));

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Reader.ReadPageAsync("flow-a", 1, anchor.Id, CancellationToken.None));
        var allRuns = await fixture.Reader.ReadPageAsync(null, 1, anchor.Id, CancellationToken.None);
        Assert.Equal(first.Id, Assert.Single(allRuns.Runs).Id);
        using (var db = fixture.OpenDatabase())
            db.Ado.ExecuteCommand("DELETE FROM FlowRunRecord WHERE id=@id", new SugarParameter("@id", anchor.Id));
        var before = File.ReadAllBytes(fixture.DatabasePath);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Reader.ReadPageAsync(null, 10, anchor.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Reader.ReadPageAsync(null, 10, int.MaxValue, CancellationToken.None));
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Fact]
    public async Task EmptyStorageAndAnUnmatchedExactFilterReturnExplicitEmptyPages()
    {
        using var fixture = new DatabaseFixture();
        using (fixture.CreateDatabase()) { }
        var before = File.ReadAllBytes(fixture.DatabasePath);

        var page = await fixture.Reader.ReadPageAsync(null, 10, null, CancellationToken.None);

        Assert.Empty(page.Runs);
        Assert.False(page.HasMore);
        Assert.False(page.TextWasTruncated);
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
        using (var db = fixture.OpenDatabase()) InsertRun(db);
        var unmatched = await fixture.Reader.ReadPageAsync("unrecorded-key", 10, null, CancellationToken.None);
        Assert.Empty(unmatched.Runs);
        Assert.False(unmatched.HasMore);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SafeMetadataIsBoundedWithoutSurrogateOrNulTailLossAndValidHashesSupportEitherEncoding(bool utf16)
    {
        using var fixture = new DatabaseFixture();
        string prefix = new('p', 255);
        using (var db = fixture.CreateDatabase(utf16))
        {
            var run = InsertRun(db);
            db.Ado.ExecuteCommand("UPDATE FlowRunRecord SET flow_name=@name,flow_key=@key WHERE id=@id",
                new SugarParameter("@name", prefix + "😀tail"), new SugarParameter("@key", "public-prefix\0hidden-tail"), new SugarParameter("@id", run.Id));
            db.Ado.ExecuteCommand("DROP INDEX ux_flow_run_run_key");
            db.Ado.ExecuteCommand("ALTER TABLE FlowRunRecord DROP COLUMN run_key");
            db.Ado.ExecuteCommand("ALTER TABLE FlowRunRecord DROP COLUMN serial_number");
            db.Ado.ExecuteCommand("ALTER TABLE FlowRunRecord DROP COLUMN recovery_reason");
        }
        var before = File.ReadAllBytes(fixture.DatabasePath);

        var page = await fixture.Reader.ReadPageAsync(null, 10, null, CancellationToken.None);

        var result = Assert.Single(page.Runs);
        Assert.Equal(prefix, result.FlowName);
        Assert.Equal("public-prefix", result.FlowKey);
        Assert.Null(result.RunKey);
        Assert.True(page.TextWasTruncated);
        Assert.Equal(new string('a', 64), result.ContentHash);
        Assert.Null(result.SerialNumber);
        Assert.Null(result.BatchId);
        Assert.Null(result.OwnerMachine);
        Assert.Null(result.OwnerInstanceId);
        Assert.Null(result.RecoveryReason);
        Assert.Equal(default, result.CompletedTime);
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Theory]
    [InlineData("\0suffix")]
    [InlineData("😀suffix")]
    public async Task InvalidHashCannotBecomeAValidTruncatedPrefix(string suffix)
    {
        using var fixture = new DatabaseFixture();
        using (var db = fixture.CreateDatabase()) InsertRun(db, contentHash: new string('a', 64) + suffix);
        var before = File.ReadAllBytes(fixture.DatabasePath);

        var page = await fixture.Reader.ReadPageAsync(null, 10, null, CancellationToken.None);

        Assert.Null(Assert.Single(page.Runs).ContentHash);
        Assert.True(page.TextWasTruncated);
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Fact]
    public async Task OnlyDeliveredRowsContributeToTextTruncationCoverage()
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord complete;
        using (var db = fixture.CreateDatabase())
        {
            InsertRun(db, contentHash: "invalid-hash");
            complete = InsertRun(db);
        }

        var page = await fixture.Reader.ReadPageAsync(null, 1, null, CancellationToken.None);

        Assert.Equal(complete.Id, Assert.Single(page.Runs).Id);
        Assert.True(page.HasMore);
        Assert.False(page.TextWasTruncated);
    }

    [Fact]
    public async Task SourceKeysBindNormalizedConfiguredPathsWithoutRevealingThem()
    {
        using var fixture = new DatabaseFixture();
        using (var db = fixture.CreateDatabase()) InsertRun(db);
        string copy = Path.Combine(fixture.Root, "other.db");
        File.Copy(fixture.DatabasePath, copy);

        var first = await fixture.Reader.ReadPageAsync(null, 10, null, CancellationToken.None);
        var same = await new FlowRunQueryReader(Path.Combine(fixture.Root.ToUpperInvariant(), ".", "runs.db"))
            .ReadPageAsync(null, 10, null, CancellationToken.None);
        var other = await new FlowRunQueryReader(copy).ReadPageAsync(null, 10, null, CancellationToken.None);

        Assert.Matches("^[a-f0-9]{64}$", first.SourceKey);
        Assert.Equal(first.SourceKey, same.SourceKey);
        Assert.NotEqual(first.SourceKey, other.SourceKey);
        Assert.DoesNotContain(fixture.Root, first.SourceKey, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(first.Runs.Select(run => run.Id), other.Runs.Select(run => run.Id));
    }

    [Theory]
    [InlineData("missing_file")]
    [InlineData("missing_table")]
    [InlineData("missing_column")]
    public async Task MissingStorageIsExplicitWithoutCreatingOrMigratingIt(string scenario)
    {
        using var fixture = new DatabaseFixture();
        if (scenario != "missing_file")
        {
            using var db = fixture.CreateDatabase();
            db.Ado.ExecuteCommand(scenario == "missing_table" ? "DROP TABLE FlowRunRecord" : "ALTER TABLE FlowRunRecord DROP COLUMN elapsed_ms");
        }
        var before = File.Exists(fixture.DatabasePath) ? File.ReadAllBytes(fixture.DatabasePath) : null;

        var exception = await Record.ExceptionAsync(() => fixture.Reader.ReadPageAsync(null, 10, null, CancellationToken.None));

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
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Reader.ReadPageAsync("\u2003\t", 10, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Reader.ReadPageAsync(new string('k', 257), 10, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Reader.ReadPageAsync(null, 0, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Reader.ReadPageAsync(null, 101, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Reader.ReadPageAsync(null, 10, 0, CancellationToken.None));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Reader.ReadPageAsync(null, 10, null, cancellation.Token));
        Assert.Empty(Directory.GetFiles(fixture.Root));
    }

    private static FlowRunRecord InsertRun(SqlSugarClient db, string? flowKey = "flow-a", DateTime? timestamp = null, string? contentHash = null)
    {
        var run = new FlowRunRecord
        {
            TemplateId = 42, FlowKey = flowKey, FlowName = "Shared visible name", RunKey = "private-recorded-key-" + Guid.NewGuid().ToString("N"),
            Status = FlowStatus.Completed, FinalOutcome = FlowFinalOutcome.Succeeded, ElapsedMs = 1000,
            StartedTimeUtc = timestamp ?? Timestamp, CompletedTimeUtc = (timestamp ?? Timestamp).AddSeconds(1),
            TemplateRevision = 7, ContentHash = contentHash ?? new string('a', 64), SnapshotId = 501,
            SerialNumber = "private-SN", BatchId = 777, OwnerMachine = "private-machine", OwnerInstanceId = "private-owner",
            RecoveryReason = "private-recovery-reason",
        };
        run.Id = db.Insertable(run).ExecuteReturnIdentity();
        return run;
    }

    private sealed class DatabaseFixture : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("CopilotFlowRunQuery-").FullName;
        public string DatabasePath => Path.Combine(Root, "runs.db");
        public FlowRunQueryReader Reader => new(DatabasePath);

        public SqlSugarClient OpenDatabase() => new(new ConnectionConfig
        {
            ConnectionString = new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString(),
            DbType = SqlSugar.DbType.Sqlite, IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute,
        });

        public SqlSugarClient CreateDatabase(bool utf16 = false)
        {
            var db = OpenDatabase();
            if (utf16)
            {
                db.CurrentConnectionConfig.IsAutoCloseConnection = false;
                db.Ado.ExecuteCommand("PRAGMA encoding='UTF-16'");
            }
            db.CodeFirst.InitTables<FlowRunRecord>();
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
            string root = Path.GetFullPath(Root);
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), Path.GetDirectoryName(root), ignoreCase: true);
            Assert.StartsWith("CopilotFlowRunQuery-", Path.GetFileName(root), StringComparison.Ordinal);
            Directory.Delete(root, recursive: true);
        }
    }
}
