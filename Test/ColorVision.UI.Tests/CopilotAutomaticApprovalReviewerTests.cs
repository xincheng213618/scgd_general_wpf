using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

[Collection(CopilotApprovalReviewTestGroup.CollectionName)]
public sealed class CopilotAutomaticApprovalReviewerTests
{
    [Theory]
    [InlineData(
        "VERDICT: APPROVE\nRISK: LOW\nREASON: Local test command.",
        "Approve",
        "Low")]
    [InlineData(
        "VERDICT: ASK_USER\nRISK: CRITICAL\nREASON: External destructive effect.",
        "RequireUser",
        "Critical")]
    public void ParserRequiresTheExactThreeLineProtocol(
        string content,
        string expectedVerdict,
        string expectedRisk)
    {
        Assert.True(CopilotAutomaticApprovalReviewer.TryParse(
            content,
            new CopilotTokenUsage(8, 3, 11),
            out var result));
        Assert.Equal(expectedVerdict, result.Verdict.ToString());
        Assert.Equal(expectedRisk, result.RiskLevel.ToString());
        Assert.Equal(11, result.Usage.EffectiveTotalTokens);

        Assert.False(CopilotAutomaticApprovalReviewer.TryParse(
            content + "\nACTION: run",
            CopilotTokenUsage.Empty,
            out _));
        Assert.False(CopilotAutomaticApprovalReviewer.TryParse(
            "VERDICT: APPROVE\nRISK: UNKNOWN\nREASON: unclear",
            CopilotTokenUsage.Empty,
            out _));
    }

