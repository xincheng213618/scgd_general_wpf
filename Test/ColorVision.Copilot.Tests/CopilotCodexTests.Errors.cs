using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIMessage = Microsoft.Extensions.AI.ChatMessage;

namespace ColorVision.Copilot.Tests;

public sealed partial class CopilotCodexTests
{
    private const string CodexErrorSecret = "sk-private-test-token https://private.example/path?token=secret";

    [Theory]
    [InlineData("\"contextWindowExceeded\"", "context_length_exceeded", false, true)]
    [InlineData("\"ContextWindowExceeded\"", "context_length_exceeded", false, true)]
    [InlineData("\"rateLimitExceeded\"", "rate_limit_exceeded", true, false)]
    [InlineData("\"serverOverloaded\"", "overloaded_error", true, false)]
    [InlineData("\"internalServerError\"", "server_error", true, false)]
    [InlineData("\"flexUnavailable\"", "service_unavailable", true, false)]
    [InlineData("\"usageLimitExceeded\"", "insufficient_quota", false, false)]
    [InlineData("\"sessionBudgetExceeded\"", "insufficient_quota", false, false)]
    [InlineData("\"unauthorized\"", "authentication_error", false, false)]
    [InlineData("\"badRequest\"", "invalid_request_error", false, false)]
    [InlineData("\"cyberPolicy\"", "permission_error", false, false)]
    [InlineData("\"sk-private-test-token\"", "codex_error", false, false)]
    [InlineData("{\"httpConnectionFailed\":{\"httpStatusCode\":null}}", "connection_error", true, false)]
    [InlineData("{\"responseStreamConnectionFailed\":{\"httpStatusCode\":408}}", "timeout_error", true, false)]
    [InlineData("{\"responseStreamDisconnected\":{\"httpStatusCode\":429}}", "rate_limit_exceeded", true, false)]
    [InlineData("{\"responseTooManyFailedAttempts\":{\"httpStatusCode\":503}}", "server_error", true, false)]
    [InlineData("{\"HttpConnectionFailed\":{\"httpStatusCode\":401}}", "authentication_error", false, false)]
    [InlineData("{\"httpConnectionFailed\":{\"httpStatusCode\":403}}", "permission_error", false, false)]
    [InlineData("{\"httpConnectionFailed\":{\"httpStatusCode\":400}}", "invalid_request_error", false, false)]
    [InlineData("{\"httpConnectionFailed\":{\"httpStatusCode\":\"secret\"}}", "codex_error", false, false)]
    [InlineData("{\"futureSecretVariant\":{\"httpStatusCode\":503}}", "codex_error", false, false)]
    public async Task StructuredCodexFailuresUseOnlySafeCodesForRecovery(
        string info, string expectedCode, bool transient, bool contextFailure)
    {
        var server = new FakeServer(CodexUsage(10, 2, 4), CodexUsage(16, 4, 6), CodexFailure(info));
        using var client = new CopilotCodexChatClient(Profile(), _ => Task.FromResult<ICopilotCodexAppServer>(server));

        var error = await Assert.ThrowsAsync<CopilotProviderPayloadException>(() =>
            client.GetResponseAsync([new AIMessage(ChatRole.User, "test")]));

        Assert.Equal(expectedCode, error.ErrorCode);
        Assert.Equal(transient, CopilotProviderRetryChatClient.TryClassifyTransientFailure(error, CancellationToken.None, out _, out _));
        Assert.Equal(contextFailure, CopilotContextWindowFailureClassifier.TryClassify(error, out _));
        Assert.False(CopilotProviderConnectionRecoveryChatClient.IsConnectionFailure(error, CancellationToken.None));
        Assert.Equal(new CopilotTokenUsage(16, 4, 20, 6), CopilotTokenBudgetChatClient.ExtractPayloadFailureUsage(error));
        Assert.DoesNotContain("sk-private-test-token", error.ToString());
        Assert.DoesNotContain("private.example", error.ToString());
        Assert.True(server.Disposed);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task CodexPreamblePreservesServerRetryAndCancellationUsage(bool cancel, bool serverRetry)
    {
        var messages = new List<string> { CodexUsage(10, 2, 4) };
        if (serverRetry)
            messages.Add(CodexErrorNotification("\"serverOverloaded\"", willRetry: true));
        if (!cancel)
            messages.AddRange([CodexUsage(16, 4, 6), CodexAnswer, CodexCompleted]);
        var server = new FakeServer(messages.ToArray());
        var servers = new Queue<FakeServer>([server]);
        var retries = new List<CopilotProviderRetryInfo>();
        var recoveries = new List<CopilotContextWindowRecoveryInfo>();
        using var client = CreateCodexErrorPipeline(servers, retries, recoveries, out var budget);
        using var cancellation = new CancellationTokenSource(TestTimeout);
        var updates = new List<ChatResponseUpdate>();
        var reading = ReadCodexUpdatesAsync(client, [new AIMessage(ChatRole.User, "test")], updates, cancellation.Token);

        if (cancel)
        {
            await server.WaitingForMessage.Task.WaitAsync(TestTimeout);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading);
            await server.DisposedSignal.Task.WaitAsync(TestTimeout);
        }
        else
        {
            await reading;
            Assert.Equal("answer", string.Concat(updates.SelectMany(update => update.Contents).OfType<TextContent>().Select(content => content.Text)));
        }

        Assert.Empty(retries);
        Assert.Empty(recoveries);
        Assert.Equal(1, budget.Snapshot.ProviderCalls);
        Assert.Equal(cancel ? 12 : 20, budget.Snapshot.ReportedTotalTokens);
        Assert.Single(server.Requests, request => request.Method == "thread/start");
        Assert.Single(server.Requests, request => request.Method == "turn/start");
        Assert.True(server.Disposed);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task TerminalCodexErrorsUseBoundedRetryAndCountEachAttemptOnce(bool streaming, bool exhaustRetries)
    {
        FakeServer FailedAttempt() => new(CodexUsage(10, 2, 4),
            CodexErrorNotification("\"rateLimitExceeded\"", willRetry: false),
            CodexUsage(16, 4, 6), CodexUsage(16, 4, 6), CodexFailure(null));
        var attempts = exhaustRetries
            ? new[] { FailedAttempt(), FailedAttempt(), FailedAttempt() }
            : new[] { FailedAttempt(), new FakeServer(CodexUsage(5, 2, 1), CodexAnswer, CodexCompleted) };
        var servers = new Queue<FakeServer>(attempts);
        var retries = new List<CopilotProviderRetryInfo>();
        var recoveries = new List<CopilotContextWindowRecoveryInfo>();
        using var client = CreateCodexErrorPipeline(servers, retries, recoveries, out var budget);
        using var cancellation = new CancellationTokenSource(TestTimeout);
        var updates = new List<ChatResponseUpdate>();
        ChatResponse? response = null;
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (streaming)
                await ReadCodexUpdatesAsync(client, [new AIMessage(ChatRole.User, "test")], updates, cancellation.Token);
            else
                response = await client.GetResponseAsync([new AIMessage(ChatRole.User, "test")], cancellationToken: cancellation.Token);
        });

