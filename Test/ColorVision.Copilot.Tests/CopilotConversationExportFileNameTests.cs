using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotConversationExportFileNameTests
{
    [Theory]
    [InlineData("con.session.md", false)]
    [InlineData("COM¹.md", false)]
    [InlineData("LPT³.notes.txt", false)]
    [InlineData("console.session.md", true)]
    [InlineData("COM10.md", true)]
    [InlineData(".con.md", true)]
    public void ExportNameHintsRespectWindowsDeviceNamesWithoutRejectingOrdinaryNames(string requestedName, bool allowed)
    {
        var accepted = CopilotConversationMarkdownExporter.TryNormalizeFileNameHint(requestedName, out var fileName, out var error);

        Assert.Equal(allowed, accepted);
        if (allowed)
        {
            Assert.Equal(requestedName, fileName);
            Assert.Empty(error);
        }
        else
        {
            Assert.Contains("Windows 保留文件名", error);
        }
    }

    [Fact]
    public void SuggestedExportNameEscapesDeviceNamesBeforeMultipleExtensions()
    {
        var conversation = new CopilotConversationRecord { Title = "con.session.backup" };

        var fileName = CopilotConversationMarkdownExporter.BuildFileName(conversation);

        Assert.False(fileName.StartsWith("con.", StringComparison.OrdinalIgnoreCase));
        Assert.True(CopilotConversationMarkdownExporter.TryNormalizeFileNameHint(fileName, out _, out var error), error);
    }

    [Fact]
    public void SuggestedExportNameKeepsACompleteUnicodePrefixWhenTheTitleIsTruncated()
    {
        var prefix = new string('a', 63);
        var conversation = new CopilotConversationRecord { Title = prefix + "😀 title" };

        var fileName = CopilotConversationMarkdownExporter.BuildFileName(conversation);

        Assert.NotEmpty(new UTF8Encoding(false, true).GetBytes(fileName));
        Assert.StartsWith(prefix + "-", fileName, StringComparison.Ordinal);
        Assert.True(CopilotConversationMarkdownExporter.TryNormalizeFileNameHint(fileName, out _, out var error), error);
    }
}