    [Fact]
    public async Task ReviewerUsesOneToolFreeBoundedCallAndTracksUsage()
    {
        using var chatClient = new CapturingChatClient(
            "VERDICT: APPROVE\nRISK: MEDIUM\nREASON: The command only runs local tests.");
        var reviewer = new CopilotAutomaticApprovalReviewer();
        var request = CreateRequest();
        var action = CreateAction("dotnet test .\\Test\\ColorVision.UI.Tests");

        var result = await reviewer.ReviewAsync(
            chatClient,
            request,
            new ReviewableTool(),
            action,
            CancellationToken.None);

        Assert.Equal(CopilotAutomaticApprovalReviewVerdict.Approve, result.Verdict);
        Assert.Equal(CopilotAutomaticApprovalRiskLevel.Medium, result.RiskLevel);
        Assert.Equal(17, result.Usage.EffectiveTotalTokens);
        Assert.Equal(1, chatClient.CallCount);
        Assert.NotNull(chatClient.Options);
        Assert.Equal(CopilotAutomaticApprovalReviewer.MaximumOutputTokens, chatClient.Options!.MaxOutputTokens);
        Assert.Empty(chatClient.Options.Tools!);
        Assert.Contains("independent permission reviewer", chatClient.Options.Instructions, StringComparison.Ordinal);
        var prompt = Assert.Single(chatClient.Messages);
        Assert.Equal(ChatRole.User, prompt.Role);
        var promptText = string.Concat(prompt.Contents
            .OfType<TextContent>()
            .Select(content => content.Text));
        Assert.Contains("dotnet test", promptText, StringComparison.Ordinal);
        Assert.DoesNotContain("tool-result-secret", promptText, StringComparison.Ordinal);
        Assert.DoesNotContain("project-instruction-secret", promptText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HighRiskApprovalVerdictStillRequiresTheUser()
    {
        using var chatClient = new CapturingChatClient(
            "VERDICT: APPROVE\nRISK: HIGH\nREASON: The command publishes a package.");
        var reviewer = new CopilotAutomaticApprovalReviewer();

        var result = await reviewer.ReviewAsync(
            chatClient,
            CreateRequest(),
            new ReviewableTool(),
            CreateAction("dotnet nuget push package.nupkg"),
            CancellationToken.None);

        Assert.Equal(CopilotAutomaticApprovalReviewVerdict.RequireUser, result.Verdict);
        Assert.Contains("风险评为 High", result.Reason, StringComparison.Ordinal);
        Assert.Equal(1, chatClient.CallCount);
    }

    [Fact]
    public async Task OversizedNativeReviewDetailsFailClosedWithoutCallingTheModel()
    {
        using var chatClient = new CapturingChatClient(
            "VERDICT: APPROVE\nRISK: LOW\nREASON: safe");
        var reviewer = new CopilotAutomaticApprovalReviewer();

        var result = await reviewer.ReviewAsync(
            chatClient,
            CreateRequest(),
            new ReviewableTool(),
            CreateAction(new string('x', CopilotAutomaticApprovalReviewer.MaximumActionEvidenceCharacters + 1)),
            CancellationToken.None);

        Assert.Equal(CopilotAutomaticApprovalReviewVerdict.RequireUser, result.Verdict);
        Assert.Contains("超过自动复核安全上限", result.Reason, StringComparison.Ordinal);
        Assert.Equal(0, chatClient.CallCount);
    }

    [Fact]
    public async Task MissingCompleteNativeReviewDetailsFailClosedWithoutCallingTheModel()
    {
        using var chatClient = new CapturingChatClient(
            "VERDICT: APPROVE\nRISK: LOW\nREASON: safe");
        var reviewer = new CopilotAutomaticApprovalReviewer();
        var action = CreateAction(string.Empty);

        var result = await reviewer.ReviewAsync(
            chatClient,
            CreateRequest(),
            new ReviewableTool(),
            action,
            CancellationToken.None);

        Assert.Equal(CopilotAutomaticApprovalReviewVerdict.RequireUser, result.Verdict);
        Assert.Contains("没有提供完整执行详情", result.Reason, StringComparison.Ordinal);
        Assert.Equal(0, chatClient.CallCount);
    }

    [Fact]
    public async Task AutomaticApprovalPreservesExactScopeAndAuditProvenance()
    {
        var coordinator = new CopilotFrameworkApprovalCoordinator();
        var request = CreateRequest();
        var handle = coordinator.RequestApproval(
            new ReviewableTool(),
            request,
            new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["command"] = "dotnet test",
                },
            },
            "call-auto-review",
            CancellationToken.None);
        CopilotMcpAuditLogger.ClearForTests();
        try
        {
            Assert.True(coordinator.ApproveAfterAutomaticReview(
                handle,
                request,
                new ReviewableTool(),
                request.WorkspacePath,
                "The command only runs local tests.",
                out var message));

            var decision = await handle.Decision;

            Assert.True(decision.IsApproved);
            Assert.Equal(CopilotFrameworkApprovalDecisionSource.AutomaticReview, decision.Source);
            Assert.Contains("automatic", decision.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("automatic permission reviewer", message, StringComparison.OrdinalIgnoreCase);
            var audit = Assert.Single(
                CopilotMcpAuditLogger.GetRecentEntries(20),
                entry => string.Equals(entry.ToolName, "action_approved", StringComparison.Ordinal));
            Assert.Equal("automatic-review", audit.ApprovalDecisionSource);
            Assert.Equal("The command only runs local tests.", audit.ApprovalDecisionReason);
            Assert.Equal(request.ConversationId, audit.ConversationId);
            Assert.Equal(request.TaskId, audit.TaskId);
            Assert.Equal(request.WorkspacePath, audit.WorkspacePath);
        }
        finally
        {
            CopilotMcpConfirmationStore.Instance.Cancel(
                handle.Action.ActionId,
                out _,
                "Automatic approval test cleanup.");
        }
    }

    [Fact]
    public async Task AutomaticApprovalCoordinatorRejectsAReviewWithoutAnActiveTaskGrant()
    {
        var coordinator = new CopilotFrameworkApprovalCoordinator();
        var request = CreateRequest(withTemporaryGrant: false);
        var tool = new ReviewableTool();
        var handle = coordinator.RequestApproval(
            tool,
            request,
            new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["command"] = "dotnet test",
                },
            },
            "call-auto-review-without-grant",
            CancellationToken.None);
        try
        {
            Assert.False(coordinator.ApproveAfterAutomaticReview(
                handle,
                request,
                tool,
                request.WorkspacePath,
                "The command only runs local tests.",
                out var message));
            Assert.Contains("temporary task grant", message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(ConfirmableActionStatus.Pending, handle.Action.Status);
        }
        finally
        {
            coordinator.Cancel(handle);
            await handle.Decision;
        }
    }