        if (exhaustRetries)
            Assert.Equal("rate_limit_exceeded", Assert.IsType<CopilotProviderPayloadException>(failure).ErrorCode);
        else
            Assert.Null(failure);
        var usage = streaming ? SumCodexUpdates(updates)
            : failure != null ? CopilotProviderRetryChatClient.ExtractFailureUsage(failure)
            : CopilotTokenBudgetChatClient.ExtractResponseUsage(response!);
        Assert.Equal(exhaustRetries ? 60 : 27, usage.EffectiveTotalTokens);
        Assert.Equal(exhaustRetries ? 18 : 7, usage.CachedInputTokens);
        Assert.Equal(usage.EffectiveTotalTokens, budget.Snapshot.ReportedTotalTokens);
        Assert.Equal(attempts.Length, budget.Snapshot.ProviderCalls);
        Assert.Equal(attempts.Length - 1, retries.Count);
        Assert.Empty(recoveries);
        Assert.Empty(servers);
        Assert.All(attempts, attempt => Assert.True(attempt.Disposed));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CodexContextRejectionCompactsOnceWithoutTransientRetries(bool streaming, bool rejectAgain)
    {
        var first = new FakeServer(CodexUsage(10, 2, 4), CodexUsage(16, 4, 6), CodexUsage(16, 4, 6), CodexFailure("\"contextWindowExceeded\""));
        var second = rejectAgain
            ? new FakeServer(CodexUsage(16, 4, 6), CodexFailure("\"contextWindowExceeded\""))
            : new FakeServer(CodexUsage(5, 2, 1), CodexAnswer, CodexCompleted);
        var servers = new Queue<FakeServer>([first, second]);
        var retries = new List<CopilotProviderRetryInfo>();
        var recoveries = new List<CopilotContextWindowRecoveryInfo>();
        using var client = CreateCodexErrorPipeline(servers, retries, recoveries, out var budget);
        using var cancellation = new CancellationTokenSource(TestTimeout);
        var history = CodexRecoveryHistory();
        var updates = new List<ChatResponseUpdate>();
        ChatResponse? response = null;
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (streaming)
                await ReadCodexUpdatesAsync(client, history, updates, cancellation.Token);
            else
                response = await client.GetResponseAsync(history, cancellationToken: cancellation.Token);
        });

        if (rejectAgain)
            Assert.IsType<CopilotAgentContextWindowRecoveryExhaustedException>(failure);
        else
            Assert.Null(failure);
        Assert.Single(recoveries);
        Assert.Empty(retries);
        Assert.Equal(2, budget.Snapshot.ProviderCalls);
        var usage = streaming ? SumCodexUpdates(updates)
            : failure != null ? CopilotProviderRetryChatClient.ExtractFailureUsage(failure)
            : CopilotTokenBudgetChatClient.ExtractResponseUsage(response!);
        Assert.Equal(rejectAgain ? 40 : 27, usage.EffectiveTotalTokens);
        Assert.Equal(usage.EffectiveTotalTokens, budget.Snapshot.ReportedTotalTokens);
        Assert.True(Assert.Single(second.Requests, request => request.Method == "thread/inject_items").Parameters["items"]!.AsArray().Count
            < Assert.Single(first.Requests, request => request.Method == "thread/inject_items").Parameters["items"]!.AsArray().Count);
        Assert.Equal(11, history.Length);
        Assert.True(first.Disposed && second.Disposed);
        Assert.Empty(servers);
    }

    [Theory]
    [InlineData(false, "\"rateLimitExceeded\"")]
    [InlineData(true, "\"rateLimitExceeded\"")]
    [InlineData(false, "\"contextWindowExceeded\"")]
    [InlineData(true, "\"contextWindowExceeded\"")]
    public async Task CodexFailureAfterVisibleProgressDoesNotReplayOrDuplicateUsage(bool reasoning, string info)
    {
        var progress = reasoning
            ? """{"method":"item/reasoning/summaryTextDelta","params":{"threadId":"thread","turnId":"turn","delta":"progress"}}"""
            : CodexAnswer;
        var first = new FakeServer(progress, CodexUsage(16, 4, 6), CodexFailure(info));
        var unused = new FakeServer(CodexAnswer, CodexCompleted);
        var servers = new Queue<FakeServer>([first, unused]);
        var retries = new List<CopilotProviderRetryInfo>();
        var recoveries = new List<CopilotContextWindowRecoveryInfo>();
        using var client = CreateCodexErrorPipeline(servers, retries, recoveries, out var budget);
        using var cancellation = new CancellationTokenSource(TestTimeout);
        var updates = new List<ChatResponseUpdate>();

        await Assert.ThrowsAsync<CopilotProviderPayloadException>(() => ReadCodexUpdatesAsync(client, CodexRecoveryHistory(), updates, cancellation.Token));

        Assert.Single(updates, CopilotProviderResponseContent.HasProgress);
        Assert.Equal(20, SumCodexUpdates(updates).EffectiveTotalTokens);
        Assert.Equal(20, budget.Snapshot.ReportedTotalTokens);
        Assert.Equal(1, budget.Snapshot.ProviderCalls);
        Assert.Empty(retries);
        Assert.Empty(recoveries);
        Assert.Same(unused, Assert.Single(servers));
        Assert.True(first.Disposed);
    }

    private const string CodexAnswer = """{"method":"item/agentMessage/delta","params":{"threadId":"thread","turnId":"turn","delta":"answer"}}""";
    private const string CodexCompleted = """{"method":"turn/completed","params":{"threadId":"thread","turn":{"id":"turn","status":"completed"}}}""";

    private static string CodexUsage(int input, int output, int cached) => JsonSerializer.Serialize(new
    {
        method = "thread/tokenUsage/updated",
        @params = new { threadId = "thread", tokenUsage = new { total = new { inputTokens = input, outputTokens = output, totalTokens = input + output, cachedInputTokens = cached } } },
    });

    private static object? CodexError(string? info) => info == null ? null : new
    {
        message = "请求未完成。" + CodexErrorSecret + " context_length_exceeded server_error",
        additionalDetails = CodexErrorSecret,
        codexErrorInfo = JsonNode.Parse(info),
    };

    private static string CodexFailure(string? info) => JsonSerializer.Serialize(new
    {
        method = "turn/completed",
        @params = new { threadId = "thread", turn = new { id = "turn", status = "failed", error = CodexError(info) } },
    });

    private static string CodexErrorNotification(string info, bool willRetry) => JsonSerializer.Serialize(new
    {
        method = "error",
        @params = new { threadId = "thread", turnId = "turn", error = CodexError(info), willRetry },
    });

    private static AIMessage[] CodexRecoveryHistory() => Enumerable.Range(0, 11)
        .Select(index => new AIMessage(index % 2 == 0 ? ChatRole.User : ChatRole.Assistant,
            $"original-{index} " + new string((char)('a' + index), 2_000))).ToArray();

    private static CopilotTokenUsage SumCodexUpdates(IEnumerable<ChatResponseUpdate> updates) =>
        updates.Aggregate(CopilotTokenUsage.Empty, (usage, update) => usage.Add(CopilotTokenBudgetChatClient.ExtractUsage(update.Contents)));

    private static async Task ReadCodexUpdatesAsync(IChatClient client, AIMessage[] messages, List<ChatResponseUpdate> updates, CancellationToken token)
    {
        await foreach (var update in client.GetStreamingResponseAsync(messages, cancellationToken: token))
            updates.Add(update);
    }

    private static CopilotContextWindowRecoveryChatClient CreateCodexErrorPipeline(
        Queue<FakeServer> servers, List<CopilotProviderRetryInfo> retries,
        List<CopilotContextWindowRecoveryInfo> recoveries, out CopilotTokenBudgetChatClient budgetClient)
    {
        var provider = new CopilotCodexChatClient(Profile(), _ => Task.FromResult<ICopilotCodexAppServer>(servers.Dequeue()));
        // Keep the production order, including the preamble-buffering connection recovery layer.
        var connection = new CopilotProviderConnectionRecoveryChatClient(
            new CopilotProviderInactivityChatClient(new CopilotCancellationGuardChatClient(provider)),
            delayAsync: (_, _) => throw new InvalidOperationException("A terminal Codex error must use bounded provider retry."));
        var budget = new CopilotAgentTokenBudget { ContextWindowTokens = 65_536, MaxOutputTokens = 128, RequestTokenBudget = 131_072 };
        budgetClient = new CopilotTokenBudgetChatClient(connection, budget);
        var retry = new CopilotProviderRetryChatClient(budgetClient, retries.Add,
            delayFactory: _ => TimeSpan.Zero, delayAsync: (_, _) => Task.CompletedTask);
        return new CopilotContextWindowRecoveryChatClient(retry, budget.InputBudgetTokens, recoveries.Add);
    }
}
