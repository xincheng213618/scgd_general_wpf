using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowGraphValidationServiceTests
{
    private const string Revision = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ACompletedHostCheckIsSuccessfulEvenWhenTheWholeGraphIsInvalid(bool valid)
    {
        var invalidNode = valid ? (Guid?)null : Guid.NewGuid();
        var service = Service(Snapshot(valid) with { InvalidNodeId = invalidNode, NodeCount = 201, IsExecutionActive = true, IsEditorReadOnly = true });
        var result = await service.ValidateAsync(Revision.ToUpperInvariant(), CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        using var json = JsonDocument.Parse(result.Content);
        var root = json.RootElement;
        Assert.Equal("current_flow_editor", root.GetProperty("source").GetString());
        Assert.Equal("host_save_structure", root.GetProperty("validation_kind").GetString());
        Assert.Equal(valid, root.GetProperty("valid").GetBoolean());
        Assert.Equal(201, root.GetProperty("node_count").GetInt32());
        Assert.True(root.GetProperty("is_execution_active").GetBoolean());
        Assert.True(root.GetProperty("is_editor_read_only").GetBoolean());
        Assert.True(root.GetProperty("coverage").GetProperty("full_current_graph").GetBoolean());
        if (invalidNode.HasValue) Assert.Equal(invalidNode.Value, root.GetProperty("invalid_node_id").GetGuid());
        else Assert.Equal(JsonValueKind.Null, root.GetProperty("invalid_node_id").ValueKind);
        Assert.Contains("does not prove every branch", root.GetProperty("interpretation").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LongUntrustedHostDiagnosticsRemainBoundedRedactedAndUnicodeComplete()
    {
        const string secret = "sk-1234567890abcdef1234567890abcdef";
        var diagnostic = new string('中', 2047) + "😀" + secret + new string('中', 4096);
        var result = await Service(Snapshot(false) with { Diagnostic = diagnostic }).ValidateAsync(Revision, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.InRange(result.Content.Length, 1, CopilotFlowGraphValidationService.MaximumContentCharacters);
        Assert.DoesNotContain(secret, result.Content, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(result.Content);
        var root = json.RootElement;
        Assert.Equal(new string('中', 2047), root.GetProperty("diagnostic").GetString());
        Assert.True(root.GetProperty("coverage").GetProperty("diagnostic_truncated").GetBoolean());
        Assert.True(root.GetProperty("coverage").GetProperty("diagnostic_redacted").GetBoolean());
        Assert.NotEmpty(result.PartialResultMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("revision")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public async Task InvalidRevisionCannotEnterTheHost(string revision)
    {
        var calls = 0;
        var service = new CopilotFlowGraphValidationService((_, _) => { calls++; throw new InvalidOperationException(); });
        var result = await service.ValidateAsync(revision, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StaleGraphEvidenceCannotPublishAValidationVerdict(bool explicitMismatch)
    {
        var source = Snapshot(false) with
        {
            RevisionMatches = !explicitMismatch, Revision = explicitMismatch ? Revision : new string('b', 64),
            IsValid = explicitMismatch ? null : false, Diagnostic = "PRIVATE_OLD_DIAGNOSTIC",
        };
        var result = await Service(source).ValidateAsync(Revision, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        Assert.Empty(result.Content);
        Assert.DoesNotContain("PRIVATE", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingEditorIsNotAnInvalidGraphVerdict()
    {
        var service = new CopilotFlowGraphValidationService((_, _) => Task.FromResult<CopilotFlowValidationSnapshot?>(null));
        var result = await service.ValidateAsync(Revision, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.NotFound, result.FailureKind);
        Assert.Empty(result.Content);
    }

    [Theory]
    [InlineData("verdict")]
    [InlineData("counts")]
    [InlineData("revision")]
    [InlineData("success-node")]
    public async Task InconsistentHostResultsCannotBecomeSuccessfulValidation(string mutation)
    {
        var source = mutation switch
        {
            "verdict" => Snapshot() with { IsValid = null }, "counts" => Snapshot() with { NodeCount = -1 },
            "revision" => Snapshot() with { Revision = "unknown" }, _ => Snapshot() with { InvalidNodeId = Guid.NewGuid() },
        };
        var result = await Service(source).ValidateAsync(Revision, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Internal, result.FailureKind);
        Assert.Empty(result.Content);
    }

    [Theory]
    [InlineData("scope", CopilotToolFailureKind.Validation)]
    [InlineData("timeout", CopilotToolFailureKind.Transient)]
    [InlineData("exception", CopilotToolFailureKind.Internal)]
    public async Task HostFailuresAreDifferentFromStructuralInvalidity(string failure, CopilotToolFailureKind kind)
    {
        Exception exception = failure switch
        {
            "scope" => new CopilotFlowValidationScopeChangedException(), "timeout" => new TimeoutException(),
            _ => new InvalidOperationException("PRIVATE_HOST_DETAILS"),
        };
        var service = new CopilotFlowGraphValidationService((_, _) => Task.FromException<CopilotFlowValidationSnapshot?>(exception));
        var result = await service.ValidateAsync(Revision, CancellationToken.None);
        Assert.Equal(kind, result.FailureKind);
        Assert.Empty(result.Content);
        Assert.DoesNotContain("PRIVATE", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallerCancellationRejectsALateHostVerdictEvenWhenTheHostIgnoresCancellation()
    {
        var released = new TaskCompletionSource<CopilotFlowValidationSnapshot?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var service = new CopilotFlowGraphValidationService((_, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            return released.Task;
        });
        var pending = service.ValidateAsync(Revision, cancellation.Token);
        cancellation.Cancel();
        released.SetResult(Snapshot());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    private static CopilotFlowValidationSnapshot Snapshot(bool valid = true) => new(
        Revision, true, valid, valid ? string.Empty : "No execution path connects a start and an endpoint.", null, 2, 1, false, false);
    private static CopilotFlowGraphValidationService Service(CopilotFlowValidationSnapshot snapshot) =>
        new((_, _) => Task.FromResult<CopilotFlowValidationSnapshot?>(snapshot));
}
