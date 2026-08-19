using ColorVision.Copilot;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotToolOutputArchiveTests
{
    private const int SmallTokenLimit = 256;

    [Fact]
    public async Task TruncatedToolOutputCanBeReadOnlyFromItsOwningConversation()
    {
        using var registry = new CopilotToolOutputArchiveRegistry();
        var request = new CopilotAgentRequest
        {
            ConversationId = "archive-conversation",
            TaskId = "archive-task",
        };
        const string secret = "private-tool-output-secret";
        var originalContent = $"api_key={secret};\n"
            + new string('x', 30_000)
            + "\nfull-result-tail";
        var outcome = CreateOutcome(
            request,
            new CopilotReadLocalFileTool(),
            originalContent);

        var formatted = CopilotToolOutputArchivePolicy.Format(
            outcome,
            SmallTokenLimit,
            registry);
        using var document = JsonDocument.Parse(formatted);
        var root = document.RootElement;
        var archive = root.GetProperty("content_archive");
        var archiveId = archive.GetProperty("archive_id").GetString();

        Assert.NotNull(archiveId);
        Assert.StartsWith("tool:", archiveId, StringComparison.Ordinal);
        Assert.Equal("ReadToolOutput", archive.GetProperty("retrieval_tool").GetString());
        Assert.True(archive.GetProperty("content_redacted").GetBoolean());
        Assert.True(root.GetProperty("content_truncated").GetBoolean());
        Assert.True(CopilotTokenEstimator.EstimateTextWeight(formatted)
            <= SmallTokenLimit * CopilotTokenEstimator.AsciiCharactersPerToken);
        Assert.Equal(originalContent, outcome.Result.Content);
        Assert.Equal(formatted, outcome.FormattedModelResult);
        Assert.Single(registry.GetSnapshots(request.ConversationId));

        var readTool = new CopilotReadToolOutputTool(registry);
        var page = await readTool.ExecuteAsync(
            request,
            CreateReadInput(archiveId!),
            CancellationToken.None);

        Assert.True(page.Success);
        Assert.Contains("api_key=<redacted>;", page.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, page.Content, StringComparison.Ordinal);
        Assert.Contains("content_redacted: true", page.Content, StringComparison.Ordinal);

        var otherConversation = await readTool.ExecuteAsync(
            new CopilotAgentRequest { ConversationId = "other-conversation" },
            CreateReadInput(archiveId!),
            CancellationToken.None);

        Assert.False(otherConversation.Success);
        Assert.Equal(CopilotToolFailureKind.NotFound, otherConversation.FailureKind);
        Assert.Equal(1, registry.ClearConversation(request.ConversationId));
        Assert.Empty(registry.GetSnapshots(request.ConversationId));
    }

    [Fact]
    public async Task TruncatedFailureErrorCanBeReadFromItsRedactedArchive()
    {
        using var registry = new CopilotToolOutputArchiveRegistry();
        var request = new CopilotAgentRequest
        {
            ConversationId = "failure-archive-conversation",
            TaskId = "failure-archive-task",
        };
        const string secret = "private-failure-secret";
        var error = $"token={secret};\n"
            + new string('x', 30_000)
            + "\nfull-error-tail";
        var tool = new LargeOutputTool(string.Empty);
        var outcome = CreateFailedOutcome(request, tool, error);

        var formatted = CopilotToolOutputArchivePolicy.Format(
            outcome,
            SmallTokenLimit,
            registry);
        using var document = JsonDocument.Parse(formatted);
        var root = document.RootElement;
        var archiveId = root
            .GetProperty("content_archive")
            .GetProperty("archive_id")
            .GetString();

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.NotNull(archiveId);
        Assert.Equal(error, outcome.Result.ErrorMessage);
        Assert.Single(registry.GetSnapshots(request.ConversationId));

        var page = await new CopilotReadToolOutputTool(registry).ExecuteAsync(
            request,
            CreateReadInput(archiveId!),
            CancellationToken.None);

        Assert.True(page.Success);
        Assert.Contains("[Tool Error]", page.Content, StringComparison.Ordinal);
        Assert.Contains("token=<redacted>;", page.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, page.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void ArchiveIsNotCreatedWhenItsReferenceCannotFitTheProviderBudget()
    {
        using var registry = new CopilotToolOutputArchiveRegistry();
        var request = new CopilotAgentRequest
        {
            ConversationId = "zero-budget-conversation",
            TaskId = "zero-budget-task",
        };
        var outcome = CreateOutcome(
            request,
            new CopilotReadLocalFileTool(),
            new string('x', 30_000));

        var formatted = CopilotToolOutputArchivePolicy.Format(
            outcome,
            toolOutputTokenLimit: 0,
            registry);

        Assert.Empty(formatted);
        Assert.Null(outcome.ToolOutputArchive);
        Assert.Empty(registry.GetSnapshots(request.ConversationId));
    }

    [Fact]
    public void ReadingAnArchiveNeverCreatesARecursiveArchive()
    {
        using var registry = new CopilotToolOutputArchiveRegistry();
        var request = new CopilotAgentRequest
        {
            ConversationId = "archive-reader-conversation",
            TaskId = "archive-reader-task",
        };
        var outcome = CreateOutcome(
            request,
            new CopilotReadToolOutputTool(registry),
            new string('x', 30_000));

        var formatted = CopilotToolOutputArchivePolicy.Format(
            outcome,
            SmallTokenLimit,
            registry);
        using var document = JsonDocument.Parse(formatted);

        Assert.True(document.RootElement.GetProperty("content_truncated").GetBoolean());
        Assert.False(document.RootElement.TryGetProperty("content_archive", out _));
        Assert.Empty(registry.GetSnapshots(request.ConversationId));
    }

    [Fact]
    public void DedicatedShellOutputArchivesAreNotDuplicated()
    {
        using var registry = new CopilotToolOutputArchiveRegistry();
        var request = new CopilotAgentRequest
        {
            ConversationId = "shell-archive-conversation",
            TaskId = "shell-archive-task",
        };
        var outcome = CreateOutcome(
            request,
            new CopilotShellCommandTool(),
            new string('x', 30_000));

        var formatted = CopilotToolOutputArchivePolicy.Format(
            outcome,
            SmallTokenLimit,
            registry);
        using var document = JsonDocument.Parse(formatted);

        Assert.True(document.RootElement.GetProperty("content_truncated").GetBoolean());
        Assert.False(document.RootElement.TryGetProperty("content_archive", out _));
        Assert.Empty(registry.GetSnapshots(request.ConversationId));
    }

    [Fact]
    public async Task ExecutorPublishesTheExactArchivedModelResult()
    {
        var conversationId = "runtime-archive-" + Guid.NewGuid().ToString("N");
        var request = new CopilotAgentRequest
        {
            ConversationId = conversationId,
            TaskId = "runtime-archive-task",
            ToolOutputTokenLimitOverride = int.MaxValue,
        };
        var tool = new LargeOutputTool(new string('x', 400_000));
        var events = new List<CopilotAgentEvent>();
        try
        {
            var outcome = await new CopilotToolExecutor([]).ExecuteAsync(
                new CopilotToolInvocation
                {
                    CallId = "call:runtime-archive",
                    Round = 1,
                    Attempt = 1,
                    MaxAttempts = 1,
                    RuntimeName = "test",
                    Tool = tool,
                    AgentRequest = request,
                    ToolInput = CopilotAgentToolInput.Empty,
                },
                events.Add,
                CancellationToken.None);

            var toolResultEvent = Assert.Single(
                events,
                item => item.Type == CopilotAgentEventType.ToolResult);
            Assert.NotNull(outcome.ToolOutputArchive);
            Assert.NotNull(outcome.FormattedModelResult);
            Assert.Equal(outcome.FormattedModelResult, toolResultEvent.ModelToolResult);
            Assert.True(
                toolResultEvent.ModelToolResult.Length
                    <= CopilotCodeReviewSnapshot.MaximumModelObservationCharacters);
            CopilotAgentEventProtocol.Validate(toolResultEvent);
            using var document = JsonDocument.Parse(toolResultEvent.ModelToolResult);
            Assert.Equal(
                outcome.ToolOutputArchive!.Id,
                document.RootElement
                    .GetProperty("content_archive")
                    .GetProperty("archive_id")
                    .GetString());
        }
        finally
        {
            CopilotToolOutputArchiveRegistry.Shared.ClearConversation(conversationId);
        }
    }

    [Fact]
    public void ArchiveRetentionEvictsAndDisposesTheOldestEntry()
    {
        using var registry = new CopilotToolOutputArchiveRegistry();
        CopilotToolOutputArchiveSnapshot? first = null;
        CopilotToolOutputArchiveSnapshot? latest = null;
        for (var index = 0;
            index <= CopilotToolOutputArchiveRegistry.MaximumRetainedArchives;
            index++)
        {
            latest = registry.Retain(
                "retention-conversation",
                "ReadLocalFile",
                $"call:{index}",
                $"content-{index}");
            first ??= latest;
        }

        Assert.NotNull(first);
        Assert.NotNull(latest);
        Assert.Equal(
            CopilotToolOutputArchiveRegistry.MaximumRetainedArchives,
            registry.GetSnapshots("retention-conversation").Count);
        Assert.False(registry.Read(
            "retention-conversation",
            first!.Id,
            0,
            100,
            CancellationToken.None).Success);
        Assert.True(registry.Read(
            "retention-conversation",
            latest!.Id,
            0,
            100,
            CancellationToken.None).Success);
    }

    [Fact]
    public void ArchiveCapacityAndPagingDoNotSplitUnicodeSurrogatePairs()
    {
        using var archive = CopilotTemporaryRedactedOutputArchive.TryCreate(
            "ToolOutput",
            "content",
            maximumCharacters: 3);
        Assert.NotNull(archive);

        archive!.Append("😀😀");
        archive.Complete();

        Assert.Equal(2, archive.ArchivedCharacters);
        Assert.True(archive.IsTruncated);
        var page = archive.Read(
            offsetCharacters: 0,
            maximumCharacters: 1,
            CancellationToken.None);
        Assert.Equal("😀", page.Content);
        Assert.Equal(2, page.ReturnedCharacters);
        Assert.Equal(2, page.NextOffsetCharacters);

        var interiorOffset = archive.Read(
            offsetCharacters: 1,
            maximumCharacters: 1,
            CancellationToken.None);
        Assert.Equal(2, interiorOffset.OffsetCharacters);
        Assert.Empty(interiorOffset.Content);
        Assert.True(interiorOffset.EndOfAvailableOutput);
    }

    private static CopilotToolExecutionOutcome CreateOutcome(
        CopilotAgentRequest request,
        ICopilotTool tool,
        string content)
    {
        const string CallId = "call:tool-output-archive";
        return new CopilotToolExecutionOutcome
        {
            Invocation = new CopilotToolInvocation
            {
                CallId = CallId,
                Round = 1,
                Attempt = 1,
                MaxAttempts = 1,
                RuntimeName = "test",
                Tool = tool,
                AgentRequest = request,
                ToolInput = CopilotAgentToolInput.Empty,
                ToolCall = new CopilotToolCall
                {
                    ToolName = tool.Name,
                    ToolInput = CopilotAgentToolInput.Empty,
                },
            },
            Result = new CopilotToolResult
            {
                ToolName = tool.Name,
                Success = true,
                Summary = "Produced a large text result.",
                Content = content,
            },
            Execution = new CopilotToolExecutionInfo
            {
                CallId = CallId,
                ToolName = tool.Name,
                Attempt = 1,
                MaxAttempts = 1,
                State = CopilotToolExecutionState.Completed,
            },
        };
    }

    private static CopilotToolExecutionOutcome CreateFailedOutcome(
        CopilotAgentRequest request,
        ICopilotTool tool,
        string error)
    {
        const string CallId = "call:failed-tool-output-archive";
        return new CopilotToolExecutionOutcome
        {
            Invocation = new CopilotToolInvocation
            {
                CallId = CallId,
                Round = 1,
                Attempt = 1,
                MaxAttempts = 1,
                RuntimeName = "test",
                Tool = tool,
                AgentRequest = request,
                ToolInput = CopilotAgentToolInput.Empty,
            },
            Result = new CopilotToolResult
            {
                ToolName = tool.Name,
                Success = false,
                Summary = "The tool failed with a large diagnostic.",
                ErrorMessage = error,
                FailureKind = CopilotToolFailureKind.Internal,
                FailureCode = "large_diagnostic",
            },
            Execution = new CopilotToolExecutionInfo
            {
                CallId = CallId,
                ToolName = tool.Name,
                Attempt = 1,
                MaxAttempts = 1,
                State = CopilotToolExecutionState.Failed,
                FailureKind = CopilotToolFailureKind.Internal,
            },
        };
    }

    private static CopilotAgentToolInput CreateReadInput(string archiveId) =>
        new()
        {
            Arguments = new Dictionary<string, object?>
            {
                ["archiveId"] = archiveId,
                ["maximumCharacters"] = CopilotOutputArchiveLimits.MaximumReadCharacters,
            },
        };

    private sealed class LargeOutputTool(string content) : ICopilotTool
    {
        public string Name => "LargeOutput";

        public string Description => "Returns a large result for runtime archive tests.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ReadOnly();

        public CopilotToolInputSchema InputSchema => CopilotToolInputSchema.Empty;

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) => Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Produced a large text result.",
                Content = content,
            });
    }
}
