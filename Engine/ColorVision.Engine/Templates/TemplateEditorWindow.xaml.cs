using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using ColorVision.Engine.Templates.Browser;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Templates
{
    public class TemplateSetting : ViewModelBase, IConfig
    {
        public static TemplateSetting Instance => ConfigService.Instance.GetRequiredService<TemplateSetting>();

        public string DefaultCreateTemplateName { get => _DefaultCreateTemplateName; set { _DefaultCreateTemplateName = value; OnPropertyChanged(); } }
        private string _DefaultCreateTemplateName = Properties.Resources.DefaultCreateTemplateName;

        [Browsable(false)]
        public bool FlowBrowserListView { get; set; }
        [Browsable(false)]
        public bool PoiBrowserListView { get; set; }
        [Browsable(false)]
        public TemplateBrowserIconSize FlowBrowserIconSize { get; set; } = TemplateBrowserIconSize.Medium;
        [Browsable(false)]
        public TemplateBrowserIconSize PoiBrowserIconSize { get; set; } = TemplateBrowserIconSize.Medium;
        [Browsable(false)]
        public TemplateBrowserSort FlowBrowserSort { get; set; }
        [Browsable(false)]
        public TemplateBrowserSort PoiBrowserSort { get; set; }
        [Browsable(false)]
        public bool FlowBrowserSortDescending { get; set; } = true;
        [Browsable(false)]
        public bool PoiBrowserSortDescending { get; set; } = true;
        [Browsable(false)]
        public TemplateBrowserGroup FlowBrowserGroup { get; set; }
        [Browsable(false)]
        public TemplateBrowserGroup PoiBrowserGroup { get; set; }
        [Browsable(false)]
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
    }


    /// <summary>
    /// CalibrationTemplate.xaml 的交互逻辑
    /// </summary>
    public partial class TemplateEditorWindow : Window
    {
        private const string TemplateDragFormat = "ColorVision.TemplateEditor.Template";
        private bool savingOrder;

        public ITemplate ITemplate { get; set; }

        public int DefaultIndex { get; set; }

        public TemplateEditorWindow(ITemplate template, int defaultIndex = 0)
        {
            ITemplate = template;
            DefaultIndex = defaultIndex < 0 ? -1 : defaultIndex;
            template.Load();
            InitializeComponent();
            this.ApplyCaption();
            Closing += (_, e) => { if (savingOrder) e.Cancel = true; };
            MainGrid.CommandBindings.Add(new CommandBinding(ApplicationCommands.New, (s, e) => New(), (s, e) => e.CanExecute = true));
            MainGrid.CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (s, e) => {
                if (!TrySaveEditor()) return;
                HandyControl.Controls.Growl.SuccessGlobal(string.Format(Properties.Resources.TemplateEditor_SaveSuccess, Title));
            }, (s, e) => e.CanExecute = true));
            MainGrid.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = ListView1.SelectedIndex > -1));
            MainGrid.CommandBindings.Add(new CommandBinding(Commands.ReName, (s, e) =>
            {
                if (e.Parameter is TemplateBase row) BeginRename(row);
                else ReName();
            }, (s, e) => e.CanExecute = ListView1.SelectedIndex > -1));

        }
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
        public static TemplateSetting Config => TemplateSetting.Instance;
        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
            HandyControl.Controls.Growl.SetGrowlParent(this, true);
            if (ListView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                foreach (var column in GridViewColumnVisibilitys)
                    column.IsVisible = Equals(column.ColumnName, Properties.Resources.Name);
                Config.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }

            if (ITemplate.IsSideHide)
            {
                GridProperty.Visibility = Visibility.Collapsed;
                Grid.SetColumnSpan(TemplateGrid, 2);

                MinWidth = 360;
                Width = 360;
            }

            if (ITemplate.IsUserControl)
            {
                GridProperty.Children.Clear();
                GridProperty.Margin = new Thickness(5, 5, 5, 5);
                UserControl userControl = ITemplate.GetUserControl();
                if (userControl.Parent is Grid grid)
                    grid.Children.Remove(userControl);
                GridProperty.Children.Add(userControl);
                if (!double.IsNaN(userControl.Height))
                {
                    userControl.Height = double.NaN;
                }
                if (!double.IsNaN(userControl.Width))
                {
                    Width = userControl.Width + 350;
                    userControl.Width = double.NaN;
                }
                else
                {
                    Width = Width + 350;
                }
                TemplateGrid.MinWidth = 200;
                ScrollInfo.HorizontalAlignment = HorizontalAlignment.Right;
            }

            Title = ITemplate.Title;
            ListView1.ItemsSource = ITemplate.ItemsSource;
            ListView1.SelectedIndex = DefaultIndex;

            Closed += WindowTemplate_Closed;

            this.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape && TemplateSearchBar.Visibility == Visibility.Visible && TemplateGrid.IsKeyboardFocusWithin)
                {
                    CloseTemplateSearch();
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter && !Searchbox.IsKeyboardFocusWithin && LastSelectTemplateBase != null)
                    LastSelectTemplateBase.IsEditMode = false;
            };
            var gesture = new KeyGesture(Key.F, ModifierKeys.Control);
            var command = new RoutedCommand();
            command.InputGestures.Add(gesture);
            CommandBindings.Add(new CommandBinding(command, (s, e) =>
            {
                TemplateSearchBar.Visibility = Visibility.Visible;
                Searchbox.Focus();
                Searchbox.SelectAll();
            }));
            ListView1.Focus();

            this.Deactivated += (s, e) =>
            {
                if (LastSelectTemplateBase != null)
                    LastSelectTemplateBase.IsEditMode = false;
            };
        }

        private TemplateBase LastSelectTemplateBase;

        public void ReName()
        {
            if (ListView1.SelectedItem is TemplateBase template) BeginRename(template);
        }

        private void BeginRename(TemplateBase template)
        {
            LastSelectTemplateBase = template;
            template.IsEditMode = true;
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && ListView1.View is GridView gridView)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
        }

        private void ContextMenuItem_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu contextMenu)
                return;

            var template = (contextMenu.PlacementTarget as FrameworkElement)?.DataContext as TemplateBase;
            if (template != null && !ListView1.SelectedItems.Contains(template)) ListView1.SelectedItem = template;

            foreach (var menuItem in contextMenu.Items.OfType<MenuItem>())
            {
                menuItem.CommandParameter = template;
                if (Equals(menuItem.Tag, "SetJsonAsDefault"))
                    menuItem.Visibility = ITemplate.CanSetJsonAsDefault ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void MenuItem_SetJsonAsDefault_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = sender is MenuItem { CommandParameter: TemplateBase target } ? ITemplate.GetSourceIndex(target) : -1;
            SetJsonAsDefault(selectedIndex);
        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader gridViewColumnHeader && gridViewColumnHeader.Content != null)
            {
                var columnName = gridViewColumnHeader.Content.ToString();
                foreach (var item in GridViewColumnVisibilitys)
                {
                    if (item.ColumnName.ToString() == columnName)
                    {
                        item.IsSortD = !item.IsSortD;

                        var collection = ITemplate.GetValue();
                        var itemType = collection.GetType().GetGenericArguments().FirstOrDefault();
 
                        if (itemType != null)
                        {
                            if (columnName == Properties.Resources.SerialNumber1)
                            {
                                SortableExtension.SortByProperty(collection,"Id", item.IsSortD);

                            }
                            else if (columnName == Properties.Resources.Name)
                            {
                                SortableExtension.SortByProperty(collection, "Key", item.IsSortD);
                            }
                        }
                        if (columnName == Properties.Resources.Choice)
                        {
                            if (ListView1.SelectedItems.Count == ListView1.Items.Count) ListView1.UnselectAll();
                            else ListView1.SelectAll();
                        }
                    }
                }
            }
            e.Handled = true;
        }



        private void WindowTemplate_Closed(object? sender, EventArgs e)
        {
            ITemplate.Load();
        }

        private void ListView1_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) return;
            DependencyObject source = (DependencyObject)e.OriginalSource;

            while (source != null && !(source is ListViewItem))
            {
                source = VisualTreeHelper.GetParent(source);
            }

            if (source is ListViewItem)
            {
                int selectedIndex = GetSelectedTemplateSourceIndex();
                if (selectedIndex > -1)
                    ITemplate.PreviewMouseDoubleClick(selectedIndex);
            }

        }

        private Point _dragStartPoint;
        private TemplateBase? _draggedTemplate;
        private ScrollViewer? _listScrollViewer;

        private void ListView1_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _draggedTemplate = null;
            if (IsDragBlockedElement(e.OriginalSource as DependencyObject))
                return;

            var item = FindVisualParent<ListViewItem>(e.OriginalSource as DependencyObject);
            if (item != null && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                item.Focus();
                item.SetCurrentValue(ListViewItem.IsSelectedProperty, !item.IsSelected);
                e.Handled = true;
                return;
            }

            _dragStartPoint = e.GetPosition(ListView1);
            _draggedTemplate = item?.Content as TemplateBase;
        }

        private void ListView1_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedTemplate == null)
                return;

            Point currentPosition = e.GetPosition(ListView1);
            Vector difference = _dragStartPoint - currentPosition;
            if (Math.Abs(difference.X) <= SystemParameters.MinimumHorizontalDragDistance && Math.Abs(difference.Y) <= SystemParameters.MinimumVerticalDragDistance)
                return;

            TemplateBase draggedTemplate = _draggedTemplate;
            _draggedTemplate = null;
            DragDrop.DoDragDrop(ListView1, new DataObject(TemplateDragFormat, draggedTemplate), DragDropEffects.Move);
        }

        private void ListView1_DragOver(object sender, DragEventArgs e)
        {
            TemplateBase? draggedTemplate = e.Data.GetData(TemplateDragFormat) as TemplateBase;
            TemplateBase? targetTemplate = FindVisualParent<ListViewItem>(e.OriginalSource as DependencyObject)?.Content as TemplateBase;
            bool canDrop = draggedTemplate != null && targetTemplate != null && !ReferenceEquals(draggedTemplate, targetTemplate);
            e.Effects = canDrop ? DragDropEffects.Move : DragDropEffects.None;
            if (draggedTemplate != null)
                AutoScrollTemplateList(e.GetPosition(ListView1));
            e.Handled = true;
        }

        private async void ListView1_Drop(object sender, DragEventArgs e)
        {
            TemplateBase? draggedTemplate = e.Data.GetData(TemplateDragFormat) as TemplateBase;
            ListViewItem? targetItem = FindVisualParent<ListViewItem>(e.OriginalSource as DependencyObject);
            if (draggedTemplate == null || targetItem?.Content is not TemplateBase targetTemplate || ReferenceEquals(draggedTemplate, targetTemplate))
                return;

            int sourceIndex = ITemplate.GetSourceIndex(draggedTemplate);
            int targetIndex = ITemplate.GetSourceIndex(targetTemplate);
            if (sourceIndex < 0 || targetIndex < 0)
                return;

            e.Handled = true;
            savingOrder = true;
            IsEnabled = false;
            try
            {
                bool saved = await ITemplate.SwapTemplateOrderAsync(sourceIndex, targetIndex);
                RefreshTemplateList(draggedTemplate);
                if (saved) HandyControl.Controls.Growl.SuccessGlobal(Properties.Resources.TemplateEditor_OrderSwapped);
                else MessageBox1.Show(this, "排序保存失败，请重新打开管理窗口核对后重试。", "ColorVision");
            }
            finally { savingOrder = false; IsEnabled = true; }
        }

        private static T? FindVisualParent<T>(DependencyObject? element) where T : DependencyObject
        {
            while (element != null)
            {
                if (element is T parent)
                    return parent;
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }

        private static T? FindVisualChild<T>(DependencyObject element) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(element, i);
                if (child is T result)
                    return result;

                T? descendant = FindVisualChild<T>(child);
                if (descendant != null)
                    return descendant;
            }
            return null;
        }

        private void AutoScrollTemplateList(Point position)
        {
            _listScrollViewer ??= FindVisualChild<ScrollViewer>(ListView1);
            if (_listScrollViewer == null)
                return;

            const double scrollEdge = 24;
            if (position.Y < scrollEdge)
                _listScrollViewer.LineUp();
            else if (position.Y > ListView1.ActualHeight - scrollEdge)
                _listScrollViewer.LineDown();
        }

        private static bool IsDragBlockedElement(DependencyObject? element)
        {
            while (element != null && element is not ListViewItem)
            {
                if (element is ButtonBase or TextBox)
                    return true;
                element = VisualTreeHelper.GetParent(element);
            }
            return false;
        }

        private TemplateBase? activeTemplate;
        private bool _restoringSelection;

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_restoringSelection || !ReferenceEquals(e.OriginalSource, ListView1) || ReferenceEquals(activeTemplate, ListView1.SelectedItem)) return;
            if (GridProperty.Children.OfType<ITemplateEditorValidation>().Any(editor => !editor.TryCommitPendingEdits()))
            {
                _restoringSelection = true;
                try
                {
                    foreach (var added in e.AddedItems) ListView1.SelectedItems.Remove(added);
                    if (activeTemplate != null && ListView1.Items.Contains(activeTemplate) && !ListView1.SelectedItems.Contains(activeTemplate))
                        ListView1.SelectedItems.Add(activeTemplate);
                    foreach (var removed in e.RemovedItems)
                        if (ListView1.Items.Contains(removed) && !ListView1.SelectedItems.Contains(removed)) ListView1.SelectedItems.Add(removed);
                }
                finally { _restoringSelection = false; }
                return;
            }
            UpdateActiveTemplate();
        }

        private void UpdateActiveTemplate()
        {
            int selectedIndex = GetSelectedTemplateSourceIndex();
            if (selectedIndex < 0 || ReferenceEquals(activeTemplate, ListView1.SelectedItem)) return;

            if (activeTemplate != null) activeTemplate.IsEditMode = false;
            activeTemplate = (TemplateBase)ListView1.SelectedItem;
            ITemplate.SetSaveIndex(selectedIndex);
            if (ITemplate.IsUserControl)
                ITemplate.SetUserControlDataContext(selectedIndex);
            else
                PropertyGrid1.SelectedObject = ITemplate.GetParamValue(selectedIndex);
        }


        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            if (TrySaveEditor()) Close();
        }

        private bool TrySaveEditor()
        {
            var editors = GridProperty.Children.OfType<ITemplateEditorValidation>().ToArray();
            if (editors.Any(editor => !editor.TryCommitPendingEdits())) return false;
            try { ITemplate.Save(); }
            catch (Exception ex) when (ITemplate is POI.TemplatePoi or Flow.TemplateFlow)
            {
                MessageBox1.Show(this, $"{ITemplate.Title}保存失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            foreach (var editor in editors) editor.AcceptSavedChanges();
            return true;
        }

        private void New()
        {
            ITemplate.ClearCreateTemplateSource();
            OpenCreateWindow();
        }

        private void OpenCreateWindow()
        {
            int oldCount = ITemplate.Count;
            ITemplate.OpenCreate();
            RefreshAfterCreate(oldCount);
        }

        private void RefreshAfterCreate(int oldCount, string? createdName = null)
        {
            if (oldCount == ITemplate.Count)
                return;

            Searchbox.Text = string.Empty;
            ListView1.ItemsSource = ITemplate.ItemsSource;

            int createdIndex = string.IsNullOrWhiteSpace(createdName) ? ITemplate.Count - 1 : ITemplate.GetTemplateIndex(createdName);
            if (createdIndex < 0)
                createdIndex = ITemplate.Count - 1;

            ListView1.SelectedItem = ITemplate.ItemsSource.Cast<object>().ElementAtOrDefault(createdIndex);
            if (ListView1.SelectedItem != null)
                ListView1.ScrollIntoView(ListView1.SelectedItem);

            if (ListView1.View is GridView gridView)
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
        }
        private void Delete()
        {
            var targets = ListView1.SelectedItems.OfType<TemplateBase>().ToArray();
            if (targets.Length == 0) return;
            string names = string.Join(Environment.NewLine, targets.Select(target => target.Key));
            string prompt = string.Format(Properties.Resources.TemplateBrowserDeletePrompt, targets.Length, ITemplate.Code, names);
            if (MessageBox1.Show(this, prompt, Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
                return;

            int visibleIndex = ListView1.SelectedIndex;
            var sourceBeforeDelete = ITemplate.ItemsSource.OfType<TemplateBase>().ToArray();
            var pendingSaves = ITemplate.SaveIndex.Where(index => index >= 0 && index < sourceBeforeDelete.Length)
                .Select(index => sourceBeforeDelete[index]).ToArray();
            var currentTargets = targets.Where(target => ITemplate.GetSourceIndex(target) >= 0).ToArray();
            _restoringSelection = true;
            try
            {
                if (currentTargets.Length > 0) ITemplate.DeleteTemplates(currentTargets);
            }
            catch (Exception ex)
            {
                MessageBox1.Show(this, ex.Message, Properties.Resources.TemplateBrowserDeleteFailed, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                ITemplate.SaveIndex = pendingSaves.Select(ITemplate.GetSourceIndex).Where(index => index >= 0).ToList();
                _restoringSelection = false;
                ApplyTemplateSearch();
                if (ListView1.SelectedIndex < 0 && ListView1.Items.Count > 0)
                    ListView1.SelectedIndex = Math.Clamp(visibleIndex, 0, ListView1.Items.Count - 1);
                UpdateActiveTemplate();
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is TemplateBase templateModelBase)
            {
                templateModelBase.IsEditMode = false;
            }
        }

        private void Button_Export_Click(object sender, RoutedEventArgs e)
        {
            var targets = ListView1.SelectedItems.OfType<TemplateBase>().Where(target => ITemplate.GetSourceIndex(target) >= 0).ToArray();
            if (targets.Length == 0)
            {
                MessageBox1.Show(Properties.Resources.TemplateEditor_SelectFlowToExport, "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                return;
            }
            try { ITemplate.ExportTemplates(targets); }
            catch (Exception ex) when (ITemplate is POI.TemplatePoi or Flow.TemplateFlow)
            {
                MessageBox1.Show(this, $"{ITemplate.Title}导出失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Button_Import_Click(object sender, RoutedEventArgs e)
        {
            ITemplate.ClearCreateTemplateSource();
            if (ITemplate.Import())
            {
                int oldnum = ITemplate.Count;
                TemplateCreate createWindow = new TemplateCreate(ITemplate, true) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                createWindow.ShowDialog();
                RefreshAfterCreate(oldnum, createWindow.CreateName);
            }
        }

        private void Searchbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyTemplateSearch();
            if (ListView1.SelectedIndex < 0 && ListView1.Items.Count > 0 && !string.IsNullOrEmpty(Searchbox.Text))
                ListView1.SelectedIndex = 0;
        }

        private void ApplyTemplateSearch()
        {
            string query = Searchbox.Text;
            var selected = ListView1.SelectedItems.Cast<object>().ToArray();
            _restoringSelection = true;
            try
            {
                ListView1.ItemsSource = string.IsNullOrEmpty(query)
                    ? ITemplate.ItemsSource
                    : FilterHelper.FilterByKeywords<TemplateBase>(ITemplate.ItemsSource, query);
                foreach (var item in selected)
                    if (ListView1.Items.Contains(item) && !ListView1.SelectedItems.Contains(item)) ListView1.SelectedItems.Add(item);
            }
            finally { _restoringSelection = false; }

            bool noResults = !string.IsNullOrEmpty(query) && ListView1.Items.Count == 0;
            SearchNoneText.Visibility = noResults ? Visibility.Visible : Visibility.Collapsed;
            if (noResults) SearchNoneText.Text = string.Format(Properties.Resources.TemplateEditor_NoSearchResults, query);

            if (!ReferenceEquals(activeTemplate, ListView1.SelectedItem) &&
                !GridProperty.Children.OfType<ITemplateEditorValidation>().Any(editor => !editor.TryCommitPendingEdits())) UpdateActiveTemplate();
        }

        private void CloseTemplateSearch()
        {
            Searchbox.Clear();
            TemplateSearchBar.Visibility = Visibility.Collapsed;
            if (ListView1.SelectedItem == null && activeTemplate != null && ListView1.Items.Contains(activeTemplate))
                ListView1.SelectedItem = activeTemplate;
            ListView1.Focus();
        }
        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control button && button.Tag is TemplateBase templateModelBase)
            {
                templateModelBase.IsEditMode = true;
            }
        }

        private void Button_CreateCopy_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = sender is MenuItem { CommandParameter: TemplateBase target } ? ITemplate.GetSourceIndex(target) : -1;
            if (selectedIndex < 0)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.TemplateEditor_SelectFirst, "ColorVision");
                return;
            }

            string sourceName = ITemplate.GetTemplateName(selectedIndex);
            ITemplate.ClearCreateTemplateSource();
            if (ITemplate.CopyTo(selectedIndex))
            {
                ITemplate.ImportName = ITemplate.NewCreateFileName($"{sourceName}_Copy");
                int oldnum = ITemplate.Count;
                ITemplate.OpenCreate();
                RefreshAfterCreate(oldnum);
            }
        }

        private int GetSelectedTemplateSourceIndex()
        {
            return ListView1.SelectedItem is TemplateBase selectedTemplate ? ITemplate.GetSourceIndex(selectedTemplate) : -1;
        }

        private void RefreshTemplateList(TemplateBase selectedTemplate)
        {
            if (string.IsNullOrEmpty(Searchbox.Text))
            {
                ListView1.Items.Refresh();
            }
            else
            {
                ListView1.ItemsSource = FilterHelper.FilterByKeywords<TemplateBase>(ITemplate.ItemsSource, Searchbox.Text);
            }

            ListView1.SelectedItem = selectedTemplate;
            ListView1.ScrollIntoView(selectedTemplate);
        }

        private void Searchbox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                int sourceIndex = GetSelectedTemplateSourceIndex();
                if (sourceIndex >= 0)
                {
                    CloseTemplateSearch();
                    ITemplate.PreviewMouseDoubleClick(sourceIndex);
                }
                e.Handled = true;
            }
            else if (e.Key is Key.Up or Key.Down && ListView1.Items.Count > 0)
            {
                int offset = e.Key == Key.Up ? -1 : 1;
                ListView1.SelectedIndex = Math.Clamp(ListView1.SelectedIndex + offset, 0, ListView1.Items.Count - 1);
                if (ListView1.SelectedItem != null) ListView1.ScrollIntoView(ListView1.SelectedItem);
                e.Handled = true;
            }
        }

        private void SetJsonAsDefault(int selectedIndex)
        {
            if (selectedIndex < 0)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.TemplateEditor_SelectFirst, "ColorVision");
                return;
            }

            if (MessageBox1.Show(
                    Application.Current.GetActiveWindow(),
                    Properties.Resources.TemplateEditor_ConfirmSetJsonAsDefault,
                    "ColorVision",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning) != MessageBoxResult.OK)
                return;

            if (ITemplate.SetJsonAsDefault(selectedIndex, out string message))
            {
                ITemplate.Load();

                HandyControl.Controls.Growl.SuccessGlobal(message);
            }
            else
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), message, "ColorVision");
            }
        }
    }
}
