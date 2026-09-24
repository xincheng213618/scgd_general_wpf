using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using AIMessage = Microsoft.Extensions.AI.ChatMessage;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexTests
{
    private static CopilotProfileConfig Profile() => new()
    {
        ProviderType = CopilotProviderType.LocalCodex,
        VendorType = CopilotVendorType.OpenAI,
        BaseUrl = string.Empty,
        ApiKey = string.Empty,
        Model = string.Empty,
    };

    [Fact]
    public void LocalProfileNeedsNoKeyOrEndpointAndRoundTrips()
    {
        var profile = Profile();
        Assert.True(profile.IsConfigured);
        Assert.False(CopilotProfileConfig.CreateDefault().IsConfigured);
        var restored = Newtonsoft.Json.JsonConvert.DeserializeObject<CopilotProfileConfig>(Newtonsoft.Json.JsonConvert.SerializeObject(profile));
        Assert.True(restored!.Clone().IsLocalCodex);
        Assert.True(restored.IsConfigured);
        Assert.Empty(restored.ApiKey);
        Assert.Empty(restored.BaseUrl);
        Assert.Contains("默认模型", restored.SecondaryLabel);
        using var client = CopilotMicrosoftAgentFrameworkRuntime.CreateChatClient(restored);
        Assert.IsType<CopilotCodexChatClient>(client);
    }

    [Fact]
    public void FindsDesktopRuntimeWithoutPathOrNpmAndIgnoresRelativePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "ColorVision-CodexLocator-" + Guid.NewGuid().ToString("N"));
        var desktop = Path.Combine(root, "OpenAI", "Codex", "bin", "release-hash", "codex.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(desktop)!);
        File.WriteAllBytes(desktop, []);
        try
        {
            Assert.Equal([desktop], CopilotCodexRuntimeLocator.FindCandidates(root, root, ".;relative;" + Path.GetDirectoryName(desktop), root));
            Assert.Empty(CopilotCodexRuntimeLocator.FindCandidates(Path.Combine(root, "absent"), root, ".;relative", root));
        }
        finally { File.Delete(desktop); }
    }

    [Fact]
    public void FindsNpmNativeBinaryWithoutExecutingPowerShellShim()
    {
        var root = Path.Combine(Path.GetTempPath(), "ColorVision-CodexLocator-" + Guid.NewGuid().ToString("N"));
        var native = Path.Combine(root, "npm", "node_modules", "@openai", "codex", "node_modules", "@openai", "codex-win32-x64", "vendor", "x86_64-pc-windows-msvc", "bin", "codex.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(native)!);
        File.WriteAllBytes(native, []);
        try { Assert.Equal([native], CopilotCodexRuntimeLocator.FindCandidates(root, root, string.Empty, root)); }
        finally { File.Delete(native); }
    }

    [Fact]
    public async Task StreamsReplyAndUsageThroughExistingChatContract()
    {
        var server = new FakeServer(
            """{"method":"item/agentMessage/delta","params":{"threadId":"thread","turnId":"turn","delta":"你好"}}""",
            """{"method":"thread/tokenUsage/updated","params":{"threadId":"thread","tokenUsage":{"total":{"inputTokens":10,"outputTokens":2,"totalTokens":12,"cachedInputTokens":4}}}}""",
            """{"method":"turn/completed","params":{"threadId":"thread","turn":{"id":"turn","status":"completed"}}}""");
        using var client = new CopilotCodexChatClient(Profile(), _ => Task.FromResult<ICopilotCodexAppServer>(server));
        var response = await client.GetResponseAsync([new AIMessage(ChatRole.User, "你好")]);
        Assert.Equal("你好", response.Text);
        Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
        Assert.Equal(12, response.Usage!.TotalTokenCount);
        Assert.True(server.Disposed);
        var thread = server.Requests.Single(r => r.Method == "thread/start").Parameters;
        Assert.True(thread["ephemeral"]!.GetValue<bool>());
        Assert.Equal("read-only", thread["sandbox"]!.GetValue<string>());
    }

    [Fact]
    public async Task ReturnsToolRequestToHostWithoutExecutingIt()
    {
        var executed = false;
        var function = AIFunctionFactory.Create((string value) => { executed = true; return value; }, "colorvision_test");
        var server = new FakeServer("""{"id":101,"method":"item/tool/call","params":{"threadId":"thread","turnId":"turn","callId":"call-1","tool":"colorvision_test","arguments":{"value":"sample"}}}""");
        using var client = new CopilotCodexChatClient(Profile(), _ => Task.FromResult<ICopilotCodexAppServer>(server));
        var response = await client.GetResponseAsync([new AIMessage(ChatRole.User, "test")], new ChatOptions { Tools = [function] });
        var call = Assert.Single(response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>());
        Assert.Equal("call-1", call.CallId);
        Assert.Equal("colorvision_test", call.Name);
        Assert.Equal(ChatFinishReason.ToolCalls, response.FinishReason);
        Assert.False(executed);
        Assert.True(server.Disposed);
    }

    [Theory]
    [InlineData("""{"id":101,"method":"item/tool/call","params":{"tool":"unregistered","arguments":{}}}""")]
    [InlineData("""{"id":102,"method":"item/commandExecution/requestApproval","params":{}}""")]
    [InlineData("""{"method":"item/started","params":{"item":{"type":"commandExecution"}}}""")]
    [InlineData("""{"method":"turn/completed","params":{"turn":{"status":"failed"}}}""")]
    public async Task RefusesUnknownToolsIndependentApprovalsAndFailedTurns(string message)
    {
        var server = new FakeServer(message);
        using var client = new CopilotCodexChatClient(Profile(), _ => Task.FromResult<ICopilotCodexAppServer>(server));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetResponseAsync([new AIMessage(ChatRole.User, "test")]));
        Assert.True(server.Disposed);
    }

    [Fact]
    public async Task SignedOutAccountFailsBeforeCreatingThread()
    {
        var server = new FakeServer() { SignedIn = false };
        using var client = new CopilotCodexChatClient(Profile(), _ => Task.FromResult<ICopilotCodexAppServer>(server));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetResponseAsync([new AIMessage(ChatRole.User, "test")]));
        Assert.Contains("尚未登录", error.Message);
        Assert.DoesNotContain(server.Requests, r => r.Method == "thread/start");
        Assert.True(server.Disposed);
    }

    [Fact]
    public async Task CancellationDisposesOnlyOwnedServer()
    {
        var server = new FakeServer();
        using var client = new CopilotCodexChatClient(Profile(), _ => Task.FromResult<ICopilotCodexAppServer>(server));
        using var cancellation = new CancellationTokenSource();
        var response = client.GetResponseAsync([new AIMessage(ChatRole.User, "test")], cancellationToken: cancellation.Token);
        await server.Reading.Task;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => response);
        Assert.True(server.Disposed);
    }

    [Fact]
    public void HistoryPreservesRolesAndActualHostToolResults()
    {
        var history = CopilotCodexChatClient.BuildHistory([
            new AIMessage(ChatRole.User, "question"),
            new AIMessage(ChatRole.Assistant, [new FunctionCallContent("call", "colorvision_test", new Dictionary<string, object?> { ["value"] = "sample" })]),
            new AIMessage(ChatRole.Tool, [new FunctionResultContent("call", "actual host result")]),
        ]);
        Assert.Equal("user", history[0]!["role"]!.GetValue<string>());
        Assert.Equal("function_call", history[1]!["type"]!.GetValue<string>());
        Assert.Equal("call", history[2]!["call_id"]!.GetValue<string>());
        Assert.Equal("actual host result", history[2]!["output"]!.GetValue<string>());
    }

    private sealed class FakeServer(params string[] messages) : ICopilotCodexAppServer
    {
        private readonly Queue<string> _messages = new(messages);
        public bool SignedIn { get; init; } = true;
        public bool Disposed { get; private set; }
        public TaskCompletionSource Reading { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<(string Method, JsonNode Parameters)> Requests { get; } = [];
        public Task<JsonNode> RequestAsync(string method, object parameters, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add((method, JsonSerializer.SerializeToNode(parameters)!));
            return Task.FromResult(JsonNode.Parse(method switch
            {
                "account/read" => SignedIn ? """{"account":{"type":"chatgpt"}}""" : """{"account":null}""",
                "thread/start" => """{"thread":{"id":"thread"}}""",
                "turn/start" => """{"turn":{"id":"turn"}}""",
                _ => "{}",
            })!);
        }
        public async Task<JsonObject> ReadAsync(CancellationToken cancellationToken)
        {
            Reading.TrySetResult();
            if (_messages.TryDequeue(out var message)) return JsonNode.Parse(message)!.AsObject();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }
        public void Dispose() => Disposed = true;
    }
}
