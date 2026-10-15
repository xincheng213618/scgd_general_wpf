using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using WpfMath.Controls;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotMarkdownViewTests
{
    [Theory]
    [InlineData("**before `literal` after**", "before `literal` after")]
    [InlineData("**before ``a`b`` after**", "before ``a`b`` after")]
    public void BoldSpansKeepTheirCodeDelimitersAsStyledLiteralText(string markdown, string expectedText)
    {
        StaTest.Run(() =>
        {
            var paragraph = Assert.IsType<Paragraph>(Assert.Single(BuildDocument(markdown).Blocks.Cast<Block>()));
            var span = Assert.IsType<Span>(Assert.Single(paragraph.Inlines));

            Assert.Equal(FontWeights.SemiBold, span.FontWeight);
            Assert.Equal(expectedText, ParagraphText(paragraph));
            Assert.All(span.Inlines.Cast<Inline>(), inline => Assert.IsType<Run>(inline));
            Assert.DoesNotContain(span.Inlines.OfType<Run>(), run => run.FontFamily.Source == "Consolas");
        }, TimeSpan.FromSeconds(10), "The STA bold Markdown compatibility test did not finish.");
    }

    [Theory]
    [InlineData("`[`", "[")]
    [InlineData("``a`[``", "a`[")]
    public void BracketsInsideInlineCodeDoNotConsumeTheFollowingLink(string codeSpan, string expectedCode)
    {
        StaTest.Run(() =>
        {
            var document = BuildDocument(codeSpan + " [guide](https://example.test/readme)");
            var paragraph = Assert.IsType<Paragraph>(Assert.Single(document.Blocks.Cast<Block>()));

            Assert.Equal(expectedCode + " guide", ParagraphText(paragraph));
            var codeRun = Assert.Single(paragraph.Inlines.OfType<Run>(), run => run.FontFamily.Source == "Consolas");
            Assert.Equal(expectedCode, codeRun.Text);
            var link = Assert.Single(paragraph.Inlines.OfType<Hyperlink>());
            Assert.Equal("guide", Assert.IsType<Run>(Assert.Single(link.Inlines)).Text);
            Assert.Equal("https://example.test/readme", link.NavigateUri.AbsoluteUri);
        }, TimeSpan.FromSeconds(10), "The STA code and link compatibility test did not finish.");
    }

    [Theory]
    [InlineData("[guide", "https://example.test/readme")]
    [InlineData("outer[inner", "https://example.test/path(one)/after")]
    [InlineData("guide", "https://example.test/a(one)b(two)")]
    public void LinksPreserveBracketLabelsAndOneLevelParenthesizedTargets(string label, string target)
    {
        StaTest.Run(() =>
        {
            var document = BuildDocument("before [" + label + "](" + target + ") after");
            var paragraph = Assert.IsType<Paragraph>(Assert.Single(document.Blocks.Cast<Block>()));
            var link = Assert.Single(paragraph.Inlines.OfType<Hyperlink>());

            Assert.Equal("before " + label + " after", ParagraphText(paragraph));
            Assert.Equal(label, Assert.IsType<Run>(Assert.Single(link.Inlines)).Text);
            Assert.Equal(target, link.NavigateUri.AbsoluteUri);
        }, TimeSpan.FromSeconds(10), "The STA Markdown link compatibility test did not finish.");
    }

    [Theory]
    [InlineData("before [unfinished $x^2$ after", "before [unfinished  after", "x^2")]
    [InlineData("before [label](unfinished $x^2$ after", "before [label](unfinished  after", "x^2")]
    [InlineData("before **unfinished $x^2$ after", "before **unfinished  after", "x^2")]
    [InlineData("before $x+[y]$ after", "before  after", "x+[y]")]
    [InlineData(@"before \(unfinished $x^2$ after", @"before \(unfinished  after", "x^2")]
    [InlineData(@"before \( \) then \(x^2\) after", @"before \( \) then  after", "x^2")]
    [InlineData(@"before $x $x $x \(x^2\) after", "before $x $x $x  after", "x^2")]
    public void UnclosedInlineCandidatesPreservePlainTextAndMath(string markdown, string expectedPlainText, string expectedFormula)
    {
        StaTest.Run(() =>
        {
            var paragraph = Assert.IsType<Paragraph>(Assert.Single(BuildDocument(markdown).Blocks.Cast<Block>()));
            var formula = Assert.Single(paragraph.Inlines.OfType<InlineUIContainer>());

            Assert.Equal(expectedFormula, Assert.IsType<FormulaControl>(formula.Child).Formula);
            Assert.Equal(expectedPlainText, string.Concat(paragraph.Inlines.OfType<Run>().Select(run => run.Text)));
            Assert.All(paragraph.Inlines.Cast<Inline>(), inline => Assert.True(inline is Run or InlineUIContainer));
        }, TimeSpan.FromSeconds(10), "The STA incomplete inline Markdown compatibility test did not finish.");
    }

    [Theory]
    [InlineData(@"before \(x^2\) between \(y^2\) after")]
    [InlineData("before $x^2$ between $y^2$ after")]
    public void ConsecutiveInlineFormulasKeepTheirSeparateClosersAndSurroundingText(string markdown)
    {
        StaTest.Run(() =>
        {
            var paragraph = Assert.IsType<Paragraph>(Assert.Single(BuildDocument(markdown).Blocks.Cast<Block>()));
            var inlines = paragraph.Inlines.Cast<Inline>().ToArray();

            Assert.Equal(5, inlines.Length);
            Assert.Equal("before ", Assert.IsType<Run>(inlines[0]).Text);
            Assert.Equal("x^2", Assert.IsType<FormulaControl>(Assert.IsType<InlineUIContainer>(inlines[1]).Child).Formula);
            Assert.Equal(" between ", Assert.IsType<Run>(inlines[2]).Text);
            Assert.Equal("y^2", Assert.IsType<FormulaControl>(Assert.IsType<InlineUIContainer>(inlines[3]).Child).Formula);
            Assert.Equal(" after", Assert.IsType<Run>(inlines[4]).Text);
        }, TimeSpan.FromSeconds(10), "The STA consecutive inline formula test did not finish.");
    }

    [Theory]
    [InlineData("[")]
    [InlineData(@"\(x")]
    [InlineData("$x ")]
    public void LongUnclosedInlineCandidatesRenderEveryCharacterAsPlainText(string candidate)
    {
        StaTest.Run(() =>
        {
            var markdown = "before " + string.Concat(Enumerable.Repeat(candidate, 8_192)) + " after";
            var paragraph = Assert.IsType<Paragraph>(Assert.Single(BuildDocument(markdown).Blocks.Cast<Block>()));

            Assert.Equal(markdown, ParagraphText(paragraph));
            Assert.All(paragraph.Inlines.Cast<Inline>(), inline => Assert.IsType<Run>(inline));
        }, TimeSpan.FromSeconds(10), "The STA long literal Markdown compatibility test did not finish.");
    }

    [Theory]
    [InlineData("`plain`", "plain")]
    [InlineData("``a`b``", "a`b")]
    [InlineData("``a```b``", "a```b")]
    [InlineData("```a``b`c```", "a``b`c")]
    [InlineData("``a`$x$ [guide](https://example.test)``", "a`$x$ [guide](https://example.test)")]
    public void InlineCodePreservesDifferentLengthBackticksAndLiteralMathAndLinks(string codeSpan, string expectedCode)
    {
        StaTest.Run(() =>
        {
            var document = BuildDocument("before " + codeSpan + " after");
            var paragraph = Assert.IsType<Paragraph>(Assert.Single(document.Blocks.Cast<Block>()));

            Assert.Equal("before " + expectedCode + " after", ParagraphText(paragraph));
            Assert.All(paragraph.Inlines.Cast<Inline>(), inline => Assert.IsType<Run>(inline));
            var codeRun = Assert.Single(paragraph.Inlines.OfType<Run>(), run => run.FontFamily.Source == "Consolas");
            Assert.Equal(expectedCode, codeRun.Text);
        }, TimeSpan.FromSeconds(10), "The STA inline Markdown code test did not finish.");
    }

    [Theory]
    [InlineData("`plain")]
    [InlineData("``a`b")]
    [InlineData("``a```")]
    [InlineData("```a``b")]
    [InlineData("`unfinished $x^2$")]
    public void UnclosedInlineCodeDelimitersRemainLiteral(string codeSpan)
    {
        StaTest.Run(() =>
        {
            var markdown = "before " + codeSpan + " after";
            var document = BuildDocument(markdown);
            var paragraph = Assert.IsType<Paragraph>(Assert.Single(document.Blocks.Cast<Block>()));

            Assert.Equal(markdown, ParagraphText(paragraph));
            Assert.All(paragraph.Inlines.Cast<Inline>(), inline => Assert.IsType<Run>(inline));
            Assert.DoesNotContain(paragraph.Inlines.OfType<Run>(), run => run.FontFamily.Source == "Consolas");
        }, TimeSpan.FromSeconds(10), "The STA unclosed inline Markdown code test did not finish.");
    }

    [Theory]
    [InlineData("before\n$$x^2$$\nafter")]
    [InlineData("before\n\\[x^2\\]\nafter")]
    [InlineData("before\n$$\nx^2\n$$\nafter")]
    [InlineData("before\n\\[\nx^2\n\\]\nafter")]
    [InlineData("before\n$$\nx^2")]
    [InlineData("before\n\\[\nx^2")]
    public void DisplayMathInsideCodeFencesRemainsLiteralAndDoesNotConsumeFollowingBlocks(string code)
    {
        StaTest.Run(() =>
        {
            var view = new CopilotMarkdownView();
            var buildDocument = typeof(CopilotMarkdownView).GetMethod(
                "BuildMarkdownDocument",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The Markdown document builder is unavailable.");
            var document = Assert.IsType<FlowDocument>(buildDocument.Invoke(
                view,
                ["```text\n" + code + "\n```\nafter fence\n\n$$y^2$$"]));
            var blocks = document.Blocks.Cast<Block>().ToArray();

            Assert.Equal(3, blocks.Length);
            var codeBlock = Assert.IsType<BlockUIContainer>(blocks[0]);
            var border = Assert.IsType<Border>(codeBlock.Child);
            var grid = Assert.IsType<Grid>(border.Child);
            var codeTextBox = Assert.Single(grid.Children.OfType<TextBox>());
            var header = Assert.Single(grid.Children.OfType<DockPanel>());
            var copyButton = Assert.Single(header.Children.OfType<Button>());
            var expectedCode = code.Replace("\n", Environment.NewLine, StringComparison.Ordinal);

            Assert.True(codeTextBox.IsReadOnly);
            Assert.Equal(expectedCode, codeTextBox.Text);
            Assert.Equal(expectedCode, Assert.IsType<string>(copyButton.Tag));

            var paragraph = Assert.IsType<Paragraph>(blocks[1]);
            Assert.Equal("after fence", Assert.IsType<Run>(Assert.Single(paragraph.Inlines)).Text);

            var formulaBlock = Assert.IsType<BlockUIContainer>(blocks[2]);
            var viewbox = Assert.IsType<Viewbox>(formulaBlock.Child);
            Assert.Equal("y^2", Assert.IsType<FormulaControl>(viewbox.Child).Formula);
        }, TimeSpan.FromSeconds(10), "The STA Markdown document test did not finish.");
    }

    [Theory]
    [InlineData("````markdown\n```csharp\nConsole.WriteLine(1);\n```\n````", "```csharp\nConsole.WriteLine(1);\n```", "markdown")]
    [InlineData("`````csharp\n````\nint value = 1;\n```\n``````", "````\nint value = 1;\n```", "csharp")]
    [InlineData("~~~~text\n~~~\n```csharp\n$$x^2$$\n```\n~~~~~", "~~~\n```csharp\n$$x^2$$\n```", "text")]
    [InlineData("```text\nbefore\n```not-a-close\n~~~\nafter\n````", "before\n```not-a-close\n~~~\nafter", "text")]
    [InlineData("```\n```", "", "代码")]
    [InlineData("```text\nalpha\n\n\n```", "alpha\n\n", "text")]
    public void ClosedFencesPreserveTheirLiteralCodeAndTheBlocksThatFollow(string fencedMarkdown, string expectedCode, string expectedLanguage)
    {
        StaTest.Run(() =>
        {
            var document = BuildDocument(fencedMarkdown + "\nafter fence\n\n$$y^2$$");
            var blocks = document.Blocks.Cast<Block>().ToArray();

            Assert.Equal(3, blocks.Length);
            AssertCodeCard(blocks[0], expectedCode, expectedLanguage);
            Assert.Equal("after fence", ParagraphText(Assert.IsType<Paragraph>(blocks[1])));
            AssertFormulaBlock(blocks[2], "y^2");
        }, TimeSpan.FromSeconds(10), "The STA fenced Markdown document test did not finish.");
    }

    [Theory]
    [InlineData("```text\n\talpha\n   ```", "\talpha")]
    [InlineData(" ```text\n alpha\n  beta\nplain\n   ```", "alpha\n beta\nplain")]
    [InlineData("  ```text\n\talpha\n beta\n  gamma\n ```", "  alpha\nbeta\ngamma")]
    [InlineData("   ```text\n   alpha\n    beta\nplain\n```", "alpha\n beta\nplain")]
    public void FencedCodeRemovesOnlyTheOpeningIndentationColumns(string fencedMarkdown, string expectedCode)
    {
        StaTest.Run(() =>
        {
            var document = BuildDocument(fencedMarkdown + "\nafter fence\n\n$$y^2$$");
            var blocks = document.Blocks.Cast<Block>().ToArray();

            Assert.Equal(3, blocks.Length);
            AssertCodeCard(blocks[0], expectedCode, "text");
            Assert.Equal("after fence", ParagraphText(Assert.IsType<Paragraph>(blocks[1])));
            AssertFormulaBlock(blocks[2], "y^2");
        }, TimeSpan.FromSeconds(10), "The STA indented Markdown fence test did not finish.");
    }

    [Theory]
    [InlineData("```", "", "代码")]
    [InlineData("```text\nbefore\n$$x^2$$\n# still code", "before\n$$x^2$$\n# still code", "text")]
    public void AnUnclosedFenceKeepsItsCodeCardAndEveryFollowingLineLiteral(string markdown, string expectedCode, string expectedLanguage)
    {
        StaTest.Run(() =>
        {
            var document = BuildDocument(markdown);

            AssertCodeCard(Assert.Single(document.Blocks.Cast<Block>()), expectedCode, expectedLanguage);
        }, TimeSpan.FromSeconds(10), "The STA unclosed Markdown fence test did not finish.");
    }

    [Fact]
    public void ABacktickInTheOpeningInfoDoesNotTurnFollowingParagraphsAndMathIntoCode()
    {
        StaTest.Run(() =>
        {
            var document = BuildDocument("```bad`info\n\nafter invalid fence\n\n$$y^2$$");
            var blocks = document.Blocks.Cast<Block>().ToArray();

            Assert.Equal(3, blocks.Length);
            Assert.IsType<Paragraph>(blocks[0]);
            Assert.Equal("after invalid fence", ParagraphText(Assert.IsType<Paragraph>(blocks[1])));
            AssertFormulaBlock(blocks[2], "y^2");
        }, TimeSpan.FromSeconds(10), "The STA invalid Markdown fence test did not finish.");
    }

    private static FlowDocument BuildDocument(string markdown)
    {
        var view = new CopilotMarkdownView();
        var buildDocument = typeof(CopilotMarkdownView).GetMethod("BuildMarkdownDocument", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("The Markdown document builder is unavailable.");
        return Assert.IsType<FlowDocument>(buildDocument.Invoke(view, [markdown]));
    }

    private static void AssertCodeCard(Block block, string expectedCode, string expectedLanguage)
    {
        var container = Assert.IsType<BlockUIContainer>(block);
        var border = Assert.IsType<Border>(container.Child);
        var grid = Assert.IsType<Grid>(border.Child);
        var codeTextBox = Assert.Single(grid.Children.OfType<TextBox>());
        var header = Assert.Single(grid.Children.OfType<DockPanel>());
        var copyButton = Assert.Single(header.Children.OfType<Button>());
        var languageLabel = Assert.Single(header.Children.OfType<TextBlock>());
        var platformCode = expectedCode.Replace("\n", Environment.NewLine, StringComparison.Ordinal);

        Assert.True(codeTextBox.IsReadOnly);
        Assert.Equal(platformCode, codeTextBox.Text);
        Assert.Equal(platformCode, Assert.IsType<string>(copyButton.Tag));
        Assert.Equal(expectedLanguage, languageLabel.Text);
    }

    private static string ParagraphText(Paragraph paragraph) => new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.TrimEnd('\r', '\n');

    private static void AssertFormulaBlock(Block block, string expectedFormula)
    {
        var formulaBlock = Assert.IsType<BlockUIContainer>(block);
        var viewbox = Assert.IsType<Viewbox>(formulaBlock.Child);
        Assert.Equal(expectedFormula, Assert.IsType<FormulaControl>(viewbox.Child).Formula);
    }
}
