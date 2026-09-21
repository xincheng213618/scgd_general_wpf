using ColorVision.Database;
using ColorVision.Engine.Media;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using ColorVision.UI.Tests;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CameraTest.Tests;

public sealed class LocalPoiIntegrationTests
{
    private static string TestSettingsPath() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CameraTest-tests", Guid.NewGuid().ToString("N"), "settings.json");

    [Fact]
    public async Task CameraTestPoiManagerEditsAndPersistsWithoutDatabaseService()
    {
        string path = Path.Combine(Path.GetTempPath(), "CameraTest-local-poi-tests", Guid.NewGuid().ToString("N"), "ColorVision.Local.db");
        var storage = new PoiTemplateStorage(new LocalTemplateStore(path), () => false, () => throw new Exception("Unexpected MySQL access"));
        var value = new PoiParam { Id = -1, Name = "调焦区域", Width = 640, Height = 480 };
        value.PoiPoints.Add(new() { Name = "中心", PointType = PoiShape.Rect, PixX = 320, PixY = 240, PixWidth = 200, PixHeight = 160 });
        storage.Save(value);
        CameraTestWindow? window = null;
        TemplateEditorWindow? manager = null;
        EditPoiParam? editor = null;
        ImageView? view = null;
        var previousConfig = ConfigService.Instance;
        TemplateModel<PoiParam>[] previous = [];
        try
        {
            WpfTestHost.Invoke(() =>
            {
                ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
                System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) });
                previous = TemplatePoi.Params.ToArray();
                window = new CameraTestWindow(TestSettingsPath()) { ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual };
                view = (ImageView)window.FindName("ImageView");
                PoiImageViewComponent.SetIsTemplateSelectorEnabled(view, false);
                window.Show();
            });
            await Flush(view!);
            WpfTestHost.Invoke(() =>
            {
                PoiImageViewComponent.SetIsTemplateSelectorEnabled(view!, true);
                new PoiImageViewComponent(storage).Execute(view!);
            });
            await Flush(view!);
            WpfTestHost.Invoke(() =>
            {
                var selector = view!.ToolBarAl.Items.OfType<ComboBox>().Single(x => x.Name == "PoiTemplateSelector");
                selector.SelectedValue = Assert.Single(TemplatePoi.Params).Value;
                Assert.True(PoiImageViewComponent.TryGetSelectedTemplate(view, out var selected));
                Assert.Equal(value.Id, selected.Id);
                var button = view.ToolBarAl.Items.OfType<Button>().Single(x => x.Name == "PoiTemplateManager");
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                manager = System.Windows.Application.Current.Windows.OfType<TemplateEditorWindow>().Single();
                Assert.Contains("本地", manager.Title);
                manager.ITemplate.PreviewMouseDoubleClick(0);
                editor = ((TemplatePoi)manager.ITemplate).EditWindow;
            });
            await WpfTestHost.Invoke(() => (Task)typeof(EditPoiParam).GetProperty("PoiLoadTask", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(editor)!);
            await WpfTestHost.Invoke(() =>
            {
                var rectangle = Assert.Single(editor!.DrawingVisualLists.OfType<DVRectangleText>());
                rectangle.Attribute.Text = "中心已编辑";
                rectangle.Attribute.Rect = new Rect(100.25, 80.5, 200.5, 160.25);
                return (Task)typeof(EditPoiParam).GetMethod("SaveTemplateAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null)!;
            });
            var reopened = Assert.Single(new PoiTemplateStorage(new LocalTemplateStore(path), () => false).Load());
            var point = Assert.Single(reopened.PoiPoints);
            Assert.Equal("中心已编辑", point.Name);
            Assert.Equal(200.5, point.PixX);
            Assert.Equal(160.625, point.PixY);
            Assert.Equal(200.5, point.PixWidth);
            Assert.Equal(160.25, point.PixHeight);
            WpfTestHost.Invoke(() =>
            {
                editor!.Close(); editor = null;
                manager!.Close(); manager = null;
                Assert.True(PoiImageViewComponent.TryGetSelectedTemplate(view!, out var selected));
                Assert.Equal(value.Id, selected.Id);
                var rectangle = Assert.Single(view!.EditorContext.DrawingVisualLists.OfType<DVRectangleText>());
                Assert.Equal("中心已编辑", rectangle.Attribute.Text);
                Assert.Equal(100.25, rectangle.Attribute.Rect.X);
            });
            var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            WpfTestHost.Invoke(() =>
            {
                var toolbar = view!.ToolBarAl;
                var selector = toolbar.Items.OfType<ComboBox>().Single(x => x.Name == "PoiTemplateSelector");
                var save = toolbar.Items.OfType<Button>().Single(x => x.Name == "PoiTemplateSave");
                Assert.True(save.IsEnabled);
                Assert.IsType<Image>(save.Content);
                var rectangle = Assert.Single(view.EditorContext.DrawingVisualLists.OfType<DVRectangleText>());
                rectangle.Attribute.Text = "工具栏保存";
                rectangle.Attribute.Rect = new Rect(110.5, 90.25, 210.25, 150.5);
                save.IsEnabledChanged += (_, _) => { if (save.IsEnabled) saved.TrySetResult(); };
                save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            });
            await saved.Task;
            WpfTestHost.Invoke(() =>
            {
                var point = Assert.Single(Assert.Single(storage.Load()).PoiPoints);
                Assert.Equal("工具栏保存", point.Name);
                Assert.Equal(215.625, point.PixX);
                Assert.Equal(165.5, point.PixY);
                view!.UpdateLayout();
                var overflow = Assert.IsAssignableFrom<FrameworkElement>(view.ToolBarAl.Template.FindName("ButtonOverflow", view.ToolBarAl));
                Assert.Equal(Visibility.Collapsed, overflow.Visibility);
                string? preview = Environment.GetEnvironmentVariable("CAMERATEST_POI_TOOLBAR_PREVIEW");
                if (preview != null)
                {
                    var toolbar = view.ToolBarAl;
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(toolbar.ActualWidth), (int)Math.Ceiling(toolbar.ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                    var visual = new System.Windows.Media.DrawingVisual();
                    using (var drawing = visual.RenderOpen())
                    {
                        var bounds = new Rect(0, 0, toolbar.ActualWidth, toolbar.ActualHeight);
                        drawing.DrawRectangle(System.Windows.Media.Brushes.White, null, bounds);
                        drawing.DrawRectangle(new System.Windows.Media.VisualBrush(toolbar), null, bounds);
                    }
                    bitmap.Render(visual);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using var output = File.Create(preview);
                    encoder.Save(output);
                }
            });
        }
        finally
        {
            WpfTestHost.Invoke(() =>
            {
                editor?.Close(); manager?.Close(); window?.Close();
                TemplatePoi.Params.Clear(); foreach (var item in previous) TemplatePoi.Params.Add(item);
                ConfigService.SetInstance(previousConfig!);
            });
        }
    }

    private static Task Flush(ImageView view) => WpfTestHost.Invoke(() => view.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle).Task);
}
