using ColorVision.Copilot;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotContextDiagnosticsTests
{
    [Fact]
    public void ContextPreviewKeepsHistoryCompactionAndInstructionEvidenceWithoutBodies()
    {
        const string privateInstructions = "PRIVATE_PROJECT_INSTRUCTION_BODY";
        var snapshot = new CopilotContextDiagnosticSnapshot
        {
            ProfileLabel = "Current Profile",
            Mode = CopilotAgentMode.Code,
            AgentContextEnabled = true,
            SourceHistoryMessages = 12,
            RetainedHistoryMessages = 3,
            SourceHistoryCharacters = 900,
            RetainedHistoryCharacters = 240,
            CurrentModelSurfaceMessages = 3,
            ShadowedModelSurfaceMessages = 8,
            LogOnlySurfaceMessages = 1,
            HasCurrentCompactionSummary = true,
            AutoCompactConversationHistory = true,
            AutoCompactThresholdPercent = 75,
            CompactedSourceMessages = 8,
            CompactionSummaryCharacters = 180,
            CompactionRequests = 2,
            CompactionUsage = new CopilotTokenUsage { InputTokens = 100, OutputTokens = 20 },
            ConversationGoalCharacters = 36,
            ConversationGoalState = CopilotConversationGoalState.Active,
            AgentMaxToolCalls = 7,
            ProjectInstructionDocuments = 1,
            ProjectInstructions = [new()
            {
                Path = Path.Combine(Path.GetTempPath(), "context-project", "AGENTS.md"),
                Content = privateInstructions,
                IsTruncated = true,
            }],
        };

        string report = CopilotContextDiagnostics.Format(snapshot);

        Assert.Contains("模型：Current Profile", report, StringComparison.Ordinal);
        Assert.Contains("3/12 条，240/900 字符保留", report, StringComparison.Ordinal);
        Assert.Contains("当前 3 + 1 条压缩摘要；已被摘要替代 8；仅本地日志 1", report, StringComparison.Ordinal);
        Assert.Contains("75% 时在发送前压缩", report, StringComparison.Ordinal);
        Assert.Contains("8 条来源已归纳为 180 字符摘要", report, StringComparison.Ordinal);
        Assert.Contains("压缩模型调用：2 次", report, StringComparison.Ordinal);
        Assert.Contains("累计输入 100 / 输出 20 / 总计 120 Token", report, StringComparison.Ordinal);
        Assert.Contains("36 字符", report, StringComparison.Ordinal);
        Assert.Contains("工具 7", report, StringComparison.Ordinal);
        Assert.Contains("AGENTS.md", report, StringComparison.Ordinal);
        Assert.Contains("1 个个人/项目指令文档已截断", report, StringComparison.Ordinal);
        Assert.Contains("对话历史已被窗口预算裁剪", report, StringComparison.Ordinal);
        Assert.Contains("运行中的任务仍使用提交时快照", report, StringComparison.Ordinal);
        Assert.DoesNotContain(privateInstructions, report, StringComparison.Ordinal);
        Assert.DoesNotContain("config.toml", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Codex model", report, StringComparison.Ordinal);
        Assert.Equal(12, snapshot.SourceHistoryMessages);
        Assert.Equal(privateInstructions, Assert.Single(snapshot.ProjectInstructions).Content);
    }

    [Fact]
    public void ChatPreviewDoesNotClaimAgentInstructionsOrToolsAreInjected()
    {
        string report = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
        {
            ProfileLabel = "Chat Profile",
            Mode = CopilotAgentMode.Chat,
            AgentContextEnabled = false,
            SystemPromptCharacters = 432,
        });

        Assert.Contains("有效系统提示：432 字符", report, StringComparison.Ordinal);
        Assert.Contains("Chat 模式不注入个人/项目指令、Skills 或 MCP 工具", report, StringComparison.Ordinal);
        Assert.Contains("未调用模型、工具或 MCP，也不会加入模型历史", report, StringComparison.Ordinal);
        Assert.DoesNotContain("模型权限说明：注入", report, StringComparison.Ordinal);
        Assert.DoesNotContain("命令工具：开启", report, StringComparison.Ordinal);
        Assert.DoesNotContain("运行环境上下文：注入", report, StringComparison.Ordinal);
        Assert.DoesNotContain("外部 MCP：", report, StringComparison.Ordinal);
    }

    [Fact]
    public void SleepPreventionReportsRuntimeFailureWithoutAnExternalConfigurationOverride()
    {
        string report = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
        {
            CodexPreventIdleSleep = true,
            ActiveSleepPreventionLeaseCount = 1,
            SleepPreventionLastErrorCode = 5,
            SleepPreventionLastFailure = "Access\r\ndenied",
        });

        Assert.Contains("活动轮次防休眠：开启 · 系统请求 1", report, StringComparison.Ordinal);
        Assert.Contains("最近失败：Access denied · Win32 5", report, StringComparison.Ordinal);
        Assert.DoesNotContain("config.toml", report, StringComparison.Ordinal);
    }
}
