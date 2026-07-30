using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotFeedbackDraftBuilderTests
{
    [Fact]
    public void FeedbackCommandAcceptsAnOptionalReportDuringAnAgentTask()
    {
        var invocation = CopilotLocalCommandCatalog.Parse("/feedback Camera preview froze");

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.Feedback, invocation.Command.Kind);
        Assert.Equal("Camera preview froze", invocation.Arguments);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.Contains(
            CopilotLocalCommandCatalog.Suggest("/"),
            command => command.Name == "/feedback");
    }

    [Fact]
    public void DraftIncludesOnlyCompletedVisibleConversationContent()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile-id", "Primary");
        var user = new CopilotChatMessage(CopilotChatRole.User, "Visible request")
        {
            RequestContent = "hidden request body",
        };
        user.Attachments.Add(CopilotAttachmentItem.CreateFile(@"C:\captures\frame.png"));
        conversation.Messages.Add(user);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Visible answer")
        {
            ReasoningContent = "hidden reasoning",
            ExecutionContent = "hidden execution",
            AgentTraceEntries =
            {
                new CopilotAgentTraceEntry
                {
                    ToolName = "SensitiveTool",
                    ArgumentSummary = "hidden arguments",
                },
            },
        });
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "streaming partial")
        {
            IsResponsePending = true,
        });

        var draft = CopilotFeedbackDraftBuilder.Create(conversation, "  Camera froze  ");

        Assert.Equal("Camera froze", draft.Report);
        Assert.True(draft.HasConversationAttachment);
        Assert.Equal(2, draft.IncludedMessageCount);
        Assert.Equal(0, draft.OmittedMessageCount);
        Assert.Contains("Visible request", draft.ConversationMarkdown, StringComparison.Ordinal);
        Assert.Contains("Visible answer", draft.ConversationMarkdown, StringComparison.Ordinal);
        Assert.Contains(@"C:\captures\frame.png", draft.ConversationMarkdown, StringComparison.Ordinal);
        Assert.Contains("提交前可在反馈窗口移除此附件", draft.ConversationMarkdown, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden request body", draft.ConversationMarkdown, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden reasoning", draft.ConversationMarkdown, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden execution", draft.ConversationMarkdown, StringComparison.Ordinal);
        Assert.DoesNotContain("SensitiveTool", draft.ConversationMarkdown, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden arguments", draft.ConversationMarkdown, StringComparison.Ordinal);
        Assert.DoesNotContain("streaming partial", draft.ConversationMarkdown, StringComparison.Ordinal);
    }

    [Fact]
    public void ConversationAttachmentIsBoundedToRecentMessages()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile-id", "Primary");
        for (var index = 0; index < CopilotFeedbackDraftBuilder.MaximumMessages + 5; index++)
        {
            conversation.Messages.Add(new CopilotChatMessage(
                CopilotChatRole.User,
                $"message-{index:D2} " + new string('x', 6_000)));
        }

        var draft = CopilotFeedbackDraftBuilder.Create(conversation, null);

        Assert.True(draft.HasConversationAttachment);
        Assert.True(draft.ConversationMarkdown.Length <= CopilotFeedbackDraftBuilder.MaximumConversationCharacters);
        Assert.True(draft.IncludedMessageCount <= CopilotFeedbackDraftBuilder.MaximumMessages);
        Assert.True(draft.OmittedMessageCount >= 5);
        Assert.DoesNotContain("message-00", draft.ConversationMarkdown, StringComparison.Ordinal);
        Assert.Contains("message-54", draft.ConversationMarkdown, StringComparison.Ordinal);
        Assert.Contains("因反馈大小限制未附加", draft.ConversationMarkdown, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportLimitDoesNotSplitASurrogatePair()
    {
        var report = new string(
            'a',
            CopilotFeedbackDraftBuilder.MaximumReportCharacters - 1) + "😀tail";

        var normalized = CopilotFeedbackDraftBuilder.NormalizeReport(report);

        Assert.Equal(CopilotFeedbackDraftBuilder.MaximumReportCharacters, normalized.Length);
        Assert.False(char.IsHighSurrogate(normalized[^2]));
        Assert.Equal('…', normalized[^1]);
    }

    [Fact]
    public void EmptyConversationStillOpensAPlainFeedbackDraft()
    {
        var draft = CopilotFeedbackDraftBuilder.Create(null, "problem details");

        Assert.Equal("problem details", draft.Report);
        Assert.False(draft.HasConversationAttachment);
        Assert.Empty(draft.ConversationMarkdown);
        Assert.Equal(0, draft.IncludedMessageCount);
        Assert.Equal(0, draft.OmittedMessageCount);
    }
}
