using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationExportTests
{
    [Fact]
    public void ExportCommandAcceptsAnOptionalFileNameAndCanRunDuringAnAgentTask()
    {
        var clipboard = CopilotLocalCommandCatalog.Parse("/export");
        var file = CopilotLocalCommandCatalog.Parse("/export \"Camera session.md\"");

        Assert.NotNull(clipboard);
        Assert.Equal(CopilotLocalCommandKind.ExportConversation, clipboard.Command.Kind);
        Assert.Empty(clipboard.Arguments);
        Assert.True(clipboard.Command.AvailableWhileAgentRuns);
        Assert.NotNull(file);
        Assert.Equal("\"Camera session.md\"", file.Arguments);
        Assert.True(file.Command.AvailableWhileAgentRuns);
        Assert.Contains(CopilotLocalCommandCatalog.Suggest("/"), command => command.Name == "/export");
    }

    [Theory]
    [InlineData("summary", "summary.md")]
    [InlineData(" report.MD ", "report.MD")]
    [InlineData("\"Camera session.md\"", "Camera session.md")]
    [InlineData("conversation.txt", "conversation.txt")]
    public void FileNameHintKeepsOnlyAValidatedFileName(string requested, string expected)
    {
        var success = CopilotConversationMarkdownExporter.TryNormalizeFileNameHint(
            requested,
            out var fileName,
            out var errorMessage);

        Assert.True(success);
        Assert.Equal(expected, fileName);
        Assert.Empty(errorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData(@"..\secret.md")]
    [InlineData(@"C:\secret.md")]
    [InlineData("folder/report.md")]
    [InlineData("bad|name.md")]
    [InlineData("CON.md")]
    [InlineData("report.json")]
    [InlineData("report.")]
    public void FileNameHintRejectsPathsUnsafeNamesAndUnsupportedExtensions(string requested)
    {
        var success = CopilotConversationMarkdownExporter.TryNormalizeFileNameHint(
            requested,
            out _,
            out var errorMessage);

        Assert.False(success);
        Assert.NotEmpty(errorMessage);
    }

    [Fact]
    public void FileNameHintRejectsAnOverlongName()
    {
        var success = CopilotConversationMarkdownExporter.TryNormalizeFileNameHint(
            new string('a', 129),
            out _,
            out var errorMessage);

        Assert.False(success);
        Assert.Contains("128", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkdownExportsCompletedVisibleMessagesAndAttachmentReferencesOnly()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile-id", "Primary");
        conversation.Title = "Camera #1";
        var user = new CopilotChatMessage(CopilotChatRole.User, "Inspect the camera");
        user.Attachments.Add(CopilotAttachmentItem.CreateFile(@"C:\captures\frame.png"));
        user.Attachments.Add(CopilotAttachmentItem.CreateContext(
            "hidden context body",
            "Selected menu",
            "menu://Camera/Inspect"));
        conversation.Messages.Add(user);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Visible answer")
        {
            ReasoningContent = "hidden reasoning",
            ExecutionContent = "hidden execution",
        });
        var interrupted = new CopilotChatMessage(CopilotChatRole.Assistant, "Visible partial answer");
        interrupted.MarkResponseInterrupted("Response stopped by application exit.");
        conversation.Messages.Add(interrupted);
        var active = new CopilotChatMessage(CopilotChatRole.Assistant, "streaming partial")
        {
            IsExecutionInProgress = true,
        };
        active.Attachments.Add(CopilotAttachmentItem.CreateContext("active attachment", "Active"));
        conversation.Messages.Add(active);

        var snapshot = CopilotConversationMarkdownExporter.Capture(conversation);
        var markdown = CopilotConversationMarkdownExporter.BuildMarkdown(snapshot);

        Assert.Equal(3, snapshot.Messages.Count);
        Assert.Contains("# Camera \\#1", markdown, StringComparison.Ordinal);
        Assert.Contains("Inspect the camera", markdown, StringComparison.Ordinal);
        Assert.Contains("Visible answer", markdown, StringComparison.Ordinal);
        Assert.Contains("Visible partial answer", markdown, StringComparison.Ordinal);
        Assert.Contains("Response stopped by application exit.", markdown, StringComparison.Ordinal);
        Assert.Contains(@"C:\captures\frame.png", markdown, StringComparison.Ordinal);
        Assert.Contains("menu://Camera/Inspect", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden context body", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden reasoning", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden execution", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("streaming partial", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("active attachment", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveMessagesAloneDoNotMakeAConversationExportable()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile-id", "Primary");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "partial")
        {
            IsResponsePending = true,
        });

        Assert.False(CopilotConversationMarkdownExporter.CanExport(conversation));
        Assert.Empty(CopilotConversationMarkdownExporter.Capture(conversation).Messages);
    }

    [Fact]
    public void GeneratedFileNameAvoidsWindowsReservedNames()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile-id", "Primary");
        conversation.Title = "CON";
        conversation.UpdatedAt = new DateTime(2026, 7, 30, 12, 34, 0);

        Assert.Equal("_CON-20260730-1234.md", CopilotConversationMarkdownExporter.BuildFileName(conversation));
    }
}
