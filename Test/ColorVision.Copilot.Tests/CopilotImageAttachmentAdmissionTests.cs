using ColorVision.Copilot;
using SkiaSharp;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotImageAttachmentAdmissionTests
{
    [Fact]
    public async Task InvalidBatchStartsNoManagedWrites()
    {
        var root = CreateTemporaryDirectory();
        var validPath = Path.Combine(root, "valid.png");
        var invalidPath = Path.Combine(root, "invalid.png");
        var storePath = Path.Combine(root, "store");
        await File.WriteAllBytesAsync(validPath, CreatePng());
        await File.WriteAllTextAsync(invalidPath, "not an image");

        try
        {
            var error = await Assert.ThrowsAsync<CopilotImageAttachmentAdmissionException>(() =>
                CopilotImageAttachmentAdmission.PersistAsync(
                    [
                        CopilotAttachmentItem.CreateImage(validPath),
                        CopilotAttachmentItem.CreateImage(invalidPath),
                    ],
                    storePath,
                    CancellationToken.None));

            Assert.Equal(
                CopilotImageAttachmentAdmissionFailureKind.RejectedInput,
                error.FailureKind);
            Assert.False(Directory.Exists(storePath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AcceptedImagesBecomeStableContentAddressedAttachments()
    {
        var root = CreateTemporaryDirectory();
        var sourcePath = Path.Combine(root, "source.png");
        var storePath = Path.Combine(root, "store");
        await File.WriteAllBytesAsync(sourcePath, CreatePng());
        var source = CopilotAttachmentItem.CreateImage(sourcePath, "Evidence");
        var context = CopilotAttachmentItem.CreateContext("stable context");

        try
        {
            var first = await CopilotImageAttachmentAdmission.PersistAsync(
                [source, context],
                storePath,
                CancellationToken.None);
            var firstImage = first[0];

            Assert.Equal(CopilotAttachmentType.Image, firstImage.Type);
            Assert.Equal("Evidence", firstImage.Title);
            Assert.StartsWith(
                Path.Combine(storePath, "image-"),
                firstImage.Value,
                StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(firstImage.Value));
            Assert.NotSame(source, firstImage);
            Assert.Equal("stable context", first[1].Value);

            File.Delete(sourcePath);
            var second = await CopilotImageAttachmentAdmission.PersistAsync(
                first,
                storePath,
                CancellationToken.None);

            Assert.Equal(firstImage.Value, second[0].Value);
            Assert.Single(Directory.GetFiles(storePath, "image-*"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ManagedStorageFailuresAreNotReportedAsInputRejection()
    {
        var root = CreateTemporaryDirectory();
        var sourcePath = Path.Combine(root, "source.png");
        var blockedStorePath = Path.Combine(root, "blocked");
        await File.WriteAllBytesAsync(sourcePath, CreatePng());
        await File.WriteAllTextAsync(blockedStorePath, "not a directory");

        try
        {
            var error = await Assert.ThrowsAsync<CopilotImageAttachmentAdmissionException>(() =>
                CopilotImageAttachmentAdmission.PersistAsync(
                    [CopilotAttachmentItem.CreateImage(sourcePath)],
                    blockedStorePath,
                    CancellationToken.None));

            Assert.Equal(
                CopilotImageAttachmentAdmissionFailureKind.Storage,
                error.FailureKind);
            Assert.Contains("磁盘空间", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            nameof(CopilotImageAttachmentAdmissionTests),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static byte[] CreatePng()
    {
        using var bitmap = new SKBitmap(4, 3);
        bitmap.Erase(new SKColor(32, 96, 160, 255));
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        Assert.NotNull(encoded);
        return encoded.ToArray();
    }
}
