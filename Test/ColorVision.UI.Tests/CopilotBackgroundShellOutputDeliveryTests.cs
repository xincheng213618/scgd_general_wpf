using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class CopilotBackgroundShellOutputDeliveryTests
{
    [Fact]
    public async Task NextAgentRunReceivesDelayedOutputBeforeCurrentUserRequest()
    {
        using var provider = new CapturingChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => provider,
            EmptyExternalToolProvider.Instance,
            new CopilotCapabilityCatalog());
        Assert.True(runtime.TryEnqueueBackgroundShellCommandOutput(
            CreateOutputEvent("conversation", "ready")));

        var result = await runtime.RunAsync(
            CreateRequest("conversation", "Handle the current user request."),
            _ => { },
            CancellationToken.None);

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        var messages = Assert.Single(provider.StreamingCalls);
        var userMessages = messages
            .Where(message => message.Role == ChatRole.User)
            .ToArray();
        var finalUserMessage = Assert.Single(userMessages);
        var delayedEventIndex = finalUserMessage.Text.IndexOf(
            "<background_command_output_event>",
            StringComparison.Ordinal);
        var currentRequestIndex = finalUserMessage.Text.IndexOf(
            "Handle the current user request.",
            StringComparison.Ordinal);
        Assert.True(delayedEventIndex >= 0);
        Assert.True(currentRequestIndex > delayedEventIndex);
        Assert.Contains(
            "\"delivery\":\"delayed\"",
            finalUserMessage.Text,
            StringComparison.Ordinal);
        Assert.Contains(
            result.TaskEventJournal.Events,
            item => item.Type
                == CopilotAgentTaskEventType
                    .BackgroundCommandOutputObserved);
    }

    private static CopilotAgentRequest CreateRequest(
        string conversationId,
        string userText)
    {
        return new CopilotAgentRequest
        {
            ConversationId = conversationId,
            TaskId = "task",
            UserText = userText,
            Profile = new CopilotProfileConfig
            {
                VendorType = CopilotVendorType.Custom,
                ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "test-key",
                BaseUrl = "https://example.test/v1",
                Model = "test-model",
                MaxTokens = 4_096,
            },
            Mode = CopilotAgentMode.Code,
        };
    }

    private static CopilotBackgroundShellOutputMonitorEventArgs
        CreateOutputEvent(
            string conversationId,
            string content)
    {
        return new CopilotBackgroundShellOutputMonitorEventArgs(
            new CopilotBackgroundShellOutputMonitorSnapshot(
                "monitor:deferred",
                conversationId,
                "background:deferred",
                CopilotBackgroundShellOutputStream.StandardOutput,
                "readiness",
                DateTimeOffset.Parse("2026-07-31T00:00:00Z"),
                DateTimeOffset.Parse("2026-07-31T01:00:00Z"),
                CopilotBackgroundShellOutputMonitorState.Running,
                PublishedEvents: 1,
                SuppressedEvents: 0),
            content,
            suppressedEvents: 0);
    }

    private sealed class CapturingChatClient : IChatClient
    {
        public List<IReadOnlyList<ChatMessage>> StreamingCalls { get; } =
            new();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "done"))
            {
                FinishReason = ChatFinishReason.Stop,
            });
        }

        public async IAsyncEnumerable<ChatResponseUpdate>
            GetStreamingResponseAsync(
                IEnumerable<ChatMessage> messages,
                ChatOptions? options = null,
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StreamingCalls.Add(messages.ToArray());
            yield return new ChatResponseUpdate(ChatRole.Assistant, "done")
            {
                FinishReason = ChatFinishReason.Stop,
            };
            await Task.CompletedTask;
        }

        public object? GetService(
            Type serviceType,
            object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }

    private sealed class EmptyExternalToolProvider :
        ICopilotExternalToolProvider
    {
        public static EmptyExternalToolProvider Instance { get; } = new();

        public Task<CopilotExternalToolLease> DiscoverAsync(
            CopilotAgentRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotExternalToolLease());
    }
}
