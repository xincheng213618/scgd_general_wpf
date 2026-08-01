using ColorVision.Copilot;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotComposerStashTests
{
    [Fact]
    public void CaptureKeepsExactComposerStateAndCopiesAttachments()
    {
        var attachment = CopilotAttachmentItem.CreateContext(
            "captured context",
            "Inspection context",
            "colorvision://context/current");

        var stash = CopilotComposerStash.Capture(
            "Inspect\r\nthis flow",
            caretIndex: 7,
            CopilotAgentMode.Plan,
            [attachment]);
        attachment.Title = "Changed later";
        var restoredAttachments = stash.CreateAttachmentSnapshots();

        Assert.Equal("Inspect\r\nthis flow", stash.Text);
        Assert.Equal(7, stash.CaretIndex);
        Assert.Equal(CopilotAgentMode.Plan, stash.RequestMode);
        var capturedAttachment = Assert.Single(stash.Attachments);
        var restoredAttachment = Assert.Single(restoredAttachments);
        Assert.Equal("Inspection context", capturedAttachment.Title);
        Assert.Equal("Inspection context", restoredAttachment.Title);
        Assert.NotSame(attachment, capturedAttachment);
        Assert.NotSame(capturedAttachment, restoredAttachment);
    }

    [Fact]
    public void EnsureValidBoundsCorruptPersistedState()
    {
        var stash = new CopilotComposerStash
        {
            Text = new string('x', CopilotConversationHistoryWindow.MaximumContentCharacterLimit + 10),
            CaretIndex = int.MaxValue,
            RequestMode = (CopilotAgentMode)int.MaxValue,
            Attachments = null!,
        };

        var changed = stash.EnsureValid();

        Assert.True(changed);
        Assert.Equal(CopilotConversationHistoryWindow.MaximumContentCharacterLimit, stash.Text.Length);
        Assert.Equal(stash.Text.Length, stash.CaretIndex);
        Assert.Equal(CopilotAgentMode.Auto, stash.RequestMode);
        Assert.Empty(stash.Attachments);
    }

    [Fact]
    public void PersistedStashSurvivesRestartAndProtectsManagedAttachments()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new CopilotChatStateStore(root);
            Directory.CreateDirectory(store.AttachmentDirectoryPath);
            var attachmentPath = Path.Combine(store.AttachmentDirectoryPath, "stashed.txt");
            File.WriteAllText(attachmentPath, "attachment");
            var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
            conversation.ComposerStash = CopilotComposerStash.Capture(
                "Continue after the build",
                caretIndex: 8,
                CopilotAgentMode.Code,
                [CopilotAttachmentItem.CreateFile(attachmentPath)]);
            var state = new CopilotChatState
            {
                ActiveConversationId = conversation.Id,
                ActiveProfileId = "profile",
                Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
            };

            store.Save(state);
            var loaded = store.Load();
            var deletedCount = store.CleanupOrphanedAttachments(loaded);
            var restoredConversation = Assert.Single(loaded.Conversations);
            var restoredStash = Assert.IsType<CopilotComposerStash>(restoredConversation.ComposerStash);

            Assert.Equal(CopilotChatState.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.Equal("Continue after the build", restoredStash.Text);
            Assert.Equal(8, restoredStash.CaretIndex);
            Assert.Equal(CopilotAgentMode.Code, restoredStash.RequestMode);
            Assert.Equal(attachmentPath, Assert.Single(restoredStash.Attachments).Value);
            Assert.True(restoredConversation.HasComposerStash);
            Assert.True(CopilotConversationService.IsHistory(restoredConversation));
            Assert.False(CopilotConversationService.IsReusableEmpty(restoredConversation));
            Assert.StartsWith("已暂存：", restoredConversation.ConversationListPreviewText, StringComparison.Ordinal);
            Assert.Equal(0, deletedCount);
            Assert.True(File.Exists(attachmentPath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
