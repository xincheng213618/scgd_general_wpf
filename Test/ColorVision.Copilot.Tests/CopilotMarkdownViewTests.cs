using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using WpfMath.Controls;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotMarkdownViewTests
{
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
    [InlineData("`plain`", "plain")]
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
    [InlineData("````markdown\n```csharp\nConsole.WriteLine(1);\n```\n````", "```csharp\nConsole.WriteLine(1);\n```", "markdown")]
    [InlineData("~~~~text\n~~~\n```csharp\n$$x^2$$\n```\n~~~~~", "~~~\n```csharp\n$$x^2$$\n```", "text")]
    [InlineData("```\n```", "", "代码")]
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
