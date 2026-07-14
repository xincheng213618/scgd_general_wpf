using ColorVision.Copilot;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using WpfMath.Controls;
using WpfMath.Parsers;

namespace ColorVision.UI.Tests;

public sealed class CopilotMarkdownMathTests
{
    private static readonly string[] StreamingMarkdownUpdates =
    {
        "光通量 $\\Phi_v=683\\int P_e(\\lambda)V(\\lambda)d\\lambda$",
        "EQE $$\\mathrm{EQE}=\\frac{N_{photons}}{N_{electrons}}$$",
        "光通量与 EQE：\n\n$$\\Phi_v=683\\int_0^\\infty P_{e,\\lambda}(\\lambda)V(\\lambda)\\,d\\lambda$$\n\n"
            + "$$\\mathrm{EQE}=\\frac{P_{opt}/(hc/\\lambda)}{I/q}$$\n\n"
            + "兴奋纯度 $p_e=\\frac{\\sqrt{(x-x_n)^2+(y-y_n)^2}}{\\sqrt{(x_d-x_n)^2+(y_d-y_n)^2}}$",
        "最终内容包含无法解析的公式 $\\frac{$，但仍应显示。",
    };

    [Fact]
    public void InlineParserRecognizesMathButSkipsCodeEscapesAndCurrency()
    {
        const string source = "半径 $r = \\sqrt{x^2 + y^2}$，`$not_math$`，\\$100，\\(k_1 + k_2\\)；价格 $10 到 $20";

        var segments = CopilotMarkdownMath.ParseInline(source);
        var formulas = segments.Where(segment => segment.IsMath).ToArray();

        Assert.Collection(
            formulas,
            formula => Assert.Equal("r = \\sqrt{x^2 + y^2}", formula.Content),
            formula => Assert.Equal("k_1 + k_2", formula.Content));
        Assert.Contains("`$not_math$`", string.Concat(segments.Select(segment => segment.OriginalText)), StringComparison.Ordinal);
        Assert.Contains("价格 $10 到 $20", string.Concat(segments.Select(segment => segment.OriginalText)), StringComparison.Ordinal);
    }

    [Fact]
    public void DisplayParserSplitsAdjacentDollarAndBracketFormulas()
    {
        Assert.True(CopilotMarkdownMath.TryParseDisplayLine(
            "$$x_{corrected}=x(1+k_1r^2)$$$$y_{corrected}=y(1+k_1r^2)$$",
            out var adjacent));
        Assert.Collection(
            adjacent,
            formula => Assert.Equal("x_{corrected}=x(1+k_1r^2)", formula.Content),
            formula => Assert.Equal("y_{corrected}=y(1+k_1r^2)", formula.Content));

        Assert.True(CopilotMarkdownMath.TryParseDisplayLine("\\[r = \\sqrt{x^2+y^2}\\]", out var bracket));
        var bracketFormula = Assert.Single(bracket);
        Assert.Equal("r = \\sqrt{x^2+y^2}", bracketFormula.Content);
        Assert.Equal("\\[r = \\sqrt{x^2+y^2}\\]", bracketFormula.OriginalText);
    }

    [Fact]
    public void DisplayParserDetectsMultilineStartAndRejectsMalformedInput()
    {
        Assert.True(CopilotMarkdownMath.TryStartDisplayBlock(
            "$$x = \\frac{1}{2}",
            out var opening,
            out var closing,
            out var initialContent));
        Assert.Equal("$$", opening);
        Assert.Equal("$$", closing);
        Assert.Equal("x = \\frac{1}{2}", initialContent);

        Assert.False(CopilotMarkdownMath.TryParseDisplayLine("$$not closed", out _));
        Assert.False(CopilotMarkdownMath.TryParseDisplayLine("$$$$", out _));
    }

    [Theory]
    [InlineData(@"x_{corrected}=x(1+k_1r^2+k_2r^4+k_3r^6)")]
    [InlineData(@"r=\sqrt{x^2+y^2}")]
    [InlineData(@"x_{corrected}=x+[2p_1xy+p_2(r^2+2x^2)]")]
    [InlineData(@"\Phi_v=683\int_0^\infty P_{e,\lambda}(\lambda)V(\lambda)\,d\lambda")]
    [InlineData(@"\mathrm{EQE}=\frac{P_{opt}/(hc/\lambda)}{I/q}")]
    public void ScreenshotFormulasAreAcceptedByWpfMath(string latex)
    {
        var formula = WpfTeXFormulaParser.Instance.Parse(latex);

        Assert.NotNull(formula);
    }

    [Fact]
    public void MarkdownViewCreatesInlineAndDisplayFormulaControls()
    {
        RunOnStaThread(() =>
        {
            var view = new CopilotMarkdownView
            {
                Markdown = "半径 $r=\\sqrt{x^2+y^2}$。\n\n$$x_{corrected}=x(1+k_1r^2)$$\n\n无法解析 $\\frac{$",
            };
            var window = new Window
            {
                Content = view,
                Height = 320,
                ShowInTaskbar = false,
                Width = 640,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                PumpDispatcher(TimeSpan.FromMilliseconds(250));

                var viewer = Assert.IsType<RichTextBox>(LogicalTreeHelper.FindLogicalNode(view, "DocumentViewer"));
                var inlineFormula = viewer.Document.Blocks
                    .OfType<Paragraph>()
                    .SelectMany(paragraph => paragraph.Inlines.OfType<InlineUIContainer>())
                    .Select(container => container.Child)
                    .OfType<FormulaControl>()
                    .Single();
                var displayFormula = viewer.Document.Blocks
                    .OfType<BlockUIContainer>()
                    .Select(container => container.Child)
                    .OfType<Viewbox>()
                    .Select(viewbox => viewbox.Child)
                    .OfType<FormulaControl>()
                    .Single();

                Assert.Equal(@"r=\sqrt{x^2+y^2}", inlineFormula.Formula);
                Assert.Equal("x_{corrected}=x(1+k_1r^2)", displayFormula.Formula);
                Assert.Null(inlineFormula.ErrorTemplate);
                Assert.Null(displayFormula.ErrorTemplate);
                var fallbackText = string.Concat(viewer.Document.Blocks
                    .OfType<Paragraph>()
                    .SelectMany(paragraph => paragraph.Inlines.OfType<Run>())
                    .Select(run => run.Text));
                Assert.Contains(@"$\frac{$", fallbackText, StringComparison.Ordinal);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MarkdownViewCanReplaceFormulaDocumentDuringStreamingUpdates()
    {
        RunOnStaThread(() =>
        {
            var view = new CopilotMarkdownView();
            var window = new Window
            {
                Content = view,
                Height = 320,
                ShowInTaskbar = false,
                Width = 640,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                foreach (var markdown in StreamingMarkdownUpdates)
                {
                    view.Markdown = markdown;
                    PumpDispatcher(TimeSpan.FromMilliseconds(150));
                }

                var viewer = Assert.IsType<RichTextBox>(LogicalTreeHelper.FindLogicalNode(view, "DocumentViewer"));
                var text = new TextRange(viewer.Document.ContentStart, viewer.Document.ContentEnd).Text;
                Assert.Contains("最终内容", text, StringComparison.Ordinal);
                Assert.Contains(@"$\frac{$", text, StringComparison.Ordinal);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "The WPF render thread did not complete.");
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void PumpDispatcher(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = duration,
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
