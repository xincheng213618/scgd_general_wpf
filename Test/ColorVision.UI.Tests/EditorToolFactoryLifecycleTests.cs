using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace ColorVision.UI.Tests;

public sealed class EditorToolFactoryLifecycleTests
{
    [Fact]
    public void DrawToolbarCanRefreshRepeatedlyWithReusableUiElementIcons()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            ImageView imageView = new();
            IEditorToolFactory factory = imageView.IEditorToolFactory;
            IEditorTool brush = Assert.IsType<BrushManager>(factory.GetIEditorTool<BrushManager>());
            IEditorTool arrow = Assert.IsAssignableFrom<IEditorTool>(factory.GetIEditorTool("ArrowManager"));

            try
            {
                factory.RefreshToolBars();
                factory.RefreshToolBars();

                ToolBar drawToolbar = Assert.IsType<ToolBar>(imageView.GetRegionToolBar(ToolBarLocal.Draw));
                ToggleButton brushButton = Assert.Single(drawToolbar.Items.OfType<ToggleButton>(), item => ReferenceEquals(item.DataContext, brush));
                ToggleButton arrowButton = Assert.Single(drawToolbar.Items.OfType<ToggleButton>(), item => ReferenceEquals(item.DataContext, arrow));
                Assert.Same(brush.Icon, brushButton.Content);
                Assert.Same(arrow.Icon, arrowButton.Content);
            }
            finally
            {
                imageView.Dispose();
            }

            Assert.Null(LogicalTreeHelper.GetParent((DependencyObject)brush.Icon!));
            Assert.Null(LogicalTreeHelper.GetParent((DependencyObject)arrow.Icon!));
        });
    }

    private static void EnsureImageViewTestResources()
    {
        Application application = Application.Current ?? new Application();
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        application.Resources["ToolBarImage"] = new Style(typeof(Image));
        application.Resources["BaseStyle"] = new Style(typeof(Control));
        application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }
}
