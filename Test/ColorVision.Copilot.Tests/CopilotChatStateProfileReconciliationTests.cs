using ColorVision.Copilot;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotChatStateProfileReconciliationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiveProfileReconciliationPreservesDispatchingCommandUntilRestart(bool hasNewerDraft)
    {
        using var fixture = new StateFixture();
        var profile = CopilotProfileConfig.CreateDefault();
        var config = new CopilotConfig { Profiles = [profile] };
        var source = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        var selected = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        selected.DraftText = "another conversation draft";
        if (hasNewerDraft)
        {
            source.DraftText = "newer draft";
            source.Attachments.Add(CopilotAttachmentItem.CreateContext("newer draft context"));
        }
        var image = fixture.CreateManagedImage();
        var recovery = new CopilotQueuedFollowUpRecoveryRecord
        {
            RunId = "dispatching-command",
            ConversationId = source.Id,
            ProfileId = profile.Id,
            IsLocalCommand = true,
            Prompt = "/reasoning high",
            ResumeAfterRestart = false,
            ComposerState = CopilotComposerStash.Capture(
                "/reasoning high", 15, CopilotAgentMode.Auto, [image]),
        };
        var state = new CopilotChatState
        {
            ActiveProfileId = profile.Id,
            ActiveConversationId = selected.Id,
            Conversations = [source, selected],
            QueuedFollowUpRecoveries = [recovery],
        };

        CopilotChatStateProfileReconciler.Apply(state, config, profile.Id);

        Assert.Same(recovery, Assert.Single(state.QueuedFollowUpRecoveries));
        Assert.Equal(hasNewerDraft ? "newer draft" : string.Empty, source.DraftText);
        Assert.Equal(hasNewerDraft ? 1 : 0, source.Attachments.Count);
        Assert.DoesNotContain(source.Attachments, attachment => attachment.Id == image.Id);
        Assert.Equal("another conversation draft", selected.DraftText);
        Assert.Empty(selected.Attachments);
        Assert.Empty(source.Messages);
        Assert.Empty(selected.Messages);
        Assert.Equal(selected.Id, state.ActiveConversationId);

        fixture.Store.Save(state);
        var orphanPath = Path.Combine(fixture.Store.AttachmentDirectoryPath, "orphan.txt");
        File.WriteAllText(orphanPath, "unreferenced");
        Assert.Equal(1, fixture.Store.CleanupOrphanedAttachments(state));
        Assert.False(File.Exists(orphanPath));
        Assert.True(File.Exists(image.Value));

        var restartedStore = new CopilotChatStateStore(fixture.Root);
        var restored = restartedStore.Load();
        Assert.Equal(CopilotChatStateLoadSource.Primary, restartedStore.LastLoadStatus.Source);
        Assert.Single(restored.QueuedFollowUpRecoveries);

        Assert.True(restored.EnsureInitializedAfterRestore(config));

        var restoredSource = Assert.Single(restored.Conversations, conversation => conversation.Id == source.Id);
        var restoredSelected = Assert.Single(restored.Conversations, conversation => conversation.Id == selected.Id);
        Assert.Empty(restored.QueuedFollowUpRecoveries);
        Assert.Equal(1, restored.RecoveredQueuedFollowUpCount);
        Assert.Equal(
            hasNewerDraft ? "newer draft" + Environment.NewLine + Environment.NewLine + "/reasoning high" : "/reasoning high",
            restoredSource.DraftText);
        Assert.Equal(hasNewerDraft ? 2 : 1, restoredSource.Attachments.Count);
        var restoredImage = Assert.Single(restoredSource.Attachments, attachment => attachment.Id == image.Id);
        Assert.Equal(image.Value, restoredImage.Value);
        if (hasNewerDraft)
            Assert.Contains(restoredSource.Attachments, attachment => attachment.Value == "newer draft context");
        Assert.Equal("another conversation draft", restoredSelected.DraftText);
        Assert.Empty(restoredSelected.Attachments);
        Assert.Empty(restoredSource.Messages);
        Assert.Equal(selected.Id, restored.ActiveConversationId);
        Assert.Equal(0, restartedStore.CleanupOrphanedAttachments(restored));
        Assert.True(File.Exists(image.Value));
    }

    [Fact]
    public void LiveProfileReconciliationPreservesAutomaticContinuationUntilRestart()
    {
        using var fixture = new StateFixture();
        var profile = CopilotProfileConfig.CreateDefault();
        var config = new CopilotConfig { Profiles = [profile] };
        var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        conversation.DraftText = "user draft";
        conversation.Goal = CopilotConversationGoal.Create("continue the task", DateTimeOffset.UtcNow);
        var recovery = new CopilotQueuedFollowUpRecoveryRecord
        {
            RunId = "automatic-continuation",
            ConversationId = conversation.Id,
            ProfileId = profile.Id,
            GoalId = conversation.Goal.Id,
            AutomaticGoalContinuation = true,
            Prompt = "internal automatic continuation",
            ResumeAfterRestart = true,
        };
        var state = new CopilotChatState
        {
            ActiveProfileId = profile.Id,
            ActiveConversationId = conversation.Id,
            Conversations = [conversation],
            QueuedFollowUpRecoveries = [recovery],
        };

        CopilotChatStateProfileReconciler.Apply(state, config, profile.Id);

        Assert.Same(recovery, Assert.Single(state.QueuedFollowUpRecoveries));
        Assert.Equal("user draft", conversation.DraftText);
        fixture.Store.Save(state);
        var restored = new CopilotChatStateStore(fixture.Root).Load();

        Assert.True(restored.EnsureInitializedAfterRestore(config));

        Assert.Empty(restored.QueuedFollowUpRecoveries);
        Assert.Equal(0, restored.RecoveredQueuedFollowUpCount);
        Assert.Equal("user draft", Assert.Single(restored.Conversations).DraftText);
    }

    private sealed class StateFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(
            Path.GetTempPath(), nameof(CopilotChatStateProfileReconciliationTests), Guid.NewGuid().ToString("N"));

        public CopilotChatStateStore Store { get; }

        public StateFixture()
        {
            Store = new CopilotChatStateStore(Root);
        }

        public CopilotAttachmentItem CreateManagedImage()
        {
            Directory.CreateDirectory(Store.AttachmentDirectoryPath);
            var path = Path.Combine(Store.AttachmentDirectoryPath, "queued-image.png");
            File.WriteAllBytes(path, Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aWQAAAABJRU5ErkJggg=="));
            return new CopilotAttachmentItem { Type = CopilotAttachmentType.Image, Value = path };
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
