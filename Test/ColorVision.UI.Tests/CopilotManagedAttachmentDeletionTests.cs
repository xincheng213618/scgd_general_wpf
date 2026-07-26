using ColorVision.Copilot;
using System.IO;

namespace ColorVision.UI.Tests;

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

    private sealed class ManagedAttachmentFixture : IDisposable
    {
        private ManagedAttachmentFixture(string root)
        {
            Root = root;
            AttachmentRoot = Path.Combine(root, "Attachments");
            OutsideRoot = Path.Combine(root, "Outside");
            Directory.CreateDirectory(AttachmentRoot);
            Directory.CreateDirectory(OutsideRoot);
        }

        public string Root { get; }

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

            var linkedDirectory = Path.Combine(AttachmentRoot, "linked");
            if (Directory.Exists(linkedDirectory))
                Directory.Delete(linkedDirectory, recursive: false);
            Directory.Delete(Root, recursive: true);
        }
    }
}
