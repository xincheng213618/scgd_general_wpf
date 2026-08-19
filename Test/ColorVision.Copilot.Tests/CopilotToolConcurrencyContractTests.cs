using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ColorVision.Copilot;
using Microsoft.Extensions.AI;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotToolConcurrencyContractTests
{
    [Fact]
    public async Task ReadGateBoundsParallelism()
    {
        var gate = new CopilotToolExecutionGate(maximumConcurrentReads: 2);
        var resourceKeys = CreateDistinctResourceStripeKeys(3);
        using var first = await gate.AcquireAsync(
            CopilotToolConcurrencyMode.SharedRead,
            resourceKeys[0],
            CancellationToken.None);
        using var second = await gate.AcquireAsync(
            CopilotToolConcurrencyMode.SharedRead,
            resourceKeys[1],
            CancellationToken.None);

        var thirdTask = gate.AcquireAsync(
            CopilotToolConcurrencyMode.SharedRead,
            resourceKeys[2],
            CancellationToken.None).AsTask();

        await Task.Delay(50);
        Assert.False(thirdTask.IsCompleted);

        first.Dispose();
        using var third = await thirdTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private static string[] CreateDistinctResourceStripeKeys(int count)
    {
        var keysByStripe = new Dictionary<int, string>();
        for (var index = 0; keysByStripe.Count < count; index++)
        {
            var candidate = $"resource-{index}";
            var hash = StringComparer.OrdinalIgnoreCase.GetHashCode(candidate) & int.MaxValue;
            keysByStripe.TryAdd(hash % CopilotToolExecutionGate.ResourceStripeCount, candidate);
        }
        return keysByStripe.Values.Take(count).ToArray();
    }

    [Fact]
    public async Task ExclusiveGateWaitsForReadersAndBlocksNewReaders()
    {
        var gate = new CopilotToolExecutionGate(maximumConcurrentReads: 2);
        using var activeReader = await gate.AcquireAsync(
            CopilotToolConcurrencyMode.SharedRead,
            "resource-a",
            CancellationToken.None);

        var writerTask = gate.AcquireAsync(
            CopilotToolConcurrencyMode.Exclusive,
            string.Empty,
            CancellationToken.None).AsTask();
        await Task.Delay(50);
        Assert.False(writerTask.IsCompleted);

        activeReader.Dispose();
        using var writer = await writerTask.WaitAsync(TimeSpan.FromSeconds(2));
        var laterReaderTask = gate.AcquireAsync(
            CopilotToolConcurrencyMode.SharedRead,
            "resource-b",
            CancellationToken.None).AsTask();
        await Task.Delay(50);
        Assert.False(laterReaderTask.IsCompleted);

        writer.Dispose();
        using var laterReader = await laterReaderTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ConcurrentFunctionResultsRemainInProviderCallOrder()
    {
        var slowStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSlow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completionOrder = new ConcurrentQueue<string>();

        async Task<string> SlowAsync()
        {
            slowStarted.TrySetResult();
            await releaseSlow.Task.WaitAsync(TimeSpan.FromSeconds(2));
            completionOrder.Enqueue("slow-call");
            return "slow-result";
        }

        async Task<string> FastAsync()
        {
            await slowStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            completionOrder.Enqueue("fast-call");
            releaseSlow.TrySetResult();
            return "fast-result";
        }

        using var inner = new ToolOrderingChatClient();
        using var client = new FunctionInvokingChatClient(
            inner,
            loggerFactory: null,
            functionInvocationServices: null)
        {
            AllowConcurrentInvocation = true,
        };
        var options = new ChatOptions
        {
            Tools =
            [
                AIFunctionFactory.Create(SlowAsync, "slow_tool"),
                AIFunctionFactory.Create(FastAsync, "fast_tool"),
            ],
        };

        await foreach (var _ in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "Run both tools.")],
            options))
        {
        }

        Assert.Equal(["fast-call", "slow-call"], completionOrder);
        Assert.Equal(["slow-call", "fast-call"], inner.ResultCallIds);
    }

    [Fact]
    public async Task ConcurrentFunctionFailureWaitsForStartedSibling()
    {
        var siblingStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var failureStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSibling = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<string> SlowAsync()
        {
            siblingStarted.TrySetResult();
            await releaseSibling.Task.WaitAsync(TimeSpan.FromSeconds(2));
            return "slow-result";
        }

        async Task<string> FailAsync()
        {
            await siblingStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            failureStarted.TrySetResult();
            throw new InvalidOperationException("expected tool failure");
        }

        using var inner = new ToolOrderingChatClient();
        using var client = new FunctionInvokingChatClient(
            inner,
            loggerFactory: null,
            functionInvocationServices: null)
        {
            AllowConcurrentInvocation = true,
        };
        var options = new ChatOptions
        {
            Tools =
            [
                AIFunctionFactory.Create(SlowAsync, "slow_tool"),
                AIFunctionFactory.Create(FailAsync, "fast_tool"),
            ],
        };

        var runTask = DrainAsync(client, options);
        await failureStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(50);

        Assert.False(runTask.IsCompleted);
        Assert.Equal(1, inner.CallCount);

        releaseSibling.TrySetResult();
        await runTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(["slow-call", "fast-call"], inner.ResultCallIds);
    }

    private static async Task DrainAsync(
        FunctionInvokingChatClient client,
        ChatOptions options)
    {
        await foreach (var _ in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "Run both tools.")],
            options))
        {
        }
    }

    private sealed class ToolOrderingChatClient : IChatClient
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public IReadOnlyList<string> ResultCallIds { get; private set; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("This contract test exercises the streaming path.");

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var call = Interlocked.Increment(ref _callCount);
            await Task.CompletedTask;
            if (call == 1)
            {
                yield return new ChatResponseUpdate(
                    ChatRole.Assistant,
                    [
                        new FunctionCallContent("slow-call", "slow_tool", new Dictionary<string, object?>()),
                        new FunctionCallContent("fast-call", "fast_tool", new Dictionary<string, object?>()),
                    ])
                {
                    FinishReason = ChatFinishReason.ToolCalls,
                };
                yield break;
            }

            ResultCallIds = messages
                .SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>()
                .Select(result => result.CallId)
                .ToArray();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Both tools finished.")
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
