using ColorVision.Copilot;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationUsageTests
{
    [Fact]
    public void UsageCommandIsReadOnlyAndAvailableDuringAnActiveRequest()
    {
        var invocation = CopilotLocalCommandCatalog.Parse("/usage");

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.Usage, invocation.Command.Kind);
        Assert.Empty(invocation.Arguments);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.Contains(CopilotLocalCommandCatalog.Suggest("/"), command => command.Name == "/usage");
    }

    [Fact]
    public void TerminalResponsesPersistAndAggregateProviderReportedUsage()
    {
        var conversation = CreateConversation();
        var first = AddAssistant(conversation, "First answer");
        CopilotHostedTurnCompletion.CompleteTerminalTurn(
            conversation,
            first,
            new CopilotTokenUsage(100, 20, 120, 40));
        var unreported = AddAssistant(conversation, "Legacy answer");
        var second = AddAssistant(conversation, "Second answer");
        CopilotHostedTurnCompletion.CompleteTerminalTurn(
            conversation,
            second,
            new CopilotTokenUsage(200, 50, 260));
        var active = AddAssistant(conversation, string.Empty);
        active.IsResponsePending = true;

        var snapshot = CopilotConversationUsageDiagnostics.Capture(conversation);
        var report = CopilotConversationUsageDiagnostics.Format(conversation);

        Assert.Equal(new CopilotTokenUsage(300, 70, 380, 40), snapshot.TotalUsage);
        Assert.Equal(second.ReportedUsage, snapshot.LastUsage);
        Assert.Equal(2, snapshot.TrackedResponses);
        Assert.Equal(1, snapshot.UnreportedResponses);
        Assert.Equal(1, snapshot.ActiveResponses);
        Assert.False(unreported.ReportedUsage.HasAny);
        Assert.Contains("Provider", report, StringComparison.Ordinal);
        Assert.Contains("不代表账户账单", report, StringComparison.Ordinal);
        Assert.Contains("进行中：1 条", report, StringComparison.Ordinal);
    }

    [Fact]
    public void InterruptedResponsesKeepActualUsageWithoutMasqueradingAsComplete()
    {
        var conversation = CreateConversation();
        var interrupted = AddAssistant(conversation, "Partial answer");
        interrupted.MarkResponseInterrupted("Provider stopped early.");

        CopilotHostedTurnCompletion.CompleteTerminalTurn(
            conversation,
            interrupted,
            new CopilotTokenUsage(80, 20, 100));

        var snapshot = CopilotConversationUsageDiagnostics.Capture(conversation);
        var report = CopilotConversationUsageDiagnostics.Format(conversation);

        Assert.Equal(1, snapshot.TrackedResponses);
        Assert.Equal(1, snapshot.InterruptedResponses);
        Assert.Equal(100, snapshot.TotalUsage.EffectiveTotalTokens);
        Assert.Contains("已记录轮次：1", report, StringComparison.Ordinal);
        Assert.Contains("标记中断：1 条", report, StringComparison.Ordinal);
        Assert.DoesNotContain("成功完成", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportedUsageRoundTripsAndLegacyLastUsageMigratesOnlyToTheLatestAnswer()
    {
        var conversation = CreateConversation();
        var older = AddAssistant(conversation, "Older answer");
        var latest = AddAssistant(conversation, "Latest answer");
        conversation.SetLastUsage(new CopilotTokenUsage(90, 10, 100, 25));

        Assert.True(conversation.EnsureValid());
        Assert.False(older.ReportedUsage.HasAny);
        Assert.Equal(conversation.LastUsage, latest.ReportedUsage);

        var serialized = JsonConvert.SerializeObject(conversation);
        var restored = JsonConvert.DeserializeObject<CopilotConversationRecord>(serialized);

        Assert.NotNull(restored);
        restored.EnsureValid();
        Assert.False(restored.EnsureValid());
        Assert.Equal(new CopilotTokenUsage(90, 10, 100, 25), restored.Messages[^1].ReportedUsage);
        Assert.Contains(nameof(CopilotChatMessage.ReportedUsageTotalTokens), serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ReportedUsage\":", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyLastUsageSkipsAnAnswerInterruptedByRestart()
    {
        var conversation = CreateConversation();
        var completed = AddAssistant(conversation, "Completed answer");
        var interrupted = AddAssistant(conversation, string.Empty);
        interrupted.IsResponsePending = true;
        conversation.SetLastUsage(new CopilotTokenUsage(75, 15, 90));

        Assert.True(conversation.EnsureValid());

        Assert.True(interrupted.WasResponseInterrupted);
        Assert.False(interrupted.ReportedUsage.HasAny);
        Assert.Equal(conversation.LastUsage, completed.ReportedUsage);
    }

    [Fact]
    public void BranchUsageStopsAtTheSelectedAssistantMessage()
    {
        var conversation = CreateConversation();
        var first = AddAssistant(conversation, "First answer");
        first.SetReportedUsage(new CopilotTokenUsage(60, 10, 70));
        var second = AddAssistant(conversation, "Second answer");
        second.SetReportedUsage(new CopilotTokenUsage(120, 20, 140));

        var branch = CopilotConversationBranchService.CreateBranch(conversation, first);
        var sourceUsage = CopilotConversationUsageDiagnostics.Capture(conversation);
        var branchUsage = CopilotConversationUsageDiagnostics.Capture(branch);

        Assert.Equal(210, sourceUsage.TotalUsage.EffectiveTotalTokens);
        Assert.Equal(70, branchUsage.TotalUsage.EffectiveTotalTokens);
        Assert.Single(branch.Messages);
        Assert.Equal(first.ReportedUsage, branch.Messages[0].ReportedUsage);
    }

    [Fact]
    public void UsageAdditionPreservesProviderTotalsAndSaturatesInsteadOfOverflowing()
    {
        var combined = new CopilotTokenUsage(int.MaxValue, 1, int.MaxValue)
            .Add(new CopilotTokenUsage(1, int.MaxValue, int.MaxValue));

        Assert.Equal(int.MaxValue, combined.InputTokens);
        Assert.Equal(int.MaxValue, combined.OutputTokens);
        Assert.Equal(int.MaxValue, combined.EffectiveTotalTokens);
    }

    private static CopilotConversationRecord CreateConversation()
    {
        return CopilotConversationRecord.CreateEmpty("profile-id", "Primary");
    }

    private static CopilotChatMessage AddAssistant(
        CopilotConversationRecord conversation,
        string content)
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, content);
        conversation.Messages.Add(message);
        return message;
    }
}
