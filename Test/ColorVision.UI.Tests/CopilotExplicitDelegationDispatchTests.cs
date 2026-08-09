using ColorVision.Copilot;
using Microsoft.Extensions.AI;

namespace ColorVision.UI.Tests;

public sealed class CopilotExplicitDelegationDispatchTests
{
    private const string DelegateFunctionName = "colorvision_delegate_explore";
    private const string ExplicitTask = "Use only DelegateExplore; do not use parent agent file tools.";

    [Fact]
    public async Task ExplicitDelegationWithoutCustomAgentsStillUsesTheDirectDispatchFastPath()
    {
        var inner = new RecordingChatClient();
        var dispatchCount = 0;
        using var client = new CopilotExplicitDelegationDispatchChatClient(
            inner,
            CreateRequest(Array.Empty<CopilotCodexCustomSubagentDefinition>()),
            DelegateFunctionName,
            taskLedgerEnabled: false,
            () => dispatchCount++);

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, ExplicitTask)],
            CreateOptions());

        var call = Assert.Single(response.Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionCallContent>());
        Assert.Equal(DelegateFunctionName, call.Name);
        Assert.Equal(ExplicitTask, call.Arguments?["task"]?.ToString());
        Assert.Equal(1, dispatchCount);
        Assert.Equal(0, inner.CallCount);
    }

    [Fact]
    public async Task AvailableCustomAgentsReturnExplicitDelegationToParentPlanning()
    {
        var inner = new RecordingChatClient();
        var dispatchCount = 0;
        using var client = new CopilotExplicitDelegationDispatchChatClient(
            inner,
            CreateRequest(
            [
                new CopilotCodexCustomSubagentDefinition
                {
                    Name = "reviewer",
                    Description = "Review bounded workspace evidence.",
                    DeveloperInstructions = "Prioritize authorization boundaries.",
                },
            ]),
            DelegateFunctionName,
            taskLedgerEnabled: false,
            () => dispatchCount++);

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, ExplicitTask)],
            CreateOptions());

        Assert.Empty(response.Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionCallContent>());
        Assert.Equal("provider-planned", response.Text);
        Assert.Equal(0, dispatchCount);
        Assert.Equal(1, inner.CallCount);
    }

    private static CopilotAgentRequest CreateRequest(
        IReadOnlyList<CopilotCodexCustomSubagentDefinition> customSubagents) => new()
        {
            ConversationId = "explicit-delegation-" + Guid.NewGuid().ToString("N"),
            UserText = ExplicitTask,
            TaskIntentText = ExplicitTask,
            Profile = new CopilotProfileConfig
            {
                VendorType = CopilotVendorType.Custom,
                ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "test-key",
                BaseUrl = "https://example.test/v1",
                Model = "parent-model",
                MaxTokens = 4_096,
            },
            CodexAgentsEnabled = true,
            CodexCustomSubagents = customSubagents,
            SearchRootPaths = [@"C:\workspace"],
            RequiredSuccessfulToolNames = ["DelegateExplore"],
            RequiresDelegatedWorkspaceEvidence = true,
        };

    private static ChatOptions CreateOptions() => new()
    {
        Tools =
        [
            AIFunctionFactory.Create(
                (string task) => task,
                DelegateFunctionName,
                "Delegates a bounded read-only workspace investigation."),
        ],
    };

    private sealed class RecordingChatClient : IChatClient
    {
        public int CallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "provider-planned"))
            {
                FinishReason = ChatFinishReason.Stop,
            });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            await Task.CompletedTask;
            yield return new ChatResponseUpdate(ChatRole.Assistant, "provider-planned")
            {
                FinishReason = ChatFinishReason.Stop,
            };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
