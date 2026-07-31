using ColorVision.Copilot;
using System.IO;
using System.Linq;
using System.Threading;

namespace ColorVision.UI.Tests;

public sealed class CopilotComposerAttachmentServiceTests
{
    [Fact]
    public void NormalizeFilePathsTrimsCanonicalizesAndDeduplicates()
    {
        var relativePath = Path.Combine("attachments", "capture.png");

        var paths = CopilotComposerAttachmentService.NormalizeFilePaths(
            [" ", $"  {relativePath}  ", relativePath.ToUpperInvariant(), null!]);

        var path = Assert.Single(paths);
        Assert.Equal(Path.GetFullPath(relativePath), path);
    }

    [Fact]
    public void FilterExistingFilePathsKeepsOnlyReadableCandidatesAndHonorsCancellation()
    {
        var root = Path.Combine(Path.GetTempPath(), "ColorVision-CopilotAttachmentTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var existingPath = Path.Combine(root, "existing.txt");
            var missingPath = Path.Combine(root, "missing.txt");
            File.WriteAllText(existingPath, "test");

            var paths = CopilotComposerAttachmentService.FilterExistingFilePaths(
                [existingPath, missingPath],
                CancellationToken.None);

            Assert.Equal([existingPath], paths);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() =>
            {
                CopilotComposerAttachmentService.FilterExistingFilePaths([existingPath], cancellation.Token);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("script.ps1", true)]
    [InlineData("notes.txt", false)]
    [InlineData("README", false)]
    public void UnsafeFilePolicyBlocksExecutableShellExtensions(string fileName, bool expected)
    {
        Assert.Equal(expected, CopilotComposerAttachmentService.IsUnsafeFilePath(fileName));
    }

    [Fact]
    public void CapacityChecksImageLimitBeforeTotalLimit()
    {
        var conversation = new CopilotConversationRecord();
        for (var index = 0; index < CopilotComposerAttachmentService.MaximumAttachmentCount; index++)
            conversation.Attachments.Add(CopilotAttachmentItem.CreateImage($"image-{index}.png"));

        Assert.Equal(
            CopilotAttachmentCapacityResult.ImageLimit,
            CopilotComposerAttachmentService.EvaluateCapacity(conversation, CopilotAttachmentType.Image));
        Assert.Equal(
            CopilotAttachmentCapacityResult.AttachmentLimit,
            CopilotComposerAttachmentService.EvaluateCapacity(conversation, CopilotAttachmentType.Context));
    }

    [Fact]
    public void ValidateReportsAttachmentAndImageCounts()
    {
        var attachments = Enumerable.Range(0, CopilotComposerAttachmentService.MaximumAttachmentCount + 1)
            .Select(index => index == 0
                ? CopilotAttachmentItem.CreateImage("image.png")
                : CopilotAttachmentItem.CreateContext($"context-{index}"))
            .ToArray();

        var validation = CopilotComposerAttachmentService.Validate(attachments);

        Assert.False(validation.IsValid);
        Assert.Equal(CopilotAttachmentValidationFailure.AttachmentLimit, validation.Failure);
        Assert.Equal(CopilotComposerAttachmentService.MaximumAttachmentCount + 1, validation.AttachmentCount);
        Assert.Equal(1, validation.ImageCount);
    }
}
