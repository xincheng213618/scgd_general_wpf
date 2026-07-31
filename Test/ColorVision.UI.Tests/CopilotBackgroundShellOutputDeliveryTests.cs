using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

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

    [Fact]
    public async Task FailureBeforeFirstProviderUpdateReturnsDeliveryForRetry()
    {
        using var failingProvider =
            new FailBeforeFirstUpdateChatClient();
        using var recoveredProvider = new CapturingChatClient();
        var providerNumber = 0;
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => Interlocked.Increment(ref providerNumber) == 1
                ? failingProvider
                : recoveredProvider,
            EmptyExternalToolProvider.Instance,
            new CopilotCapabilityCatalog());
        Assert.True(runtime.TryEnqueueBackgroundShellCommandOutput(
            CreateOutputEvent("conversation", "retry me")));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runtime.RunAsync(
                CreateRequest("conversation", "First attempt."),
                _ => { },
                CancellationToken.None));
        var failedPrompt = Assert.Single(
            Assert.Single(failingProvider.StreamingCalls),
            message => message.Role == ChatRole.User);

        var recoveredResult = await runtime.RunAsync(
            CreateRequest("conversation", "Retry attempt."),
            _ => { },
            CancellationToken.None);

        Assert.Equal(
            CopilotAgentStopReason.Completed,
            recoveredResult.StopReason);
        var retriedPrompt = Assert.Single(
            Assert.Single(recoveredProvider.StreamingCalls),
            message => message.Role == ChatRole.User);
        Assert.Contains(
            "\"content\":\"retry me\"",
            retriedPrompt.Text,
            StringComparison.Ordinal);
        Assert.Equal(
            ExtractDeliveryId(failedPrompt.Text),
            ExtractDeliveryId(retriedPrompt.Text));
        Assert.Single(
            recoveredResult.TaskEventJournal.Events,
            item => item.Type
                == CopilotAgentTaskEventType
                    .BackgroundCommandOutputObserved);
    }

    [Fact]
    public async Task AnsweredQuestionTransfersQueuedOutputIntoSameAgentRun()
    {
        using var provider = new QuestionThenAnswerChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => provider,
            EmptyExternalToolProvider.Instance,
            new CopilotCapabilityCatalog());
        var questionReady =
            new TaskCompletionSource<CopilotUserQuestionSnapshot>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        var request = CreateRequest(
            "conversation",
            "Ask one question, then finish after the answer.");
        var runTask = runtime.RunAsync(
            request,
            agentEvent =>
            {
                if (agentEvent.Type
                        == CopilotAgentEventType.UserQuestionRequested
                    && agentEvent.UserQuestion != null)
                {
                    questionReady.TrySetResult(agentEvent.UserQuestion);
                }
            },
            CancellationToken.None);
        var question = await questionReady.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.True(runtime.TryEnqueueBackgroundShellCommandOutput(
            CreateOutputEvent(
                "conversation",
                "arrived while waiting")));
        Assert.True(runtime.TryAnswerUserQuestion(
            request.TaskId,
            question.RequestId,
            "typed answer"));

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        var resumedCall = Assert.Single(
            provider.StreamingCalls,
            call => call.Any(message => message.Text.Contains(
                "<background_command_output_event>",
                StringComparison.Ordinal)));
        var functionResultIndex = resumedCall
            .Select((message, index) => new
            {
                Message = message,
                Index = index,
            })
            .First(item => item.Message.Contents
                .OfType<FunctionResultContent>()
                .Any())
            .Index;
        var outputEventIndex = resumedCall
            .Select((message, index) => new
            {
                Message = message,
                Index = index,
            })
            .First(item => item.Message.Text.Contains(
                "<background_command_output_event>",
                StringComparison.Ordinal))
            .Index;
        var functionResult = resumedCall
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Single();
        Assert.Contains(
            "typed answer",
            Convert.ToString(functionResult.Result) ?? string.Empty,
            StringComparison.Ordinal);
        Assert.True(outputEventIndex > functionResultIndex);
        Assert.Contains(
            "\"content\":\"arrived while waiting\"",
            resumedCall[outputEventIndex].Text,
            StringComparison.Ordinal);
        Assert.Single(
            result.TaskEventJournal.Events,
            item => item.Type
                == CopilotAgentTaskEventType
                    .BackgroundCommandOutputObserved);
    }

    [Fact]
    public async Task CompletionDuringQuestionTransfersOutputBeforeTerminalIntoSameAgentRun()
    {
        using var provider = new QuestionThenAnswerChatClient();
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => provider,
            EmptyExternalToolProvider.Instance,
            new CopilotCapabilityCatalog());
        var questionReady =
            new TaskCompletionSource<CopilotUserQuestionSnapshot>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        var request = CreateRequest(
            "conversation",
            "Ask one question, then finish after the answer.");
        var runTask = runtime.RunAsync(
            request,
            agentEvent =>
            {
                if (agentEvent.Type
                        == CopilotAgentEventType.UserQuestionRequested
                    && agentEvent.UserQuestion != null)
                {
                    questionReady.TrySetResult(agentEvent.UserQuestion);
                }
            },
            CancellationToken.None);
        var question = await questionReady.Task.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.True(runtime.TryEnqueueBackgroundShellCommandOutput(
            CreateOutputEvent(
                "conversation",
                "final output before completion")));
        Assert.True(runtime.TryEnqueueBackgroundShellCommandCompletion(
            CreateCompletedCommand("conversation")));
        Assert.True(runtime.TryAnswerUserQuestion(
            request.TaskId,
            question.RequestId,
            "typed answer"));

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        var resumedCall = Assert.Single(
            provider.StreamingCalls,
            call => call.Any(message => message.Text.Contains(
                "<background_command_event>",
                StringComparison.Ordinal)));
        var functionResultIndex = resumedCall
            .Select((message, index) => new
            {
                Message = message,
                Index = index,
            })
            .First(item => item.Message.Contents
                .OfType<FunctionResultContent>()
                .Any())
            .Index;
        var injectedMessage = resumedCall
            .Select((message, index) => new
            {
                Message = message,
                Index = index,
            })
            .Single(item => item.Message.Text.Contains(
                "<background_command_event>",
                StringComparison.Ordinal));
        var outputEventIndex = injectedMessage.Message.Text.IndexOf(
            "<background_command_output_event>",
            StringComparison.Ordinal);
        var terminalEventIndex = injectedMessage.Message.Text.IndexOf(
            "<background_command_event>",
            StringComparison.Ordinal);
        Assert.True(injectedMessage.Index > functionResultIndex);
        Assert.True(outputEventIndex >= 0);
        Assert.True(terminalEventIndex > outputEventIndex);
        Assert.Contains(
            "\"content\":\"final output before completion\"",
            injectedMessage.Message.Text,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"background_id\":\"background:deferred\"",
            injectedMessage.Message.Text,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"state\":\"completed\"",
            injectedMessage.Message.Text,
            StringComparison.Ordinal);
        Assert.Single(
            result.TaskEventJournal.Events,
            item => item.Type
                == CopilotAgentTaskEventType
                    .BackgroundCommandOutputObserved);
        Assert.Single(
            result.TaskEventJournal.Events,
            item => item.Type
                == CopilotAgentTaskEventType
                    .BackgroundCommandCompleted);
    }

    private static string ExtractDeliveryId(string prompt)
    {
        var match = Regex.Match(
            prompt,
            "\"delivery_id\":\"(?<id>[^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success);
        return match.Groups["id"].Value;
    }

    private static CopilotAgentRequest CreateRequest(
        string conversationId,
        string userText)
    {
        return new CopilotAgentRequest
        {
            ConversationId = conversationId,
            TaskId = CopilotAgentTaskEventIds.CreateRunId(),
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

    private static CopilotBackgroundShellCommandSnapshot
        CreateCompletedCommand(string conversationId)
    {
        return new CopilotBackgroundShellCommandSnapshot(
            "background:deferred",
            conversationId,
            "task:deferred",
            CopilotShellKind.PowerShell,
            @"C:\workspace",
            "background command",
            new string('a', 64),
            DateTimeOffset.Parse("2026-07-31T00:00:00Z"),
            DateTimeOffset.Parse("2026-07-31T00:00:01Z"),
            42,
            true,
            CopilotBackgroundShellCommandState.Completed,
            0,
            "final output before completion",
            string.Empty)
        {
            ObservedStandardOutputCharacters =
                "final output before completion".Length,
        };
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

    private sealed class FailBeforeFirstUpdateChatClient : IChatClient
    {
        public List<IReadOnlyList<ChatMessage>> StreamingCalls { get; } =
            new();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "The failed initial stream must not enter finalization.");
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
            await Task.CompletedTask;
            if (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException(
                    "Provider failed before its first update.");
            }
            yield break;
        }

        public object? GetService(
            Type serviceType,
            object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }

    private sealed class QuestionThenAnswerChatClient : IChatClient
    {
        private int _callCount;

        public List<IReadOnlyList<ChatMessage>> StreamingCalls { get; } =
            new();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "The question scenario must remain on the streaming path.");
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
            if (Interlocked.Increment(ref _callCount) == 1)
            {
                var observedToolNames = options?.Tools?
                    .OfType<AIFunction>()
                    .Select(tool => tool.Name)
                    .ToArray()
                    ?? Array.Empty<string>();
                var questionToolName = observedToolNames
                    .First(name => name.Contains(
                        "Question",
                        StringComparison.OrdinalIgnoreCase));
                yield return new ChatResponseUpdate(
                    ChatRole.Assistant,
                    [
                        new FunctionCallContent(
                            "call-question",
                            questionToolName,
                            new Dictionary<string, object?>
                            {
                                ["header"] = "Target",
                                ["question"] =
                                    "Which target should be used?",
                                ["options"] =
                                    JsonSerializer.SerializeToElement(
                                    new[]
                                    {
                                        new
                                        {
                                            label =
                                                "Option A (Recommended)",
                                            description =
                                                "Use the first target.",
                                        },
                                        new
                                        {
                                            label = "Option B",
                                            description =
                                                "Use the second target.",
                                        },
                                    }),
                            }),
                    ])
                {
                    FinishReason = ChatFinishReason.ToolCalls,
                };
                yield break;
            }

            yield return new ChatResponseUpdate(
                ChatRole.Assistant,
                "done")
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
