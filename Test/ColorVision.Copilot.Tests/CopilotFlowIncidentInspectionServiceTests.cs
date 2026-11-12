using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Services.FlowDiagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowIncidentInspectionServiceTests
{
    [Theory]
    [InlineData("recorded")]
    [InlineData("unknown")]
    [InlineData("invalid_hash")]
    public async Task BoundedEvidenceKeepsTheLinkedAttemptAndLatestEventWithoutPrivatePayloads(string identity)
    {
        const long incidentId = 5_000_000_001;
        const string secret = "sk-1234567890abcdef1234567890abcdef";
        var incident = new FlowIncident
        {
            Id = incidentId, RunRecordId = 17, AttemptId = 1, NodeId = "camera", State = "Open", Kind = "NodeFailure",
            Severity = "Error", Summary = "Inspect the linked failed attempt.", DetectedTimeUtc = new(2026, 10, 27, 1, 0, 0),
            DetailsJson = "PRIVATE_INCIDENT_PAYLOAD", OperatorName = "PRIVATE_OPERATOR", AcknowledgmentNote = "PRIVATE_NOTE",
        };
        var run = new FlowRunRecord
        {
            Id = 17, FlowName = "Camera flow", Status = FlowStatus.Completed, SerialNumber = "PRIVATE_SERIAL",
            OwnerMachine = "PRIVATE_MACHINE", StartedTimeUtc = incident.DetectedTimeUtc,
            TemplateRevision = identity == "unknown" ? null : 7,
            SnapshotId = identity == "unknown" ? null : 5_000_000_002,
            ContentHash = identity == "recorded" ? new string('A', 64)
                : identity == "invalid_hash" ? "PRIVATE_INVALID_HASH" : null,
        };
        var attempts = Enumerable.Range(1, 50).Select(number => new FlowNodeAttempt
        {
            Id = number, RunRecordId = 17, NodeId = "camera", AttemptNo = number,
            StartedTimeUtc = incident.DetectedTimeUtc.AddSeconds(number), Outcome = "Failed", ErrorCode = "CAMERA_TIMEOUT",
            ErrorMessage = secret + " " + new string('中', 600),
        }).ToArray();
        var events = Enumerable.Range(1, 100).Select(number => new FlowExecutionEvent
        {
            Id = number, RunRecordId = 17, SequenceNo = number, AttemptId = 1, EventType = "AttemptFailed",
            OccurredTimeUtc = incident.DetectedTimeUtc.AddSeconds(number), Message = new string('文', 600),
            DataJson = "PRIVATE_EVENT_PAYLOAD",
        }).ToArray();
        var source = new FlowIncidentInspectionResult(incident, run, attempts, events, false, false, false, false);
        var service = new CopilotFlowIncidentInspectionService((id, maximum, token) =>
        {
            Assert.Equal(incidentId, id);
            Assert.Equal(100, maximum);
            return Task.FromResult<FlowIncidentInspectionResult?>(source);
        });

        var result = await service.InspectAsync(incidentId, 100, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.InRange(result.Content.Length, 1, CopilotFlowIncidentInspectionService.MaximumContentCharacters);
        Assert.NotEmpty(result.PartialResultMessage);
        Assert.Equal(result.Content, string.Concat(result.Content.EnumerateRunes().Select(rune => rune.ToString())));
        foreach (var value in new[] { secret, "PRIVATE_INCIDENT_PAYLOAD", "PRIVATE_OPERATOR", "PRIVATE_NOTE", "PRIVATE_SERIAL", "PRIVATE_MACHINE", "PRIVATE_EVENT_PAYLOAD", "PRIVATE_INVALID_HASH" })
            Assert.DoesNotContain(value, result.Content, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(result.Content);
        var root = document.RootElement;
        Assert.Equal(incidentId, root.GetProperty("incident").GetProperty("incident_id").GetInt64());
        var runEvidence = root.GetProperty("run");
        if (identity == "unknown")
        {
            Assert.Equal(JsonValueKind.Null, runEvidence.GetProperty("template_revision").ValueKind);
            Assert.Equal(JsonValueKind.Null, runEvidence.GetProperty("snapshot_id").ValueKind);
        }
        else
        {
            Assert.Equal(7, runEvidence.GetProperty("template_revision").GetInt32());
            Assert.Equal(5_000_000_002, runEvidence.GetProperty("snapshot_id").GetInt64());
        }
        Assert.Equal(identity == "recorded" ? new string('a', 64) : null, runEvidence.GetProperty("content_hash").GetString());
        Assert.EndsWith("Z", root.GetProperty("incident").GetProperty("detected_time_utc").GetString(), StringComparison.Ordinal);
        Assert.Contains(root.GetProperty("attempts").EnumerateArray(), item => item.GetProperty("id").GetInt64() == 1 && item.GetProperty("linked").GetBoolean());
        Assert.Equal(100, root.GetProperty("events").EnumerateArray().Last().GetProperty("sequence_no").GetInt64());
        var coverage = root.GetProperty("coverage");
        var templateIdentity = coverage.GetProperty("run_template_identity");
        Assert.Equal("retained_run_metadata", templateIdentity.GetProperty("source").GetString());
        Assert.False(templateIdentity.GetProperty("snapshot_content_verified").GetBoolean());
        Assert.Equal(identity switch { "recorded" => "valid_format", "unknown" => "not_recorded", _ => "invalid" },
            templateIdentity.GetProperty("content_hash_status").GetString());
        Assert.False(coverage.GetProperty("returned_window_complete").GetBoolean());
        Assert.True(coverage.GetProperty("text_truncated").GetBoolean());
        Assert.True(coverage.GetProperty("attempts_truncated").GetBoolean() || coverage.GetProperty("events_truncated").GetBoolean());
    }

    [Fact]
    public async Task MissingLinkedEvidenceIsReportedWithoutInventingRunState()
    {
        var source = new FlowIncidentInspectionResult(new FlowIncident { Id = 9, RunRecordId = 17, AttemptId = 99 },
            null, [], [], false, false, true, false);
        var service = new CopilotFlowIncidentInspectionService((_, _, _) => Task.FromResult<FlowIncidentInspectionResult?>(source));

        var result = await service.InspectAsync(9, 40, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotEmpty(result.PartialResultMessage);
        using var document = JsonDocument.Parse(result.Content);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("run").ValueKind);
        var coverage = document.RootElement.GetProperty("coverage");
        Assert.True(coverage.GetProperty("run_missing").GetBoolean());
        Assert.True(coverage.GetProperty("linked_attempt_missing").GetBoolean());
        Assert.False(coverage.GetProperty("returned_window_complete").GetBoolean());
    }

    [Theory]
    [InlineData("missing", CopilotToolFailureKind.NotFound)]
    [InlineData("schema", CopilotToolFailureKind.Validation)]
    [InlineData("access", CopilotToolFailureKind.Authorization)]
    public async Task UnavailableSourcesReturnClassifiedFailuresWithoutPrivateExceptionDetails(string failure, CopilotToolFailureKind expected)
    {
        var service = new CopilotFlowIncidentInspectionService((_, _, _) => Task.FromException<FlowIncidentInspectionResult?>(failure switch
        {
            "missing" => new FileNotFoundException("PRIVATE_DATABASE_PATH"),
            "schema" => new InvalidDataException("PRIVATE_DATABASE_PATH"),
            _ => new UnauthorizedAccessException("PRIVATE_DATABASE_PATH"),
        }));

        var result = await service.InspectAsync(9, 40, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(expected, result.FailureKind);
        Assert.Empty(result.Content);
        Assert.DoesNotContain("PRIVATE_DATABASE_PATH", result.ErrorMessage, StringComparison.Ordinal);
    }
}
