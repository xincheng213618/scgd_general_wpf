using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFinalAnswerSteeringTests
{
    private const string FirstSteering = "FIRST_STEERING: 最终只输出中文表格。";
    private const string SecondSteering = "SECOND_STEERING: 表格中的状态保留原始英文名称。";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AutomaticFinalAnswerKeepsDeliveredSteeringInOrder(bool lengthLimited)
    {
        using var fixture = new RunFixture(lengthLimited, exhaustToolBudget: false);
        var run = fixture.Start();
        await fixture.Provider.FirstCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        fixture.EnqueueSteering();
        fixture.Provider.ReleaseFirstCall.TrySetResult();

        var result = await run.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(2, fixture.Provider.StreamingCalls);
        AssertSteeringOrder(fixture.Provider.LastStreamingInput);
        var delivered = fixture.Events
            .Where(item => item.Type == CopilotAgentEventType.SteeringDelivered)
            .SelectMany(item => item.SteeringMessages)
            .Select(item => item.Text)
            .ToArray();
        Assert.Equal([FirstSteering, SecondSteering], delivered);
        Assert.DoesNotContain(fixture.Events, item => item.Type == CopilotAgentEventType.SteeringRecovery);
        AssertSteeringOrder(fixture.Provider.FinalAnswerInput);
        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        fixture.AssertFinalAnswerHasNoTools();
        Assert.Equal(0, fixture.Tool.CallCount);
        Assert.Empty(result.StepRecords);
    }

    [Fact]
    public async Task ToolBudgetFinalizationDoesNotConsumeUndeliveredSteering()
    {
        using var fixture = new RunFixture(lengthLimited: false, exhaustToolBudget: true);
        var run = fixture.Start();
        await fixture.Provider.FirstCallStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        fixture.EnqueueSteering();
        fixture.Provider.ReleaseFirstCall.TrySetResult();

        var result = await run.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(1, fixture.Provider.StreamingCalls);
        Assert.DoesNotContain(FirstSteering, fixture.Provider.LastStreamingInput, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Events, item => item.Type == CopilotAgentEventType.SteeringDelivered);
        var recovered = fixture.Events
            .Where(item => item.Type == CopilotAgentEventType.SteeringRecovery)
            .SelectMany(item => item.SteeringMessages)
            .Select(item => item.Text)
            .ToArray();
        Assert.Equal([FirstSteering, SecondSteering], recovered);
        Assert.DoesNotContain(FirstSteering, fixture.Provider.FinalAnswerInput, StringComparison.Ordinal);
        Assert.DoesNotContain(SecondSteering, fixture.Provider.FinalAnswerInput, StringComparison.Ordinal);
        Assert.True(result.Budget.ToolBudgetExhausted);
        Assert.Equal(CopilotAgentStopReason.BudgetExhausted, result.StopReason);
        fixture.AssertFinalAnswerHasNoTools();
        Assert.Equal(1, fixture.Tool.CallCount);
    }

    private static void AssertSteeringOrder(string text)
    {
        var first = text.IndexOf(FirstSteering, StringComparison.Ordinal);
        var second = text.IndexOf(SecondSteering, StringComparison.Ordinal);
        Assert.True(first >= 0, "The provider request lost the first steering instruction.");
        Assert.True(second > first, "The provider request lost or reordered the second steering instruction.");
        Assert.Equal(first, text.LastIndexOf(FirstSteering, StringComparison.Ordinal));
        Assert.Equal(second, text.LastIndexOf(SecondSteering, StringComparison.Ordinal));
    }

    private sealed class RunFixture : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("CopilotFinalAnswerSteering-");
        private readonly CancellationTokenSource _cancellation = new(TimeSpan.FromSeconds(30));
        private readonly CopilotMicrosoftAgentFrameworkRuntime _runtime;
        private readonly CopilotAgentRequest _request;

        public RunFixture(bool lengthLimited, bool exhaustToolBudget)
        {
            Provider = new FinalAnswerProbeChatClient(lengthLimited, exhaustToolBudget);
            var capabilities = new CopilotCapabilityCatalog();
            capabilities.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "steering-probe", "Steering probe", [Tool]);
            _runtime = new CopilotMicrosoftAgentFrameworkRuntime(
                new CopilotToolRegistry([Tool]),
                new CopilotAgentContextBuilder(),
                new CopilotToolExecutor(),
                _ => Provider,
                new EmptyExternalToolProvider(),
                capabilities,
                new CopilotAgentSkillUsageStore(_directory.FullName));
            _request = new CopilotAgentRequest
            {
                ConversationId = "final-answer-steering-conversation",
                TaskId = "final-answer-steering-task",
                WorkspacePath = _directory.FullName,
                UserText = "Give a short explanation using the supplied evidence.",
                TaskIntentText = "Give a short explanation using the supplied evidence.",
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
                HarnessFeatures = CopilotAgentHarnessFeatures.None,
                RunBudgetOverride = new CopilotAgentRunBudgetOverride
                {
                    RequestTokenBudget = 32_768,
                    MaxToolCalls = 1,
                    MaxAgentPasses = 1,
                    TotalDuration = TimeSpan.FromSeconds(30),
                },
            };
        }

        public FinalAnswerProbeChatClient Provider { get; }
        public ProbeTool Tool { get; } = new();
        public ConcurrentQueue<CopilotAgentEvent> Events { get; } = new();

        public Task<CopilotAgentRunResult> Start() => _runtime.RunAsync(_request, Events.Enqueue, _cancellation.Token);

        public void EnqueueSteering()
        {
            Assert.True(_runtime.EnqueueSteeringMessage(_request.TaskId, FirstSteering).IsAccepted);
            Assert.True(_runtime.EnqueueSteeringMessage(_request.TaskId, SecondSteering).IsAccepted);
        }

        public void AssertFinalAnswerHasNoTools()
        {
            Assert.Equal(1, Provider.FinalAnswerCalls);
            Assert.Equal(0, Provider.FinalAnswerToolCount);
            Assert.Contains(Events, item => item.Type == CopilotAgentEventType.AnswerDelta && item.Text == FinalAnswerProbeChatClient.FinalAnswer);
        }

        public void Dispose()
        {
            _cancellation.Cancel();
            Provider.ReleaseFirstCall.TrySetResult();
            _cancellation.Dispose();
            _directory.Delete(recursive: true);
        }
    }

    private sealed class EmptyExternalToolProvider : ICopilotExternalToolProvider
    {
        public Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CopilotExternalToolLease());
        }
    }

    private sealed class ProbeTool : ICopilotAgentDrivenTool
    {
        public string Name => "SteeringProbe";
        public string Description => "Returns deterministic read-only evidence.";
        public int CallCount { get; private set; }
        public bool CanHandle(CopilotAgentRequest request) => true;
        public bool IsAvailable(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(new CopilotToolResult { ToolName = Name, Success = true, Summary = "The probe completed." });
        }
    }

    private sealed class FinalAnswerProbeChatClient(bool lengthLimited, bool exhaustToolBudget) : IChatClient
    {
        public const string FinalAnswer = "The final answer used only supplied evidence.";
        public TaskCompletionSource FirstCallStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstCall { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int StreamingCalls { get; private set; }
        public int FinalAnswerCalls { get; private set; }
        public int FinalAnswerToolCount { get; private set; }
        public string LastStreamingInput { get; private set; } = string.Empty;
        public string FinalAnswerInput { get; private set; } = string.Empty;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FinalAnswerCalls++;
            FinalAnswerToolCount = options?.Tools?.Count ?? 0;
            FinalAnswerInput = string.Join("\n", messages.Select(message => message.Text)) + "\n" + options?.Instructions;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, FinalAnswer)) { FinishReason = ChatFinishReason.Stop });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastStreamingInput = string.Join("\n", messages.Select(message => message.Text));
            var call = ++StreamingCalls;
            Assert.InRange(call, 1, exhaustToolBudget ? 1 : 2);
            if (call == 1)
            {
                FirstCallStarted.TrySetResult();
                await ReleaseFirstCall.Task.WaitAsync(cancellationToken);
            }

            if (exhaustToolBudget)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant,
                [
                    new FunctionCallContent("probe-call-1", "colorvision_steering_probe", new Dictionary<string, object?>()),
                    new FunctionCallContent("probe-call-2", "colorvision_steering_probe", new Dictionary<string, object?>()),
                ]) { FinishReason = ChatFinishReason.ToolCalls };
            }
            else
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, call == 2 && lengthLimited ? "Partial answer" : string.Empty)
                {
                    FinishReason = call == 2 && lengthLimited ? ChatFinishReason.Length : ChatFinishReason.Stop,
                };
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
