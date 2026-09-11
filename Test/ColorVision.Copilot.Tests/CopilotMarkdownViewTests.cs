using ColorVision.Copilot;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Documents;
using WpfMath.Controls;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotMarkdownViewTests
{
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
