using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using System;
using System.Collections.ObjectModel;
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

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
    }


    /// <summary>
    /// CalibrationTemplate.xaml 的交互逻辑
    /// </summary>
    public partial class TemplateEditorWindow : Window
    {
        private const string TemplateDragFormat = "ColorVision.TemplateEditor.Template";

        public ITemplate ITemplate { get; set; }

        public int DefaultIndex { get; set; }

        public TemplateEditorWindow(ITemplate template, int defaultIndex = 0)
        {
            ITemplate = template;
            DefaultIndex = defaultIndex < 0 ? -1 : defaultIndex;
            template.Load();
            InitializeComponent();
            this.ApplyCaption();
            MainGrid.CommandBindings.Add(new CommandBinding(ApplicationCommands.New, (s, e) => New(), (s, e) => e.CanExecute = true));
            MainGrid.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, (s, e) => CreateCopy(), (s, e) => e.CanExecute = ListView1.SelectedIndex > -1));
            MainGrid.CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (s, e) => {
                ITemplate.Save();
                HandyControl.Controls.Growl.SuccessGlobal(string.Format(Properties.Resources.TemplateEditor_SaveSuccess, Title));
            }, (s, e) => e.CanExecute = true));
            MainGrid.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = ListView1.SelectedIndex > -1));
            MainGrid.CommandBindings.Add(new CommandBinding(Commands.ReName, (s, e) => ReName(), (s, e) => e.CanExecute = ListView1.SelectedIndex > -1));

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
                GridViewColumnVisibility? moveActionsColumnVisibility = GridViewColumnVisibilitys.FirstOrDefault(item => item.GridViewColumn == MoveActionsColumn);
                if (moveActionsColumnVisibility != null)
                    GridViewColumnVisibilitys.Remove(moveActionsColumnVisibility);
                Config.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }

            if (ITemplate.IsSideHide)
            {
                GridProperty.Visibility = Visibility.Collapsed;
                Grid.SetColumnSpan(TemplateGrid, 2);
                Grid.SetRowSpan(TemplateGrid, 1);
                Grid.SetColumnSpan(CreateGrid, 2);
                Grid.SetColumn(CreateGrid, 0);

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
                if (e.Key == Key.Enter)
                {
                    if (ListView1.SelectedIndex > -1 && ITemplate.GetValue(ListView1.SelectedIndex) is TemplateBase templateModelBase)
                    {
                        templateModelBase.IsEditMode = false;
                    }
                }
            };
            // 设置快捷键 Ctrl + F
            var gesture = new KeyGesture(Key.F, ModifierKeys.Control);
            var command = new RoutedCommand();
            command.InputGestures.Add(gesture);
            CommandBindings.Add(new CommandBinding(command, (s,e) => { Searchbox.Focus(); }));
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
            if (ListView1.SelectedIndex > -1 && ITemplate.GetValue(ListView1.SelectedIndex) is TemplateBase templateModelBase)
            {
                LastSelectTemplateBase = templateModelBase;
                templateModelBase.IsEditMode = true;
            }
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

            if (contextMenu.PlacementTarget is FrameworkElement { DataContext: TemplateBase templateModel })
                ListView1.SelectedItem = templateModel;

            foreach (var menuItem in contextMenu.Items.OfType<MenuItem>())
            {
                if (Equals(menuItem.Tag, "SetJsonAsDefault"))
                    menuItem.Visibility = ITemplate.CanSetJsonAsDefault ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void MenuItem_SetJsonAsDefault_Click(object sender, RoutedEventArgs e)
        {
            SetJsonAsDefault();
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
                            else if (columnName == Properties.Resources.Choice)
                            {
                                foreach (var modebase in ITemplate.ItemsSource.OfType<TemplateBase>())
                                {
                                    modebase.IsSelected = item.IsSortD;
                                }
                                e.Handled = true;
                            }
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

            _dragStartPoint = e.GetPosition(ListView1);
            _draggedTemplate = FindVisualParent<ListViewItem>(e.OriginalSource as DependencyObject)?.Content as TemplateBase;
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

        private void ListView1_Drop(object sender, DragEventArgs e)
        {
            TemplateBase? draggedTemplate = e.Data.GetData(TemplateDragFormat) as TemplateBase;
            ListViewItem? targetItem = FindVisualParent<ListViewItem>(e.OriginalSource as DependencyObject);
            if (draggedTemplate == null || targetItem?.Content is not TemplateBase targetTemplate || ReferenceEquals(draggedTemplate, targetTemplate))
                return;

            int sourceIndex = GetTemplateSourceIndex(draggedTemplate);
            int targetIndex = GetTemplateSourceIndex(targetTemplate);
            if (sourceIndex < 0 || targetIndex < 0)
                return;

            bool dropAfterTarget = e.GetPosition(targetItem).Y > targetItem.ActualHeight / 2;
            int destinationIndex = targetIndex;
            if (dropAfterTarget && sourceIndex > targetIndex)
                destinationIndex++;
            else if (!dropAfterTarget && sourceIndex < targetIndex)
                destinationIndex--;

            destinationIndex = Math.Clamp(destinationIndex, 0, ITemplate.Count - 1);
            if (sourceIndex == destinationIndex)
                return;

            if (MoveTemplateToIndex(sourceIndex, destinationIndex))
            {
                RefreshTemplateList(draggedTemplate);
                HandyControl.Controls.Growl.SuccessGlobal(Properties.Resources.TemplateEditor_OrderSwapped);
            }
            else
            {
                RefreshTemplateList(draggedTemplate);
                MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.TemplateEditor_SwapFailed, "ColorVision");
            }

            e.Handled = true;
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

        private int LastSelectedIndex = -1;
        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView)
            {
                int selectedIndex = GetSelectedTemplateSourceIndex();
                if (selectedIndex < 0)
                    return;

                if (LastSelectedIndex >= 0 && LastSelectedIndex < ITemplate.Count)
                {
                    if (ITemplate.GetValue(LastSelectedIndex) is TemplateBase templateModelBase)
                    {
                        templateModelBase.IsEditMode = false;
                    }
                }

                ITemplate.SetSaveIndex(selectedIndex);
                if (ITemplate.IsUserControl)
                {
                    ITemplate.SetUserControlDataContext(selectedIndex);
                }
                else
                {
                    PropertyGrid1.SelectedObject = ITemplate.GetParamValue(selectedIndex);
                }
                LastSelectedIndex = selectedIndex;
            }
        }


        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            ITemplate.Save();
            Close();
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
            if (MessageBox1.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.TemplateEditor_ConfirmDelete, ITemplate.Code), "ColorVision", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
            {
                int index = ListView1.SelectedIndex;
                ITemplate.Delete(ListView1.SelectedIndex);
                if (index > ITemplate.Count)
                    index = ITemplate.Count - 1;
                ListView1.SelectedIndex = index;
            }
        }


        private void Button_Del_Click(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                Delete();
            }
            else
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.TemplateEditor_SelectFirst, "ColorVision");
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
            if (ListView1.SelectedIndex < 0)
            {
                MessageBox1.Show(Properties.Resources.TemplateEditor_SelectFlowToExport, "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                return;
            }
            ITemplate.Export(ListView1.SelectedIndex);
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
            if (sender is TextBox textBox)
            {
                if (SearchNoneText.Visibility == Visibility.Visible)
                    SearchNoneText.Visibility = Visibility.Hidden;
                if (string.IsNullOrEmpty(textBox.Text))
                {
                    ListView1.ItemsSource = ITemplate.ItemsSource;
                }
                else
                {

                    var filteredResults = FilterHelper.FilterByKeywords<TemplateBase>(ITemplate.ItemsSource, textBox.Text);
                    // 更新 ListView 的数据源
                    ListView1.ItemsSource = filteredResults;

                    // 显示或隐藏无结果文本
                    if (filteredResults.Count == 0)
                    {
                        SearchNoneText.Visibility = Visibility.Visible;
                        SearchNoneText.Text = string.Format(Properties.Resources.TemplateEditor_NoSearchResults, textBox.Text);
                    }
                    else
                    {
                        ListView1.SelectedIndex = 0;
                    }
                }
            }
        }
        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control button && button.Tag is TemplateBase templateModelBase)
            {
                templateModelBase.IsEditMode = true;
            }
        }

        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            new TemplateSettingEdit(ITemplate) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        private void CreateCopy()
        {
            int selectedIndex = GetSelectedTemplateSourceIndex();
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
            return ListView1.SelectedItem is TemplateBase selectedTemplate ? GetTemplateSourceIndex(selectedTemplate) : -1;
        }

        private int GetTemplateSourceIndex(TemplateBase template)
        {
            int index = 0;
            foreach (var sourceTemplate in ITemplate.ItemsSource.OfType<TemplateBase>())
            {
                if (ReferenceEquals(sourceTemplate, template))
                    return index;
                index++;
            }
            return -1;
        }

        private bool MoveTemplateToIndex(int sourceIndex, int destinationIndex)
        {
            int direction = sourceIndex < destinationIndex ? 1 : -1;
            for (int index = sourceIndex; index != destinationIndex; index += direction)
            {
                if (!ITemplate.SwapTemplateOrder(index, index + direction))
                    return false;
            }
            return true;
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

        private void Button_CreateCopy_Click(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                CreateCopy();
            }
            else
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.TemplateEditor_SelectFirst, "ColorVision");
            }



        }

        private void Searchbox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (ListView1.SelectedIndex > -1)
                {
                    Searchbox.Text = string.Empty;
                    ITemplate.PreviewMouseDoubleClick(ListView1.SelectedIndex);
                }
            }
            if (e.Key == System.Windows.Input.Key.Up)
            {
                if (ListView1.SelectedIndex > 0)
                    ListView1.SelectedIndex -= 1;
            }
            if (e.Key == System.Windows.Input.Key.Down)
            {
                if (ListView1.SelectedIndex < ITemplate.Count - 1)
                    ListView1.SelectedIndex += 1;


            }
        }

        private void CreateTemplate_Click(object sender, RoutedEventArgs e)
        {
            SaveSelectedTemplatesAsSamples();
        }

        private void SaveSelectedTemplatesAsSamples()
        {
            var templateEntries = ITemplate.ItemsSource
                .OfType<TemplateBase>()
                .Select((template, index) => new { Template = template, Index = index })
                .ToList();

            var selectedEntries = templateEntries.Where(item => item.Template.IsSelected).ToList();
            if (selectedEntries.Count == 0 && ListView1.SelectedItem is TemplateBase selectedTemplate)
            {
                var entry = templateEntries.FirstOrDefault(item => ReferenceEquals(item.Template, selectedTemplate));
                if (entry != null)
                    selectedEntries.Add(entry);
            }

            if (selectedEntries.Count == 0)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.TemplateEditor_SelectFirst, "ColorVision");
                return;
            }

            TemplateSampleLibrary sampleLibrary = TemplateSampleLibrary.GetInstance();
            string defaultName = selectedEntries.Count == 1 ? selectedEntries[0].Template.Key : string.Empty;
            TemplateSampleSaveWindow saveWindow = new TemplateSampleSaveWindow(sampleLibrary.GetGroupNames(ITemplate), defaultName, selectedEntries.Count)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (saveWindow.ShowDialog() != true)
                return;

            try
            {
                foreach (var entry in selectedEntries)
                {
                    string sampleName = selectedEntries.Count == 1 ? saveWindow.SampleName : entry.Template.Key;
                    sampleLibrary.SaveFromTemplate(ITemplate, entry.Index, saveWindow.GroupName, sampleName, saveWindow.Description);
                }

                HandyControl.Controls.Growl.SuccessGlobal(string.Format(Properties.Resources.TemplateEditor_SamplesSaved, selectedEntries.Count));
            }
            catch (Exception ex)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.TemplateEditor_SaveSamplesFailed, ex.Message), "ColorVision");
            }
        }

        private void SwapTemplateAndUpdateUI(int newIndex, string errorMessage)
        {
            int currentIndex = GetSelectedTemplateSourceIndex();
            TemplateBase? selectedTemplate = ListView1.SelectedItem as TemplateBase;
            if (currentIndex >= 0 && selectedTemplate != null && ITemplate.SwapTemplateOrder(currentIndex, newIndex))
            {
                RefreshTemplateList(selectedTemplate);
                HandyControl.Controls.Growl.SuccessGlobal(Properties.Resources.TemplateEditor_OrderSwapped);
            }
            else
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), errorMessage, "ColorVision");
            }
        }

        private void Button_MoveUp_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = GetSelectedTemplateSourceIndex();
            if (selectedIndex > 0)
            {
                SwapTemplateAndUpdateUI(selectedIndex - 1, Properties.Resources.TemplateEditor_SwapFailed);
            }
            else
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.TemplateEditor_AlreadyFirst, "ColorVision");
            }
        }

        private void Button_MoveDown_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = GetSelectedTemplateSourceIndex();
            if (selectedIndex >= 0 && selectedIndex < ITemplate.Count - 1)
            {
                SwapTemplateAndUpdateUI(selectedIndex + 1, Properties.Resources.TemplateEditor_SwapFailed);
            }
            else
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.TemplateEditor_AlreadyLast, "ColorVision");
            }
        }

        private void SetJsonAsDefault()
        {
            if (ListView1.SelectedIndex < 0)
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

            int selectedIndex = ListView1.SelectedIndex;
            if (ITemplate.SetJsonAsDefault(selectedIndex, out string message))
            {
                ITemplate.Load();
                if (selectedIndex > -1 && selectedIndex < ITemplate.Count)
                    ListView1.SelectedIndex = selectedIndex;

                HandyControl.Controls.Growl.SuccessGlobal(message);
            }
            else
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), message, "ColorVision");
            }
        }
    }
}
