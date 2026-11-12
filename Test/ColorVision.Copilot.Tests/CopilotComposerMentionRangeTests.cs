namespace ColorVision.Copilot.Tests;

public sealed class CopilotComposerMentionRangeTests
{
    [Fact]
    public void OrdinaryAndSkillCompletionsReplaceOnlyTheQueryRangeAndKeepEndCompletionCompatible()
    {
        const string input = "Use @review\nKeep this @later reference";
        Assert.True(CopilotComposerReferenceCatalog.TryParseMention(input, out var mention, "Use @review".Length));

        Assert.Equal(
            "Use @[Review] \nKeep this @later reference",
            CopilotComposerReferenceCatalog.CompleteMention(input, mention, "Review"));
        Assert.Equal(
            "Use $document-review \nKeep this @later reference",
            CopilotComposerReferenceCatalog.CompleteSkillMention(input, mention, "document-review"));

        const string endInput = "Use @review";
        Assert.True(CopilotComposerReferenceCatalog.TryParseMention(endInput, out var endMention));
        Assert.Equal("Use @[Review] ", CopilotComposerReferenceCatalog.CompleteMention(endInput, endMention, "Review"));
        Assert.Equal("Use $document-review ", CopilotComposerReferenceCatalog.CompleteSkillMention(endInput, endMention, "document-review"));
    }

    [Fact]
    public void QueryLengthAndLineLimitsApplyBeforeTheCaretRatherThanToThePreservedSuffix()
    {
        var query = new string('x', 80);
        var input = "@" + query + "\n" + new string('y', 200);

        Assert.True(CopilotComposerReferenceCatalog.TryParseMention(input, out var mention, query.Length + 1));
        Assert.Equal(query, mention.Query);
        Assert.False(CopilotComposerReferenceCatalog.TryParseMention("@" + query + "x", out _));
        Assert.False(CopilotComposerReferenceCatalog.TryParseMention(input, out _));
    }
}
