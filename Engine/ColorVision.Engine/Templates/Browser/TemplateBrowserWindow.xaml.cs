using ColorVision.Engine.Templates.Flow;
using ColorVision.Themes;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.Engine.Templates.Browser;

/// <summary>Shared browsing behavior; each template retains its own storage and editor.</summary>
public partial class TemplateBrowserWindow : Window
{
    public static readonly DependencyProperty TileWidthProperty = DependencyProperty.Register(nameof(TileWidth), typeof(double), typeof(TemplateBrowserWindow), new PropertyMetadata(212d));
    public double TileWidth { get => (double)GetValue(TileWidthProperty); private set => SetValue(TileWidthProperty, value); }
    public static readonly DependencyProperty CoverHeightProperty = DependencyProperty.Register(nameof(CoverHeight), typeof(double), typeof(TemplateBrowserWindow), new PropertyMetadata(111d));
    public double CoverHeight { get => (double)GetValue(CoverHeightProperty); private set => SetValue(CoverHeightProperty, value); }
    private static readonly ILog log = LogManager.GetLogger(typeof(TemplateBrowserWindow));
    private const string DragFormat = "ColorVision.TemplateBrowser.Template";
    private readonly ITemplate template;
    private readonly TemplateBrowserOptions options;
    private IEnumerable<TemplateBase> SourceItems => template.ItemsSource.Cast<TemplateBase>();
    private readonly ListCollectionView items;
    private readonly ObservableCollection<TemplateBase> browserItems;
    private readonly TemplateBrowserOrderStore orderStore;
    private readonly string orderScope;
    private readonly ViewBase listView;
    private readonly ItemsPanelTemplate listPanel;
    private readonly HashSet<TemplateBase> renamed = [];
    private readonly HashSet<FlowTemplateCover> covers = [];
    private bool isClosed, coverRefreshPending;
    private double listOffset, tileOffset;
    private int orderRevision;
    private bool orderSaveFailed;
    private ListViewItem? dropTarget;
    private long lastDragScroll;
    internal Task OrderSaveTask { get; private set; } = Task.CompletedTask;
    internal Task OrderLoadTask { get; private set; } = Task.CompletedTask;
    private int modeGeneration;
    private Point dragStart;
    private TemplateBase? dragItem;
    internal bool IsTileMode { get; private set; }
    internal FlowTemplateCoverService CoverService { get; }

