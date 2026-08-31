using ColorVision.Copilot;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotChatStateRecoveryAttachmentTests
{
    [Theory]
    [InlineData(CopilotChatStateLoadSource.Backup)]
    [InlineData(CopilotChatStateLoadSource.RecoverySnapshot)]
    public void OlderSnapshotRecoveryPreservesNewerAttachmentsAcrossLoads(CopilotChatStateLoadSource source)
    {
        using var fixture = new RecoveryAttachmentFixture();
        fixture.PrepareFallback(source);

        var recovered = fixture.Store.Load();

        Assert.Equal(source, fixture.Store.LastLoadStatus.Source);
        Assert.Empty(Assert.Single(recovered.Conversations).Attachments);
        Assert.True(fixture.Store.IsManagedAttachmentCleanupProtected);
        Assert.Equal(0, fixture.Store.CleanupOrphanedAttachments(recovered));
        Assert.True(File.Exists(fixture.AttachmentPath));

        var restartedStore = new CopilotChatStateStore(fixture.Root);
        var restarted = restartedStore.Load();

        Assert.Equal(CopilotChatStateLoadSource.Primary, restartedStore.LastLoadStatus.Source);
        Assert.True(restartedStore.IsManagedAttachmentCleanupProtected);
        Assert.Equal(0, restartedStore.CleanupOrphanedAttachments(restarted));
        Assert.True(File.Exists(fixture.AttachmentPath));

        Assert.Single(restarted.Conversations).Attachments.Add(fixture.CreateAttachment());
        Assert.Equal(0, restartedStore.CleanupOrphanedAttachments(restarted));
        Assert.False(restartedStore.IsManagedAttachmentCleanupProtected);
        Assert.True(File.Exists(fixture.AttachmentPath));
    }

    [Theory]
    [InlineData(CopilotChatStateLoadSource.Backup)]
    [InlineData(CopilotChatStateLoadSource.RecoverySnapshot)]
    public async Task FallbackStaysProtectedAcrossSavesAndLoadsWhenTheMarkerCannotBeWritten(CopilotChatStateLoadSource source)
    {
        using var fixture = new RecoveryAttachmentFixture();
        fixture.PrepareFallback(source);
        Directory.CreateDirectory(fixture.Store.AttachmentProtectionMarkerPath);

        var recovered = fixture.Store.Load();

        Assert.Equal(source, fixture.Store.LastLoadStatus.Source);
        Assert.False(fixture.Store.IsManagedAttachmentCleanupProtected);
        Assert.True(fixture.Store.LastLoadStatus.RequiresRecoveryProtection);
        Assert.Equal(0, fixture.Store.CleanupOrphanedAttachments(recovered));
        Assert.True(File.Exists(fixture.AttachmentPath));
        Assert.Throws<IOException>(() => fixture.Store.Save(recovered));
        await Assert.ThrowsAsync<IOException>(() => fixture.Store.SaveSerializedAsync(fixture.Store.Serialize(recovered)));

        var restartedStore = new CopilotChatStateStore(fixture.Root);
        var restarted = restartedStore.Load();

        Assert.Equal(source, restartedStore.LastLoadStatus.Source);
        Assert.True(restartedStore.LastLoadStatus.RequiresRecoveryProtection);
        Assert.Equal(0, restartedStore.CleanupOrphanedAttachments(restarted));
        Assert.True(File.Exists(fixture.AttachmentPath));

        Directory.Delete(restartedStore.AttachmentProtectionMarkerPath);
        restartedStore.Save(restarted);
        Assert.True(restartedStore.IsManagedAttachmentCleanupProtected);
        Assert.True(File.Exists(fixture.AttachmentPath));
    }

    [Theory]
    [InlineData(CopilotChatStateLoadSource.Primary)]
    [InlineData(CopilotChatStateLoadSource.Temporary)]
    public void LatestSnapshotLoadStillCleansUnreferencedAttachments(CopilotChatStateLoadSource source)
    {
        using var fixture = new RecoveryAttachmentFixture();
        fixture.Store.Save(fixture.State);
        File.WriteAllText(fixture.AttachmentPath, "orphaned attachment");
        if (source == CopilotChatStateLoadSource.Temporary)
            File.Move(fixture.Store.StateFilePath, fixture.Store.TemporaryStateFilePath);

        var loaded = fixture.Store.Load();

        Assert.Equal(source, fixture.Store.LastLoadStatus.Source);
        Assert.False(fixture.Store.LastLoadStatus.RequiresRecoveryProtection);
        Assert.False(fixture.Store.IsManagedAttachmentCleanupProtected);
        Assert.Equal(1, fixture.Store.CleanupOrphanedAttachments(loaded));
        Assert.False(File.Exists(fixture.AttachmentPath));
    }

    private sealed class RecoveryAttachmentFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(
            Path.GetTempPath(),
            nameof(CopilotChatStateRecoveryAttachmentTests),
            Guid.NewGuid().ToString("N"));

        public CopilotChatStateStore Store { get; }

        public CopilotChatState State { get; } = new()
        {
            ActiveProfileId = "profile",
            Conversations = [CopilotConversationRecord.CreateEmpty("profile", "Profile")],
        };

        public string AttachmentPath => Path.Combine(Store.AttachmentDirectoryPath, "newer-attachment.txt");

        public RecoveryAttachmentFixture()
        {
            Store = new CopilotChatStateStore(Root);
            State.ActiveConversationId = State.Conversations[0].Id;
        }

        public void PrepareFallback(CopilotChatStateLoadSource source)
        {
            Store.Save(State);
            File.WriteAllText(AttachmentPath, "attachment referenced only by the newer state");
            State.Conversations[0].Attachments.Add(CreateAttachment());
            Store.Save(State);
            File.WriteAllText(Store.StateFilePath, "{broken-primary");
            if (source == CopilotChatStateLoadSource.RecoverySnapshot)
                File.WriteAllText(Store.BackupStateFilePath, "{broken-backup");
        }

        public CopilotAttachmentItem CreateAttachment() => new()
        {
            Type = CopilotAttachmentType.File,
            Value = AttachmentPath,
        };

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
