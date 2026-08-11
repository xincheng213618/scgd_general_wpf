using ColorVision.Copilot;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotAttachmentPathTests
{
    [Fact]
    public async Task TextAttachmentThroughReparsePointIsRejectedAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-attachment-path-{Guid.NewGuid():N}");
        var outside = Path.Combine(root, "Outside");
        var linkedParent = Path.Combine(root, "LinkedParent");
        Directory.CreateDirectory(outside);
        var linkedFile = Path.Combine(linkedParent, "sample.txt");
        File.WriteAllText(Path.Combine(outside, "sample.txt"), "SECRET_PAYLOAD");
        Directory.CreateSymbolicLink(linkedParent, outside);

        try
        {
            var block = await CopilotConversationRequestBuilder.BuildAttachmentContextBlockAsync(
                [CopilotAttachmentItem.CreateFile(linkedFile)],
                cancellationToken: CancellationToken.None);

            Assert.Contains("reparse point", block, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SECRET_PAYLOAD", block, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(linkedParent, recursive: false);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ImagePayloadThroughReparsePointIsRejectedAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-image-path-{Guid.NewGuid():N}");
        var outside = Path.Combine(root, "Outside");
        var linkedParent = Path.Combine(root, "LinkedParent");
        Directory.CreateDirectory(outside);
        var linkedFile = Path.Combine(linkedParent, "sample.png");
        File.WriteAllBytes(Path.Combine(outside, "sample.png"), [0x89, 0x50, 0x4E, 0x47]);
        Directory.CreateSymbolicLink(linkedParent, outside);

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CopilotImagePayloadLoader.LoadImageBytesAsync(
                    linkedFile,
                    "sample.png",
                    CancellationToken.None));

            Assert.Contains("reparse point", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(linkedParent, recursive: false);
            Directory.Delete(root, recursive: true);
        }
    }
}
