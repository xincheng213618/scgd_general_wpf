using ColorVision.Copilot;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotSubagentDiagnosticsTests
{
    [Fact]
    public void CommandsExposeRoleAndRunDiagnosticsDuringAnActiveRequest()
    {
        var agents = Assert.IsType<CopilotLocalCommandInvocation>(
            CopilotLocalCommandCatalog.Parse("/agents runs 5"));
        var subagents = Assert.IsType<CopilotLocalCommandInvocation>(
            CopilotLocalCommandCatalog.Parse("/subagents roles"));
        var suggestions = CopilotLocalCommandCatalog.Suggest("/agents ");

        Assert.Equal(CopilotLocalCommandKind.Subagents, agents.Command.Kind);
        Assert.Equal("runs 5", agents.Arguments);
        Assert.True(agents.Command.AvailableWhileAgentRuns);
        Assert.Equal(CopilotLocalCommandKind.Subagents, subagents.Command.Kind);
        Assert.Contains(suggestions, item => item.Name == "/agents roles");
        Assert.Contains(suggestions, item => item.Name == "/agents runs");
        Assert.Contains(suggestions, item => item.Name == "/agents show");
        Assert.Contains(suggestions, item => item.Name == "/agents steer");
        Assert.Contains(suggestions, item => item.Name == "/agents stop");
    }

    [Theory]
    [InlineData(null, 0, 8)]
    [InlineData("", 0, 8)]
    [InlineData("roles", 1, 0)]
    [InlineData("ROLES", 1, 0)]
    [InlineData("runs", 2, 8)]
    [InlineData("runs 1", 2, 1)]
    [InlineData("runs 20", 2, 20)]
    [InlineData("runs 0", 3, 0)]
    [InlineData("runs 21", 3, 0)]
    [InlineData("runs all", 3, 0)]
    [InlineData("roles 2", 3, 0)]
    public void CommandArgumentsAreBounded(
        string? arguments,
        int expectedAction,
        int expectedLimit)
    {
        var request = CopilotSubagentDiagnostics.ParseCommand(arguments);

        Assert.Equal((CopilotSubagentDiagnosticAction)expectedAction, request.Action);
        Assert.Equal(expectedLimit, request.Limit);
        Assert.Empty(request.RunId);
        Assert.Empty(request.Message);
    }

    [Theory]
    [InlineData("stop explore-123abc", true)]
    [InlineData("STOP scout-abc123", true)]
    [InlineData("stop", false)]
    [InlineData("stop explore-123 extra", false)]
    [InlineData("stop ../explore-123", false)]
    public void StopCommandRequiresOneBoundedRunId(string arguments, bool expectedValid)
    {
        var request = CopilotSubagentDiagnostics.ParseCommand(arguments);

        Assert.Equal(
            expectedValid
                ? CopilotSubagentDiagnosticAction.Stop
                : CopilotSubagentDiagnosticAction.Invalid,
            request.Action);
        Assert.Equal(expectedValid ? arguments.Split(' ')[1] : string.Empty, request.RunId);
        Assert.Empty(request.Message);
    }

    [Theory]
    [InlineData("show explore-123abc", true)]
    [InlineData("SHOW scout-abc123", true)]
    [InlineData("show", false)]
    [InlineData("show explore-123 extra", false)]
    [InlineData("show ../explore-123", false)]
    public void ShowCommandRequiresOneBoundedRunId(string arguments, bool expectedValid)
    {
        var request = CopilotSubagentDiagnostics.ParseCommand(arguments);

        Assert.Equal(
            expectedValid
                ? CopilotSubagentDiagnosticAction.Show
                : CopilotSubagentDiagnosticAction.Invalid,
            request.Action);
        Assert.Equal(expectedValid ? arguments.Split(' ')[1] : string.Empty, request.RunId);
        Assert.Empty(request.Message);
    }

    [Theory]
    [InlineData("steer explore-123abc inspect the exact failure branch", "explore-123abc", "inspect the exact failure branch")]
    [InlineData("STEER scout-abc123   compare A  and B", "scout-abc123", "compare A  and B")]
    public void SteerCommandPreservesTheBoundedMessage(
        string arguments,
        string expectedRunId,
        string expectedMessage)
    {
        var request = CopilotSubagentDiagnostics.ParseCommand(arguments);

        Assert.Equal(CopilotSubagentDiagnosticAction.Steer, request.Action);
        Assert.Equal(expectedRunId, request.RunId);
        Assert.Equal(expectedMessage, request.Message);
    }

    [Theory]
    [InlineData("steer")]
    [InlineData("steer explore-123")]
    [InlineData("steer ../explore-123 inspect it")]
    public void SteerCommandRejectsMissingOrInvalidTargets(string arguments)
    {
        Assert.Equal(
            CopilotSubagentDiagnosticAction.Invalid,
            CopilotSubagentDiagnostics.ParseCommand(arguments).Action);
    }

    [Fact]
    public void SteerCommandRejectsMessagesBeyondTheRuntimeLimit()
    {
        var request = CopilotSubagentDiagnostics.ParseCommand(
            "steer explore-123 "
                + new string('x', CopilotSteeringMessagePolicy.MaximumMessageCharacters + 1));

        Assert.Equal(CopilotSubagentDiagnosticAction.Invalid, request.Action);
        Assert.Empty(request.Message);
    }

    [Theory]
    [InlineData(0, "已请求停止子代理 explore-live；父 Agent 将继续运行")]
    [InlineData(1, "子代理 explore-live 已在停止中；父 Agent 保持运行")]
    [InlineData(2, "当前会话中没有正在运行的子代理 explore-live")]
    public void StopResultKeepsParentContinuationExplicit(
        int result,
        string expected)
    {
        Assert.Contains(
            expected,
            CopilotSubagentDiagnostics.FormatCancelResult(
                "explore-live",
                (CopilotSubagentCancelResult)result),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CopilotSteeringAdmissionReason.Accepted, "已将新指令排入子代理 explore-live；父 Agent 将继续运行")]
    [InlineData(CopilotSteeringAdmissionReason.NoActiveTask, "没有可接收指令的运行中子代理 explore-live")]
    [InlineData(CopilotSteeringAdmissionReason.RuntimeUnavailable, "仍在启动或切换阶段")]
    [InlineData(CopilotSteeringAdmissionReason.QueueFull, "运行中指令队列已满")]
    public void SteeringResultExplainsAdmissionWithoutEchoingTheMessage(
        CopilotSteeringAdmissionReason reason,
        string expected)
    {
        var report = CopilotSubagentDiagnostics.FormatSteeringResult(
            "explore-live",
            new CopilotSteeringAdmissionResult(
                reason,
                reason == CopilotSteeringAdmissionReason.Accepted ? "message-id" : string.Empty));

        Assert.Contains(expected, report, StringComparison.Ordinal);
        Assert.DoesNotContain("secret steering", report, StringComparison.Ordinal);
    }

    [Fact]
    public void OverviewDescribesTheActualRequestScopedReadOnlyRoles()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Title = "Agent diagnostics";

        var report = CopilotSubagentDiagnostics.Format(conversation, string.Empty);

        Assert.StartsWith("子代理 · Agent diagnostics", report);
        Assert.Contains("请求级只读委派", report);
        Assert.Contains("并发硬上限 2", report);
        Assert.Contains("单次硬上限 16,384 tokens", report);
        Assert.Contains("请求合计硬上限 32,768 tokens", report);
        Assert.Contains("- explore · Explore · 工作区只读 · 子模式 Code", report);
        Assert.Contains("来源：ColorVision [builtin] v10 · tool DelegateExplore", report);
        Assert.Contains("SearchFiles, GrepText, ReadLocalFile, ListDirectory", report);
        Assert.Contains("运行期间可按 run_id 排入新指令或单独停止，父 Agent 继续运行", report);
        Assert.Contains("- scout · Scout · 公共网页只读 · 子模式 Web", report);
        Assert.Contains("来源：ColorVision [builtin] v3 · tool DelegateScout", report);
        Assert.Contains("WebSearch, FetchUrl", report);
        Assert.Contains("同一父请求内，可用完成结果给出的 run_id 续跑同角色", report);
        Assert.Contains("不是可切换、跨请求或应用重启后可恢复的独立会话", report);
        Assert.Contains("当前会话没有可见的子代理运行轨迹", report);
    }

    [Fact]
    public void RunsShowBoundedNewestMetadataWithoutPromptOrAnswerContent()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Title = "Delegation";
        var older = new CopilotChatMessage(CopilotChatRole.Assistant, "Older answer");
        older.AgentTraceEntries.Add(new CopilotAgentTraceEntry
        {
            ToolName = "DelegateScout",
            State = CopilotToolExecutionState.Completed,
            DelegatedRoleId = "scout",
            DelegatedRunId = "scout-old",
            DelegatedStopReason = CopilotAgentStopReason.Completed,
            DelegatedRequestTokenBudget = 4_096,
            DelegatedConsumedTokens = 1_024,
            DelegatedProviderCalls = 1,
            DelegatedToolCalls = 2,
            ArgumentSummary = "secret prompt",
            ResultSummary = "secret answer",
            DelegatedAnswerText = "secret delegated answer",
        });
        var newer = new CopilotChatMessage(CopilotChatRole.Assistant, "Newer answer");
        newer.AgentTraceEntries.Add(new CopilotAgentTraceEntry
        {
            ToolName = "DelegateExplore",
            State = CopilotToolExecutionState.Completed,
            DelegatedRunId = "explore-new",
            DelegatedResumeFromRunId = "explore-source",
            DelegatedStopReason = CopilotAgentStopReason.Completed,
            DelegatedRequestTokenBudget = 8_192,
            DelegatedConsumedTokens = 2_048,
            DelegatedProviderCalls = 2,
            DelegatedToolCalls = 5,
            DelegatedRegisteredToolCount = 4,
            DelegatedAvailableToolCount = 3,
            DelegatedQueueDurationMs = 125,
            DurationMs = 1_500,
            StartedAtUtc = new DateTimeOffset(2026, 7, 31, 4, 0, 0, TimeSpan.Zero),
        });
        conversation.Messages.Add(older);
        conversation.Messages.Add(newer);

        var runs = CopilotSubagentDiagnostics.CaptureRuns(conversation);
        var report = CopilotSubagentDiagnostics.Format(conversation, "runs 1");

        Assert.Equal(2, runs.Count);
        Assert.Equal("explore-new", runs[0].RunId);
        Assert.Equal("explore-source", runs[0].ResumeFromRunId);
        Assert.Equal("explore", runs[0].RoleId);
        Assert.Contains("显示 1 / 2 次（新到旧）", report);
        Assert.Contains("#1 · explore · explore-new · state=Completed · resumed_from=explore-source · stop=Completed", report);
        Assert.Contains("耗时 1.5s · 排队 125ms · tokens 2,048/8,192 · 模型 2 · 工具 5 · 工具面 3/4", report);
        Assert.Contains("另有 1 次较早运行未显示", report);
        Assert.DoesNotContain("scout-old", report);
        Assert.DoesNotContain("secret prompt", report);
        Assert.DoesNotContain("secret answer", report);
        Assert.DoesNotContain("secret delegated answer", report);
    }

    [Fact]
    public void ShowDisplaysOnlyTheSelectedRunResultAndSteeringAudit()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Title = "Delegation";
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Parent answer");
        assistant.AgentTraceEntries.Add(new CopilotAgentTraceEntry
        {
            ToolName = "DelegateScout",
            State = CopilotToolExecutionState.Completed,
            DelegatedRoleId = "scout",
            DelegatedRunId = "scout-other",
            DelegatedAnswerText = "other run result",
        });
        assistant.AgentTraceEntries.Add(new CopilotAgentTraceEntry
        {
            ToolName = "DelegateExplore",
            State = CopilotToolExecutionState.Completed,
            FailureKind = CopilotToolFailureKind.None,
            DelegatedRoleId = "explore",
            DelegatedRunId = "explore-selected",
            DelegatedResumeFromRunId = "explore-source",
            DelegatedStopReason = CopilotAgentStopReason.Completed,
            DelegatedRequestTokenBudget = 8_192,
            DelegatedConsumedTokens = 2_048,
            DelegatedProviderCalls = 2,
            DelegatedToolCalls = 5,
            DelegatedDeliveredSteeringCount = 2,
            DelegatedUndeliveredSteeringCount = 1,
            DelegatedRegisteredToolCount = 48,
            DelegatedAvailableToolCount = 4,
            DelegatedAvailableToolDefinitionCharacters = 4_396,
            DelegatedHarnessInstructionCharacters = 7_228,
            DelegatedQueueDurationMs = 125,
            StartedAtUtc = new DateTimeOffset(2026, 7, 31, 4, 0, 0, TimeSpan.Zero),
            CompletedAtUtc = new DateTimeOffset(2026, 7, 31, 4, 0, 2, TimeSpan.Zero),
            DurationMs = 2_000,
            ArgumentSummary = "secret task prompt",
            ResultSummary = "generic tool summary",
            DelegatedAnswerText = "Selected finding.\n- exact evidence",
            DelegatedAnswerHasSuccessfulEvidence = true,
            DelegatedAnswerWasTruncated = false,
        });
        conversation.Messages.Add(assistant);

        var report = CopilotSubagentDiagnostics.Format(conversation, "show explore-selected");

        Assert.Contains("运行详情", report);
        Assert.Contains("explore · explore-selected · state=Completed · resumed_from=explore-source · stop=Completed", report);
        Assert.Contains("用量：tokens 2,048/8,192 · 模型 2 · 工具 5", report);
        Assert.Contains("工具面：4/48 · 定义 4,396 字符 · harness 7,228 字符", report);
        Assert.Contains("运行中指令：已送达 2 · 未送达 1", report);
        Assert.Contains("Selected finding.\n- exact evidence", report);
        Assert.Contains("结果证明：已取得成功工具证据 · 输出截断：否", report);
        Assert.DoesNotContain("secret task prompt", report);
        Assert.DoesNotContain("generic tool summary", report);
        Assert.DoesNotContain("other run result", report);
        Assert.DoesNotContain("Parent answer", report);
    }

    [Fact]
    public void ShowReportsUnknownRunWithoutFallingBackToAnotherResult()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");

        var report = CopilotSubagentDiagnostics.Format(conversation, "show explore-missing");

        Assert.Contains("当前会话没有可查看的子代理运行 explore-missing", report);
        Assert.DoesNotContain("结果：", report);
    }

    [Fact]
    public void DelegatedAnswerPreviewAndSteeringAuditAreBoundedAndPersisted()
    {
        var answer = new string('x', 20_050);
        var trace = CopilotAgentTraceEntry.FromResult(
            new CopilotToolExecutionInfo
            {
                CallId = "delegate-call",
                Round = 1,
                ToolName = "DelegateExplore",
                State = CopilotToolExecutionState.Completed,
                StartedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow,
            },
            new CopilotToolResult
            {
                ToolName = "DelegateExplore",
                Success = true,
                Summary = "Explore completed.",
                DelegatedRunUsage = new CopilotDelegatedRunUsage
                {
                    RoleId = "explore",
                    RunId = "explore-persisted",
                    DeliveredSteeringCount = 3,
                    UndeliveredSteeringCount = 2,
                },
                DelegatedAnswer = new CopilotDelegatedAnswer
                {
                    Text = answer,
                    StopReason = CopilotAgentStopReason.Completed,
                    HasSuccessfulEvidence = true,
                },
            });

        var restored = JsonConvert.DeserializeObject<CopilotAgentTraceEntry>(
            JsonConvert.SerializeObject(trace));

        Assert.NotNull(restored);
        Assert.False(restored.EnsureValid(DateTimeOffset.UtcNow));
        Assert.Equal(CopilotAgentTraceEntry.CurrentSchemaVersion, restored.SchemaVersion);
        Assert.Equal(3, restored.DelegatedDeliveredSteeringCount);
        Assert.Equal(2, restored.DelegatedUndeliveredSteeringCount);
        Assert.True(restored.DelegatedAnswerHasSuccessfulEvidence);
        Assert.True(restored.DelegatedAnswerWasTruncated);
        Assert.StartsWith(new string('x', 100), restored.DelegatedAnswerText);
        Assert.EndsWith("...<子代理结果预览已截断>", restored.DelegatedAnswerText);
        Assert.True(restored.DelegatedAnswerText.Length < answer.Length);
    }

    [Fact]
    public void RunningDelegateShowsItsIdentityAndBudgetBeforeTerminalUsageReturns()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        assistant.AgentTraceEntries.Add(CopilotAgentTraceEntry.FromProgress(
            new CopilotToolExecutionInfo
            {
                ToolName = "DelegateExplore",
                State = CopilotToolExecutionState.Running,
            },
            "Explore 子 Agent 正在执行 ReadLocalFile",
            new CopilotToolProgressUpdate
            {
                Message = "Explore 子 Agent 正在执行 ReadLocalFile",
                DelegatedRun = new CopilotDelegatedRunProgress
                {
                    RoleId = "explore",
                    RunId = "explore-live",
                    ResumeFromRunId = "explore-source",
                    RequestTokenBudget = 8_192,
                    QueueDurationMs = 25,
                    ConsumedTokens = 2_048,
                    ProviderCalls = 2,
                    ToolCalls = 3,
                },
            }));
        conversation.Messages.Add(assistant);

        var report = CopilotSubagentDiagnostics.Format(conversation, "runs");

        Assert.Contains("#1 · explore · explore-live · state=Running · resumed_from=explore-source · activity=Explore 子 Agent 正在执行 ReadLocalFile", report);
        Assert.Contains("排队 25ms · tokens 2,048/8,192 · 模型 2 · 工具 3", report);
        Assert.DoesNotContain("ID 待回传", report);
        Assert.DoesNotContain("等待子运行回传用量", report);
    }

    [Fact]
    public void InvalidArgumentsReturnUsageWithoutReadingConversationMetadata()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Title = "Sensitive title";

        var report = CopilotSubagentDiagnostics.Format(conversation, "runs 100");

        Assert.Equal(CopilotSubagentDiagnostics.Usage, report);
        Assert.DoesNotContain("Sensitive title", report);
    }
}
