using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using AIMessage = Microsoft.Extensions.AI.ChatMessage;

namespace ColorVision.Copilot.Tests;

public sealed partial class CopilotCodexTests
{
    [Fact]
    public async Task AlreadyCancelledCodexRequestDoesNotConnect()
    {
        var connections = 0;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var client = new CopilotCodexChatClient(Profile(), _ =>
        {
            connections++;
            return Task.FromResult<ICopilotCodexAppServer>(new CancellationBoundaryServer());
        });

        await using var stream = client.GetStreamingResponseAsync([new AIMessage(ChatRole.User, "test")],
            cancellationToken: cancellation.Token).GetAsyncEnumerator();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stream.MoveNextAsync().AsTask());
        Assert.Equal(0, connections);
    }

    [Theory]
    [InlineData("connect", 0)]
    [InlineData("account/read", 1)]
    [InlineData("thread/start", 2)]
    [InlineData("thread/inject_items", 3)]
    [InlineData("turn/start", 4)]
    public async Task SuccessfulSetupReturnAfterCancellationCannotContinueCodexStartup(string cancelledStage, int expectedRequests)
    {
        using var cancellation = new CancellationTokenSource();
        var server = new CancellationBoundaryServer(CodexAnswer, CodexCompleted)
        {
            OnRequest = method => { if (method == cancelledStage) cancellation.Cancel(); },
        };
        using var client = new CopilotCodexChatClient(Profile(), _ =>
        {
            if (cancelledStage == "connect") cancellation.Cancel();
            return Task.FromResult<ICopilotCodexAppServer>(server);
        });
        AIMessage[] messages = [new(ChatRole.User, "earlier"), new(ChatRole.Assistant, "previous answer"), new(ChatRole.User, "test")];

        await using var stream = client.GetStreamingResponseAsync(messages,
            cancellationToken: cancellation.Token).GetAsyncEnumerator();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stream.MoveNextAsync().AsTask());

        Assert.Equal(new[] { "account/read", "thread/start", "thread/inject_items", "turn/start" }.Take(expectedRequests), server.Requests);
        Assert.Equal(0, server.ReadCount);
        Assert.True(server.DisposedAsync);
        Assert.False(server.DisposedSynchronously);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CancelledCodexNotificationCannotGenerateTextOrTool(bool tool, bool cancelDuringRead)
    {
        using var cancellation = new CancellationTokenSource();
        var notification = tool
            ? """{"method":"item/tool/call","params":{"tool":"colorvision_test","callId":"call","arguments":{"value":"after cancellation"}}}"""
            : CodexAnswer;
        var server = new CancellationBoundaryServer(CodexUsage(10, 2, 4), notification, CodexCompleted)
        {
            InitialSignedIn = true,
            OnRead = count => { if (cancelDuringRead && count == 2) cancellation.Cancel(); },
        };
        using var client = new CopilotCodexChatClient(Profile(), _ => Task.FromResult<ICopilotCodexAppServer>(server));
        var options = new ChatOptions { Tools = [AIFunctionFactory.Create((string value) => value, "colorvision_test")] };
        await using var stream = client.GetStreamingResponseAsync([new AIMessage(ChatRole.User, "test")], options,
            cancellation.Token).GetAsyncEnumerator();

        Assert.True(await stream.MoveNextAsync());
        Assert.IsType<UsageContent>(Assert.Single(stream.Current.Contents));
        if (!cancelDuringRead) cancellation.Cancel();
        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stream.MoveNextAsync().AsTask());

        Assert.Equal(new CopilotTokenUsage(10, 2, 12, 4), CopilotTokenBudgetChatClient.ExtractPayloadFailureUsage(failure));
        Assert.Equal(cancelDuringRead ? 2 : 1, server.ReadCount);
        Assert.True(server.DisposedAsync);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public async Task CancellationBetweenCompletedItemsStopsFallbackTextAndStop(bool identifiedItems, int itemCount)
    {
        using var cancellation = new CancellationTokenSource();
        var items = new JsonArray();
        for (var index = 0; index < itemCount; index++)
        {
            var item = new JsonObject { ["type"] = "agentMessage", ["text"] = index == 0 ? "first" : "second" };
            if (identifiedItems) item["id"] = "item-" + index;
            items.Add(item);
        }
        var completed = new JsonObject
        {
            ["method"] = "turn/completed",
            ["params"] = new JsonObject { ["turn"] = new JsonObject { ["status"] = "completed", ["items"] = items } },
        };
        var server = new CancellationBoundaryServer(CodexUsage(10, 2, 4), completed.ToJsonString()) { InitialSignedIn = true };
        using var client = new CopilotCodexChatClient(Profile(), _ => Task.FromResult<ICopilotCodexAppServer>(server));
        await using var stream = client.GetStreamingResponseAsync([new AIMessage(ChatRole.User, "test")],
            cancellationToken: cancellation.Token).GetAsyncEnumerator();

        Assert.True(await stream.MoveNextAsync());
        Assert.IsType<UsageContent>(Assert.Single(stream.Current.Contents));
        Assert.True(await stream.MoveNextAsync());
        Assert.Equal("first", stream.Current.Text);
        cancellation.Cancel();
        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stream.MoveNextAsync().AsTask());

        // Usage already accompanied visible content; do not reattach the preamble on cancellation.
        Assert.Equal(CopilotTokenUsage.Empty, CopilotTokenBudgetChatClient.ExtractPayloadFailureUsage(failure));
        Assert.Equal(2, server.ReadCount);
        Assert.True(server.DisposedAsync);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    public async Task CodexStartupUsesOnlyTheCurrentConnectionsAccountSnapshot(bool? initialSignedIn, bool fallbackSignedIn, bool expectedSignedIn)
    {
        var server = new CancellationBoundaryServer(CodexCompleted)
        {
            InitialSignedIn = initialSignedIn,
            FallbackSignedIn = fallbackSignedIn,
        };
        using var client = new CopilotCodexChatClient(Profile(), _ => Task.FromResult<ICopilotCodexAppServer>(server));
        var response = client.GetResponseAsync([new AIMessage(ChatRole.User, "test")]);

        if (expectedSignedIn)
            Assert.Equal(ChatFinishReason.Stop, (await response).FinishReason);
        else
            Assert.Contains("尚未登录", (await Assert.ThrowsAsync<InvalidOperationException>(() => response)).Message);

        Assert.Equal(initialSignedIn.HasValue ? 0 : 1, server.Requests.Count(method => method == "account/read"));
        Assert.Equal(expectedSignedIn ? 1 : 0, server.Requests.Count(method => method == "turn/start"));
        Assert.True(server.DisposedAsync);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task CodexAccountSnapshotIsNotReusedAcrossConnections(bool? secondSnapshot)
    {
        var first = new CancellationBoundaryServer(CodexCompleted) { InitialSignedIn = true };
        var second = new CancellationBoundaryServer(CodexCompleted) { InitialSignedIn = secondSnapshot, FallbackSignedIn = false };
        var connections = new Queue<ICopilotCodexAppServer>([first, second]);
        using var client = new CopilotCodexChatClient(Profile(), _ => Task.FromResult(connections.Dequeue()));

        await client.GetResponseAsync([new AIMessage(ChatRole.User, "first")]);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetResponseAsync([new AIMessage(ChatRole.User, "second")]));

        Assert.Contains("尚未登录", failure.Message);
        Assert.Empty(connections);
        Assert.DoesNotContain("account/read", first.Requests);
        Assert.Equal(secondSnapshot.HasValue ? Array.Empty<string>() : new[] { "account/read" }, second.Requests);
        Assert.True(first.DisposedAsync);
        Assert.True(second.DisposedAsync);
    }

    private sealed class CancellationBoundaryServer(params string[] notifications) : ICopilotCodexAppServer
    {
        private readonly Queue<string> _notifications = new(notifications);
        public bool? InitialSignedIn { get; init; }
        public bool FallbackSignedIn { get; init; } = true;
        public Action<string>? OnRequest { get; init; }
        public Action<int>? OnRead { get; init; }
        public List<string> Requests { get; } = [];
        public int ReadCount { get; private set; }
        public bool DisposedAsync { get; private set; }
        public bool DisposedSynchronously { get; private set; }

        public Task<JsonNode> RequestAsync(string method, object parameters, CancellationToken cancellationToken)
        {
            // Deliberately return successful completion even when cancellation wins at this boundary.
            Requests.Add(method);
            OnRequest?.Invoke(method);
            return Task.FromResult(JsonNode.Parse(method switch
            {
                "account/read" => FallbackSignedIn ? """{"account":{"type":"chatgpt"}}""" : """{"account":null}""",
                "thread/start" => """{"thread":{"id":"thread"}}""",
                "turn/start" => """{"turn":{"id":"turn"}}""",
                _ => "{}",
            })!);
        }

        public Task<JsonObject> ReadAsync(CancellationToken cancellationToken)
        {
            ReadCount++;
            OnRead?.Invoke(ReadCount);
            return Task.FromResult(JsonNode.Parse(_notifications.Dequeue())!.AsObject());
        }

        public void Dispose() => DisposedSynchronously = true;

        public ValueTask DisposeAsync()
        {
            DisposedAsync = true;
            return ValueTask.CompletedTask;
        }
    }
}
