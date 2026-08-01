using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class CopilotExplicitDelegationDispatchChatClientTests
{
    private const string DelegateFunctionName = "colorvision_delegate_explore";

    [Fact]
    public async Task EligibleStreamingRequestSynthesizesExclusiveDelegateCall()
    {
        using var provider = new RecordingChatClient("provider fallback");
        var dispatchCount = 0;
        using var client = new CopilotExplicitDelegationDispatchChatClient(
            provider,
            ExclusiveExploreRequest(),
            DelegateFunctionName,
            taskLedgerEnabled: false,
            () => dispatchCount++);

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(
            [new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, "inspect")],
            ToolOptions(),
            CancellationToken.None))
        {
            updates.Add(update);
        }

        var singleUpdate = Assert.Single(updates);
        var call = Assert.Single(singleUpdate.Contents.OfType<FunctionCallContent>());
        Assert.Equal(0, provider.CallCount);
        Assert.Equal(1, dispatchCount);
        Assert.Equal(ChatFinishReason.ToolCalls, singleUpdate.FinishReason);
        Assert.StartsWith("call-explicit-delegate-", call.CallId, StringComparison.Ordinal);
        Assert.Equal(DelegateFunctionName, call.Name);
        Assert.Equal(ExclusiveExploreRequest().UserText, call.Arguments!["task"]);
    }

    [Fact]
    public async Task NonStreamingDispatchOccursOnlyOnceThenFallsBackToProvider()
    {
        using var provider = new RecordingChatClient("provider fallback");
        var dispatchCount = 0;
        using var client = new CopilotExplicitDelegationDispatchChatClient(
            provider,
            ExclusiveExploreRequest(),
            DelegateFunctionName,
            taskLedgerEnabled: false,
            () => dispatchCount++);

        var first = await client.GetResponseAsync(
            [new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, "inspect")],
            ToolOptions(),
            CancellationToken.None);
        var call = Assert.Single(first.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>());
        var second = await client.GetResponseAsync(
            [
                new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, "inspect"),
                new Microsoft.Extensions.AI.ChatMessage(
                    ChatRole.Tool,
                    [new FunctionResultContent(call.CallId, "{}")]),
            ],
            ToolOptions(),
            CancellationToken.None);

        Assert.Equal(1, provider.CallCount);
        Assert.Equal(1, dispatchCount);
        Assert.Equal("provider fallback", second.Text);
    }

    [Fact]
    public async Task ActiveGoalUsesProviderForGoalAwarePlanning()
    {
        using var provider = new RecordingChatClient("provider fallback");
        var dispatchCount = 0;
        using var client = new CopilotExplicitDelegationDispatchChatClient(
            provider,
            ExclusiveExploreRequest(activeGoalText: "Keep investigating until every requested file is covered."),
            DelegateFunctionName,
            taskLedgerEnabled: false,
            () => dispatchCount++);

        var response = await client.GetResponseAsync(
            [new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, "inspect")],
            ToolOptions(),
            CancellationToken.None);

        Assert.Equal(1, provider.CallCount);
        Assert.Equal(0, dispatchCount);
        Assert.Equal("provider fallback", response.Text);
    }

    [Theory]
    [InlineData(true, true, true, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, true)]
    public async Task UnsafeOrContextDependentRequestsUseProvider(
        bool taskLedgerEnabled,
        bool exclusiveRequest,
        bool includeDelegateTool,
        bool hasConversationHistory)
    {
        using var provider = new RecordingChatClient("provider fallback");
        var request = exclusiveRequest
            ? ExclusiveExploreRequest(hasConversationHistory)
            : new CopilotAgentRequest
            {
                UserText = "Inspect the workspace.",
                SearchRootPaths = [@"C:\workspace"],
            };
        var dispatchCount = 0;
        using var client = new CopilotExplicitDelegationDispatchChatClient(
            provider,
            request,
            DelegateFunctionName,
            taskLedgerEnabled,
            () => dispatchCount++);

        var response = await client.GetResponseAsync(
            [new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, "inspect")],
            includeDelegateTool ? ToolOptions() : new ChatOptions(),
            CancellationToken.None);

        Assert.Equal(1, provider.CallCount);
        Assert.Equal(0, dispatchCount);
        Assert.Equal("provider fallback", response.Text);
    }

    private static CopilotAgentRequest ExclusiveExploreRequest(
        bool hasConversationHistory = false,
        string? activeGoalText = null) => new()
    {
        UserText =
            "请只使用 DelegateExplore 子 Agent 检查 C:\\workspace 下的 Coordinator.cs 和 Explore.cs，"
            + "返回准确文件路径与行号证据，不要由父 Agent 直接读取文件。",
        TaskIntentText =
            "请只使用 DelegateExplore 子 Agent 检查 C:\\workspace 下的 Coordinator.cs 和 Explore.cs，"
            + "返回准确文件路径与行号证据，不要由父 Agent 直接读取文件。",
        SearchRootPaths = [@"C:\workspace"],
        Mode = CopilotAgentMode.Code,
        History = hasConversationHistory
            ? [new CopilotRequestMessage("assistant", "Prior context that requires parent interpretation.")]
            : Array.Empty<CopilotRequestMessage>(),
        RequiredSuccessfulToolNames = ["DelegateExplore"],
        RequiresDelegatedWorkspaceEvidence = true,
        ActiveGoalText = activeGoalText,
    };

    private static ChatOptions ToolOptions() => new()
    {
        Tools =
        [
            AIFunctionFactory.Create(
                (string task) => task,
                DelegateFunctionName,
                "Delegates a read-only workspace investigation."),
        ],
    };

    private sealed class RecordingChatClient(string responseText) : IChatClient
    {
        public int CallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ChatResponse(
                new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, responseText))
            {
                FinishReason = ChatFinishReason.Stop,
            });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            CallCount++;
            yield return new ChatResponseUpdate(ChatRole.Assistant, responseText)
            {
                FinishReason = ChatFinishReason.Stop,
            };
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }
}
