using ColorVision.Copilot;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotManagedAttachmentDeletionTests
{
    [Fact]
    public void ManagedFileIsDeletedInsideAttachmentRoot()
    {
        using var fixture = ManagedAttachmentFixture.Create();
        var managedFile = Path.Combine(fixture.AttachmentRoot, "managed.png");
        File.WriteAllText(managedFile, "managed");

        var deleted = CopilotChatStateStore.TryDeleteManagedAttachmentFile(
            fixture.AttachmentRoot,
            managedFile);

        Assert.True(deleted);
        Assert.False(File.Exists(managedFile));
    }

    [Fact]
    public void DirectoryLinkCannotRedirectManagedFileDeletionOutsideAttachmentRoot()
    {
        using var fixture = ManagedAttachmentFixture.Create();
        var outsideFile = Path.Combine(fixture.OutsideRoot, "keep.png");
        File.WriteAllText(outsideFile, "keep");
        var linkedDirectory = Path.Combine(fixture.AttachmentRoot, "linked");
        Directory.CreateSymbolicLink(linkedDirectory, fixture.OutsideRoot);

        var deleted = CopilotChatStateStore.TryDeleteManagedAttachmentFile(
            fixture.AttachmentRoot,
            Path.Combine(linkedDirectory, Path.GetFileName(outsideFile)));

        Assert.False(deleted);
        Assert.True(File.Exists(outsideFile));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LinkedAttachmentRootCannotDeleteOutsideFilesOrClearRecoveryProtection(bool hasProtectionMarker)
    {
        using var fixture = ManagedAttachmentFixture.Create();
        var outsideFile = Path.Combine(fixture.OutsideRoot, "keep.png");
        byte[] originalBytes = [1, 2, 3, 4];
        File.WriteAllBytes(outsideFile, originalBytes);
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        if (hasProtectionMarker)
        {
            conversation.Attachments.Add(CopilotAttachmentItem.CreateImage(
                Path.Combine(fixture.AttachmentRoot, "keep.png")));
        }
        fixture.Store.Save(new CopilotChatState { Conversations = [conversation] });
        if (hasProtectionMarker)
            File.WriteAllText(fixture.Store.AttachmentProtectionMarkerPath, "preserve recovery protection");
        Directory.Delete(fixture.AttachmentRoot, recursive: false);
        Directory.CreateSymbolicLink(fixture.AttachmentRoot, fixture.OutsideRoot);

        var loaded = fixture.Store.Load();
        var deleted = fixture.Store.CleanupOrphanedAttachments(loaded);

        Assert.Equal(CopilotChatStateLoadSource.Primary, fixture.Store.LastLoadStatus.Source);
        Assert.Equal(0, deleted);
        Assert.Equal(originalBytes, File.ReadAllBytes(outsideFile));
        Assert.Equal(hasProtectionMarker, fixture.Store.IsManagedAttachmentCleanupProtected);
    }

    [Fact]
    public void PrimaryStateCleanupDeletesOnlyUnreferencedFilesInsideANormalAttachmentRoot()
    {
        using var fixture = ManagedAttachmentFixture.Create();
        var referencedFile = Path.Combine(fixture.AttachmentRoot, "referenced.png");
        var orphanFile = Path.Combine(fixture.AttachmentRoot, "orphan.png");
        File.WriteAllText(referencedFile, "referenced");
        File.WriteAllText(orphanFile, "orphan");
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Attachments.Add(CopilotAttachmentItem.CreateImage(referencedFile));
        fixture.Store.Save(new CopilotChatState { Conversations = [conversation] });

        var loaded = fixture.Store.Load();
        var deleted = fixture.Store.CleanupOrphanedAttachments(loaded);

        Assert.Equal(CopilotChatStateLoadSource.Primary, fixture.Store.LastLoadStatus.Source);
        Assert.Equal(1, deleted);
        Assert.Equal("referenced", File.ReadAllText(referencedFile));
        Assert.False(File.Exists(orphanFile));
    }

    private sealed class ManagedAttachmentFixture : IDisposable
    {
        private ManagedAttachmentFixture(string root)
        {
            Root = root;
            Store = new CopilotChatStateStore(root);
            AttachmentRoot = Store.AttachmentDirectoryPath;
            OutsideRoot = Path.Combine(root, "Outside");
            Directory.CreateDirectory(AttachmentRoot);
            Directory.CreateDirectory(OutsideRoot);
        }

        public string Root { get; }

        public CopilotChatStateStore Store { get; }

        public string AttachmentRoot { get; }

        public string OutsideRoot { get; }

        public static ManagedAttachmentFixture Create()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "ColorVision.UI.Tests",
                nameof(CopilotManagedAttachmentDeletionTests),
                Guid.NewGuid().ToString("N"));
            return new ManagedAttachmentFixture(root);
        }

        public void Dispose()
        {
            if (!Directory.Exists(Root))
                return;

            if (Directory.Exists(AttachmentRoot)
                && (File.GetAttributes(AttachmentRoot) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(AttachmentRoot, recursive: false);
            }
            else
            {
                var linkedDirectory = Path.Combine(AttachmentRoot, "linked");
                if (Directory.Exists(linkedDirectory))
                    Directory.Delete(linkedDirectory, recursive: false);
            }
            Directory.Delete(Root, recursive: true);
        }
    }
}
