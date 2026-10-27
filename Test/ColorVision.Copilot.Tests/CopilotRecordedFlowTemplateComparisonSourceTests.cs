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

public sealed class CopilotRecordedFlowTemplateComparisonSourceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceKeysAreStableForNormalizedPathsAndDistinguishCopiedJournalPaths(bool copiedJournal)
    {
        using var fixture = new DatabaseFixture();
        var snapshot = fixture.Snapshot([1, 2, 3]);
        var run = fixture.Run(snapshot: snapshot);
        var first = await fixture.Reader.ReadPairAsync(run.Id, run.Id, CancellationToken.None);
        var repeated = await fixture.Reader.ReadPairAsync(run.Id, run.Id, CancellationToken.None);
        var alternatePath = copiedJournal
            ? Path.Combine(fixture.Root, "copied.db")
            : Path.Combine(fixture.Root, ".", "pair.db").Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
        if (copiedJournal)
        {
            File.Copy(fixture.DatabasePath, alternatePath);
            Assert.Equal(File.ReadAllBytes(fixture.DatabasePath), File.ReadAllBytes(alternatePath));
        }

        var alternate = await new FlowRecordedTemplateReader(alternatePath).ReadPairAsync(run.Id, run.Id, CancellationToken.None);

        Assert.Equal(first.SourceKey, repeated.SourceKey);
        Assert.Equal(copiedJournal, first.SourceKey != alternate.SourceKey);
        Assert.Equal(first.Baseline!.Snapshot!.Content, alternate.Baseline!.Snapshot!.Content);
        foreach (var key in new[] { first.SourceKey, alternate.SourceKey })
        {
            Assert.Equal(64, key.Length);
            Assert.All(key, character => Assert.True(char.IsAsciiHexDigit(character)));
            Assert.DoesNotContain(fixture.Root, key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("pair.db", key, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task StoredUnknownNodesCanBeComparedThroughTheRealPairedReaderWithoutPublishingValuesOrChangingStorage()
    {
        using var fixture = new DatabaseFixture();
        var nodeId = Guid.Parse("c845d969-109d-4d53-a1c2-515c474e5e03");
        byte[] SavedBytes(string value)
        {
            var payload = STNodeSavedState.Write("Unavailable.Plugin", "Unavailable.Plugin.RecordedNode", new Dictionary<string, byte[]>
            {
                ["Guid"] = nodeId.ToByteArray(), ["PrivateValue"] = Encoding.UTF8.GetBytes(value),
                [STNodeSavedState.PortLayoutKey] = JsonSerializer.SerializeToUtf8Bytes(new STNodePortLayout
                {
                    Title = "Recorded unknown node", Inputs = [], Outputs = [],
                }),
            });
            using var stream = new MemoryStream();
            STNodeCanvasWriter.WriteRaw(stream, [payload], [], 0, 0, 1);
            return stream.ToArray();
        }
        var baselineSnapshot = fixture.Snapshot(SavedBytes("PRIVATE_BASELINE_VALUE"));
        var currentSnapshot = fixture.Snapshot(SavedBytes("PRIVATE_CURRENT_VALUE"), templateId: 43, revision: 9);
        var baseline = fixture.Run(snapshot: baselineSnapshot);
        var current = fixture.Run(snapshot: currentSnapshot, templateId: 43, revision: 9);
        var before = File.ReadAllBytes(fixture.DatabasePath);
        var service = new CopilotRecordedFlowTemplateComparisonService(fixture.Reader.ReadPairAsync);

        var result = await service.CompareAsync(baseline.Id, current.Id, 80, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        var evidence = document.RootElement;
        Assert.Equal("flow_key", evidence.GetProperty("identity_basis").GetString());
        Assert.Equal(baseline.Id, evidence.GetProperty("baseline").GetProperty("run_record_id").GetInt32());
        Assert.Equal(current.Id, evidence.GetProperty("current").GetProperty("run_record_id").GetInt32());
        Assert.Equal(baselineSnapshot.Id, evidence.GetProperty("baseline").GetProperty("snapshot_id").GetInt64());
        Assert.Equal(currentSnapshot.Id, evidence.GetProperty("current").GetProperty("snapshot_id").GetInt64());
        Assert.True(evidence.GetProperty("baseline").GetProperty("stored_integrity_verified").GetBoolean());
        Assert.True(evidence.GetProperty("current").GetProperty("stored_integrity_verified").GetBoolean());
        var change = Assert.Single(evidence.GetProperty("node_changes").EnumerateArray());
        Assert.Equal(nodeId, change.GetProperty("node_id").GetGuid());
        Assert.Equal("modified", change.GetProperty("kind").GetString());
        Assert.Equal("Unavailable.Plugin.RecordedNode", change.GetProperty("current").GetProperty("type_key").GetString());
        Assert.Equal("PrivateValue", Assert.Single(change.GetProperty("changed_properties").EnumerateArray()).GetString());
        Assert.True(evidence.GetProperty("coverage").GetProperty("comparison_complete").GetBoolean());
        Assert.DoesNotContain("PRIVATE_BASELINE_VALUE", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_CURRENT_VALUE", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("private-shared-SN", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("private-machine", result.Content, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
        Assert.Equal(new[] { "pair.db" }, Directory.GetFiles(fixture.Root).Select(Path.GetFileName));
    }

    [Fact]
    public async Task PairReadsBothExactReferencedSnapshotsWithoutPrivateFieldsOrDatabaseChanges()
    {
        using var fixture = new DatabaseFixture();
        var baselineSnapshot = fixture.Snapshot([1, 2, 3]);
        var currentSnapshot = fixture.Snapshot([4, 5], templateId: 43, revision: 9);
        var baseline = fixture.Run(snapshot: baselineSnapshot);
        var current = fixture.Run(snapshot: currentSnapshot, templateId: 43, revision: 9);
        var before = File.ReadAllBytes(fixture.DatabasePath);

        var pair = await fixture.Reader.ReadPairAsync(baseline.Id, current.Id, CancellationToken.None);

        Assert.Equal("flow_key", pair.IdentityBasis);
        Assert.Equal(baseline.Id, pair.Baseline!.Run.Id);
        Assert.Equal(current.Id, pair.Current!.Run.Id);
        Assert.Equal(baselineSnapshot.Id, pair.Baseline.Snapshot!.Id);
        Assert.Equal(currentSnapshot.Id, pair.Current.Snapshot!.Id);
        Assert.Equal(baselineSnapshot.Content, pair.Baseline.Snapshot.Content);
        Assert.Equal(currentSnapshot.Content, pair.Current.Snapshot.Content);
        Assert.False(pair.Baseline.TextWasTruncated);
        Assert.False(pair.Current.TextWasTruncated);
        Assert.All(new[] { pair.Baseline.Run, pair.Current.Run }, run =>
        {
            Assert.Null(run.SerialNumber);
            Assert.Null(run.BatchId);
            Assert.Null(run.OwnerMachine);
            Assert.Null(run.OwnerInstanceId);
        });
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
        Assert.Equal(new[] { "pair.db" }, Directory.GetFiles(fixture.Root).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("flow-a", "flow-a", 42, 43, "flow_key")]
    [InlineData("flow-a", "flow-b", 42, 42, "different")]
    [InlineData(null, null, 42, 42, "legacy_template_id")]
    [InlineData("\u2003\t", "\u00a0", 42, 42, "legacy_template_id")]
    [InlineData(null, null, 42, 43, "unavailable")]
    [InlineData(null, "flow-a", 42, 42, "unavailable")]
    [InlineData(null, null, 0, 0, "unavailable")]
    public async Task PairIdentityUsesOnlyNormalizedKeysOrSharedPositiveLegacyIds(
        string? baselineKey, string? currentKey, int baselineTemplateId, int currentTemplateId, string expected)
    {
        using var fixture = new DatabaseFixture(includeSnapshots: false);
        var baseline = fixture.Run(flowKey: baselineKey, templateId: baselineTemplateId);
        var current = fixture.Run(flowKey: currentKey, templateId: currentTemplateId);

        var pair = await fixture.Reader.ReadPairAsync(baseline.Id, current.Id, CancellationToken.None);

        Assert.Equal(expected, pair.IdentityBasis);
        Assert.Null(pair.Baseline!.Snapshot);
        Assert.Null(pair.Current!.Snapshot);
    }

    [Fact]
    public async Task FullUnicodeNormalizedKeysRemainDistinctBeyondTheBoundedMetadataPrefix()
    {
        using var fixture = new DatabaseFixture(includeSnapshots: false);
        var prefix = new string('k', 300);
        var baseline = fixture.Run(flowKey: "\u2003" + prefix + "one\t");
        var same = fixture.Run(flowKey: prefix + "one");
        var other = fixture.Run(flowKey: prefix + "two");

        var samePair = await fixture.Reader.ReadPairAsync(baseline.Id, same.Id, CancellationToken.None);
        var otherPair = await fixture.Reader.ReadPairAsync(baseline.Id, other.Id, CancellationToken.None);

        Assert.Equal("flow_key", samePair.IdentityBasis);
        Assert.Equal("different", otherPair.IdentityBasis);
        Assert.Equal(otherPair.Current!.Run.FlowKey, samePair.Current!.Run.FlowKey);
        Assert.True(otherPair.Current!.TextWasTruncated);
    }

    [Fact]
    public async Task SameRunIsLegalAndMissingSidesRemainDistinctFromMissingSnapshotReferences()
    {
        using var fixture = new DatabaseFixture();
        var snapshot = fixture.Snapshot([9, 8, 7]);
        var run = fixture.Run(snapshot: snapshot);

        var same = await fixture.Reader.ReadPairAsync(run.Id, run.Id, CancellationToken.None);
        var missingCurrent = await fixture.Reader.ReadPairAsync(run.Id, run.Id + 100, CancellationToken.None);
        var missingBaseline = await fixture.Reader.ReadPairAsync(run.Id + 100, run.Id, CancellationToken.None);
        var missingBoth = await fixture.Reader.ReadPairAsync(run.Id + 100, run.Id + 100, CancellationToken.None);

        Assert.Equal("flow_key", same.IdentityBasis);
        Assert.Equal(snapshot.Content, same.Baseline!.Snapshot!.Content);
        Assert.Equal(snapshot.Content, same.Current!.Snapshot!.Content);
        Assert.NotNull(missingCurrent.Baseline);
        Assert.Null(missingCurrent.Current);
        Assert.Equal("unavailable", missingCurrent.IdentityBasis);
        Assert.Null(missingBaseline.Baseline);
        Assert.NotNull(missingBaseline.Current);
        Assert.Null(missingBoth.Baseline);
        Assert.Null(missingBoth.Current);
        var single = await fixture.Reader.ReadAsync(run.Id, CancellationToken.None);
        Assert.Equal(single!.Snapshot!.Content, same.Baseline.Snapshot.Content);
    }

    [Fact]
    public async Task MissingReferencedRowsDoNotFallBackToAnotherSnapshotWithTheSameHash()
    {
        using var fixture = new DatabaseFixture();
        var existing = fixture.Snapshot([1, 2, 3]);
        var unreferenced = fixture.Run();
        var missing = fixture.Run();
        fixture.Db.Ado.ExecuteCommand("UPDATE FlowRunRecord SET snapshot_id=@snapshot,content_hash=@hash WHERE id=@id",
            new SugarParameter("@snapshot", existing.Id + 1), new SugarParameter("@hash", existing.ContentHash), new SugarParameter("@id", missing.Id));

        var pair = await fixture.Reader.ReadPairAsync(unreferenced.Id, missing.Id, CancellationToken.None);

        Assert.NotNull(pair.Baseline);
        Assert.Null(pair.Baseline.Snapshot);
        Assert.NotNull(pair.Current);
        Assert.Null(pair.Current.Snapshot);
    }

    [Theory]
    [InlineData("run_hash_nul")]
    [InlineData("snapshot_hash_nul")]
    [InlineData("length")]
    [InlineData("oversize")]
    [InlineData("sha256")]
    [InlineData("schema")]
    public async Task InvalidCurrentEvidenceFailsThePairInsteadOfPublishingAPartialSuccessfulComparison(string kind)
    {
        using var fixture = new DatabaseFixture();
        var baselineSnapshot = fixture.Snapshot([1, 2, 3]);
        var currentSnapshot = fixture.Snapshot([4, 5]);
        var baseline = fixture.Run(snapshot: baselineSnapshot);
        var current = fixture.Run(snapshot: currentSnapshot);
        switch (kind)
        {
            case "run_hash_nul":
                fixture.Db.Ado.ExecuteCommand("UPDATE FlowRunRecord SET content_hash=@hash WHERE id=@id",
                    new SugarParameter("@hash", currentSnapshot.ContentHash + "\0suffix"), new SugarParameter("@id", current.Id));
                break;
            case "snapshot_hash_nul":
                fixture.Db.Ado.ExecuteCommand("UPDATE FlowTemplateSnapshot SET content_hash=@hash WHERE id=@id",
                    new SugarParameter("@hash", currentSnapshot.ContentHash + "\0suffix"), new SugarParameter("@id", currentSnapshot.Id));
                break;
            case "length":
                fixture.Db.Ado.ExecuteCommand("UPDATE FlowTemplateSnapshot SET content_length=content_length+1 WHERE id=@id", new SugarParameter("@id", currentSnapshot.Id));
                break;
            case "oversize":
                fixture.Db.Ado.ExecuteCommand("UPDATE FlowTemplateSnapshot SET content=zeroblob(@length),content_length=@length WHERE id=@id",
                    new SugarParameter("@length", FlowRecordedTemplateReader.MaximumSnapshotBytes + 1), new SugarParameter("@id", currentSnapshot.Id));
                break;
            case "sha256":
                fixture.Db.Ado.ExecuteCommand("UPDATE FlowTemplateSnapshot SET content=@content WHERE id=@id",
                    new SugarParameter("@content", new byte[] { 7, 8 }), new SugarParameter("@id", currentSnapshot.Id));
                break;
            case "schema":
                fixture.Db.Ado.ExecuteCommand("ALTER TABLE FlowTemplateSnapshot DROP COLUMN content_length");
                break;
        }
        var before = File.ReadAllBytes(fixture.DatabasePath);

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Reader.ReadPairAsync(baseline.Id, current.Id, CancellationToken.None));

        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Fact]
    public async Task PairedReadsPreserveValidHashesInUtf16Storage()
    {
        using var fixture = new DatabaseFixture(utf16: true);
        var snapshot = fixture.Snapshot([1, 2, 3]);
        var baseline = fixture.Run(snapshot: snapshot);
        var current = fixture.Run(snapshot: snapshot, templateId: 43, revision: 9);
        Assert.Equal(128L, Convert.ToInt64(fixture.Db.Ado.GetScalar("SELECT length(CAST(content_hash AS BLOB)) FROM FlowRunRecord LIMIT 1")));

        var pair = await fixture.Reader.ReadPairAsync(baseline.Id, current.Id, CancellationToken.None);

        Assert.Equal("flow_key", pair.IdentityBasis);
        Assert.Equal(snapshot.ContentHash, pair.Baseline!.Snapshot!.ContentHash);
        Assert.Equal(snapshot.ContentHash, pair.Current!.Snapshot!.ContentHash);
    }

    [Fact]
    public async Task InvalidIdsCancellationAndMissingFileNeverCreatePairedStorage()
    {
        using var fixture = new DatabaseFixture(includeSnapshots: false);
        var path = Path.Combine(fixture.Root, "missing.db");
        var reader = new FlowRecordedTemplateReader(path);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.ReadPairAsync(0, 1, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.ReadPairAsync(1, 0, CancellationToken.None));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadPairAsync(1, 2, cancellation.Token));
        await Assert.ThrowsAsync<FileNotFoundException>(() => reader.ReadPairAsync(1, 2, CancellationToken.None));
        Assert.False(File.Exists(path));
    }

    private sealed class DatabaseFixture : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("CopilotRecordedTemplatePair-").FullName;
        public string DatabasePath => Path.Combine(Root, "pair.db");
        public SqlSugarClient Db { get; }
        public FlowRecordedTemplateReader Reader => new(DatabasePath);

        public DatabaseFixture(bool includeSnapshots = true, bool utf16 = false)
        {
            Db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString(),
                DbType = SqlSugar.DbType.Sqlite, IsAutoCloseConnection = !utf16, InitKeyType = InitKeyType.Attribute,
            });
            if (utf16)
                Db.Ado.ExecuteCommand("PRAGMA encoding='UTF-16'");
            Db.CodeFirst.InitTables<FlowRunRecord>();
            if (includeSnapshots)
                Db.CodeFirst.InitTables<FlowTemplateSnapshot>();
            if (utf16)
            {
                Assert.StartsWith("UTF-16", Convert.ToString(Db.Ado.GetScalar("PRAGMA encoding")), StringComparison.OrdinalIgnoreCase);
                Db.Ado.Close();
                Db.CurrentConnectionConfig.IsAutoCloseConnection = true;
            }
        }

        public FlowTemplateSnapshot Snapshot(byte[] content, int templateId = 42, int revision = 7)
        {
            var snapshot = new FlowTemplateSnapshot
            {
                TemplateId = templateId, FlowKey = "flow-a", TemplateRevision = revision, Content = content,
                ContentLength = content.Length, ContentHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
                CapturedTimeUtc = new DateTime(2026, 10, 27, 1, 0, 0, DateTimeKind.Utc),
            };
            snapshot.Id = Db.Insertable(snapshot).ExecuteReturnBigIdentity();
            return snapshot;
        }

        public FlowRunRecord Run(string? flowKey = "flow-a", int templateId = 42, int revision = 7, FlowTemplateSnapshot? snapshot = null)
        {
            var run = new FlowRunRecord
            {
                TemplateId = templateId, FlowKey = flowKey, TemplateRevision = revision, SnapshotId = snapshot?.Id,
                ContentHash = snapshot?.ContentHash, FlowName = "Shared name", RunKey = Guid.NewGuid().ToString("N"),
                SerialNumber = "private-shared-SN", BatchId = 777, OwnerMachine = "private-machine", OwnerInstanceId = "private-instance",
            };
            run.Id = Db.Insertable(run).ExecuteReturnIdentity();
            return run;
        }

        public void Dispose()
        {
            Db.Ado.Close();
            Db.Dispose();
            var root = Path.GetFullPath(Root);
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), Path.GetDirectoryName(root), ignoreCase: true);
            Assert.StartsWith("CopilotRecordedTemplatePair-", Path.GetFileName(root), StringComparison.Ordinal);
            Directory.Delete(root, recursive: true);
        }
    }
}
