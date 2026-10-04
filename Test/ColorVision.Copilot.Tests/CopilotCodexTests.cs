using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using AIMessage = Microsoft.Extensions.AI.ChatMessage;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

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
    public async Task CumulativeUsageSnapshotsAreCountedOnceThroughSdkResponseAggregation()
    {
        var server = new FakeServer(
            """{"method":"thread/tokenUsage/updated","params":{"threadId":"thread","tokenUsage":{"total":{"inputTokens":10,"outputTokens":2,"totalTokens":12,"cachedInputTokens":4}}}}""",
            """{"method":"item/agentMessage/delta","params":{"threadId":"thread","turnId":"turn","delta":"当前回复"}}""",
            """{"method":"thread/tokenUsage/updated","params":{"threadId":"thread","tokenUsage":{"total":{"inputTokens":16,"outputTokens":4,"totalTokens":20,"cachedInputTokens":6}}}}""",
            """{"method":"thread/tokenUsage/updated","params":{"threadId":"thread","tokenUsage":{"total":{"inputTokens":16,"outputTokens":4,"totalTokens":20,"cachedInputTokens":6}}}}""",
            """{"method":"turn/completed","params":{"threadId":"thread","turn":{"id":"turn","status":"completed"}}}""");
        using var client = new CopilotCodexChatClient(Profile(), _ => Task.FromResult<ICopilotCodexAppServer>(server));

        var response = await client.GetResponseAsync([new AIMessage(ChatRole.User, "test")]);

        Assert.Equal("当前回复", response.Text);
        Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
        Assert.Equal(20, response.Usage?.TotalTokenCount);
        Assert.Equal(16, response.Usage?.InputTokenCount);
        Assert.Equal(4, response.Usage?.OutputTokenCount);
        Assert.Equal(6, response.Usage?.CachedInputTokenCount);
        Assert.True(server.Disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrCancelledResponseRetainsOfficialCumulativeUsage(bool cancel)
    {
        string[] messages =
        [
            """{"method":"thread/tokenUsage/updated","params":{"threadId":"thread","tokenUsage":{"total":{"inputTokens":10,"outputTokens":2,"totalTokens":12,"cachedInputTokens":4}}}}""",
            """{"method":"thread/tokenUsage/updated","params":{"threadId":"thread","tokenUsage":{"total":{"inputTokens":16,"outputTokens":4,"totalTokens":20,"cachedInputTokens":6}}}}""",
            """{"method":"thread/tokenUsage/updated","params":{"threadId":"thread","tokenUsage":{"total":{"inputTokens":16,"outputTokens":4,"totalTokens":20,"cachedInputTokens":6}}}}""",
        ];
        if (!cancel)
            messages = [.. messages, """{"method":"turn/completed","params":{"threadId":"thread","turn":{"id":"turn","status":"failed"}}}"""];
        var server = new FakeServer(messages);
        using var client = new CopilotTokenBudgetChatClient(
            new CopilotCodexChatClient(Profile(), _ => Task.FromResult<ICopilotCodexAppServer>(server)),
            new CopilotAgentTokenBudget
            {
                ContextWindowTokens = CopilotAgentTokenBudget.MinimumContextWindowTokens,
                MaxOutputTokens = 1_024,
                RequestTokenBudget = 4_096,
            });
        using var cancellation = new CancellationTokenSource();
        var response = client.GetResponseAsync([new AIMessage(ChatRole.User, new string('x', 512))],
            cancellationToken: cancellation.Token);
        try
        {
            Exception failure;
            if (cancel)
            {
                await server.WaitingForMessage.Task.WaitAsync(TestTimeout);
                cancellation.Cancel();
                failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => response);
            }
            else
            {
                failure = await Assert.ThrowsAsync<InvalidOperationException>(() => response);
            }

            Assert.Equal(new CopilotTokenUsage(16, 4, 20, 6),
                CopilotTokenBudgetChatClient.ExtractPayloadFailureUsage(failure));
            Assert.Equal(20, client.Snapshot.ReportedTotalTokens);
            Assert.Equal(16, client.Snapshot.ReportedInputTokens);
            Assert.Equal(4, client.Snapshot.ReportedOutputTokens);
            Assert.Equal(6, client.Snapshot.ReportedCachedInputTokens);
            Assert.Equal(20, client.Snapshot.ConsumedTokens);
            Assert.False(client.Snapshot.UsedEstimatedUsage);
            Assert.Equal(1, client.Snapshot.ProviderCalls);
            Assert.True(server.Disposed);
        }
        finally
        {
            cancellation.Cancel();
            await Record.ExceptionAsync(() => response.WaitAsync(TestTimeout));
        }
    }

    [Fact]
    public async Task StreamingCumulativeUsageSnapshotsKeepTheBudgetAtTheOfficialTotal()
    {
        var server = new FakeServer(
            """{"method":"thread/tokenUsage/updated","params":{"threadId":"thread","tokenUsage":{"total":{"inputTokens":10,"outputTokens":2,"totalTokens":12,"cachedInputTokens":4}}}}""",
            """{"method":"item/agentMessage/delta","params":{"threadId":"thread","turnId":"turn","delta":"当前回复"}}""",
            """{"method":"thread/tokenUsage/updated","params":{"threadId":"thread","tokenUsage":{"total":{"inputTokens":16,"outputTokens":4,"totalTokens":20,"cachedInputTokens":6}}}}""",
            """{"method":"thread/tokenUsage/updated","params":{"threadId":"thread","tokenUsage":{"total":{"inputTokens":16,"outputTokens":4,"totalTokens":20,"cachedInputTokens":6}}}}""",
            """{"method":"turn/completed","params":{"threadId":"thread","turn":{"id":"turn","status":"completed"}}}""");
        using var client = new CopilotTokenBudgetChatClient(
            new CopilotCodexChatClient(Profile(), _ => Task.FromResult<ICopilotCodexAppServer>(server)),
            new CopilotAgentTokenBudget
            {
                ContextWindowTokens = CopilotAgentTokenBudget.MinimumContextWindowTokens,
                MaxOutputTokens = 1_024,
                RequestTokenBudget = 4_096,
            });
        var usageSnapshots = new List<long?>();
        var text = new StringBuilder();
        var completed = false;

        await foreach (var update in client.GetStreamingResponseAsync([new AIMessage(ChatRole.User, "test")]))
        {
            usageSnapshots.AddRange(update.Contents.OfType<UsageContent>().Select(usage => usage.Details.TotalTokenCount));
            foreach (var content in update.Contents.OfType<TextContent>())
                text.Append(content.Text);
            completed |= update.FinishReason == ChatFinishReason.Stop;
        }

        Assert.Equal(new long?[] { 12, 20, 20 }, usageSnapshots);
        Assert.Equal("当前回复", text.ToString());
        Assert.True(completed);
        Assert.Equal(20, client.Snapshot.ReportedTotalTokens);
        Assert.Equal(16, client.Snapshot.ReportedInputTokens);
        Assert.Equal(4, client.Snapshot.ReportedOutputTokens);
        Assert.Equal(6, client.Snapshot.ReportedCachedInputTokens);
        Assert.Equal(20, client.Snapshot.ConsumedTokens);
        Assert.False(client.Snapshot.UsedEstimatedUsage);
        Assert.Equal(1, client.Snapshot.ProviderCalls);
        Assert.True(server.Disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HarnessStreamingCumulativeUsageCountsEachProviderRequestOnce(bool requestTool)
    {
        string[] usageMessages =
        [
            """{"method":"thread/tokenUsage/updated","params":{"threadId":"thread","tokenUsage":{"total":{"inputTokens":10,"outputTokens":2,"totalTokens":12,"cachedInputTokens":4}}}}""",
            """{"method":"thread/tokenUsage/updated","params":{"threadId":"thread","tokenUsage":{"total":{"inputTokens":16,"outputTokens":4,"totalTokens":20,"cachedInputTokens":6}}}}""",
            """{"method":"thread/tokenUsage/updated","params":{"threadId":"thread","tokenUsage":{"total":{"inputTokens":16,"outputTokens":4,"totalTokens":20,"cachedInputTokens":6}}}}""",
        ];
        var answerServer = new FakeServer(
            [.. usageMessages,
             """{"method":"item/agentMessage/delta","params":{"threadId":"thread","turnId":"turn","delta":"Validation complete."}}""",
             """{"method":"turn/completed","params":{"threadId":"thread","turn":{"id":"turn","status":"completed"}}}"""]);
        var toolServer = new FakeServer(
            [.. usageMessages,
             """{"id":101,"method":"item/tool/call","params":{"threadId":"thread","turnId":"turn","callId":"usage-probe","tool":"colorvision_run_workspace_validation","arguments":{}}}"""]);
        var servers = new Queue<FakeServer>(requestTool ? [toolServer, answerServer] : [answerServer]);
        var directory = Directory.CreateTempSubdirectory("CopilotCodexHarnessUsageTests-");
        try
        {
            var tool = new UsageProbeTool();
            var catalog = new CopilotCapabilityCatalog();
            catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "codex-usage-tests", "Codex usage tests", [tool]);
            var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
                new CopilotToolRegistry([tool]), new CopilotAgentContextBuilder(), new CopilotToolExecutor(),
                profile => new CopilotCodexChatClient(profile,
                    _ => Task.FromResult<ICopilotCodexAppServer>(servers.Dequeue())),
                new EmptyExternalToolProvider(), catalog, new CopilotAgentSkillUsageStore(directory.FullName));
            var request = new CopilotAgentRequest
            {
                Profile = Profile(),
                ConversationId = "codex-harness-usage",
                TaskId = "codex-harness-usage-task",
                WorkspacePath = directory.FullName,
                UserText = "Run the workspace validation probe and summarize its result.",
                Mode = CopilotAgentMode.Code,
                HarnessFeatures = CopilotAgentHarnessFeatures.None,
                RunBudgetOverride = new CopilotAgentRunBudgetOverride
                {
                    RequestTokenBudget = 32_768, MaxToolCalls = 2, MaxAgentPasses = 1,
                    TotalDuration = TimeSpan.FromSeconds(60),
                },
            };
            using var cancellation = new CancellationTokenSource(TestTimeout);
            var assistant = new CopilotChatMessage { Role = CopilotChatRole.Assistant, RequestMode = request.Mode };
            var conversation = new CopilotConversationRecord();
            conversation.Messages.Add(assistant);
            var turn = CopilotTurnEventState.Create(request.Mode, request.TaskId);
            CopilotAgentRunResult? runResult = null;
            var budgetDiagnostics = new List<string>();
            await foreach (var turnEvent in CopilotTurnEventStream.RunAsync(request.TaskId, request.Mode, async (sink, token) =>
            {
                runResult = await runtime.RunAsync(request, agentEvent =>
                {
                    sink.OnAgentEvent(agentEvent);
                    if (agentEvent.Type == CopilotAgentEventType.BudgetUpdated)
                        sink.OnTokenUsageUpdated(CopilotTurnRuntime.GetReportedTokenUsage(agentEvent.Budget!));
                    if (agentEvent.Type == CopilotAgentEventType.RuntimeDiagnostic
                        && agentEvent.Text.StartsWith("Agent budget used ", StringComparison.Ordinal))
                        budgetDiagnostics.Add(agentEvent.Text);
                }, token);
                sink.OnPlanUpdated(CopilotTurnPlanSnapshot.FromTaskLedger(runResult.TaskLedger));
                sink.OnTokenUsageUpdated(runResult.Usage);
                return CopilotTurnResult.FromAgent(request.Mode, runResult.Usage, runResult);
            }, cancellation.Token))
            {
                turn = CopilotTurnEventReducer.Reduce(turn, turnEvent);
                if (turnEvent is CopilotTurnAgentEvent agent)
                    CopilotAssistantMessagePresenter.ApplyAgentEvent(assistant, agent.Event);
                else if (turnEvent is CopilotTurnTokenUsageUpdatedEvent usage)
                    assistant.SetReportedUsage(usage.Usage);
                else if (turnEvent is CopilotTurnCompletedEvent completed)
                    CopilotHostedTurnCompletion.CompleteTerminalTurn(conversation, assistant, completed.Result!.Usage);
            }

            var result = Assert.IsType<CopilotAgentRunResult>(runResult);
            var providerCalls = requestTool ? 2 : 1;
            var expectedUsage = new CopilotTokenUsage(16 * providerCalls, 4 * providerCalls, 20 * providerCalls, 6 * providerCalls);
            Assert.Equal(providerCalls, result.Budget.ProviderCalls);
            Assert.Equal(requestTool ? 1 : 0, tool.CallCount);
            Assert.Equal(requestTool ? 1 : 0, result.StepRecords.Count);
            Assert.Equal(requestTool ? 1 : 0, result.Budget.ToolCalls);
            Assert.Equal(expectedUsage, CopilotTurnRuntime.GetReportedTokenUsage(result.Budget));
            Assert.Equal(expectedUsage.EffectiveTotalTokens, result.Budget.ConsumedTokens);
            Assert.False(result.Budget.UsedEstimatedUsage);
            Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
            Assert.Equal(CopilotAgentStopReason.Completed.ToString(),
                Assert.Single(result.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.RunStopped).State);
            Assert.Empty(servers);
            Assert.True(answerServer.Disposed);
            if (requestTool)
                Assert.True(toolServer.Disposed);
            Assert.Equal(expectedUsage, result.Usage);
            Assert.Equal(expectedUsage, CopilotTurnEventReducer.RequireCompletion(turn).Usage);
            Assert.Equal(expectedUsage, assistant.ReportedUsage);
            Assert.Equal(expectedUsage, conversation.LastUsage);
            Assert.Contains(
                $" · cache reads {expectedUsage.EffectiveCachedInputTokens:N0}/{expectedUsage.InputTokens:N0} input tokens ({expectedUsage.CachedInputPercentage:0.#}%)",
                Assert.Single(budgetDiagnostics), StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
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
    [InlineData("")]
    [InlineData("答")]
    [InlineData("答案")]
    public async Task CompletedMessageSuppliesMissingTextWithoutRepeatingDeltas(string delta)
    {
        var server = new FakeServer(
            JsonSerializer.Serialize(new { method = "item/agentMessage/delta", @params = new { threadId = "thread", turnId = "turn", itemId = "answer", delta } }),
            """{"method":"item/completed","params":{"threadId":"thread","turnId":"turn","item":{"id":"answer","type":"agentMessage","text":"答案"}}}""",
            """{"method":"turn/completed","params":{"threadId":"thread","turn":{"id":"turn","status":"completed","items":[{"id":"answer","type":"agentMessage","text":"答案"}]}}}""");
        using var client = new CopilotCodexChatClient(Profile(), _ => Task.FromResult<ICopilotCodexAppServer>(server));

        var response = await client.GetResponseAsync([new AIMessage(ChatRole.User, "test")]);

        Assert.Equal("答案", response.Text);
        Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
        Assert.True(server.Disposed);
    }

    [Fact]
    public async Task CompletedFinalMessageIsPreservedAfterAnotherItemStreamed()
    {
        var server = new FakeServer(
            """{"method":"item/agentMessage/delta","params":{"threadId":"thread","turnId":"turn","itemId":"progress","delta":"正在检查。"}}""",
            """{"method":"item/completed","params":{"threadId":"thread","turnId":"turn","item":{"id":"progress","type":"agentMessage","text":"正在检查。"}}}""",
            """{"method":"item/completed","params":{"threadId":"thread","turnId":"turn","item":{"id":"answer","type":"agentMessage","text":"已完成。"}}}""",
            """{"method":"turn/completed","params":{"threadId":"thread","turn":{"id":"turn","status":"completed","items":[]}}}""");
        using var client = new CopilotCodexChatClient(Profile(), _ => Task.FromResult<ICopilotCodexAppServer>(server));

        var response = await client.GetResponseAsync([new AIMessage(ChatRole.User, "test")]);

        Assert.Equal("正在检查。已完成。", response.Text);
    }

    [Fact]
    public async Task CompletionFromAnotherTurnCannotEndCurrentReply()
    {
        var server = new FakeServer(
            """{"method":"turn/completed","params":{"threadId":"thread","turn":{"id":"other-turn","status":"failed"}}}""",
            """{"method":"item/completed","params":{"threadId":"thread","turnId":"other-turn","item":{"id":"other","type":"agentMessage","text":"旧回复"}}}""",
            """{"method":"item/completed","params":{"threadId":"thread","turnId":"turn","item":{"id":"answer","type":"agentMessage","text":"当前回复"}}}""",
            """{"method":"turn/completed","params":{"threadId":"thread","turn":{"id":"turn","status":"completed"}}}""");
        using var client = new CopilotCodexChatClient(Profile(), _ => Task.FromResult<ICopilotCodexAppServer>(server));

        var response = await client.GetResponseAsync([new AIMessage(ChatRole.User, "test")]);

        Assert.Equal("当前回复", response.Text);
        Assert.Equal(ChatFinishReason.Stop, response.FinishReason);
    }

    [Fact]
    public async Task LegacyTurnSnapshotPreservesAllMessagesWithoutItemIds()
    {
        var server = new FakeServer(
            """{"method":"turn/completed","params":{"threadId":"thread","turn":{"id":"turn","status":"completed","items":[{"type":"agentMessage","text":"第一段。"},{"type":"agentMessage","text":"第二段。"}]}}}""");
        using var client = new CopilotCodexChatClient(Profile(), _ => Task.FromResult<ICopilotCodexAppServer>(server));

        var response = await client.GetResponseAsync([new AIMessage(ChatRole.User, "test")]);

        Assert.Equal("第一段。第二段。", response.Text);
    }

    [Fact]
    public async Task ConflictingCompletedMessageFailsWithoutInventingAReply()
    {
        var server = new FakeServer(
            """{"method":"item/agentMessage/delta","params":{"threadId":"thread","turnId":"turn","itemId":"answer","delta":"原回复"}}""",
            """{"method":"item/completed","params":{"threadId":"thread","turnId":"turn","item":{"id":"answer","type":"agentMessage","text":"不一致的回复"}}}""");
        using var client = new CopilotCodexChatClient(Profile(), _ => Task.FromResult<ICopilotCodexAppServer>(server));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetResponseAsync([new AIMessage(ChatRole.User, "test")]));

        Assert.Contains("不一致", error.Message);
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

    [Theory]
    [InlineData("thread", null)]
    [InlineData("thread", "")]
    [InlineData("thread", " \t\r\n")]
    [InlineData("turn", null)]
    [InlineData("turn", "")]
    [InlineData("turn", " \t\r\n")]
    public async Task InvalidStartupIdentityFailsBeforeReadingProtocolOutput(string stage, string? returnedId)
    {
        var server = new FakeServer(
            """{"method":"turn/completed","params":{"turn":{"status":"completed"}}}""")
        {
            ReturnedThreadId = stage == "thread" ? returnedId : "thread",
            ReturnedTurnId = stage == "turn" ? returnedId : "turn",
        };
        using var client = new CopilotCodexChatClient(Profile(), _ => Task.FromResult<ICopilotCodexAppServer>(server));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetResponseAsync([new AIMessage(ChatRole.User, "test")]));

        Assert.Contains("Codex", error.Message, StringComparison.Ordinal);
        Assert.Contains("标识", error.Message, StringComparison.Ordinal);
        Assert.False(server.Reading.Task.IsCompleted);
        Assert.True(server.Disposed);
        if (stage == "thread")
            Assert.DoesNotContain(server.Requests, request => request.Method == "turn/start");
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
        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => response);
        Assert.Equal(CopilotTokenUsage.Empty, CopilotTokenBudgetChatClient.ExtractPayloadFailureUsage(failure));
        Assert.True(server.Disposed);
    }

    [Fact]
    public async Task ProtocolReaderPreservesChunkedUtf8MessagesAndLineEndings()
    {
        using var stream = new ProtocolStream(
            "{\"method\":\"first\",\"params\":{\"delta\":\"第一段😀\"}}\r\n"
            + "{\"method\":\"second\"}\r{\"method\":\"third\"}\n{\"method\":\"last\"}", maximumChunkBytes: 1);
        using var reader = CreateProtocolReader(stream);

        var first = await CopilotCodexAppServer.ReadProtocolMessageAsync(reader, CancellationToken.None);
        var second = await CopilotCodexAppServer.ReadProtocolMessageAsync(reader, CancellationToken.None);
        var third = await CopilotCodexAppServer.ReadProtocolMessageAsync(reader, CancellationToken.None);
        var last = await CopilotCodexAppServer.ReadProtocolMessageAsync(reader, CancellationToken.None);

        Assert.Equal("first", first["method"]!.GetValue<string>());
        Assert.Equal("第一段😀", first["params"]!["delta"]!.GetValue<string>());
        Assert.Equal("second", second["method"]!.GetValue<string>());
        Assert.Equal("third", third["method"]!.GetValue<string>());
        Assert.Equal("last", last["method"]!.GetValue<string>());
        await Assert.ThrowsAsync<IOException>(() => CopilotCodexAppServer.ReadProtocolMessageAsync(reader, CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProtocolReaderEnforcesTheCharacterLimitBeforeAnUnterminatedLineFinishes(bool oversized)
    {
        const string prefix = "{\"text\":\"";
        const string suffix = "\"}";
        var textLength = CopilotCodexAppServer.MaximumProtocolMessageCharacters - prefix.Length - suffix.Length + (oversized ? 1 : 0);
        using var stream = new ProtocolStream(prefix + new string('x', textLength) + suffix,
            maximumChunkBytes: 4_096, stallAtEnd: oversized);
        using var reader = CreateProtocolReader(stream);
        using var cancellation = new CancellationTokenSource();
        var reading = CopilotCodexAppServer.ReadProtocolMessageAsync(reader, cancellation.Token);
        try
        {
            if (oversized)
            {
                var completed = await Task.WhenAny(reading, stream.ReadBlocked.Task).WaitAsync(TestTimeout);
                Assert.Same(reading, completed);
                await Assert.ThrowsAsync<InvalidOperationException>(() => reading);
                Assert.False(stream.ReadBlocked.Task.IsCompleted);
            }
            else
            {
                var message = await reading.WaitAsync(TestTimeout);
                Assert.Equal(textLength, message["text"]!.GetValue<string>().Length);
            }
        }
        finally
        {
            cancellation.Cancel();
            stream.ReleaseRead();
            try { await reading.WaitAsync(TestTimeout); }
            catch { }
        }
    }

    [Fact]
    public async Task ProtocolReaderCancelsWhileWaitingForTheRestOfALineAndDisposesItsSource()
    {
        using var stream = new ProtocolStream("{\"method\":\"partial", maximumChunkBytes: 1, stallAtEnd: true);
        using var reader = CreateProtocolReader(stream);
        using var cancellation = new CancellationTokenSource();
        var reading = CopilotCodexAppServer.ReadProtocolMessageAsync(reader, cancellation.Token);
        try
        {
            await stream.ReadBlocked.Task.WaitAsync(TestTimeout);
            Assert.False(reading.IsCompleted);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading.WaitAsync(TestTimeout));
            reader.Dispose();
            Assert.True(stream.IsDisposed);
        }
        finally
        {
            cancellation.Cancel();
            stream.ReleaseRead();
            try { await reading.WaitAsync(TestTimeout); }
            catch { }
        }
    }

    [Fact]
    public async Task ProtocolReaderHonorsCancellationBeforeReturningAnAlreadyBufferedMessage()
    {
        using var source = new StringReader("{\"method\":\"first\"}\n{\"method\":\"buffered\"}\n");
        using var reader = new CopilotBoundedTextLineReader(source,
            CopilotCodexAppServer.MaximumProtocolMessageCharacters, "Codex stdout");
        using var cancellation = new CancellationTokenSource();
        var first = await CopilotCodexAppServer.ReadProtocolMessageAsync(reader, cancellation.Token);
        Assert.Equal("first", first["method"]!.GetValue<string>());
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CopilotCodexAppServer.ReadProtocolMessageAsync(reader, cancellation.Token));
        var buffered = await CopilotCodexAppServer.ReadProtocolMessageAsync(reader, CancellationToken.None);
        Assert.Equal("buffered", buffered["method"]!.GetValue<string>());
    }

    [Fact]
    public async Task StandardErrorDrainConsumesChunkedContentWithoutANewlineUntilEof()
    {
        using var stream = new ProtocolStream(new string('x', 8_193), maximumChunkBytes: 17);

        await CopilotCodexAppServer.DrainStandardErrorAsync(stream).WaitAsync(TestTimeout);

        Assert.Equal(stream.Length, stream.Position);
        Assert.InRange(stream.LargestReadBuffer, 1, 4_096);
        Assert.True(stream.ReusesReadBuffer);
        Assert.False(stream.IsDisposed);
    }

    [Theory]
    [InlineData("disposed")]
    [InlineData("canceled")]
    public async Task StandardErrorDrainConsumesExpectedFailureWhenItsOwnerClosesAPendingRead(string failureKind)
    {
        Exception failure = failureKind == "disposed"
            ? new ObjectDisposedException("owned stderr")
            : new OperationCanceledException("Owned stderr read was closed.");
        using var stream = new FailingStandardErrorStream(failure, waitForClose: true);
        var draining = CopilotCodexAppServer.DrainStandardErrorAsync(stream);
        try
        {
            await stream.ReadBlocked.Task.WaitAsync(TestTimeout);
            Assert.False(draining.IsCompleted);
            stream.Dispose();

            await draining.WaitAsync(TestTimeout);
            Assert.True(draining.IsCompletedSuccessfully);
            Assert.True(stream.IsDisposed);
        }
        finally
        {
            stream.Dispose();
            try { await draining.WaitAsync(TestTimeout); }
            catch { }
        }
    }

    [Fact]
    public async Task StandardErrorDrainConsumesIoFailureWithoutSurfacingTheRawDiagnostic()
    {
        using var stream = new FailingStandardErrorStream(new IOException("Synthetic private stderr token=do-not-display"));
        var draining = CopilotCodexAppServer.DrainStandardErrorAsync(stream);

        await draining.WaitAsync(TestTimeout);

        Assert.True(stream.ReadCalled);
        Assert.True(draining.IsCompletedSuccessfully);
    }

    [Theory]
    [InlineData("io")]
    [InlineData("invalid_operation")]
    public async Task StopOwnedProcessTerminatesItsNativeChildAfterStandardInputCloseFails(string failureKind)
    {
        using var process = CreateOwnedPowerShellProcess(
            "[Console]::Out.WriteLine('READY'); [Console]::Out.Flush(); while ($true) { [System.Threading.Thread]::Sleep(1000) }");
        StreamWriter? standardInput = null;
        StreamReader? standardOutput = null;
        StreamReader? standardError = null;
        var started = false;
        try
        {
            started = process.Start();
            Assert.True(started);
            standardInput = process.StandardInput;
            standardOutput = process.StandardOutput;
            standardError = process.StandardError;
            using var handshakeCancellation = new CancellationTokenSource(TestTimeout);
            Assert.Equal("READY", await standardOutput.ReadLineAsync(handshakeCancellation.Token));
            Assert.False(process.HasExited);
            Exception failure = failureKind == "io"
                ? new IOException("Controlled standard input close failure.")
                : new InvalidOperationException("Controlled standard input close failure.");
            using var throwingInput = new ThrowingTextWriter(failure);

            CopilotCodexAppServer.StopOwnedProcess(process, throwingInput);

            Assert.True(throwingInput.CloseCalled);
            await process.WaitForExitAsync().WaitAsync(TestTimeout);
            Assert.True(process.HasExited);
        }
        finally
        {
            await CleanupOwnedTestProcessAsync(process, started, standardInput, standardOutput, standardError);
        }
    }

    [Fact]
    public async Task StopOwnedProcessSafelyHandlesAnAlreadyExitedNativeChild()
    {
        using var process = CreateOwnedPowerShellProcess("exit 0");
        StreamWriter? standardInput = null;
        StreamReader? standardOutput = null;
        StreamReader? standardError = null;
        var started = false;
        try
        {
            started = process.Start();
            Assert.True(started);
            standardInput = process.StandardInput;
            standardOutput = process.StandardOutput;
            standardError = process.StandardError;
            await process.WaitForExitAsync().WaitAsync(TestTimeout);
            Assert.Equal(0, process.ExitCode);

            CopilotCodexAppServer.StopOwnedProcess(process, standardInput);
            CopilotCodexAppServer.StopOwnedProcess(process, standardInput);

            Assert.True(process.HasExited);
            Assert.Equal(0, process.ExitCode);
        }
        finally
        {
            await CleanupOwnedTestProcessAsync(process, started, standardInput, standardOutput, standardError);
        }
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

    private static CopilotBoundedTextLineReader CreateProtocolReader(Stream stream) =>
        new(new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4_096),
            CopilotCodexAppServer.MaximumProtocolMessageCharacters, "Codex stdout");

    private static Process CreateOwnedPowerShellProcess(string script)
    {
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        Assert.True(Path.IsPathFullyQualified(executable));
        Assert.True(File.Exists(executable));
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        start.ArgumentList.Add("-NoLogo");
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-EncodedCommand");
        start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
        return new Process { StartInfo = start };
    }

    private static async Task CleanupOwnedTestProcessAsync(
        Process process, bool started, StreamWriter? standardInput, StreamReader? standardOutput, StreamReader? standardError)
    {
        try
        {
            if (started)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch (Exception exception) when ((exception is InvalidOperationException or System.ComponentModel.Win32Exception) && process.HasExited)
                {
                }
                await process.WaitForExitAsync().WaitAsync(TestTimeout);
            }
        }
        finally
        {
            try { standardInput?.Dispose(); }
            catch (IOException) { }
            finally
            {
                try { standardOutput?.Dispose(); }
                finally { standardError?.Dispose(); }
            }
        }
    }

    private sealed class ThrowingTextWriter(Exception failure) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public bool CloseCalled { get; private set; }
        public override void Close()
        {
            CloseCalled = true;
            throw failure;
        }
    }

    private sealed class ProtocolStream(string text, int maximumChunkBytes, bool stallAtEnd = false)
        : MemoryStream(Encoding.UTF8.GetBytes(text))
    {
        private readonly TaskCompletionSource<int> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private byte[]? _readBuffer;
        public TaskCompletionSource ReadBlocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsDisposed { get; private set; }
        public int LargestReadBuffer { get; private set; }
        public bool ReusesReadBuffer { get; private set; } = true;

        public void ReleaseRead() => _release.TrySetResult(0);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            LargestReadBuffer = Math.Max(LargestReadBuffer, buffer.Length);
            if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out var segment) && segment.Array != null)
            {
                ReusesReadBuffer &= _readBuffer == null || ReferenceEquals(_readBuffer, segment.Array);
                _readBuffer ??= segment.Array;
            }
            else
            {
                ReusesReadBuffer = false;
            }
            if (Position < Length)
                return await base.ReadAsync(buffer[..Math.Min(buffer.Length, maximumChunkBytes)], cancellationToken);
            if (!stallAtEnd)
                return 0;
            ReadBlocked.TrySetResult();
            return await _release.Task.WaitAsync(cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsDisposed = true;
                ReleaseRead();
            }
            base.Dispose(disposing);
        }
    }

    private sealed class FailingStandardErrorStream(Exception failure, bool waitForClose = false) : MemoryStream
    {
        private readonly TaskCompletionSource<int> _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReadBlocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool ReadCalled { get; private set; }
        public bool IsDisposed { get; private set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadCalled = true;
            if (!waitForClose)
                throw failure;
            ReadBlocked.TrySetResult();
            return await _closed.Task.WaitAsync(cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsDisposed = true;
                if (ReadBlocked.Task.IsCompleted)
                    _closed.TrySetException(failure);
                else
                    _closed.TrySetResult(0);
            }
            base.Dispose(disposing);
        }
    }

    private sealed class UsageProbeTool : ICopilotAgentDrivenTool
    {
        public string Name => "RunWorkspaceValidation";
        public string Description => "Returns deterministic validation evidence.";
        public int CallCount { get; private set; }
        public bool CanHandle(CopilotAgentRequest request) => true;
        public bool IsAvailable(CopilotAgentRequest request) => true;
        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name, Success = true, Summary = "Validation probe completed.", ProcessOperation = "test", ProcessExitCode = 0,
            });
        }
    }

    private sealed class EmptyExternalToolProvider : ICopilotExternalToolProvider
    {
        public Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new CopilotExternalToolLease());
    }

    private sealed class FakeServer(params string[] messages) : ICopilotCodexAppServer
    {
        private readonly Queue<string> _messages = new(messages);
        public bool SignedIn { get; init; } = true;
        public string? ReturnedThreadId { get; init; } = "thread";
        public string? ReturnedTurnId { get; init; } = "turn";
        public bool Disposed { get; private set; }
        public TaskCompletionSource Reading { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource WaitingForMessage { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<(string Method, JsonNode Parameters)> Requests { get; } = [];
        public Task<JsonNode> RequestAsync(string method, object parameters, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add((method, JsonSerializer.SerializeToNode(parameters)!));
            return Task.FromResult(JsonNode.Parse(method switch
            {
                "account/read" => SignedIn ? """{"account":{"type":"chatgpt"}}""" : """{"account":null}""",
                "thread/start" => JsonSerializer.Serialize(new { thread = new { id = ReturnedThreadId } }),
                "turn/start" => JsonSerializer.Serialize(new { turn = new { id = ReturnedTurnId } }),
                _ => "{}",
            })!);
        }
        public async Task<JsonObject> ReadAsync(CancellationToken cancellationToken)
        {
            Reading.TrySetResult();
            if (_messages.TryDequeue(out var message)) return JsonNode.Parse(message)!.AsObject();
            WaitingForMessage.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }
        public void Dispose() => Disposed = true;
    }
}