    internal TemplateBrowserWindow(ITemplate template, int selectedIndex, TemplateBrowserOptions options, FlowTemplateCoverService coverService)
    {
        this.options = options;
        this.template = template;
        CoverService = coverService;
        template.Load();
        browserItems = new ObservableCollection<TemplateBase>(SourceItems);
        items = new ListCollectionView(browserItems);
        orderStore = new TemplateBrowserOrderStore(coverService.DatabasePath);
        orderScope = options.GetOrderScope();
        InitializeComponent();
        this.ApplyCaption();
        HeadingText.Text = $"{options.Label}模板";
        NewLabel.Text = $"新建{options.Label}";
        NewButton.ToolTip = NewLabel.Text;
        System.Windows.Automation.AutomationProperties.SetName(NewButton, NewLabel.Text);
        SearchPlaceholder.Text = $"搜索{options.Label}名称";
        System.Windows.Automation.AutomationProperties.SetName(SearchBox, SearchPlaceholder.Text);
        TileModeButton.ToolTip = options.HasCovers ? "封面平铺" : "图标平铺";
        Title = template.Title;
        listView = TemplateList.View;
        listPanel = TemplateList.ItemsPanel;
        TemplateList.ItemsSource = items;
        TemplateList.SelectedItem = SourceItems.ElementAtOrDefault(selectedIndex);
        ((INotifyCollectionChanged)items).CollectionChanged += Items_CollectionChanged;
        ((INotifyCollectionChanged)template.ItemsSource).CollectionChanged += Source_CollectionChanged;
        foreach (var item in browserItems) item.PropertyChanged += Item_PropertyChanged;
        UpdateEmptyState();
        UpdateStatus();
        CommandBindings.Add(new CommandBinding(ApplicationCommands.New, (_, _) => TryAction(Create, "新建失败")));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (_, _) => TryAction(Delete, "删除失败"), (_, e) => e.CanExecute = Selected != null));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, async (_, _) =>
        {
            TryAction(SaveRenames, "保存失败");
            if (orderSaveFailed) await PersistOrderAsync();
            UpdateStatus();
        }, (_, e) => e.CanExecute = renamed.Count > 0 || orderSaveFailed));
        CommandBindings.Add(new CommandBinding(Commands.ReName, (_, _) => BeginRename(), (_, e) => e.CanExecute = Selected != null));
        InputBindings.Add(new KeyBinding(Commands.ReName, Key.F2, ModifierKeys.None));
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { SearchBox.Focus(); e.Handled = true; }
        };
        TemplateList.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => { UpdateTileSize(); QueueCoverRefresh(); }));
        TemplateList.SelectionChanged += (_, _) => UpdateStatus();
        TemplateList.SizeChanged += (_, _) =>
        {
            if (!IsTileMode && listView is GridView grid) grid.Columns[2].Width = Math.Max(150, TemplateList.ActualWidth - 126);
            UpdateTileSize();
            QueueCoverRefresh();
        };
        Activated += (_, _) => QueueCoverRefresh();
        SetViewMode(true);
        Loaded += (_, _) => OrderLoadTask = LoadBrowserOrderAsync();
        Closing += Window_Closing;
        Closed += (_, _) =>
        {
            isClosed = true;
            ((INotifyCollectionChanged)items).CollectionChanged -= Items_CollectionChanged;
            ((INotifyCollectionChanged)template.ItemsSource).CollectionChanged -= Source_CollectionChanged;
            foreach (var item in browserItems) item.PropertyChanged -= Item_PropertyChanged;
            foreach (var item in renamed) item.IsEditMode = false;
            foreach (var cover in covers.ToArray()) cover.Cancel();
            // Preserve the old manager's close/reload contract, but never reload on a view switch.
            try { template.Load(); }
            catch (Exception ex) { log.Warn("Reloading templates after closing the manager failed.", ex); }
        };
    }

    private void UpdateTileSize()
    {
        if (!IsTileMode) return;
        double available = FindChild<ScrollContentPresenter>(TemplateList)?.ActualWidth ?? TemplateList.ActualWidth;
        if (available <= 0) return;
        int columns = Math.Max(1, (int)(available / (options.HasCovers ? 220 : 144)));
        double width = Math.Floor(available / columns) - 8;
        if (Math.Abs(TileWidth - width) < 0.5) return;
        TileWidth = Math.Max(options.HasCovers ? 160 : 120, width);
        CoverHeight = (TileWidth - 14) * 9 / 16;
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!OrderSaveTask.IsCompleted)
        {
            e.Cancel = true;
            await OrderSaveTask;
            if (!orderSaveFailed && !isClosed) Close();
            return;
        }
        if (!renamed.Any(SourceItems.Contains)) return;
        Keyboard.ClearFocus();
        var result = MessageBox.Show(this, "是否保存名称修改？", Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (result == MessageBoxResult.Cancel) e.Cancel = true;
        else if (result == MessageBoxResult.Yes)
        {
            try { SaveRenames(); }
            catch (Exception ex) { e.Cancel = true; log.Warn("Saving template names failed.", ex); MessageBox.Show(this, ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }

    private async Task LoadBrowserOrderAsync()
    {
        try
        {
            string[] keys = await orderStore.LoadAsync(orderScope);
            if (!isClosed && orderRevision == 0) ApplySavedOrder(keys);
        }
        catch (Exception ex) { log.Warn("Loading local template browser order failed.", ex); if (!isClosed) StatusText.Text = "本机排序暂不可用，可继续浏览"; }
        if (!isClosed) { if (Selected != null) TemplateList.ScrollIntoView(Selected); QueueCoverRefresh(); }
    }

    private string OrderKey(TemplateBase item) => options.GetOrderKey(item);

    private void Source_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var source = SourceItems.ToHashSet();
        foreach (var removed in browserItems.Where(item => !source.Contains(item)).ToArray())
        {
            removed.PropertyChanged -= Item_PropertyChanged;
            browserItems.Remove(removed);
        }
        foreach (var added in SourceItems.Where(item => !browserItems.Contains(item)))
        {
            added.PropertyChanged += Item_PropertyChanged;
            browserItems.Add(added);
        }
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TemplateBase.IsSelected) or nameof(TemplateBase.Key)) UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (CountText == null) return;
        CountText.Text = items.Count == browserItems.Count ? $"{browserItems.Count} 个{options.Label}模板" : $"{items.Count} / {browserItems.Count} 个{options.Label}模板";
        int marked = browserItems.Count(item => item.IsSelected);
        SelectionText.Text = marked > 0 ? $"已勾选 {marked} 项" : Selected == null ? "未选择模板" : "已选择 1 项";
        DeleteButton.Content = marked > 0 ? $"删除 ({marked})" : "删除";
        CommandManager.InvalidateRequerySuggested();
    }

    private void ApplySavedOrder(IEnumerable<string> keys)
    {
        var selected = Selected;
        var remaining = browserItems.ToList();
        var ordered = new List<TemplateBase>();
        foreach (string key in keys)
        {
            var item = remaining.FirstOrDefault(item => OrderKey(item) == key);
            if (item != null) { ordered.Add(item); remaining.Remove(item); }
        }
        ordered.AddRange(remaining);
        for (int i = 0; i < ordered.Count; i++)
        {
            int previous = browserItems.IndexOf(ordered[i]);
            if (previous != i) browserItems.Move(previous, i);
        }
        TemplateList.SelectedItem = selected;
    }

    private TemplateBase? Selected => TemplateList.SelectedItem as TemplateBase;

    internal void SelectTemplate(int id) => TemplateList.SelectedItem = browserItems.FirstOrDefault(item => item.Id == id);
    private int SourceIndex => Selected == null ? -1 : SourceItems.ToList().IndexOf(Selected);

    internal void SetViewMode(bool tiles)
    {
        if (tiles == IsTileMode) { UpdateModeButtons(); return; }
        var selected = Selected;
        var scroll = FindChild<ScrollViewer>(TemplateList);
        if (IsTileMode) tileOffset = scroll?.VerticalOffset ?? 0;
        else listOffset = scroll?.VerticalOffset ?? 0;
        IsTileMode = tiles;
        foreach (var cover in covers.ToArray()) cover.Cancel();
        TemplateList.View = tiles ? null : listView;
        if (tiles) TemplateList.Template = (ControlTemplate)Resources["TemplateTileList"];
        else TemplateList.ClearValue(TemplateProperty);
        TemplateList.ItemTemplate = tiles ? (DataTemplate)Resources[options.HasCovers ? "FlowTile" : "PoiTile"] : null;
        TemplateList.ItemsPanel = tiles ? (ItemsPanelTemplate)Resources["TemplateTilePanel"] : listPanel;
        TemplateList.ItemContainerStyle = (Style)Resources[tiles ? "TemplateTileItem" : "TemplateListItem"];
        ScrollViewer.SetCanContentScroll(TemplateList, !tiles);
        TemplateList.SelectedItem = selected;
        UpdateModeButtons();
        int generation = ++modeGeneration;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (isClosed || generation != modeGeneration) return;
            TemplateList.UpdateLayout();
            UpdateTileSize();
            FindChild<ScrollViewer>(TemplateList)?.ScrollToVerticalOffset(tiles ? tileOffset : listOffset);
            QueueCoverRefresh();
        });
    }

    private void UpdateModeButtons() { ListModeButton.IsChecked = !IsTileMode; TileModeButton.IsChecked = IsTileMode; }
    private void ListMode_Click(object sender, RoutedEventArgs e) => SetViewMode(false);
    private void TileMode_Click(object sender, RoutedEventArgs e) => SetViewMode(true);

    private void More_Click(object sender, RoutedEventArgs e)
    {
        var menu = (ContextMenu)Resources["MoreMenu"];
        menu.PlacementTarget = (Button)sender;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void LegacyManager_Click(object sender, RoutedEventArgs e) => TryAction(() =>
    {
        Keyboard.ClearFocus();
        if (renamed.Any(SourceItems.Contains))
        {
            var result = MessageBox.Show(this, "是否保存重命名后打开旧版管理？选择“否”将放弃未保存的重命名。", Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Cancel) return;
            if (result == MessageBoxResult.Yes) SaveRenames();
        }
        var selected = Selected;
        new TemplateEditorWindow(template, Math.Max(0, SourceIndex)) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        foreach (var item in renamed) item.IsEditMode = false;
        renamed.Clear();
        items.Refresh();
        TemplateList.SelectedItem = selected != null && items.Contains(selected) ? selected : items.Cast<object>().FirstOrDefault();
        Title = template.Title;
        QueueCoverRefresh();
    }, "打开旧版管理失败");

    internal void RegisterCover(FlowTemplateCover cover) { covers.Add(cover); QueueCoverRefresh(); }
    internal void UnregisterCover(FlowTemplateCover cover) { covers.Remove(cover); cover.Cancel(); }

    private void QueueCoverRefresh()
    {
        if (!options.HasCovers || isClosed || !IsTileMode || coverRefreshPending) return;
        coverRefreshPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            coverRefreshPending = false;
            if (isClosed || !IsTileMode) return;
            var viewport = FindChild<ScrollContentPresenter>(TemplateList);
            if (viewport == null) return;
            foreach (var cover in covers.ToArray())
            {
                // TemplateModel.Value is replaced by legacy reloads without notifying its binding.
                cover.GetBindingExpression(FlowTemplateCover.FlowProperty)?.UpdateTarget();
                cover.Refresh(viewport, CoverService);
            }
        });
    }

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) { UpdateEmptyState(); UpdateStatus(); QueueCoverRefresh(); }
    private void UpdateEmptyState()
    {
        EmptyText.Visibility = items.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = string.IsNullOrWhiteSpace(SearchBox.Text) ? $"暂无{options.Label}模板" : "没有找到匹配的模板";
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (items == null || TemplateList == null) return;
        var selected = Selected;
        string[] words = SearchBox.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        items.Filter = words.Length == 0 ? null : value => value is TemplateBase flow && words.All(word => flow.Key.Contains(word, StringComparison.OrdinalIgnoreCase));
        TemplateList.SelectedItem = selected != null && items.Contains(selected) ? selected : items.Cast<object>().FirstOrDefault();
        UpdateEmptyState();
        QueueCoverRefresh();
    }

    internal void OpenSelected()
    {
        int index = SourceIndex;
        if (index >= 0) TryAction(() => template.PreviewMouseDoubleClick(index), "打开失败");
    }

    private void Open_Click(object sender, RoutedEventArgs e) => OpenSelected();
    private void TemplateList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindParent<ListViewItem>(e.OriginalSource as DependencyObject) is { Content: TemplateBase item }
            && !IsEditingControl(e.OriginalSource as DependencyObject)) { TemplateList.SelectedItem = item; OpenSelected(); e.Handled = true; }
    }
    private void TemplateList_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && !IsEditingControl(e.OriginalSource as DependencyObject)) { OpenSelected(); e.Handled = true; } }
    private void Search_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { OpenSelected(); e.Handled = true; } }
    private void TemplateContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu { PlacementTarget: FrameworkElement { DataContext: TemplateBase item } }) TemplateList.SelectedItem = item;
    }

    private void BeginRename() { if (Selected is { } item) { renamed.Add(item); item.IsEditMode = true; StatusText.Text = "名称有修改，点击保存应用"; UpdateStatus(); } }
    private void RenameBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox { IsVisible: true } box) { box.Focus(); box.SelectAll(); }
    }
    private void RenameBox_LostFocus(object sender, RoutedEventArgs e) { if (sender is TextBox { DataContext: TemplateBase item }) item.IsEditMode = false; }
    private void RenameBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { RenameBox_LostFocus(sender, e); e.Handled = true; } }

    private void Create() => CreateFrom(() => { template.ClearCreateTemplateSource(); template.OpenCreate(); });
    private void Copy_Click(object sender, RoutedEventArgs e) => TryAction(() =>
    {
        int index = SourceIndex;
        if (index < 0) return;
        CreateFrom(() =>
        {
            template.ClearCreateTemplateSource();
            if (!template.CopyTo(index)) return;
            template.ImportName = template.NewCreateFileName($"{template.GetTemplateName(index)}_Copy");
            template.OpenCreate();
        });
    }, "复制失败");

    private void CreateFrom(Action action)
    {
        var previous = SourceItems.ToHashSet();
        action();
        var added = SourceItems.LastOrDefault(item => !previous.Contains(item));
        if (added == null) return;
        SearchBox.Clear();
        TemplateList.SelectedItem = added;
        TemplateList.ScrollIntoView(added);
    }

    private void Delete()
    {
        int index = SourceIndex;
        if (index < 0) return;
        var targets = SourceItems.Where(item => item.IsSelected).ToArray();
        if (targets.Length == 0) targets = [Selected!];
        string names = string.Join("\n", targets.Take(8).Select(item => item.Key));
        if (targets.Length > 8) names += "\n…";
        if (MessageBox.Show(this, $"删除以下 {targets.Length} 个{options.Label}模板？\n\n{names}", Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        DeleteItems(targets);
        renamed.RemoveWhere(item => !SourceItems.Contains(item));
        TemplateList.SelectedItem = items.Cast<object>().FirstOrDefault();
    }

    internal void DeleteItems(IEnumerable<TemplateBase> targets)
    {
        // Some templates delete one item, while Flow deletes all checked items in one call.
        foreach (var item in targets.ToArray())
        {
            int index = SourceItems.ToList().IndexOf(item);
            if (index >= 0) template.Delete(index);
        }
    }

    private void SaveRenames()
    {
        Keyboard.ClearFocus();
        foreach (var item in renamed.Where(SourceItems.Contains)) { item.IsEditMode = false; options.SaveName(item); }
        renamed.Clear();
        StatusText.Text = "名称已保存";
        UpdateStatus();
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void ClearSearch_Click(object sender, RoutedEventArgs e) { SearchBox.Clear(); SearchBox.Focus(); }
    private void Export_Click(object sender, RoutedEventArgs e) { if (SourceIndex >= 0) TryAction(() => template.Export(SourceIndex), "导出失败"); }
    private void Import_Click(object sender, RoutedEventArgs e) => TryAction(() => CreateFrom(() =>
    {
        template.ClearCreateTemplateSource();
        if (template.Import()) new TemplateCreate(template, true) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
    }), "导入失败");

    private void TryAction(Action action, string message)
    {
        try { action(); }
        catch (Exception ex) { log.Warn(message, ex); MessageBox.Show(this, $"{message}：{ex.Message}", Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void TemplateList_MouseDown(object sender, MouseButtonEventArgs e)
    {
        dragStart = e.GetPosition(TemplateList);
        dragItem = IsEditingControl(e.OriginalSource as DependencyObject) ? null : FindParent<ListViewItem>(e.OriginalSource as DependencyObject)?.Content as TemplateBase;
    }
    private void TemplateList_MouseMove(object sender, MouseEventArgs e)
    {
        if (dragItem == null || e.LeftButton != MouseButtonState.Pressed) return;
        Vector delta = e.GetPosition(TemplateList) - dragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var item = dragItem;
        dragItem = null;
        try { DragDrop.DoDragDrop(TemplateList, new DataObject(DragFormat, item), DragDropEffects.Move); }
        finally { SetDropTarget(null); }
    }
    private void TemplateList_DragOver(object sender, DragEventArgs e)
    {
        var target = FindParent<ListViewItem>(e.OriginalSource as DependencyObject);
        bool canSwap = e.Data.GetData(DragFormat) is TemplateBase source && browserItems.Contains(source)
            && target?.Content is TemplateBase destination && !ReferenceEquals(source, destination);
        e.Effects = canSwap ? DragDropEffects.Move : DragDropEffects.None;
        SetDropTarget(canSwap ? target : null);
        if (e.Data.GetDataPresent(DragFormat) && Environment.TickCount64 - lastDragScroll >= 80 && FindChild<ScrollViewer>(TemplateList) is { } scroll)
        {
            lastDragScroll = Environment.TickCount64;
            double y = e.GetPosition(TemplateList).Y;
            if (y < 24) scroll.LineUp(); else if (y > TemplateList.ActualHeight - 24) scroll.LineDown();
        }
        e.Handled = true;
    }
    private void SetDropTarget(ListViewItem? target)
    {
        if (ReferenceEquals(target, dropTarget)) return;
        if (dropTarget != null) dropTarget.Tag = null;
        dropTarget = target;
        if (target != null) target.Tag = "SwapTarget";
    }
    private void TemplateList_DragLeave(object sender, DragEventArgs e) => SetDropTarget(null);

    private async void TemplateList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DragFormat) is not TemplateBase source
            || FindParent<ListViewItem>(e.OriginalSource as DependencyObject)?.Content is not TemplateBase target || ReferenceEquals(source, target)) return;
        SetDropTarget(null);
        e.Handled = true;
        await SwapItemsAsync(source, target);
    }

    internal Task SwapItemsAsync(TemplateBase source, TemplateBase target)
    {
        int from = browserItems.IndexOf(source), to = browserItems.IndexOf(target);
        if (from < 0 || to < 0 || from == to) return Task.CompletedTask;
        // Only two browser slots change. Never renumber MySQL records or move the runtime collection.
        browserItems[from] = target;
        browserItems[to] = source;
        TemplateList.SelectedItem = source;
        return PersistOrderAsync();
    }

    private Task PersistOrderAsync()
    {
        int revision = ++orderRevision;
        string[] keys = browserItems.Select(OrderKey).ToArray();
        StatusText.Text = "正在保存本机顺序…";
        OrderSaveTask = SaveOrderAfterAsync(OrderSaveTask, keys, revision);
        return OrderSaveTask;
    }

    private async Task SaveOrderAfterAsync(Task previous, string[] keys, int revision)
    {
        await previous;
        try
        {
            await orderStore.SaveAsync(orderScope, keys);
            if (!isClosed && revision == orderRevision) { orderSaveFailed = false; StatusText.Text = "本机顺序已保存"; UpdateStatus(); }
        }
        catch (Exception ex)
        {
            log.Warn("Saving local template browser order failed.", ex);
            if (!isClosed && revision == orderRevision)
            {
                orderSaveFailed = true;
                StatusText.Text = "顺序尚未保存，点击“保存”重试";
                UpdateStatus();
            }
        }
    }

    private static bool IsEditingControl(DependencyObject? element)
    {
        while (element != null && element is not ListViewItem)
        {
            if (element is TextBox or ButtonBase) return true;
            element = GetParent(element);
        }
        return false;
    }
    private static T? FindParent<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element != null) { if (element is T match) return match; element = GetParent(element); }
        return null;
    }
    private static DependencyObject? GetParent(DependencyObject element) => element is Visual or System.Windows.Media.Media3D.Visual3D
        ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
    internal static T? FindChild<T>(DependencyObject element) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            if (child is T match) return match;
            if (FindChild<T>(child) is { } descendant) return descendant;
        }
        return null;
    }
}
