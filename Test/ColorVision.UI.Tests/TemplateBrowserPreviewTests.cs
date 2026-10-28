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
using System.Windows.Data;
using System.Windows.Input;
using System.Globalization;

namespace ColorVision.UI.Tests;

public sealed class TemplateBrowserPreviewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrganizationPreservesSourceIdentityAndCheckedSelectionAcrossFilteringAndViewChanges(bool flow)
    {
        await WpfTestHost.Invoke<Task>(async () =>
        {
            var theme = new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Add(theme);
            var template = new PreviewTemplate();
            var settings = new TemplateSetting();
            TemplateBrowserWindow? window = null;
            TemplateBase Item(string name, int id, DateTime? date) => flow
                ? new TemplateModel<FlowParam>(name, new FlowParam { Id = id, TemplateDate = date })
                : new TemplateModel<PoiParam>(name, new PoiParam { Id = id, TemplateDate = date });
            var sample10 = Item("Sample10", -2, DateTime.Today);
            var sample2 = Item("Sample2", -3, DateTime.Today.AddDays(-1));
            var unknown = Item("中文模板", -4, null);
            template.Items.Add(sample10); template.Items.Add(sample2); template.Items.Add(unknown);
            try
            {
                window = new TemplateBrowserWindow(template, 0, new TemplateBrowserOptions("Test", flow, _ => { }), NewCoverService(), settings)
                    { Left = -10000, ShowActivated = false };
                window.Show();
                var list = (ListView)window.FindName("TemplateList");
                list.SelectedItems.Add(sample2);
                Assert.True(sample10.IsSelected);
                Assert.True(sample2.IsSelected);
                ((MenuItem)window.FindName("SortNameMenu")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Equal(new[] { sample2, sample10 }, list.Items.Cast<TemplateBase>().Where(item => item.Key.StartsWith("Sample")));
                Assert.Equal(new[] { sample10, sample2, unknown }, template.Items);
                ((MenuItem)window.FindName("SortDateMenu")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Equal(new[] { sample10, sample2, unknown }, list.Items.Cast<TemplateBase>());
                ((MenuItem)window.FindName("SortAscendingMenu")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Equal(new[] { sample2, sample10, unknown }, list.Items.Cast<TemplateBase>());
                ((MenuItem)window.FindName("SortNameMenu")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                ((MenuItem)window.FindName("GroupNameMenu")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Equal(2, ((ListCollectionView)list.ItemsSource).Groups!.Count);
                await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle);
                var groupPresenter = TemplateBrowserWindow.FindChild<ItemsPresenter>(TemplateBrowserWindow.FindChild<ScrollContentPresenter>(list)!);
                Assert.IsType<StackPanel>(VisualTreeHelper.GetChild(groupPresenter!, 0));
                var firstContainer = TemplateBrowserWindow.FindChild<ListViewItem>(list)!;
                Rect bounds = firstContainer.TransformToAncestor(list).TransformBounds(new Rect(firstContainer.RenderSize));
                var inside = new Rect(bounds.X + bounds.Width / 4, bounds.Y + bounds.Height / 4, bounds.Width / 2, bounds.Height / 2);
                window.SelectInRectangle(inside, new HashSet<TemplateBase>());
                Assert.Same(firstContainer.Content, Assert.Single(list.SelectedItems));
                window.SelectInRectangle(inside, new HashSet<TemplateBase> { sample10, sample2 });
                Assert.Equal(2, list.SelectedItems.Count);
                list.SelectedItem = sample10;
                list.SelectedItems.Add(sample2);
                ((MenuItem)window.FindName("ListViewMenu")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle);
                Assert.Equal(new[] { sample10, sample2 }, list.SelectedItems.Cast<TemplateBase>());
                Assert.NotNull(TemplateBrowserWindow.FindChild<Expander>(list));
                ((MenuItem)window.FindName("GroupDateMenu")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                var groups = ((ListCollectionView)list.ItemsSource).Groups!.Cast<CollectionViewGroup>().ToArray();
                Assert.Equal(3, groups.Length);
                Assert.Same(unknown, Assert.Single(groups[^1].Items));
                ((TextBox)window.FindName("SearchBox")).Text = "Sample2";
                Assert.Same(sample2, Assert.Single(list.Items));
                Assert.True(sample10.IsSelected); // Hidden checked items remain explicit batch targets.
                ((TextBox)window.FindName("SearchBox")).Clear();
                Assert.Equal(2, list.SelectedItems.Count);
                int deletes = 0;
                window.CommandBindings.Insert(0, new CommandBinding(ApplicationCommands.Delete, (_, e) => { deletes++; e.Handled = true; }, (_, e) => { e.CanExecute = true; e.Handled = true; }));
                var presentation = PresentationSource.FromVisual(window)!;
                list.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, presentation, 0, Key.Delete) { RoutedEvent = Keyboard.KeyDownEvent });
                Assert.Equal(1, deletes);
                var search = (TextBox)window.FindName("SearchBox");
                search.Text = "Sample";
                search.Focus();
                search.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, presentation, 0, Key.Delete) { RoutedEvent = Keyboard.KeyDownEvent });
                Assert.Equal(1, deletes);
                search.Clear();
                var background = list.ContextMenu;
                Assert.NotNull(background);
                Assert.Equal(EngineLocalization.Get("查看"), ((MenuItem)background.Items[0]).Header);
                Assert.Equal(EngineLocalization.Get("排序"), ((MenuItem)background.Items[1]).Header);
                Assert.Equal(EngineLocalization.Get("分组依据"), ((MenuItem)background.Items[2]).Header);
                Assert.Equal(TemplateBrowserSort.Name, flow ? settings.FlowBrowserSort : settings.PoiBrowserSort);
                Assert.Equal(TemplateBrowserGroup.Date, flow ? settings.FlowBrowserGroup : settings.PoiBrowserGroup);
                window.Close();
                window = new TemplateBrowserWindow(template, 0, new TemplateBrowserOptions("Test", flow, _ => { }), NewCoverService(), settings);
                list = (ListView)window.FindName("TemplateList");
                Assert.IsType<GridView>(list.View);
                Assert.Equal(3, ((ListCollectionView)list.ItemsSource).Groups!.Count);
                Assert.Equal(2, list.SelectedItems.Count);
            }
            finally
            {
                window?.Close();
                TemplateControl.ITemplateNames.Remove(template.Name);
                Application.Current.Resources.MergedDictionaries.Remove(theme);
            }
        });
    }

    [Theory]
    [InlineData("2026-10-29", 0)]
    [InlineData("2026-10-28", 1)]
    [InlineData("2026-10-27", 2)]
    [InlineData("2026-10-26", 3)]
    [InlineData("2026-10-25", 4)]
    [InlineData("2026-10-15", 5)]
    [InlineData("2026-09-30", 6)]
    [InlineData("2026-05-01", 7)]
    [InlineData("2025-12-31", 8)]
    [InlineData(null, 10)]
    public void DateGroupingUsesStoredDatesAndCalendarBoundaries(string? date, int expected)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("zh-CN").Clone();
        culture.DateTimeFormat.FirstDayOfWeek = DayOfWeek.Monday;
        var item = new DatedTemplate(date == null ? null : DateTime.Parse(date, CultureInfo.InvariantCulture));
        var grouping = new TemplateBrowserGrouping(TemplateBrowserGroup.Date, new DateTime(2026, 10, 28));
        var group = Assert.IsType<TemplateBrowserGroupKey>(grouping.GroupNameFromItem(item, 0, culture));
        Assert.Equal(expected, group.Order);
    }

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
                var toggle = (MenuItem)window.FindName("PreviewMenu");
                var preview = (TemplateBrowserPreview)window.FindName("PreviewContent");
                int usageReads = 0;
                preview.ReadUsage = (_, _) => { Interlocked.Increment(ref usageReads); return new(42, 1250, 5); };
                Assert.False(toggle.IsChecked);
                Assert.Equal(0, usageReads);
                toggle.IsChecked = true;
                toggle.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                await window.PreviewTask;
                await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle);
                Assert.Same(item, list.SelectedItem);
                Assert.NotNull(((Image)preview.FindName("PreviewImage")).Source);
                Assert.NotNull(TemplateBrowserWindow.FindChild<WrapPanel>(list));
                Assert.True(((TextBox)window.FindName("SearchBox")).ActualWidth > 0);

                ((MenuItem)window.FindName("ListViewMenu")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                window.UpdateLayout();
                Assert.NotNull(TemplateBrowserWindow.FindChild<GridViewRowPresenter>(list));
                Assert.Same(item, list.SelectedItem);
                Assert.Equal(item.Key, ((TextBlock)preview.FindName("PreviewTitle")).Text);

                toggle.IsChecked = false;
                toggle.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
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

    private sealed class DatedTemplate(DateTime? date) : TemplateBase
    {
        public override DateTime? Date => date;
    }
}
