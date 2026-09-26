using ColorVision.Database;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Browser;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.POI;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class PoiTemplateBrowserTests
{
    [Fact]
    public void IconBrowserDoesNotLoadPointsAndKeepsSelectionFilterAndEditorIndexAcrossViews()
    {
        WithBrowser((path, _) =>
        {
            int remoteReads = 0;
            var storage = new PoiTemplateStorage(new LocalTemplateStore(path), () => true,
                () => { remoteReads++; throw new InvalidOperationException("Browsing must not load POI details."); });
            string[] names = ["中心九点", "ANSI_16点", "全屏均匀性_25点", "边缘采样", "MTF_HV_测试点", "畸变检测", "ARVR_左眼", "ARVR_右眼", "白场亮度", "棋盘格_黑白区域", "镜头中心定位", "超长名称_用于检查单行居中和省略显示"];
            foreach (var (name, index) in names.Select((name, index) => (name, index)))
                TemplatePoi.Params.Add(new(name, new PoiParam { Id = 700 + index, Name = name, Storage = storage }));
            var template = new BrowserPoi(storage, masterOnly: true);
            var dialog = Open(template, path, 2);
            try
            {
                var list = Assert.IsType<ListView>(dialog.FindName("TemplateList"));
                var search = Assert.IsType<TextBox>(dialog.FindName("SearchBox"));
                var selected = list.SelectedItem;
                TemplatePoi.Params[1].IsSelected = true;
                Assert.True(dialog.IsTileMode);
                Assert.Empty(VisualChildren(list).OfType<FlowTemplateCover>());
                Assert.Empty(VisualChildren(list).OfType<Image>());
                Assert.Equal(names.Length, VisualChildren(list).OfType<Canvas>().Count(icon => icon.Name == "PoiIcon"));
                foreach (var text in VisualChildren(list).OfType<TextBlock>().Where(text => names.Contains(text.Text)))
                    Assert.Equal(TextAlignment.Center, text.TextAlignment);
                Capture(dialog, "poi-tiles.png");
                var target = Assert.IsType<ListViewItem>(list.ItemContainerGenerator.ContainerFromIndex(3));
                target.Tag = "SwapTarget";
                Capture(dialog, "poi-drag-target.png");
                target.Tag = null;
                double width = dialog.Width;
                dialog.Width = 640;
                Drain();
                Capture(dialog, "poi-compact.png");
                dialog.Width = width;
                dialog.SetViewMode(false);
                Drain();
                Assert.Equal(width, dialog.Width);
                Assert.Same(selected, list.SelectedItem);
                Capture(dialog, "poi-list.png");
                search.Text = "ARVR 右眼";
                Assert.Single(list.Items);
                dialog.SetViewMode(true);
                Drain();
                Assert.Single(list.Items);
                Assert.Equal("ARVR 右眼", search.Text);
                var container = Assert.IsType<ListViewItem>(list.ItemContainerGenerator.ContainerFromIndex(0));
                list.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    { RoutedEvent = Control.PreviewMouseDoubleClickEvent, Source = container });
                Assert.Equal(7, template.OpenedIndex);
                Assert.True(TemplatePoi.Params[1].IsSelected);
                Assert.All(TemplatePoi.Params, item => { Assert.False(item.Value.DetailsLoaded); Assert.Empty(item.Value.PoiPoints); });
                Assert.Equal(0, remoteReads);
                Assert.Equal(1, template.LoadCount);
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void LocalSwapKeepsIdsAndComboSelectionWhileRenamePreservesPointsAndDeleteHandlesHiddenChecks()
    {
        WithBrowser((path, storage) =>
        {
            foreach (string name in new[] { "一", "二", "三", "四" })
                storage.Save(new PoiParam { Id = -1, Name = name, Width = 640, Height = 480,
                    PoiPoints = [new() { Name = "中心", PixX = 123.25, PixY = 87.5, PointType = PoiShape.Circle, PixWidth = 10, PixHeight = 10 }] });
            var template = new BrowserPoi(storage);
            template.Load();
            var original = TemplatePoi.Params.ToArray();
            int[] originalIds = original.Select(item => item.Id).ToArray();
            var combo = new ComboBox { ItemsSource = TemplatePoi.Params, SelectedIndex = 1 };
            var dialog = Open(template, path, 1);
            try
            {
                var list = Assert.IsType<ListView>(dialog.FindName("TemplateList"));
                var search = Assert.IsType<TextBox>(dialog.FindName("SearchBox"));
                Assert.Same(original[1], combo.SelectedItem);
                Wait(dialog.SwapItemsAsync(original[0], original[3]));
                Assert.Equal(new[] { original[3], original[1], original[2], original[0] }, list.Items.Cast<TemplateModel<PoiParam>>());
                Assert.Equal(originalIds, TemplatePoi.Params.Select(item => item.Id));
                Assert.Same(original[1], combo.SelectedItem);
                Assert.Equal(originalIds, storage.Load().Select(item => item.Id));
                var order = new TemplateBrowserOrderStore(path);
                Wait(order.SaveAsync("local", ["flow-key"]));
                Task<string[]> flowOrder = order.LoadAsync("local"); Wait(flowOrder);
                Assert.Equal(new[] { "flow-key" }, flowOrder.Result);
                dialog.SelectTemplate(original[1].Id);
                ColorVision.UI.Commands.ReName.Execute(null, dialog);
                // Simulate a server master whose detail collection has not been populated.
                original[1].Value.PoiPoints.Clear();
                original[1].Value.DetailsLoaded = false;
                original[1].Key = "重命名后";
                ApplicationCommands.Save.Execute(null, dialog);
                var saved = storage.Load().Single(item => item.Id == original[1].Id);
                Assert.Equal("重命名后", saved.Name);
                Assert.Equal(123.25, Assert.Single(saved.PoiPoints).PixX);
                original[0].IsSelected = true;
                original[2].IsSelected = true;
                search.Text = "重命名后";
                Assert.Single(list.Items);
                dialog.DeleteItems(TemplatePoi.Params.Where(item => item.IsSelected));
                Assert.Equal(new[] { original[1].Id, original[3].Id }, storage.Load().Select(item => item.Id));
                Assert.Same(original[1], combo.SelectedItem);
            }
            finally { dialog.Close(); }
            Assert.Same(original[1], combo.SelectedItem);
            var reopened = Open(new BrowserPoi(storage), path);
            try
            {
                var list = Assert.IsType<ListView>(reopened.FindName("TemplateList"));
                Assert.Equal(new[] { original[3].Id, original[1].Id }, list.Items.Cast<TemplateBase>().Select(item => item.Id));
                Assert.True(reopened.IsTileMode);
            }
            finally { reopened.Close(); }
            var routed = Assert.IsType<PoiTemplateManagerWindow>(template.CreateManagerWindow());
            routed.Close();
            var legacy = Assert.IsType<TemplateEditorWindow>(new LegacyTemplate().CreateManagerWindow());
            legacy.Close();
        });
    }

    private sealed class LegacyTemplate : ITemplate<PoiParam>
    {
        public LegacyTemplate() { IsSideHide = true; Title = "其他模板"; }
        public override void Load() { }
    }

    private sealed class BrowserPoi(PoiTemplateStorage storage, bool masterOnly = false) : TemplatePoi(storage)
    {
        public int LoadCount { get; private set; }
        public int OpenedIndex { get; private set; } = -1;
        public override void Load() { LoadCount++; if (!masterOnly) base.Load(); }
        public override void PreviewMouseDoubleClick(int index) => OpenedIndex = index;
        public override bool SwapTemplateOrder(int first, int second) => throw new InvalidOperationException("Browser order must not renumber templates.");
    }

    private static PoiTemplateManagerWindow Open(TemplatePoi template, string path, int selectedIndex = 0)
    {
        var dialog = new PoiTemplateManagerWindow(template, selectedIndex, new FlowTemplateCoverService(path))
            { Left = -10000, Top = -10000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false };
        dialog.Show();
        Wait(dialog.OrderLoadTask);
        dialog.UpdateLayout();
        return dialog;
    }

    private static void Wait(Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) { Drain(); Thread.Sleep(10); }
        Assert.True(task.IsCompletedSuccessfully, task.Exception?.ToString());
        Drain();
    }

    private static void Drain()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    private static IEnumerable<DependencyObject> VisualChildren(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in VisualChildren(child)) yield return descendant;
        }
    }

    private static void Capture(FrameworkElement visual, string name)
    {
        string? directory = Environment.GetEnvironmentVariable("FLOW_BROWSER_PREVIEW_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        visual.UpdateLayout();
        Directory.CreateDirectory(directory);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)visual.ActualWidth, (int)visual.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, name));
        encoder.Save(stream);
    }

    private static void WithBrowser(Action<string, PoiTemplateStorage> action)
    {
        WpfTestHost.Invoke(() =>
        {
            var previous = TemplatePoi.Params;
            var registrations = TemplateControl.ITemplateNames.ToArray();
            var config = ConfigService.Instance;
            var resources = Application.Current.Resources;
            var previousResources = resources.Keys.Cast<object>().ToDictionary(key => key, key => resources[key]);
            var dictionaries = resources.MergedDictionaries.ToArray();
            Window? previousOwner = Application.Current.MainWindow;
            var owner = new Window { ShowActivated = false, ShowInTaskbar = false, Width = 1, Height = 1, Left = -10000, Top = -10000 };
            try
            {
                TemplatePoi.Params = [];
                ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
                resources.Clear(); resources.MergedDictionaries.Clear();
                foreach (string source in new[] { "/HandyControl;component/Themes/basic/colors/colorsdark.xaml", "/HandyControl;component/Themes/Theme.xaml", "/ColorVision.Themes;component/Themes/Dark.xaml", "/ColorVision.Themes;component/Themes/Base.xaml" })
                    resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.Relative) });
                Application.Current.MainWindow = owner;
                owner.Show();
                string path = Path.Combine(Path.GetTempPath(), "ColorVision-poi-browser-tests", Guid.NewGuid().ToString("N"), "browser.db");
                action(path, new PoiTemplateStorage(new LocalTemplateStore(path), () => false, () => throw new InvalidOperationException("Offline test cannot access MySQL.")));
            }
            finally
            {
                owner.Close();
                Application.Current.MainWindow = previousOwner;
                TemplatePoi.Params = previous;
                TemplateControl.ITemplateNames.Clear();
                foreach (var registration in registrations) TemplateControl.ITemplateNames.Add(registration.Key, registration.Value);
                resources.Clear(); resources.MergedDictionaries.Clear();
                foreach (var entry in previousResources) resources[entry.Key] = entry.Value;
                foreach (var dictionary in dictionaries) resources.MergedDictionaries.Add(dictionary);
                ConfigService.SetInstance(config!);
            }
        });
    }
}
