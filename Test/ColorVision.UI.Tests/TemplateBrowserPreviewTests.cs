using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Browser;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.POI;
using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class TemplateBrowserPreviewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BrowserPreservesSelectionAndPreviewAcrossVisibleCardAndListModes(bool flow)
    {
        string data = flow ? FlowTemplateCoverTests.CreateCanvas() : string.Empty;
        await WpfTestHost.Invoke<Task>(async () =>
        {
            var theme = new ResourceDictionary();
            var colors = flow ? ColorVision.Themes.ThemeManager.ResourceDictionaryDark : ColorVision.Themes.ThemeManager.ResourceDictionaryWhite;
            foreach (string source in colors.Concat(ColorVision.Themes.ThemeManager.ResourceDictionaryBase))
                theme.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.Relative) });
            Application.Current.Resources.MergedDictionaries.Add(theme);
            var template = new PreviewTemplate();
            var settings = new TemplateSetting();
            TemplateBrowserWindow? window = null;
            try
            {
                TemplateBase item;
                if (flow) item = new TemplateModel<FlowParam>("Image acquisition and luminance inspection", new FlowParam { Id = -2, DataBase64 = data });
                else
                {
                    var poi = new PoiParam { Id = -2, Width = 1920, Height = 1080, DetailsLoaded = true };
                    for (int y = 1; y <= 3; y++)
                        for (int x = 1; x <= 5; x++) poi.PoiPoints.Add(new PoiPoint { PointType = PoiShape.Circle, PixX = x * 320, PixY = y * 270, PixWidth = 100 });
                    item = new TemplateModel<PoiParam>("Luminance uniformity 15 points", poi);
                }
                template.Items.Add(item);
                template.Items.Add(flow ? new TemplateModel<FlowParam>("Empty flow", new FlowParam { Id = -3 })
                    : new TemplateModel<PoiParam>("Empty points", new PoiParam { Id = -3, DetailsLoaded = true }));
                window = new TemplateBrowserWindow(template, 0, new TemplateBrowserOptions(flow ? "Flow" : "POI", flow, _ => { }), NewCoverService(), settings)
                    { Left = -10000, ShowActivated = false };
                window.Show();
                var list = (ListView)window.FindName("TemplateList");
                var toggle = (ToggleButton)window.FindName("PreviewButton");
                var preview = (TemplateBrowserPreview)window.FindName("PreviewContent");
                int usageReads = 0;
                preview.ReadUsage = (_, _) => { Interlocked.Increment(ref usageReads); return new(42, 1250, 5); };
                Assert.False(toggle.IsChecked);
                Assert.Equal(0, usageReads);
                toggle.IsChecked = true;
                toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await window.PreviewTask;
                await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle);
                Assert.Same(item, list.SelectedItem);
                Assert.NotNull(((Image)preview.FindName("PreviewImage")).Source);
                Assert.NotNull(TemplateBrowserWindow.FindChild<WrapPanel>(list));
                Assert.True(((TextBox)window.FindName("SearchBox")).ActualWidth > 0);

                ((ToggleButton)window.FindName("ListViewButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                window.UpdateLayout();
                Assert.NotNull(TemplateBrowserWindow.FindChild<GridViewRowPresenter>(list));
                Assert.Same(item, list.SelectedItem);
                Assert.Equal(item.Key, ((TextBlock)preview.FindName("PreviewTitle")).Text);

                toggle.IsChecked = false;
                toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.Null(((Image)preview.FindName("PreviewImage")).Source);
                Assert.Equal(0, ((ColumnDefinition)window.FindName("PreviewColumn")).Width.Value);
                int completedReads = usageReads;
                list.SelectedItem = template.Items[1];
                await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle);
                Assert.Equal(completedReads, usageReads);
                Assert.True(flow ? settings.FlowBrowserListView : settings.PoiBrowserListView);
                Assert.False(flow ? settings.PoiBrowserListView : settings.FlowBrowserListView);
            }
            finally
            {
                window?.Close();
                TemplateControl.ITemplateNames.Remove(template.Name);
                Application.Current.Resources.MergedDictionaries.Remove(theme);
            }
        });
    }

    [Fact]
    public async Task FlowPreviewUsesSavedStructureAndClearsWhenSelectionIsEmpty()
    {
        string data = FlowTemplateCoverTests.CreateCanvas();
        await WpfTestHost.Invoke<Task>(async () =>
        {
            var preview = new TemplateBrowserPreview { ReadUsage = (_, _) => new(42, 1250, 5) };
            var service = NewCoverService();
            var flow = new FlowParam { Id = -2, DataBase64 = data, TemplateRevision = 7 };
            await preview.ShowAsync(new TemplateModel<FlowParam>("Saved flow", flow), service);
            Assert.NotNull(((Image)preview.FindName("PreviewImage")).Source);
            var details = Details(preview);
            Assert.Contains(details, row => row.Key == EngineLocalization.Get("TemplateBrowserNodeCount") && row.Value == "2");
            Assert.Contains(details, row => row.Key == EngineLocalization.Get("TemplateBrowserEdgeCount") && row.Value == "1");
            Assert.Contains(details, row => row.Key == EngineLocalization.Get("TemplateBrowserRevision") && row.Value == "7");
            Assert.Contains(details, row => row.Key == EngineLocalization.Get("TemplateBrowserExecutionCount") && row.Value == "42");
            Assert.Contains(details, row => row.Key == EngineLocalization.Get("TemplateBrowserExpectedTime") && row.Value == $"{1.25:N2} s");
            Assert.Equal(data, flow.DataBase64);
            await preview.ShowAsync(null, service);
            Assert.Null(((Image)preview.FindName("PreviewImage")).Source);
            Assert.Null(((ItemsControl)preview.FindName("PreviewDetails")).ItemsSource);
        });
    }

    [Fact]
    public async Task PoiPreviewReadsOnlyItsOwningStorageWithoutHydratingOrSavingTheSharedMaster()
    {
        string path = Path.Combine(Directory.CreateTempSubdirectory("ColorVision-poi-preview-").FullName, "local.db");
        var storage = new PoiTemplateStorage(new LocalTemplateStore(path), () => false);
        var saved = new PoiParam { Name = "Stored points", Width = 400, Height = 200 };
        saved.PoiPoints.Add(new PoiPoint { PointType = PoiShape.Circle, PixX = 80, PixY = 100, PixWidth = 30 });
        saved.PoiPoints.Add(new PoiPoint { PointType = PoiShape.Rect, PixX = 240, PixY = 100, PixWidth = 80, PixHeight = 40 });
        storage.Save(saved);
        await WpfTestHost.Invoke<Task>(async () =>
        {
            var master = new PoiParam { Id = saved.Id, Width = 400, Height = 200, Storage = storage };
            var points = master.PoiPoints;
            var preview = new TemplateBrowserPreview { ReadUsage = (_, _) => null };
            await preview.ShowAsync(new TemplateModel<PoiParam>("Stored points", master), NewCoverService());
            var image = Assert.IsType<DrawingImage>(((Image)preview.FindName("PreviewImage")).Source);
            Assert.True(image.IsFrozen);
            Assert.Equal(640, image.Width);
            Assert.Contains(Details(preview), row => row.Key == EngineLocalization.Get("TemplateBrowserPointCount") && row.Value == "2");
            Assert.Contains(Details(preview), row => row.Key == EngineLocalization.Get("TemplateBrowserCanvasSize") && row.Value == "400 × 200");
            Assert.Same(points, master.PoiPoints);
            Assert.Empty(master.PoiPoints);
            Assert.False(master.DetailsLoaded);
            Assert.Equal(2, storage.ReadPoints(saved.Id).Count);
        });
    }

    [Fact]
    public async Task LatePoiReadOrFailureCannotReplaceTheNewSelectionOrReviveAClosedPreview()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var storage = new PoiTemplateStorage(new LocalTemplateStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db")), () => true,
            () => { started.TrySetResult(); release.Wait(); throw new IOException("delayed read failure"); });
        await WpfTestHost.Invoke<Task>(async () =>
        {
            var preview = new TemplateBrowserPreview { ReadUsage = (_, _) => null };
            var service = NewCoverService();
            var old = preview.ShowAsync(new TemplateModel<PoiParam>("Old", new PoiParam { Id = 10, Storage = storage }), service);
            try
            {
                await started.Task;
                await preview.ShowAsync(new TemplateModel<PoiParam>("Current", new PoiParam { Id = -2, DetailsLoaded = true, Width = 200, Height = 100 }), service);
                var image = ((Image)preview.FindName("PreviewImage")).Source;
                release.Set();
                await old;
                Assert.Equal("Current", ((TextBlock)preview.FindName("PreviewTitle")).Text);
                Assert.Same(image, ((Image)preview.FindName("PreviewImage")).Source);
                preview.Cancel();
                Assert.Null(((Image)preview.FindName("PreviewImage")).Source);
                Assert.Empty(((TextBlock)preview.FindName("PreviewTitle")).Text);
                await preview.ShowAsync(new TemplateModel<FlowParam>("Broken", new FlowParam { DataBase64 = "invalid" }), service);
                Assert.Null(((Image)preview.FindName("PreviewImage")).Source);
                Assert.StartsWith(EngineLocalization.Get("TemplateBrowserPreviewFailed"), ((TextBlock)preview.FindName("PreviewStatus")).Text);
            }
            finally { release.Set(); }
        });
    }

    private static KeyValuePair<string, string>[] Details(TemplateBrowserPreview preview)
        => ((ItemsControl)preview.FindName("PreviewDetails")).Items.Cast<KeyValuePair<string, string>>().ToArray();

    private static FlowTemplateCoverService NewCoverService()
        => new(Path.Combine(Directory.CreateTempSubdirectory("ColorVision-browser-preview-").FullName, "covers.db"));

    private sealed class PreviewTemplate : ITemplate
    {
        public ObservableCollection<TemplateBase> Items { get; } = [];
        public override IEnumerable ItemsSource => Items;
        public override string Title { get; set; } = "Template browser preview";
    }
}
