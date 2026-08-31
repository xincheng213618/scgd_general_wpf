using ColorVision.Copilot;
using System.Reflection;
using System.Runtime.ExceptionServices;
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
        RunOnSta(() =>
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
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "The STA Markdown document test did not finish.");
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
