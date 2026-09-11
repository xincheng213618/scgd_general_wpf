using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotComposerMentionRangeTests
{
    [Fact]
    public void ParsingAtTheCaretIgnoresTheLaterMentionAndPreservesTheEntireSuffix()
    {
        const string input = "before @file keep @later\nlast instruction";
        const string prefixThroughCaret = "before @file";

        Assert.True(CopilotComposerReferenceCatalog.TryParseMention(input, out var mention, prefixThroughCaret.Length));

        Assert.Equal("before ".Length, mention.StartIndex);
        Assert.Equal(prefixThroughCaret.Length, mention.EndIndex);
        Assert.Equal("file", mention.Query);
        Assert.Equal(
            "before @[Target.cs]  keep @later\nlast instruction",
            CopilotComposerReferenceCatalog.CompleteMention(input, mention, "Target.cs"));
    }

    [Fact]
    public void InsertingInTheMiddleDoesNotJumpToAnUnfinishedMentionInTheSuffix()
    {
        const string input = "before keep @later";

        var updated = CopilotComposerReferenceCatalog.InsertMention(input, "before ".Length, 0, out var caretIndex);

        Assert.Equal("before @ keep @later", updated);
        Assert.Equal("before @".Length, caretIndex);
        Assert.True(CopilotComposerReferenceCatalog.TryParseMention(updated, out var mention, caretIndex));
        Assert.Equal(string.Empty, mention.Query);
        Assert.Equal("before ".Length, mention.StartIndex);
    }

    [Fact]
    public void CompletedReferencesStayClosedWhenTheCaretMovesInsideOrPastTheirLabel()
    {
        const string input = "@[File.cs] remaining text";
        for (var caretIndex = 0; caretIndex <= input.Length; caretIndex++)
        {
            Assert.False(CopilotComposerReferenceCatalog.TryParseMention(input, out _, caretIndex),
                $"Caret {caretIndex} reopened a completed reference.");
        }

        const string nextMention = "@[File.cs] @next";
        Assert.True(CopilotComposerReferenceCatalog.TryParseMention(nextMention, out var mention));
        Assert.Equal("next", mention.Query);
    }

    [Fact]
    public void AClosingBracketOnALaterLineDoesNotCloseTheCurrentMention()
    {
        const string input = "@[open\nlater bracket ]";

        Assert.True(CopilotComposerReferenceCatalog.TryParseMention(input, out var mention, "@[open".Length));

        Assert.Equal("[open", mention.Query);
        Assert.Equal("@[open".Length, mention.EndIndex);
        Assert.False(CopilotComposerReferenceCatalog.TryParseMention(input, out _));
    }

    [Theory]
    [InlineData(-100, false)]
    [InlineData(0, false)]
    [InlineData(7, true)]
    [InlineData(int.MaxValue, true)]
    public void OutOfRangeCaretsAreClampedAndTheNormalEndPositionStillParses(int caretIndex, bool expected)
    {
        const string input = "go @ref";

        Assert.Equal(expected, CopilotComposerReferenceCatalog.TryParseMention(input, out var mention, caretIndex));

        if (expected)
        {
            Assert.Equal(input.Length, mention.EndIndex);
            Assert.Equal("ref", mention.Query);
        }
        Assert.False(CopilotComposerReferenceCatalog.TryParseMention(null, out _, caretIndex));
    }

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
