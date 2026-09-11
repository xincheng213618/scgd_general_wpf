using ColorVision.ImageEditor;
using ColorVision.ImageEditor.EditorTools.AppCommand;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

public sealed class ImageViewContextMenuTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyCanvasInEditModeBuildsStandardMenuFromBothContextMenuSurfaces(bool raiseFromImageShow)
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            using ImageView view = new();
            IIEditorToolContextMenu standardMenu = Assert.Single(
                view.IEditorToolFactory.IIEditorToolContextMenus,
                item => item is ZoomEditorToolContextMenu);
            view.IEditorToolFactory.IIEditorToolContextMenus.Clear();
            view.IEditorToolFactory.IIEditorToolContextMenus.Add(standardMenu);
            view.ImageEditMode = true;
            view.EditorContext.ContextMenu.Items.Clear();

            Assert.Null(view.ViewBitmapSource);
            UIElement eventSource = raiseFromImageShow ? view.ImageShow : view.Zoombox1;

            ContextMenuEventArgs opening = (ContextMenuEventArgs)Activator.CreateInstance(
                typeof(ContextMenuEventArgs),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: [eventSource, true],
                culture: null)!;

            eventSource.RaiseEvent(opening);

            Assert.False(opening.Handled);
            Assert.NotEmpty(view.EditorContext.ContextMenu.Items);
            Assert.Contains(
                view.EditorContext.ContextMenu.Items.OfType<MenuItem>(),
                item => ReferenceEquals(item.Command, ApplicationCommands.Open));
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
