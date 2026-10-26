using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Services.FlowDiagnostics;
using Microsoft.Data.Sqlite;
using SqlSugar;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowIncidentReadSourceTests
{
    private static readonly DateTime StartedAt = new(2026, 10, 27, 1, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ValidHashMetadataIsPreservedInUtf16SqliteStorage()
    {
        using var fixture = new DatabaseFixture();
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = fixture.DatabasePath, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA encoding='UTF-16'; CREATE TABLE EncodingMarker(id INTEGER)";
            command.ExecuteNonQuery();
        }
        FlowIncident incident;
        var hash = new string('a', 64);
        using (var db = fixture.CreateDatabase())
        {
            var run = InsertRun(db, "utf16-hash");
            db.Ado.ExecuteCommand("UPDATE FlowRunRecord SET content_hash=@hash", new SugarParameter("@hash", hash));
            Assert.Equal(128, Convert.ToInt32(db.Ado.GetScalar("SELECT length(CAST(content_hash AS BLOB)) FROM FlowRunRecord LIMIT 1")));
            incident = InsertIncident(db, run.Id, null);
        }
        var result = Assert.IsType<FlowIncidentInspectionResult>(await fixture.Reader.ReadAsync(incident.Id, 40, CancellationToken.None));
        Assert.False(result.RunContentHashInvalid);
        Assert.Equal(hash, result.Run!.ContentHash);
    }

    [Theory]
    [InlineData("\0suffix")]
    [InlineData("😀suffix")]
    public async Task InvalidRecordedHashesDoNotBecomeValidIdentityAfterSqlTextTruncation(string suffix)
    {
        using var fixture = new DatabaseFixture();
        FlowIncident incident;
        using (var db = fixture.CreateDatabase())
        {
            var run = InsertRun(db, "invalid-hash");
            db.Ado.ExecuteCommand("UPDATE FlowRunRecord SET content_hash=@hash", new SugarParameter("@hash", new string('a', 64) + suffix));
            incident = InsertIncident(db, run.Id, null);
        }
        var before = File.ReadAllBytes(fixture.DatabasePath);
        var source = Assert.IsType<FlowIncidentInspectionResult>(await fixture.Reader.ReadAsync(incident.Id, 40, CancellationToken.None));
        Assert.True(source.RunContentHashInvalid);
        Assert.Null(source.Run!.ContentHash);
        var result = await new CopilotFlowIncidentInspectionService(fixture.Reader.ReadAsync).InspectAsync(incident.Id, 40, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("run").GetProperty("content_hash").ValueKind);
        Assert.Equal("invalid", document.RootElement.GetProperty("coverage").GetProperty("run_template_identity").GetProperty("content_hash_status").GetString());
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Fact]
    public async Task ListServiceContinuationFeedsAnExactIncidentIntoInspectionWithoutChangingTheDatabase()
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord targetRun;
        FlowIncident targetIncident;
        FlowIncident newerIncident;
        FlowNodeAttempt targetAttempt;
        using (var db = fixture.CreateDatabase())
        {
            targetRun = InsertRun(db, "list-inspect-target");
            var otherRun = InsertRun(db, "list-inspect-other");
            targetAttempt = InsertAttempt(db, targetRun.Id, 1);
            var otherAttempt = InsertAttempt(db, otherRun.Id, 1);
            targetIncident = InsertIncident(db, targetRun.Id, targetAttempt.Id, detectedTimeUtc: StartedAt.AddMinutes(-1));
            newerIncident = InsertIncident(db, otherRun.Id, otherAttempt.Id);
            InsertEvent(db, targetRun.Id, 1);
            InsertEvent(db, otherRun.Id, 999);
        }
        var before = File.ReadAllBytes(fixture.DatabasePath);
        var listService = new CopilotFlowIncidentQueryService(fixture.Reader.QueryAsync);
        var inspectService = new CopilotFlowIncidentInspectionService(fixture.Reader.ReadAsync);

        var first = await listService.QueryAsync("active", 1, null, CancellationToken.None);
        Assert.True(first.Success, first.ErrorMessage);
        using var firstDocument = JsonDocument.Parse(first.Content);
        Assert.Equal(newerIncident.Id, Assert.Single(firstDocument.RootElement.GetProperty("incidents").EnumerateArray())
            .GetProperty("incident_id").GetInt64());
        Assert.True(firstDocument.RootElement.GetProperty("has_more").GetBoolean());
        var cursor = firstDocument.RootElement.GetProperty("next_cursor").GetString();
        Assert.False(string.IsNullOrEmpty(cursor));
        var next = await listService.QueryAsync("active", 1, cursor, CancellationToken.None);
        Assert.True(next.Success, next.ErrorMessage);
        using var nextDocument = JsonDocument.Parse(next.Content);
        var listed = Assert.Single(nextDocument.RootElement.GetProperty("incidents").EnumerateArray());
        var listedId = listed.GetProperty("incident_id").GetInt64();
        Assert.Equal(targetIncident.Id, listedId);
        Assert.Equal(targetRun.Id, listed.GetProperty("run_record_id").GetInt32());
        Assert.False(nextDocument.RootElement.GetProperty("has_more").GetBoolean());
        Assert.Equal(JsonValueKind.Null, nextDocument.RootElement.GetProperty("next_cursor").ValueKind);

        var detail = await inspectService.InspectAsync(listedId, 100, CancellationToken.None);
        Assert.True(detail.Success, detail.ErrorMessage);
        using var detailDocument = JsonDocument.Parse(detail.Content);
        var evidence = detailDocument.RootElement;
        Assert.Equal(listedId, evidence.GetProperty("incident").GetProperty("incident_id").GetInt64());
        Assert.Equal(targetRun.Id, evidence.GetProperty("run").GetProperty("run_record_id").GetInt32());
        Assert.Equal(targetAttempt.Id, Assert.Single(evidence.GetProperty("attempts").EnumerateArray()).GetProperty("id").GetInt64());
        Assert.Equal(targetRun.Id, Assert.Single(evidence.GetProperty("events").EnumerateArray()).GetProperty("run_record_id").GetInt32());
        Assert.True(evidence.GetProperty("coverage").GetProperty("returned_window_complete").GetBoolean());
        Assert.DoesNotContain("private-", first.Content + next.Content + detail.Content, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
        Assert.Equal(new[] { "diagnostics.db" }, Directory.GetFiles(fixture.Root).Select(Path.GetFileName));
    }

    [Fact]
    public async Task RealIncidentAcknowledgementThatReencodesATimestampInvalidatesTheListCursor()
    {
        using var fixture = new DatabaseFixture();
        using var db = fixture.CreateDatabase();
        // The production triage service is internal to Engine; instantiate it only against this isolated database.
        var serviceType = typeof(FlowIncident).Assembly.GetType("ColorVision.Engine.FlowProcessing.Diagnostics.FlowIncidentService", throwOnError: true)!;
        using var triage = (IDisposable)serviceType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, [typeof(SqlSugarClient)], modifiers: null)!.Invoke([db]);
        var run = InsertRun(db, "timestamp-reencoding");
        for (var index = 0; index < 3; index++)
            InsertIncident(db, run.Id, null);
        // Keep the same wall-clock value through SqlSugar; only the stored T separator is reencoded as a space.
        const string originalTimestamp = "2026-10-27T01:00:00";
        db.Ado.ExecuteCommand("UPDATE FlowIncident SET detected_time_utc=@stamp", new SugarParameter("@stamp", originalTimestamp));
        var originalPage = await fixture.Reader.QueryAsync("active", 2, null, CancellationToken.None);
        var anchor = originalPage.Incidents[^1];
        var service = new CopilotFlowIncidentQueryService(fixture.Reader.QueryAsync);
        var first = await service.QueryAsync("active", 2, null, CancellationToken.None);
        Assert.True(first.Success, first.ErrorMessage);
        using var document = JsonDocument.Parse(first.Content);
        var cursor = document.RootElement.GetProperty("next_cursor").GetString();
        Assert.False(string.IsNullOrEmpty(cursor));

        var updated = Assert.IsType<FlowIncident>(serviceType.GetMethod("Acknowledge")!
            .Invoke(triage, [anchor.Id, "fixture-operator", "Reviewed in the isolated fixture.", StartedAt.AddMinutes(1)]));
        Assert.Equal("Acknowledged", updated.State);
        var storedTimestamp = Convert.ToString(db.Ado.GetScalar(
            "SELECT detected_time_utc FROM FlowIncident WHERE id=@id", new SugarParameter("@id", anchor.Id)));
        Assert.NotEqual(originalTimestamp, storedTimestamp);
        var sourcePage = await fixture.Reader.QueryAsync("active", 2, anchor.Id, CancellationToken.None);
        Assert.Equal(anchor.DetectedTimeUtc.Ticks, sourcePage.AnchorDetectedTimeUtc!.Value.Ticks);
        Assert.NotEqual(originalPage.DetectedTimeFingerprints[anchor.Id], sourcePage.AnchorDetectedTimeFingerprint);
        Assert.Empty(sourcePage.Incidents); // The changed raw key would otherwise falsely look like the end of the list.
        var bytesAfterAcknowledgement = File.ReadAllBytes(fixture.DatabasePath);

        var continuation = await service.QueryAsync("active", 2, cursor, CancellationToken.None);

        Assert.False(continuation.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, continuation.FailureKind);
        Assert.Empty(continuation.Content);
        Assert.Contains("Restart", continuation.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(bytesAfterAcknowledgement, File.ReadAllBytes(fixture.DatabasePath));
    }

    [Theory]
    [InlineData(" ACTIVE ", "FutureState,Acknowledged,Open")]
    [InlineData("open", "Open")]
    [InlineData("acknowledged", "Acknowledged")]
    [InlineData("resolved", "Resolved")]
    [InlineData("all", "FutureState,Resolved,Acknowledged,Open")]
    public async Task ListFiltersIncidentStatesAndRetainsUnknownActiveStates(string state, string expectedStates)
    {
        using var fixture = new DatabaseFixture();
        using (var db = fixture.CreateDatabase(incidentOnly: true))
        {
            foreach (var storedState in new[] { "Open", "Acknowledged", "Resolved", "FutureState" })
                InsertIncident(db, 42, null, state: storedState);
        }

        var page = await fixture.Reader.QueryAsync(state, 100, null, CancellationToken.None);

        Assert.Equal(expectedStates.Split(','), page.Incidents.Select(item => item.State));
        Assert.False(page.HasMore);
        Assert.False(page.TextWasTruncated);
    }

    [Fact]
    public async Task ListContinuationUsesTheStoredDateValueAndIdToAvoidGapsOrDuplicatesAtTimestampTies()
    {
        using var fixture = new DatabaseFixture();
        var tiedIds = new List<long>();
        long newerId;
        long olderId;
        using (var db = fixture.CreateDatabase(incidentOnly: true))
        {
            olderId = InsertIncident(db, 42, null).Id;
            for (var index = 0; index < 7; index++)
                tiedIds.Add(InsertIncident(db, 42, null).Id);
            newerId = InsertIncident(db, 42, null).Id;
            // The ISO timestamp deliberately differs from Sqlite's DateTime parameter formatting.
            db.Ado.ExecuteCommand("UPDATE FlowIncident SET detected_time_utc='2026-10-27T01:00:00.1234567Z'");
            db.Ado.ExecuteCommand("UPDATE FlowIncident SET detected_time_utc='2026-10-26T01:00:00.1234567Z' WHERE id=@id",
                new SugarParameter("@id", olderId));
            db.Ado.ExecuteCommand("UPDATE FlowIncident SET detected_time_utc='2026-10-28T01:00:00.1234567Z' WHERE id=@id",
                new SugarParameter("@id", newerId));
        }
        var observedIds = new List<long>();
        var pageFlags = new List<bool>();
        long? anchor = null;
        DateTime? expectedAnchorDate = null;
        do
        {
            var page = await fixture.Reader.QueryAsync("all", 3, anchor, CancellationToken.None);
            Assert.NotEmpty(page.Incidents);
            Assert.Equal(expectedAnchorDate, page.AnchorDetectedTimeUtc);
            Assert.Equal(page.Incidents.Select(item => item.Id).Order(), page.DetectedTimeFingerprints.Keys.Order());
            Assert.All(page.DetectedTimeFingerprints.Values, value => Assert.Matches("^[0-9a-f]{64}$", value));
            observedIds.AddRange(page.Incidents.Select(item => item.Id));
            pageFlags.Add(page.HasMore);
            anchor = page.HasMore ? page.Incidents[^1].Id : null;
            expectedAnchorDate = page.HasMore ? page.Incidents[^1].DetectedTimeUtc : null;
        } while (anchor.HasValue);

        Assert.Equal(new[] { newerId }.Concat(tiedIds.AsEnumerable().Reverse()).Append(olderId), observedIds);
        Assert.Equal(observedIds.Count, observedIds.Distinct().Count());
        Assert.Equal(new[] { true, true, false }, pageFlags);
    }

    [Fact]
    public async Task ListAnchorAllowsStateChangesReportsTimestampChangesAndExplicitlyRejectsDeletion()
    {
        using var fixture = new DatabaseFixture();
        using var db = fixture.CreateDatabase(incidentOnly: true);
        InsertIncident(db, 42, null);
        InsertIncident(db, 43, null);
        var first = await fixture.Reader.QueryAsync("open", 1, null, CancellationToken.None);
        var anchor = Assert.Single(first.Incidents);
        var anchorId = anchor.Id;
        Assert.True(first.HasMore);
        Assert.Null(first.AnchorDetectedTimeUtc);
        db.Updateable<FlowIncident>().SetColumns(item => item.State == "Resolved")
            .Where(item => item.Id == anchorId).ExecuteCommand();
        var changedState = await fixture.Reader.QueryAsync("open", 1, anchorId, CancellationToken.None);
        Assert.Single(changedState.Incidents);
        Assert.Equal(anchor.DetectedTimeUtc, changedState.AnchorDetectedTimeUtc);
        var changedTime = StartedAt.AddMinutes(1);
        db.Updateable<FlowIncident>().SetColumns(item => item.DetectedTimeUtc == changedTime)
            .Where(item => item.Id == anchorId).ExecuteCommand();
        var changedTimestamp = await fixture.Reader.QueryAsync("open", 1, anchorId, CancellationToken.None);
        Assert.Equal(changedTime, changedTimestamp.AnchorDetectedTimeUtc);
        Assert.NotEqual(anchor.DetectedTimeUtc, changedTimestamp.AnchorDetectedTimeUtc);
        db.Deleteable<FlowIncident>().Where(item => item.Id == anchorId).ExecuteCommand();

        var failure = await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Reader.QueryAsync("all", 1, anchorId, CancellationToken.None));

        Assert.Equal("beforeIncidentId", failure.ParamName);
        Assert.Contains("Restart", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IncidentOnlyListsKeepSafeProjectionsAndBindOnlyTheNormalizedSourcePath()
    {
        using var fixture = new DatabaseFixture();
        using var otherFixture = new DatabaseFixture();
        var diagnosticPrefix = new string('D', FlowIncidentInspectionReader.MaximumDiagnosticCharacters - 1);
        FlowIncident newest;
        FlowIncident oldest;
        using (var db = fixture.CreateDatabase(incidentOnly: true))
        {
            oldest = InsertIncident(db, 11, null, diagnosticPrefix + "😀tail", detectedTimeUtc: StartedAt.AddMinutes(-1));
            newest = InsertIncident(db, 22, null);
        }
        using (var db = otherFixture.CreateDatabase(incidentOnly: true)) { }
        var before = File.ReadAllBytes(fixture.DatabasePath);

        var first = await fixture.Reader.QueryAsync("active", 1, null, CancellationToken.None);
        var last = await fixture.Reader.QueryAsync("active", 1, newest.Id, CancellationToken.None);
        var normalizedPath = Path.Combine(fixture.Root, ".", "diagnostics.db").ToUpperInvariant().Replace('\\', '/');
        var sameSource = await new FlowIncidentInspectionReader(normalizedPath).QueryAsync("active", 1, null, CancellationToken.None);
        var otherSource = await otherFixture.Reader.QueryAsync("active", 1, null, CancellationToken.None);

        Assert.Equal(newest.Id, Assert.Single(first.Incidents).Id);
        Assert.True(first.HasMore);
        Assert.False(first.TextWasTruncated); // A truncated look-ahead row was not delivered on this page.
        Assert.Equal(oldest.Id, Assert.Single(last.Incidents).Id);
        Assert.Equal(diagnosticPrefix, last.Incidents[0].Summary);
        Assert.False(last.HasMore);
        Assert.True(last.TextWasTruncated);
        Assert.All(first.Incidents.Concat(last.Incidents), item =>
        {
            Assert.Null(item.DetailsJson);
            Assert.Null(item.OperatorName);
            Assert.Null(item.AcknowledgedOperator);
            Assert.Null(item.AcknowledgmentNote);
            Assert.Null(item.Resolution);
        });
        Assert.Equal(first.SourceKey, last.SourceKey);
        Assert.Equal(first.SourceKey, sameSource.SourceKey);
        Assert.NotEqual(first.SourceKey, otherSource.SourceKey);
        Assert.Matches("^[0-9a-f]{64}$", first.SourceKey);
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
        Assert.Equal(new[] { "diagnostics.db" }, Directory.GetFiles(fixture.Root).Select(Path.GetFileName));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Reader.ReadAsync(newest.Id, 1, CancellationToken.None));
    }

    [Theory]
    [InlineData("missing_file")]
    [InlineData("missing_table")]
    [InlineData("missing_column")]
    public async Task ListDoesNotCreateOrMigrateAnUnavailableIncidentSchema(string scenario)
    {
        using var fixture = new DatabaseFixture();
        if (scenario != "missing_file")
        {
            using var db = fixture.CreateDatabase(incidentOnly: true);
            db.Ado.ExecuteCommand(scenario == "missing_table"
                ? "DROP TABLE FlowIncident" : "ALTER TABLE FlowIncident DROP COLUMN summary");
        }
        var before = File.Exists(fixture.DatabasePath) ? File.ReadAllBytes(fixture.DatabasePath) : null;

        var failure = await Record.ExceptionAsync(() => fixture.Reader.QueryAsync("all", 1, null, CancellationToken.None));

        if (scenario == "missing_file")
        {
            Assert.IsType<FileNotFoundException>(failure);
            Assert.Empty(Directory.GetFiles(fixture.Root));
        }
        else
        {
            Assert.IsType<InvalidDataException>(failure);
            Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
            Assert.Equal(new[] { "diagnostics.db" }, Directory.GetFiles(fixture.Root).Select(Path.GetFileName));
        }
    }

    [Fact]
    public async Task InvalidListBoundsOrStateAreRejectedBeforeOpeningTheDatabase()
    {
        using var fixture = new DatabaseFixture();
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Reader.QueryAsync("unknown", 1, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Reader.QueryAsync("all", 0, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Reader.QueryAsync("all", 101, null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Reader.QueryAsync("all", 1, 0, CancellationToken.None));
        Assert.Empty(Directory.GetFiles(fixture.Root));
    }

    [Fact]
    public async Task ExactIncidentRunNeverBorrowsAnotherRunsAttemptsOrPrivatePayloads()
    {
        using var fixture = new DatabaseFixture();
        FlowRunRecord run;
        FlowIncident incident;
        FlowNodeAttempt ownAttempt;
        using (var db = fixture.CreateDatabase())
        {
            run = InsertRun(db, "run-one");
            var otherRun = InsertRun(db, "run-two");
            ownAttempt = InsertAttempt(db, run.Id, 1);
            var foreignAttempt = InsertAttempt(db, otherRun.Id, 1);
            incident = InsertIncident(db, run.Id, foreignAttempt.Id);
            InsertEvent(db, run.Id, 1);
            InsertEvent(db, otherRun.Id, 999);
        }
        var before = File.ReadAllBytes(fixture.DatabasePath);

        var result = Assert.IsType<FlowIncidentInspectionResult>(await fixture.Reader.ReadAsync(incident.Id, 100, CancellationToken.None));

        Assert.Equal(incident.Id, result.Incident.Id);
        Assert.Equal(run.Id, result.Run!.Id);
        Assert.Equal(ownAttempt.Id, Assert.Single(result.Attempts).Id);
        Assert.Equal(run.Id, Assert.Single(result.Events).RunRecordId);
        Assert.True(result.LinkedAttemptMissing);
        Assert.False(result.AttemptsTruncated);
        Assert.False(result.EventsTruncated);
        Assert.False(result.TextWasTruncated);
        Assert.Null(result.Run.SerialNumber);
        Assert.Null(result.Run.BatchId);
        Assert.Null(result.Run.OwnerMachine);
        Assert.Null(result.Run.OwnerInstanceId);
        Assert.Null(result.Incident.DetailsJson);
        Assert.Null(result.Incident.OperatorName);
        Assert.Null(result.Incident.AcknowledgedOperator);
        Assert.Null(result.Incident.AcknowledgmentNote);
        Assert.Null(result.Incident.Resolution);
        Assert.All(result.Events, item => Assert.Null(item.DataJson));
        Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
        Assert.Equal(new[] { "diagnostics.db" }, Directory.GetFiles(fixture.Root).Select(Path.GetFileName));
    }

    [Fact]
    public async Task BoundedRecentAttemptsRetainTheExactLinkedAttemptAndLatestEventsRemainOrdered()
    {
        using var fixture = new DatabaseFixture();
        FlowIncident incident;
        FlowNodeAttempt linked = null!;
        using (var db = fixture.CreateDatabase())
        {
            var run = InsertRun(db, "bounded-run");
            for (var number = 1; number <= 75; number++)
            {
                var attempt = InsertAttempt(db, run.Id, number);
                if (number == 1)
                    linked = attempt;
            }
            incident = InsertIncident(db, run.Id, linked.Id);
            for (var number = 160; number >= 1; number--)
                InsertEvent(db, run.Id, number);
        }

        var result = Assert.IsType<FlowIncidentInspectionResult>(await fixture.Reader.ReadAsync(incident.Id, 5, CancellationToken.None));

        Assert.Equal(FlowIncidentInspectionReader.MaximumAttempts, result.Attempts.Count);
        Assert.Equal(linked.Id, result.Attempts[0].Id);
        Assert.Equal(new[] { 1 }.Concat(Enumerable.Range(27, 49)), result.Attempts.Select(item => item.AttemptNo));
        Assert.Equal(Enumerable.Range(156, 5).Select(number => (long)number), result.Events.Select(item => item.SequenceNo));
        Assert.True(result.AttemptsTruncated);
        Assert.True(result.EventsTruncated);
        Assert.False(result.LinkedAttemptMissing);
        Assert.False(result.TextWasTruncated);
    }

    [Fact]
    public async Task OnlyAnAbsentIncidentReturnsNullAndAnAbsentRunDoesNotFallBackToSharedNamesOrSerials()
    {
        using var fixture = new DatabaseFixture();
        FlowIncident incident;
        using (var db = fixture.CreateDatabase())
        {
            var unrelatedRun = InsertRun(db, "unrelated-run");
            var unrelatedAttempt = InsertAttempt(db, unrelatedRun.Id, 1);
            InsertEvent(db, unrelatedRun.Id, 1);
            incident = InsertIncident(db, unrelatedRun.Id + 1, unrelatedAttempt.Id);
        }

        Assert.Null(await fixture.Reader.ReadAsync(incident.Id + 1, 10, CancellationToken.None));
        var result = Assert.IsType<FlowIncidentInspectionResult>(await fixture.Reader.ReadAsync(incident.Id, 10, CancellationToken.None));

        Assert.Null(result.Run);
        Assert.Equal(incident.RunRecordId, result.Incident.RunRecordId);
        Assert.Empty(result.Attempts);
        Assert.Empty(result.Events);
        Assert.True(result.LinkedAttemptMissing);
    }

    [Fact]
    public async Task TextProjectionIsBoundedWithoutSplittingUnicodeOrReadingPayloadColumns()
    {
        using var fixture = new DatabaseFixture();
        FlowIncident incident;
        var identifierPrefix = new string('N', FlowIncidentInspectionReader.MaximumIdentifierCharacters - 1);
        var diagnosticPrefix = new string('D', FlowIncidentInspectionReader.MaximumDiagnosticCharacters - 1);
        using (var db = fixture.CreateDatabase())
        {
            var run = InsertRun(db, "text-run");
            db.Updateable<FlowRunRecord>().SetColumns(item => new FlowRunRecord { FlowName = identifierPrefix + "😀tail" })
                .Where(item => item.Id == run.Id).ExecuteCommand();
            var attempt = InsertAttempt(db, run.Id, 1, diagnosticPrefix + "😀tail");
            incident = InsertIncident(db, run.Id, attempt.Id, diagnosticPrefix + "😀tail");
            InsertEvent(db, run.Id, 1, diagnosticPrefix + "😀tail");
            // These omitted fields are not prerequisites and must not be selected, even on a valid partial export.
            db.Ado.ExecuteCommand("ALTER TABLE FlowRunRecord DROP COLUMN serial_number");
            db.Ado.ExecuteCommand("ALTER TABLE FlowIncident DROP COLUMN details_json");
            db.Ado.ExecuteCommand("ALTER TABLE FlowExecutionEvent DROP COLUMN data_json");
        }

        var result = Assert.IsType<FlowIncidentInspectionResult>(await fixture.Reader.ReadAsync(incident.Id, 1, CancellationToken.None));

        Assert.True(result.TextWasTruncated);
        Assert.Equal(identifierPrefix, result.Run!.FlowName);
        Assert.Equal(diagnosticPrefix, result.Incident.Summary);
        Assert.Equal(diagnosticPrefix, Assert.Single(result.Attempts).ErrorMessage);
        Assert.Equal(diagnosticPrefix, Assert.Single(result.Events).Message);
        Assert.False(result.EventsTruncated);
        Assert.False(result.AttemptsTruncated);
    }

    [Theory]
    [InlineData("missing_file")]
    [InlineData("missing_table")]
    [InlineData("missing_column")]
    public async Task UnavailableOrIncompleteDatabaseIsReportedWithoutCreatingOrMigratingIt(string scenario)
    {
        using var fixture = new DatabaseFixture();
        if (scenario != "missing_file")
        {
            using var db = fixture.CreateDatabase();
            db.Ado.ExecuteCommand(scenario == "missing_table"
                ? "DROP TABLE FlowExecutionEvent" : "ALTER TABLE FlowIncident DROP COLUMN summary");
        }
        var before = File.Exists(fixture.DatabasePath) ? File.ReadAllBytes(fixture.DatabasePath) : null;

        var failure = await Record.ExceptionAsync(() => fixture.Reader.ReadAsync(1, 10, CancellationToken.None));

        if (scenario == "missing_file")
        {
            Assert.IsType<FileNotFoundException>(failure);
            Assert.False(File.Exists(fixture.DatabasePath));
            Assert.Empty(Directory.GetFiles(fixture.Root));
        }
        else
        {
            Assert.IsType<InvalidDataException>(failure);
            Assert.Equal(before, File.ReadAllBytes(fixture.DatabasePath));
            Assert.Equal(new[] { "diagnostics.db" }, Directory.GetFiles(fixture.Root).Select(Path.GetFileName));
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task InvalidBoundsAreRejectedBeforeOpeningTheDatabase(long incidentId, int maxEvents)
    {
        using var fixture = new DatabaseFixture();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Reader.ReadAsync(incidentId, maxEvents, CancellationToken.None));
        Assert.Empty(Directory.GetFiles(fixture.Root));
    }

    [Fact]
    public async Task CancellationBeforeReadingDoesNotOpenOrCreateTheDatabase()
    {
        using var fixture = new DatabaseFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Reader.ReadAsync(1, 10, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Reader.QueryAsync("active", 10, null, cancellation.Token));

        Assert.Empty(Directory.GetFiles(fixture.Root));
    }

    private static FlowRunRecord InsertRun(SqlSugarClient db, string key)
    {
        var run = new FlowRunRecord
        {
            TemplateId = 42, FlowKey = "shared-flow", FlowName = "Shared flow name", RunKey = key,
            SerialNumber = "private-shared-SN", BatchId = 123, OwnerMachine = "private-machine", OwnerInstanceId = "private-instance",
            Status = FlowStatus.Failed, StartedTimeUtc = StartedAt, CompletedTimeUtc = StartedAt.AddMinutes(1),
            CompletedTime = StartedAt.AddMinutes(1), ElapsedMs = 60_000,
        };
        run.Id = db.Insertable(run).ExecuteReturnIdentity();
        return run;
    }

    private static FlowNodeAttempt InsertAttempt(SqlSugarClient db, int runId, int number, string errorMessage = "Node failed.")
    {
        var attempt = new FlowNodeAttempt
        {
            RunRecordId = runId, NodeId = "node-one", InvocationId = Guid.NewGuid().ToString("N"), AttemptNo = number,
            StartedTimeUtc = StartedAt.AddSeconds(number), CompletedTimeUtc = StartedAt.AddSeconds(number + 1),
            Outcome = "Failed", ErrorCode = "controlled_failure", ErrorMessage = errorMessage,
        };
        attempt.Id = db.Insertable(attempt).ExecuteReturnBigIdentity();
        return attempt;
    }

    private static FlowIncident InsertIncident(SqlSugarClient db, int runId, long? attemptId, string summary = "Node execution failed.",
        string state = "Open", DateTime? detectedTimeUtc = null)
    {
        var incident = new FlowIncident
        {
            RunRecordId = runId, AttemptId = attemptId, IncidentKey = Guid.NewGuid().ToString("N"), NodeId = "node-one",
            Kind = "NodeExecutionFailed", Severity = "Error", State = state, Summary = summary, DetectedTimeUtc = detectedTimeUtc ?? StartedAt,
            DetailsJson = "private-incident-payload", OperatorName = "private-operator", AcknowledgedOperator = "private-acknowledger",
            AcknowledgmentNote = "private-note", Resolution = "private-resolution",
        };
        incident.Id = db.Insertable(incident).ExecuteReturnBigIdentity();
        return incident;
    }

    private static void InsertEvent(SqlSugarClient db, int runId, int sequence, string message = "Node diagnostic event.") =>
        db.Insertable(new FlowExecutionEvent
        {
            RunRecordId = runId, SequenceNo = sequence, EventKey = Guid.NewGuid().ToString("N"), EventType = "NodeFailed",
            OccurredTimeUtc = StartedAt.AddSeconds(sequence), NodeId = "node-one", Message = message, DataJson = "private-event-payload",
        }).ExecuteCommand();

    private sealed class DatabaseFixture : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("CopilotFlowIncidentReadSource-").FullName;
        public string DatabasePath => Path.Combine(Root, "diagnostics.db");
        public FlowIncidentInspectionReader Reader => new(DatabasePath);

        public SqlSugarClient CreateDatabase(bool incidentOnly = false)
        {
            var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString(),
                DbType = SqlSugar.DbType.Sqlite, IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute,
            });
            db.CodeFirst.InitTables<FlowIncident>();
            if (!incidentOnly)
            {
                db.CodeFirst.InitTables<FlowRunRecord>();
                db.CodeFirst.InitTables<FlowNodeAttempt>();
                db.CodeFirst.InitTables<FlowExecutionEvent>();
            }
            return db;
        }

        public void Dispose()
        {
            var fullRoot = Path.GetFullPath(Root);
            var tempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            Assert.Equal(tempRoot, Path.GetDirectoryName(fullRoot), ignoreCase: true);
            Assert.StartsWith("CopilotFlowIncidentReadSource-", Path.GetFileName(fullRoot), StringComparison.Ordinal);
            Directory.Delete(fullRoot, recursive: true);
        }
    }
}