    [Fact]
    public void AutomaticApprovalSourceIsPersistedInTheTaskEventJournal()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();

        journal.RecordApprovalDecision(
            "RunShellCommand",
            "call-auto-review",
            "approval-auto-review",
            approved: true,
            CopilotFrameworkApprovalDecisionSource.AutomaticReview.ToString());

        var snapshot = journal.Snapshot();
        var approval = Assert.Single(
            snapshot.Events,
            item => item.Type == CopilotAgentTaskEventType.ApprovalApproved);
        Assert.True(snapshot.IsStructurallyValid());
        Assert.Equal("approved:AutomaticReview", approval.State);
        Assert.Contains("automatic permission review", approval.Summary, StringComparison.Ordinal);
    }

    private static CopilotAgentRequest CreateRequest(bool withTemporaryGrant = true)
    {
        var accessContext = new CopilotAgentAccessContext();
        if (withTemporaryGrant)
        {
            accessContext.PrepareFullAccess(
                "conversation-auto-review",
                @"C:\work\ColorVision",
                "task-auto-review",
                DateTimeOffset.UtcNow.AddMinutes(15));
        }
        return new CopilotAgentRequest
        {
            ConversationId = "conversation-auto-review",
            TaskId = "task-auto-review",
            WorkspacePath = @"C:\work\ColorVision",
            UserText = "Run the local tests.",
            TaskIntentText = "Run the local tests and report the result.",
            History =
            [
                new CopilotRequestMessage("user", "Run the local tests."),
                new CopilotRequestMessage("assistant", "I will run the focused suite."),
                new CopilotRequestMessage("tool", "tool-result-secret"),
            ],
            ProjectInstructions =
            [
                new CopilotProjectInstructionDocument
                {
                    Path = @"C:\work\ColorVision\AGENTS.md",
                    Content = "project-instruction-secret",
                },
            ],
            AccessContext = accessContext,
            Profile = new CopilotProfileConfig
            {
                ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "test-key",
                BaseUrl = "https://example.test/v1",
                Model = "test-model",
                MaxTokens = 4_096,
            },
            Mode = CopilotAgentMode.Auto,
        };
    }

    private static ConfirmableAction CreateAction(string reviewDetails)
    {
        return new ConfirmableAction
        {
            ActionId = "auto-review-action",
            Title = "Run PowerShell command",
            Description = "Review the complete command.",
            RiskLevel = "confirmation-required",
            ToolName = "RunShellCommand",
            ArgumentsSummary = "command=<review-required>",
            ArgumentsDigest = new string('a', 64),
            ReviewDetails = reviewDetails,
            ResumesAgentOnApproval = true,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            RequestContext = new CopilotConfirmationRequestContext
            {
                SourceKind = CopilotApprovalSourceKind.InAppAgent,
                RequestSource = "in-app-agent-framework",
                ConversationId = "conversation-auto-review",
                TaskId = "task-auto-review",
                WorkspacePath = @"C:\work\ColorVision",
                ImpactSummary = "Runs one command in the current workspace.",
                Reversibility = CopilotApprovalReversibility.NotReversible,
                ReversibilitySummary = "Command side effects are not automatically reverted.",
            },
        };
    }

    private sealed class ReviewableTool : ICopilotTool
    {
        public string Name => "RunShellCommand";

        public string Description => "Run a protected command.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ProtectedWrite(
                CopilotToolIdempotency.NonIdempotent,
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly);

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotToolResult { ToolName = Name, Success = true });
    }

    private sealed class CapturingChatClient(string responseText) : IChatClient
    {
        public int CallCount { get; private set; }

        public IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> Messages { get; private set; } =
            Array.Empty<Microsoft.Extensions.AI.ChatMessage>();

        public ChatOptions? Options { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            Messages = messages.ToArray();
            Options = options;
            return Task.FromResult(new ChatResponse(
                new Microsoft.Extensions.AI.ChatMessage(
                    ChatRole.Assistant,
                    [
                        new TextContent(responseText),
                        new UsageContent(new UsageDetails
                        {
                            InputTokenCount = 12,
                            OutputTokenCount = 5,
                            TotalTokenCount = 17,
                        }),
                    ]))
            {
                FinishReason = ChatFinishReason.Stop,
            });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }
}
