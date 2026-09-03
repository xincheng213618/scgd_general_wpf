using ST.Library.UI.NodeEditor;
using System.Reflection;
#if COLORVISION_WINDOW_RESIZE_DIAGNOSTICS
using ColorVision.Windowing;
using System.Windows;
using System.Windows.Controls;
#endif

namespace ColorVision.UI.Tests;

public sealed class WindowResizeDiagnosticsContractTests
{
    [Fact]
    public void DiagnosticsAreOnlyPresentInExplicitDiagnosticBuilds()
    {
        Type? windowTrace = typeof(MainWindow).Assembly.GetType("ColorVision.Windowing.MainWindowResizeDiagnostics");
        Type? opaqueCaption = typeof(MainWindow).Assembly.GetType("ColorVision.Windowing.OpaqueDiagnosticCaptionButtons");
        Assert.Null(opaqueCaption);
        MethodInfo? beginCapture = typeof(STNodeEditor).GetMethod("BeginResizeDiagnosticCapture");
        MethodInfo? snapshot = typeof(STNodeEditor).GetMethod("GetResizeDiagnosticCapture");
        MethodInfo? stopCapture = typeof(STNodeEditor).GetMethod("StopResizeDiagnosticCapture");
#if COLORVISION_WINDOW_RESIZE_DIAGNOSTICS
        Assert.NotNull(windowTrace);
        Assert.NotNull(beginCapture);
        Assert.NotNull(snapshot);
        Assert.NotNull(stopCapture);
#else
        Assert.Null(windowTrace);
        Assert.Null(beginCapture);
        Assert.Null(snapshot);
        Assert.Null(stopCapture);
#endif
    }

#if COLORVISION_WINDOW_RESIZE_DIAGNOSTICS
    [Theory]
    [InlineData(null, false, false)]
    [InlineData(null, true, true)]
    [InlineData("", true, true)]
    [InlineData("native", true, false)]
    [InlineData("compact", false, true)]
    [InlineData("compact-opaque", false, false)]
    [InlineData("compact-opaque", true, true)]
    [InlineData(" compact\r\n", false, true)]
    [InlineData("NATIVE", true, true)]
    [InlineData("native;compact", true, true)]
    [InlineData("unknown", false, false)]
    public void DiagnosticModeOnlyAcceptsExplicitModesWithoutChangingConfiguration(string? text, bool configured, bool expected)
    {
        Type traceType = typeof(MainWindow).Assembly.GetType("ColorVision.Windowing.MainWindowResizeDiagnostics", throwOnError: true)!;
        MethodInfo parse = traceType.GetMethod("ParseMode", BindingFlags.Static | BindingFlags.NonPublic)!;

        Assert.Equal(expected, Assert.IsType<bool>(parse.Invoke(null, [text, configured])));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("native", false)]
    [InlineData("compact", true)]
    [InlineData("compact-opaque", null)]
    [InlineData(" invalid ", null)]
    public void DiagnosticMetadataDistinguishesAnOverrideFromFallback(string? text, bool? expected)
    {
        Type traceType = typeof(MainWindow).Assembly.GetType("ColorVision.Windowing.MainWindowResizeDiagnostics", throwOnError: true)!;
        MethodInfo parse = traceType.GetMethod("ParseModeOverride", BindingFlags.Static | BindingFlags.NonPublic)!;

        Assert.Equal(expected, (bool?)parse.Invoke(null, [text]));
    }

    [Fact]
    public void ExplicitVisualRescanFindsAnEditorAddedAfterTheInitialScanWithoutLayoutOrCapture()
    {
        StaTest.Run(() =>
        {
            var tree = new Grid();
            var found = new List<STNodeEditor>();
            var initial = MainWindowResizeDiagnostics.ScanVisualEditors(tree, found.Add, 20);
            Assert.Equal(1, initial.Visited);
            Assert.Equal(0, initial.Matches);
            Assert.False(initial.LimitReached);
            Assert.Equal(0, initial.Errors);

            using var editor = new STNodeEditor();
            tree.Children.Add(new Border { Child = editor });
            Size originalRenderSize = tree.RenderSize;
            bool originalMeasureValid = tree.IsMeasureValid;
            bool originalArrangeValid = tree.IsArrangeValid;
            var later = MainWindowResizeDiagnostics.ScanVisualEditors(tree, found.Add, 20);

            Assert.Same(editor, Assert.Single(found));
            Assert.Equal(3, later.Visited);
            Assert.Equal(1, later.Matches);
            Assert.False(later.LimitReached);
            Assert.Equal(0, later.Errors);
            Assert.Equal(originalRenderSize, tree.RenderSize);
            Assert.Equal(originalMeasureValid, tree.IsMeasureValid);
            Assert.Equal(originalArrangeValid, tree.IsArrangeValid);
            Assert.False(editor.IsLoaded);
            Assert.Null(System.Windows.PresentationSource.FromVisual(editor));
            Assert.False(editor.GetResizeDiagnosticCapture().IsCapturing);
            Assert.Empty(editor.GetResizeDiagnosticCapture().Samples);
        });
    }

    [Fact]
    public void VisualDiscoveryIsBoundedAndReportsSkippedVisuals()
    {
        StaTest.Run(() =>
        {
            using var editor = new STNodeEditor();
            var tree = new Grid();
            tree.Children.Add(new Border { Child = editor });
            var found = new List<STNodeEditor>();

            var limited = MainWindowResizeDiagnostics.ScanVisualEditors(tree, found.Add, 2);

            Assert.Equal(2, limited.Visited);
            Assert.Equal(0, limited.Matches);
            Assert.True(limited.LimitReached);
            Assert.Equal(0, limited.Errors);
            Assert.Empty(found);

            var complete = MainWindowResizeDiagnostics.ScanVisualEditors(tree, found.Add, 3);
            Assert.Equal(3, complete.Visited);
            Assert.Equal(1, complete.Matches);
            Assert.False(complete.LimitReached);
            Assert.Same(editor, Assert.Single(found));
        });
    }

    [Fact]
    public void DiscoveryCallbackFailureIsCountedAndDoesNotPreventTheNextEditor()
    {
        StaTest.Run(() =>
        {
            using var first = new STNodeEditor();
            using var second = new STNodeEditor();
            var tree = new Grid();
            tree.Children.Add(new Border { Child = first });
            tree.Children.Add(new Border { Child = second });
            var found = new List<STNodeEditor>();

            var scan = MainWindowResizeDiagnostics.ScanVisualEditors(tree, editor =>
            {
                if (ReferenceEquals(editor, first)) throw new InvalidOperationException();
                found.Add(editor);
            }, 20);

            Assert.Equal(5, scan.Visited);
            Assert.Equal(2, scan.Matches);
            Assert.Equal(1, scan.Errors);
            Assert.False(scan.LimitReached);
            Assert.Same(second, Assert.Single(found));
        });
    }
#endif
}
