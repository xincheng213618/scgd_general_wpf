using ColorVision.Copilot;
using Newtonsoft.Json;
using System.IO;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Reflection;
using ColorVision.Solution;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotConversationFullAccessTests
{
    private static readonly string Workspace = Path.GetFullPath(Path.GetTempPath());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FrameworkUsesFullAccessWithoutPromptOrReviewerAndRechecksRevocation(bool revokeBeforeExecution)
    {
        using var workspaceScope = new EmptyWorkspaceScope();
        var tool = new ProtectedProbe();
        var provider = new ProbeProvider();
        var catalog = new CopilotCapabilityCatalog();
        catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "full-access-test", "Full access test", [tool]);
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(new CopilotToolRegistry([tool]),
            new CopilotAgentContextBuilder(), new CopilotToolExecutor(), _ => provider, new NoExternalTools(), catalog,
            new CopilotAgentSkillUsageStore(Path.Combine(Workspace, "copilot-full-access-tests", Guid.NewGuid().ToString("N"))));
        var request = new CopilotAgentRequest
        {
            ConversationId = "full-access-runtime", TaskId = "full-access-runtime-task", WorkspacePath = string.Empty,
            Mode = CopilotAgentMode.Code, Profile = new CopilotProfileConfig { ProviderType = CopilotProviderType.LocalCodex },
            UserText = "Execute the full access probe once.", TaskIntentText = "Execute the full access probe once.",
            HarnessFeatures = CopilotAgentHarnessFeatures.None,
            CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.OnRequest),
        };
        request.AccessContext.PrepareUnrestrictedFullAccess(request.ConversationId, string.Empty, request.TaskId);
        var events = new List<CopilotAgentEvent>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await runtime.RunAsync(request, e =>
        {
            events.Add(e);
            if (revokeBeforeExecution && e.Text.Contains("approved by this conversation's full access", StringComparison.Ordinal))
                request.AccessContext.Revoke();
        }, cancellation.Token);
        Assert.Contains(events, e => e.Text.Contains("approved by this conversation's full access", StringComparison.Ordinal));
        Assert.Equal(revokeBeforeExecution ? 0 : 1, tool.ExecutionCount);
        Assert.DoesNotContain(events, e => e.Text.Contains("waiting for explicit ColorVision approval", StringComparison.Ordinal));
        Assert.Equal(0, provider.NonStreamingCalls);
        Assert.True(result.TaskEventJournal.IsStructurallyValid());
        Assert.Contains(result.TaskEventJournal.Events, e => e.State == "approved:ConversationFullAccess");
        if (!revokeBeforeExecution) Assert.Contains(result.StepRecords, step => step.Execution.ToolName == tool.Name && step.Execution.State == CopilotToolExecutionState.Completed);
    }

    [Fact]
    public void FullAccessApprovesShellWithoutAutomaticReviewerAndCanBeRevoked()
    {
        var request = CreateRequest();
        var tool = new CopilotShellCommandTool();
        Assert.False(CopilotAgentAccessPolicy.CanAutoApprove(request, tool, Workspace));
        request.AccessContext.PrepareUnrestrictedFullAccess(request.ConversationId, Workspace, request.TaskId);
        Assert.True(CopilotAgentAccessPolicy.CanAutoApprove(request, tool, Workspace));
        Assert.False(CopilotAgentAccessPolicy.CanAutoReview(request, tool, Workspace));
        Assert.Null(request.AccessContext.ExpiresAtUtc);
        Assert.True(request.AccessContext.Revoke());
        Assert.False(CopilotAgentAccessPolicy.CanAutoApprove(request, tool, Workspace));
    }

    [Fact]
    public void ConversationFullAccessSurvivesTaskCompletionButDoesNotAuthorizeAnotherTaskBeforeBinding()
    {
        var access = new CopilotAgentAccessContext();
        access.PrepareUnrestrictedFullAccess("conversation", Workspace, null);
        Assert.False(access.AllowsUnattendedProtectedActionsFor("conversation", "first", Workspace));
        Assert.True(access.BindToTask("conversation", "first", Workspace));
        Assert.False(access.EndTask("other"));
        Assert.True(access.EndTask("first"));
        Assert.Equal(CopilotAgentAccessMode.UnrestrictedFullAccess, access.Mode);
        Assert.False(access.AllowsUnattendedProtectedActionsFor("conversation", "first", Workspace));
        Assert.True(access.BindToTask("conversation", "second", Workspace));
        Assert.True(access.AllowsUnattendedProtectedActionsFor("conversation", "second", Workspace));
        Assert.False(access.AllowsUnattendedProtectedActionsFor("other", "second", Workspace));
    }

    [Fact]
    public void TemporaryReviewStillExpiresAtTaskCompletion()
    {
        var access = new CopilotAgentAccessContext();
        access.PrepareFullAccess("conversation", Workspace, "first", DateTimeOffset.UtcNow.AddMinutes(5));
        Assert.True(access.EndTask("first"));
        Assert.Equal(CopilotAgentAccessMode.ConfirmProtectedActions, access.Mode);
    }

    [Fact]
    public void WorkspaceChangeRevokesFullAccess()
    {
        var request = CreateRequest();
        request.AccessContext.PrepareUnrestrictedFullAccess(request.ConversationId, Workspace, request.TaskId);
        Assert.False(CopilotAgentAccessPolicy.CanAutoApprove(request, new CopilotShellCommandTool(), Path.Combine(Workspace, "other")));
        Assert.Equal(CopilotAgentAccessMode.ConfirmProtectedActions, request.AccessContext.Mode);
    }

    [Theory]
    [InlineData(CopilotAgentMode.Plan)]
    [InlineData(CopilotAgentMode.Review)]
    [InlineData(CopilotAgentMode.Diagnose)]
    public void FullAccessDoesNotChangeReadOnlyMode(CopilotAgentMode mode)
    {
        var request = CreateRequest(mode);
        request.AccessContext.PrepareUnrestrictedFullAccess(request.ConversationId, Workspace, request.TaskId);
        Assert.False(CopilotAgentAccessPolicy.CanAutoApprove(request, new CopilotShellCommandTool(), Workspace));
    }

    [Fact]
    public void ReopeningOrBranchingDoesNotInheritFullAccess()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Done");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Inspect"));
        conversation.Messages.Add(assistant);
        conversation.PrepareUnrestrictedFullAccessGrant(Workspace, null);
        var restored = JsonConvert.DeserializeObject<CopilotConversationRecord>(JsonConvert.SerializeObject(conversation))!;
        Assert.Equal(CopilotAgentAccessMode.ConfirmProtectedActions, restored.AccessMode);
        Assert.Equal(CopilotAgentAccessMode.ConfirmProtectedActions, CopilotConversationBranchService.CreateBranch(conversation, assistant).AccessMode);
        Assert.Equal(CopilotAgentAccessMode.UnrestrictedFullAccess, conversation.AccessMode);
    }

    [Fact]
    public void PermissionCommandDistinguishesFullAccessFromAutomaticReview()
    {
        Assert.Equal(CopilotPermissionCommandAction.UseFullAccess, CopilotPermissionCommand.Resolve("full"));
        Assert.Equal(CopilotPermissionCommandAction.UseTemporaryAutoReview, CopilotPermissionCommand.Resolve("auto"));
        Assert.Equal(CopilotPermissionCommandAction.UseConfirmProtectedActions, CopilotPermissionCommand.Resolve("ask"));
    }

    private static CopilotAgentRequest CreateRequest(CopilotAgentMode mode = CopilotAgentMode.Code) => new()
    {
        ConversationId = "full-access-conversation", TaskId = "full-access-task", WorkspacePath = Workspace,
        Mode = mode, Profile = new CopilotProfileConfig { ProviderType = CopilotProviderType.LocalCodex },
        UserText = "Run the requested command", WritableLocalRootPaths = [Workspace],
    };

    private sealed class NoExternalTools : ICopilotExternalToolProvider
    {
        public Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotExternalToolLease());
    }

    private sealed class EmptyWorkspaceScope : IDisposable
    {
        private readonly FieldInfo _field = typeof(SolutionManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previous;
        public EmptyWorkspaceScope()
        {
            _previous = _field.GetValue(null);
            _field.SetValue(null, RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager)));
        }
        public void Dispose() => _field.SetValue(null, _previous);
    }

    private sealed class ProtectedProbe : ICopilotAgentDrivenTool, ICopilotFrameworkApprovedTool
    {
        public string Name => "FullAccessProbe";
        public string Description => "A protected test operation.";
        public CopilotToolAccess Access => CopilotToolAccess.Write;
        public CopilotToolApprovalMode ApprovalMode => CopilotToolApprovalMode.Always;
        public CopilotToolRiskLevel RiskLevel => CopilotToolRiskLevel.High;
        public int ExecutionCount { get; private set; }
        public bool CanHandle(CopilotAgentRequest request) => true;
        public bool IsAvailable(CopilotAgentRequest request) => true;
        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput input, CancellationToken token)
            => throw new InvalidOperationException("Protected operation must use its approved execution path.");

        public Task<CopilotToolResult> ExecuteApprovedAsync(CopilotAgentRequest request, CopilotAgentToolInput input, CancellationToken token)
        {
            ExecutionCount++;
            return Task.FromResult(new CopilotToolResult { ToolName = Name, Success = true, Summary = "Probe executed." });
        }
    }

    private sealed class ProbeProvider : IChatClient
    {
        private bool _requested;
        public int NonStreamingCalls { get; private set; }
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            NonStreamingCalls++;
            throw new InvalidOperationException("Full access must not call an automatic approval reviewer.");
        }
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            if (!_requested)
            {
                _requested = true;
                var tool = Assert.Single(options!.Tools!.OfType<AIFunction>(), f => f.Name.Contains("probe", StringComparison.OrdinalIgnoreCase));
                yield return new ChatResponseUpdate(ChatRole.Assistant,
                    [new FunctionCallContent("full-access-probe-call", tool.Name, new Dictionary<string, object?>())]) { FinishReason = ChatFinishReason.ToolCalls };
            }
            else yield return new ChatResponseUpdate(ChatRole.Assistant, "The requested probe has finished.") { FinishReason = ChatFinishReason.Stop };
        }
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
