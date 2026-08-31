using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using AvalonDock.Themes.VS2013.Themes;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public class AvalonDockThemeBindingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PaneGrip_PreservesInactiveAndActiveThemeColorsWithoutDrawingBindingErrors(bool isDark)
    {
        WpfTestHost.Invoke(() =>
        {
            using var trace = new BindingTrace();
            var theme = new AvalonDockTheme(isDark);
            var model = new LayoutAnchorable { Title = "Sample" };
            var title = new AnchorablePaneTitle
            {
                Model = model,
                Style = (Style)theme.ThemeResourceDictionary[typeof(AnchorablePaneTitle)]
            };
            title.Resources.MergedDictionaries.Add(theme.ThemeResourceDictionary);

            AssertGripColors(title, model, theme.ThemeResourceDictionary);

            // An empty pane and a subsequent theme replacement must also remain usable.
            title.Model = null;
            Arrange(title);
            var replacement = new AvalonDockTheme(!isDark);
            title.Resources.MergedDictionaries.Clear();
            title.Resources.MergedDictionaries.Add(replacement.ThemeResourceDictionary);
            title.Style = (Style)replacement.ThemeResourceDictionary[typeof(AnchorablePaneTitle)];
            title.Model = model;
            AssertGripColors(title, model, replacement.ThemeResourceDictionary);

            Assert.DoesNotContain("GeometryDrawing", trace.Output);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FloatingGrip_LoadsCorrectedThemeAndRetainsCaptionControls(bool isDark)
    {
        WpfTestHost.Invoke(() =>
        {
            using var trace = new BindingTrace();
            var theme = new AvalonDockTheme(isDark);
            var manager = new DockingManager { Theme = theme };
            var model = new LayoutAnchorable { Title = "Floating sample", Content = new Border() };
            var pane = new LayoutAnchorablePane(model);
            var floatingModel = new LayoutAnchorableFloatingWindow
            {
                RootPanel = new LayoutAnchorablePaneGroup(pane)
            };
            manager.Layout.FloatingWindows.Add(floatingModel);
            // The public Float entry point shows a window. Use its internal constructor
            // here so the actual floating-window resource lifecycle is tested without UI.
            var window = (LayoutAnchorableFloatingWindowControl)Activator.CreateInstance(
                typeof(LayoutAnchorableFloatingWindowControl),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null, args: new object[] { floatingModel }, culture: null)!;
            try
            {
                AssertGripColors(window, model, theme.ThemeResourceDictionary);
                Assert.IsType<Button>(window.Template.FindName("PART_PinClose", window));
                Assert.IsType<Button>(window.Template.FindName("PART_PinMaximize", window));
                Assert.IsType<DropDownButton>(window.Template.FindName("SinglePaneContextMenu", window));
                Assert.DoesNotContain("GeometryDrawing", trace.Output);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void AssertGripColors(Control control, LayoutAnchorable model, ResourceDictionary resources)
    {
        model.IsActive = false;
        Arrange(control);
        var grip = Assert.IsType<Rectangle>(control.Template.FindName("DragHandleTexture", control));
        Assert.Same(resources[ResourceKeys.ToolWindowCaptionInactiveGrip], grip.Fill);
        var mask = Assert.IsType<DrawingBrush>(grip.OpacityMask);
        Assert.Equal(TileMode.Tile, mask.TileMode);
        Assert.Equal(new Rect(0, 0, 4, 4), mask.Viewport);
        Assert.True(mask.CanFreeze);

        model.IsActive = true;
        Arrange(control);
        Assert.Same(resources[ResourceKeys.ToolWindowCaptionActiveGrip], grip.Fill);

        model.IsActive = false;
        Arrange(control);
        Assert.Same(resources[ResourceKeys.ToolWindowCaptionInactiveGrip], grip.Fill);
    }

    private static void Arrange(FrameworkElement element)
    {
        element.ApplyTemplate();
        element.Measure(new Size(600, 400));
        element.Arrange(new Rect(0, 0, 600, 400));
        element.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private sealed class BindingTrace : IDisposable
    {
        private readonly StringWriter _writer = new();
        private readonly TextWriterTraceListener _listener;
        private readonly SourceLevels _previousLevel;

        public BindingTrace()
        {
            _previousLevel = PresentationTraceSources.DataBindingSource.Switch.Level;
            PresentationTraceSources.Refresh();
            _listener = new TextWriterTraceListener(_writer);
            PresentationTraceSources.DataBindingSource.Listeners.Add(_listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        }

        public string Output => _writer.ToString();

        public void Dispose()
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(_listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = _previousLevel;
            _listener.Dispose();
            _writer.Dispose();
        }
    }
}
