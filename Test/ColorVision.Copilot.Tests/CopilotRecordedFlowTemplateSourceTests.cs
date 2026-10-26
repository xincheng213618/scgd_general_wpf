using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Services.FlowDiagnostics;
using Microsoft.Data.Sqlite;
using SqlSugar;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotRecordedFlowTemplateSourceTests
{
    private static readonly byte[] Content = Encoding.UTF8.GetBytes("Opaque recorded bytes; reading must not instantiate execution nodes.");
    private static readonly DateTime CapturedAt = new(2026, 10, 27, 1, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task StoredUnknownNodeCanBeInspectedThroughTheRealReaderAndServiceWithoutLoadingTypesOrChangingStorage()
    {
        using var fixture = new DatabaseFixture();
        var nodeId = Guid.Parse("7312c656-c7ee-4525-a9c3-a0743fde9f1e");
        const string privateValue = "PRIVATE_RECORDED_PARAMETER_VALUE";
        var payload = STNodeSavedState.Write("Unavailable.Plugin", "Unavailable.Plugin.RecordedNode", new Dictionary<string, byte[]>
        {
            ["Guid"] = nodeId.ToByteArray(), ["PrivateValue"] = Encoding.UTF8.GetBytes(privateValue),
            [STNodeSavedState.PortLayoutKey] = JsonSerializer.SerializeToUtf8Bytes(new STNodePortLayout
            {
                Title = "Recorded unknown node", Inputs = [], Outputs = [],
            }),
        });
        using var stream = new MemoryStream();
        STNodeCanvasWriter.WriteRaw(stream, [payload], [], 0, 0, 1);
        FlowRunRecord run;
        FlowTemplateSnapshot snapshot;
        using (var db = fixture.CreateDatabase())
        {
            snapshot = InsertSnapshot(db, templateId: 42, revision: 7, content: stream.ToArray());
            run = InsertRun(db, snapshot.Id, snapshot.ContentHash, templateId: 43, revision: 9);
        }
        var before = File.ReadAllBytes(fixture.DatabasePath);
        var service = new CopilotRecordedFlowTemplateInspectionService(fixture.Reader.ReadAsync);

        var result = await service.InspectAsync(run.Id, null, 80, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        var evidence = document.RootElement;
        Assert.Equal(run.Id, evidence.GetProperty("run").GetProperty("run_record_id").GetInt32());
        Assert.Equal(snapshot.Id, evidence.GetProperty("snapshot").GetProperty("snapshot_id").GetInt64());
        Assert.Equal(snapshot.ContentHash, evidence.GetProperty("snapshot").GetProperty("content_hash").GetString());
        Assert.True(evidence.GetProperty("snapshot").GetProperty("stored_integrity_verified").GetBoolean());
        var node = Assert.Single(evidence.GetProperty("nodes").EnumerateArray());
        Assert.Equal(nodeId, node.GetProperty("node_id").GetGuid());
        Assert.Equal("Unavailable.Plugin.RecordedNode", node.GetProperty("type_key").GetString());
        Assert.Equal("Recorded unknown node", node.GetProperty("title").GetString());
        Assert.DoesNotContain(privateValue, result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("private-SN", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("private-owner", result.Content, StringComparison.Ordinal);
        Assert.True(evidence.GetProperty("coverage").GetProperty("returned_window_complete").GetBoolean());
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
        Assert.Equal(new[] { "recorded.db" }, Directory.GetFiles(fixture.Root).Select(Path.GetFileName));
    }

    [Fact]
    public async Task ExactRunReturnsVerifiedBytesAndSafeMetadataWithoutReadingPrivateColumnsOrWritingTheDatabase()
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord run;
        FlowTemplateSnapshot snapshot;
        using (var db = fixture.CreateDatabase())
        {
            snapshot = InsertSnapshot(db);
            run = InsertRun(db, snapshot.Id, snapshot.ContentHash.ToUpperInvariant());
            var other = InsertSnapshot(db, flowKey: "other-flow", content: [1, 2, 3]);
            InsertRun(db, other.Id, other.ContentHash, flowKey: "other-flow");
            db.Ado.ExecuteCommand("ALTER TABLE FlowRunRecord DROP COLUMN serial_number");
            db.Ado.ExecuteCommand("ALTER TABLE FlowRunRecord DROP COLUMN owner_instance_id");
        }
        var before = File.ReadAllBytes(fixture.DatabasePath);

        var result = Assert.IsType<FlowRecordedTemplateReadResult>(await fixture.Reader.ReadAsync(run.Id, CancellationToken.None));

        Assert.Equal(run.Id, result.Run.Id);
        Assert.Equal(run.RunKey, result.Run.RunKey);
        Assert.Equal(snapshot.Id, result.Snapshot!.Id);
        Assert.Equal(snapshot.Content, result.Snapshot.Content);
        Assert.Equal(snapshot.ContentHash, result.Snapshot.ContentHash);
        Assert.Equal(snapshot.ContentLength, result.Snapshot.ContentLength);
        Assert.Equal(snapshot.CapturedTimeUtc, result.Snapshot.CapturedTimeUtc);
        Assert.False(result.TextWasTruncated);
        Assert.Null(result.Run.SerialNumber);
        Assert.Null(result.Run.BatchId);
        Assert.Null(result.Run.OwnerMachine);
        Assert.Null(result.Run.OwnerInstanceId);
        Assert.Null(result.Run.OwnerProcessId);
        Assert.Equal(default, result.Run.CompletedTime);
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
        Assert.Equal(new[] { "recorded.db" }, Directory.GetFiles(fixture.Root).Select(Path.GetFileName));
    }

    [Fact]
    public async Task SharedFlowKeyAndBytesAllowSnapshotReuseAcrossTemplateIdsAndRevisions()
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord run;
        using (var db = fixture.CreateDatabase())
        {
            var snapshot = InsertSnapshot(db, templateId: 42, revision: 7);
            run = InsertRun(db, snapshot.Id, snapshot.ContentHash, templateId: 43, revision: 9);
        }

        var result = Assert.IsType<FlowRecordedTemplateReadResult>(await fixture.Reader.ReadAsync(run.Id, CancellationToken.None));

        Assert.Equal(43, result.Run.TemplateId);
        Assert.Equal(9, result.Run.TemplateRevision);
        Assert.Equal(42, result.Snapshot!.TemplateId);
        Assert.Equal(7, result.Snapshot.TemplateRevision);
        Assert.Equal(result.Run.FlowKey, result.Snapshot.FlowKey);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("recorded-flow", null)]
    [InlineData(null, "recorded-flow")]
    public async Task LegacyMissingFlowKeysUseOnlyTheExactPointerHashAndMatchingTemplateId(string? runKey, string? snapshotKey)
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord run;
        using (var db = fixture.CreateDatabase())
        {
            var snapshot = InsertSnapshot(db, flowKey: snapshotKey);
            run = InsertRun(db, snapshot.Id, snapshot.ContentHash, flowKey: runKey);
        }

        var result = Assert.IsType<FlowRecordedTemplateReadResult>(await fixture.Reader.ReadAsync(run.Id, CancellationToken.None));

        Assert.NotNull(result.Snapshot);
        Assert.Equal(run.SnapshotId, result.Snapshot.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrossFlowAssociationsAreRejectedEvenWhenTheBoundedKeysHaveTheSamePrefix(bool sharedLongPrefix)
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord run;
        using (var db = fixture.CreateDatabase())
        {
            var prefix = sharedLongPrefix ? new string('f', 300) : "flow-";
            var snapshot = InsertSnapshot(db, flowKey: prefix + "one");
            run = InsertRun(db, snapshot.Id, snapshot.ContentHash, flowKey: prefix + "two");
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Reader.ReadAsync(run.Id, CancellationToken.None));
    }

    [Fact]
    public async Task LegacyTemplateIdConflictsAreRejectedInsteadOfGuessingFromHash()
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord run;
        using (var db = fixture.CreateDatabase())
        {
            var snapshot = InsertSnapshot(db, flowKey: null, templateId: 42);
            run = InsertRun(db, snapshot.Id, snapshot.ContentHash, flowKey: null, templateId: 43);
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Reader.ReadAsync(run.Id, CancellationToken.None));
    }

    [Fact]
    public async Task UnicodeWhitespaceFlowKeysCannotAuthorizeSnapshotReuseAcrossLegacyTemplateIds()
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord run;
        using (var db = fixture.CreateDatabase())
        {
            // Longer than the output projection: identity normalization must inspect the full value inside SQLite.
            var missingKey = new string('\u2003', 300) + "\t\u00a0";
            var snapshot = InsertSnapshot(db, flowKey: missingKey, templateId: 42);
            run = InsertRun(db, snapshot.Id, snapshot.ContentHash, flowKey: missingKey, templateId: 43);
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Reader.ReadAsync(run.Id, CancellationToken.None));
    }

    [Theory]
    [InlineData("run_hash")]
    [InlineData("run_hash_nul")]
    [InlineData("snapshot_hash")]
    [InlineData("snapshot_hash_nul")]
    [InlineData("actual_hash")]
    [InlineData("malformed_hash")]
    [InlineData("declared_length")]
    [InlineData("oversize")]
    [InlineData("non_blob")]
    public async Task IntegrityAndLengthGuardsRejectInvalidRecordedContentWithoutModifyingIt(string scenario)
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord run;
        using (var db = fixture.CreateDatabase())
        {
            var snapshot = InsertSnapshot(db);
            run = InsertRun(db, snapshot.Id, snapshot.ContentHash);
            var differentHash = Hash([9, 8, 7]);
            switch (scenario)
            {
                case "run_hash":
                    db.Ado.ExecuteCommand("UPDATE FlowRunRecord SET content_hash=@hash", new SugarParameter("@hash", differentHash));
                    break;
                case "run_hash_nul":
                    db.Ado.ExecuteCommand("UPDATE FlowRunRecord SET content_hash=@hash", new SugarParameter("@hash", snapshot.ContentHash + "\0suffix"));
                    Assert.Equal(71L, Convert.ToInt64(db.Ado.GetScalar("SELECT length(CAST(content_hash AS BLOB)) FROM FlowRunRecord")));
                    break;
                case "snapshot_hash":
                    db.Ado.ExecuteCommand("UPDATE FlowTemplateSnapshot SET content_hash=@hash", new SugarParameter("@hash", differentHash));
                    break;
                case "snapshot_hash_nul":
                    db.Ado.ExecuteCommand("UPDATE FlowTemplateSnapshot SET content_hash=@hash", new SugarParameter("@hash", snapshot.ContentHash + "\0suffix"));
                    Assert.Equal(71L, Convert.ToInt64(db.Ado.GetScalar("SELECT length(CAST(content_hash AS BLOB)) FROM FlowTemplateSnapshot")));
                    break;
                case "actual_hash":
                    db.Ado.ExecuteCommand("UPDATE FlowRunRecord SET content_hash=@hash", new SugarParameter("@hash", differentHash));
                    db.Ado.ExecuteCommand("UPDATE FlowTemplateSnapshot SET content_hash=@hash", new SugarParameter("@hash", differentHash));
                    break;
                case "malformed_hash":
                    db.Ado.ExecuteCommand("UPDATE FlowRunRecord SET content_hash=@hash", new SugarParameter("@hash", snapshot.ContentHash + "x"));
                    break;
                case "declared_length":
                    db.Ado.ExecuteCommand("UPDATE FlowTemplateSnapshot SET content_length=content_length+1");
                    break;
                case "oversize":
                    var size = FlowRecordedTemplateReader.MaximumSnapshotBytes + 1;
                    db.Ado.ExecuteCommand("UPDATE FlowTemplateSnapshot SET content=zeroblob(@size),content_length=@size", new SugarParameter("@size", size));
                    break;
                case "non_blob":
                    db.Ado.ExecuteCommand("UPDATE FlowTemplateSnapshot SET content='opaque text',content_length=11");
                    break;
            }
        }
        var before = File.ReadAllBytes(fixture.DatabasePath);

        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Reader.ReadAsync(run.Id, CancellationToken.None));

        if (scenario == "oversize")
            Assert.Contains("8 MiB", failure.Message, StringComparison.Ordinal);
        if (scenario == "declared_length")
            Assert.Contains("length", failure.Message, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Fact]
    public async Task EmptyBytesCanPassStorageIntegrityWithoutClaimingToBeAnExecutableTemplate()
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord run;
        using (var db = fixture.CreateDatabase())
        {
            var snapshot = InsertSnapshot(db, content: []);
            run = InsertRun(db, snapshot.Id, snapshot.ContentHash);
        }

        var result = Assert.IsType<FlowRecordedTemplateReadResult>(await fixture.Reader.ReadAsync(run.Id, CancellationToken.None));

        Assert.NotNull(result.Snapshot);
        Assert.Empty(result.Snapshot.Content);
        Assert.Equal(0, result.Snapshot.ContentLength);
    }

    [Fact]
    public async Task ValidAsciiHashesRemainReadableInAUtf16EncodedDatabase()
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord run;
        using (var db = fixture.CreateDatabase(utf16: true))
        {
            var snapshot = InsertSnapshot(db);
            run = InsertRun(db, snapshot.Id, snapshot.ContentHash);
            Assert.Equal(128L, Convert.ToInt64(db.Ado.GetScalar("SELECT length(CAST(content_hash AS BLOB)) FROM FlowRunRecord")));
            Assert.Equal(128L, Convert.ToInt64(db.Ado.GetScalar("SELECT length(CAST(content_hash AS BLOB)) FROM FlowTemplateSnapshot")));
        }
        var before = File.ReadAllBytes(fixture.DatabasePath);

        var result = Assert.IsType<FlowRecordedTemplateReadResult>(await fixture.Reader.ReadAsync(run.Id, CancellationToken.None));

        Assert.Equal(Hash(Content), result.Run.ContentHash);
        Assert.Equal(Hash(Content), result.Snapshot!.ContentHash);
        Assert.Equal(Content, result.Snapshot.Content);
        Assert.False(result.TextWasTruncated);
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Fact]
    public async Task MissingRunReturnsNullAndUnreferencedRunDoesNotRequireASnapshotTable()
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord run;
        using (var db = fixture.CreateDatabase(includeSnapshot: false))
            run = InsertRun(db, null, null);

        Assert.Null(await fixture.Reader.ReadAsync(run.Id + 1, CancellationToken.None));
        var result = Assert.IsType<FlowRecordedTemplateReadResult>(await fixture.Reader.ReadAsync(run.Id, CancellationToken.None));

        Assert.Equal(run.Id, result.Run.Id);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task MissingExactSnapshotDoesNotBorrowAnExistingSnapshotWithTheSameHash()
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord run;
        using (var db = fixture.CreateDatabase())
        {
            var snapshot = InsertSnapshot(db);
            run = InsertRun(db, snapshot.Id + 1, snapshot.ContentHash);
        }

        var result = Assert.IsType<FlowRecordedTemplateReadResult>(await fixture.Reader.ReadAsync(run.Id, CancellationToken.None));

        Assert.Null(result.Snapshot);
        Assert.Equal(run.SnapshotId, result.Run.SnapshotId);
    }

    [Fact]
    public async Task MetadataIsBoundedAtUnicodeBoundariesWhileFullFlowKeysStillControlAssociation()
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord run;
        var prefix = new string('f', 255);
        var longKey = prefix + "😀shared-tail";
        using (var db = fixture.CreateDatabase())
        {
            var snapshot = InsertSnapshot(db, flowKey: longKey);
            run = InsertRun(db, snapshot.Id, snapshot.ContentHash, flowKey: longKey);
            db.Ado.ExecuteCommand("UPDATE FlowRunRecord SET flow_name=@name,run_key=@name", new SugarParameter("@name", longKey));
        }

        var result = Assert.IsType<FlowRecordedTemplateReadResult>(await fixture.Reader.ReadAsync(run.Id, CancellationToken.None));

        Assert.True(result.TextWasTruncated);
        Assert.Equal(prefix, result.Run.FlowName);
        Assert.Equal(prefix, result.Run.RunKey);
        Assert.Equal(prefix, result.Run.FlowKey);
        Assert.Equal(prefix, result.Snapshot!.FlowKey);
    }

    [Theory]
    [InlineData("missing_file")]
    [InlineData("missing_run_table")]
    [InlineData("missing_run_column")]
    [InlineData("missing_snapshot_table")]
    [InlineData("missing_snapshot_column")]
    public async Task MissingRequiredStorageIsExplicitAndNeverCreatesOrMigratesADatabase(string scenario)
    {
        using var fixture = new DatabaseFixture();
        var runId = 1;
        if (scenario != "missing_file")
        {
            using var db = fixture.CreateDatabase();
            var snapshot = InsertSnapshot(db);
            runId = InsertRun(db, snapshot.Id, snapshot.ContentHash).Id;
            db.Ado.ExecuteCommand(scenario switch
            {
                "missing_run_table" => "DROP TABLE FlowRunRecord",
                "missing_run_column" => "ALTER TABLE FlowRunRecord DROP COLUMN snapshot_id",
                "missing_snapshot_table" => "DROP TABLE FlowTemplateSnapshot",
                _ => "ALTER TABLE FlowTemplateSnapshot DROP COLUMN content_length",
            });
        }
        var before = File.Exists(fixture.DatabasePath) ? File.ReadAllBytes(fixture.DatabasePath) : null;

        var failure = await Record.ExceptionAsync(() => fixture.Reader.ReadAsync(runId, CancellationToken.None));

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
    public async Task InvalidRunIdsAndPreCancellationDoNotOpenOrCreateStorage()
    {
        using var fixture = new DatabaseFixture();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Reader.ReadAsync(0, CancellationToken.None));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Reader.ReadAsync(1, cancellation.Token));
        Assert.Empty(Directory.GetFiles(fixture.Root));
    }

    private static FlowTemplateSnapshot InsertSnapshot(SqlSugarClient db, string? flowKey = "recorded-flow", int templateId = 42,
        int revision = 7, byte[]? content = null)
    {
        var bytes = content ?? Content;
        var snapshot = new FlowTemplateSnapshot
        {
            TemplateId = templateId, FlowKey = flowKey, TemplateRevision = revision,
            ContentHash = Hash(bytes), Content = bytes, ContentLength = bytes.Length, CapturedTimeUtc = CapturedAt,
        };
        snapshot.Id = db.Insertable(snapshot).ExecuteReturnBigIdentity();
        return snapshot;
    }

    private static FlowRunRecord InsertRun(SqlSugarClient db, long? snapshotId, string? hash,
        string? flowKey = "recorded-flow", int templateId = 42, int revision = 7)
    {
        var run = new FlowRunRecord
        {
            TemplateId = templateId, FlowKey = flowKey, FlowName = "Recorded flow", RunKey = Guid.NewGuid().ToString("N"),
            TemplateRevision = revision, ContentHash = hash, SnapshotId = snapshotId,
            SerialNumber = "private-SN", BatchId = 555, OwnerInstanceId = "private-owner", OwnerMachine = "private-machine", OwnerProcessId = 777,
        };
        run.Id = db.Insertable(run).ExecuteReturnIdentity();
        return run;
    }

    private static string Hash(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private sealed class DatabaseFixture : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("CopilotRecordedFlowTemplate-").FullName;
        public string DatabasePath => Path.Combine(Root, "recorded.db");
        public FlowRecordedTemplateReader Reader => new(DatabasePath);

        public SqlSugarClient CreateDatabase(bool includeSnapshot = true, bool utf16 = false)
        {
            var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString(),
                DbType = SqlSugar.DbType.Sqlite, IsAutoCloseConnection = !utf16, InitKeyType = InitKeyType.Attribute,
            });
            if (utf16)
                db.Ado.ExecuteCommand("PRAGMA encoding='UTF-16'");
            db.CodeFirst.InitTables<FlowRunRecord>();
            if (includeSnapshot)
                db.CodeFirst.InitTables<FlowTemplateSnapshot>();
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
            var tempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            Assert.Equal(tempRoot, Path.GetDirectoryName(fullRoot), ignoreCase: true);
            Assert.StartsWith("CopilotRecordedFlowTemplate-", Path.GetFileName(fullRoot), StringComparison.Ordinal);
            Directory.Delete(fullRoot, recursive: true);
        }
    }
}
