using ColorVision.Common.Utilities;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Services.Dao;
using ColorVision.UI.Sorts;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Templates
{
    /// <summary>
    /// CalibrationTemplate.xaml 的交互逻辑
    /// </summary>
    public partial class WindowTemplate : Window 
    {
        public ITemplate ITemplate { get; set; }

        public int DefaultIndex { get; set; }

        public WindowTemplate(ITemplate template,int defaultIndex = 0)
        {
            ITemplate = template;
            DefaultIndex = defaultIndex;
            template.Load();
            InitializeComponent();
        }


        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = TemplateConfig.Instance;
            if (ITemplate.IsSideHide)
            {
                GridProperty.Visibility = Visibility.Collapsed;
                Grid.SetColumnSpan(TemplateGrid, 2);
                Grid.SetRowSpan(TemplateGrid, 1);
                Grid.SetColumnSpan(CreateGrid, 2);
                Grid.SetColumn(CreateGrid, 0);

                MinWidth = 350;
                Width = 350;
            }

            if (ITemplate.IsUserControl)
            {
                GridProperty.Children.Clear();
                GridProperty.Margin = new Thickness(5, 5, 5, 5);
                GridProperty.Children.Add(ITemplate.GetUserControl());
                Width = Width + 200;
            }

            Title = ITemplate.Title;
            ListView1.ItemsSource = ITemplate.ItemsSource;
            ListView1.SelectedIndex = DefaultIndex;
            if (ListView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                TemplateConfig.Instance.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                TemplateConfig.Instance.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }
            Closed += WindowTemplate_Closed;

            this.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.F2)
                {
                    if (ListView1.SelectedIndex >-1 && ITemplate.GetValue(ListView1.SelectedIndex) is TemplateModelBase templateModelBase)
                    {
                        templateModelBase.IsEditMode = true;
                    }
                }
                if (e.Key == Key.Enter)
                {
                    if (ListView1.SelectedIndex > -1 && ITemplate.GetValue(ListView1.SelectedIndex) is TemplateModelBase templateModelBase)
                    {
                        templateModelBase.IsEditMode = false;
                    }
                }
            };
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
                if (sender is ListView listView && listView.SelectedIndex > -1)
                {
                    ITemplate.PreviewMouseDoubleClick(listView.SelectedIndex);
                }
            }

        }
        private MeasureMasterDao measureMaster = new();
        private MeasureDetailDao measureDetail = new();

        private int LastSelectedIndex = -1;
        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                if (LastSelectedIndex >= 0 && LastSelectedIndex< listView.Items.Count)
                {
                    if (ITemplate.GetValue(LastSelectedIndex) is TemplateModelBase templateModelBase)
                    {
                        templateModelBase.IsEditMode = false;
                    }
                }

                ITemplate.SetSaveIndex(listView.SelectedIndex);
                if (ITemplate.IsUserControl)
                {
                    ITemplate.SetUserControlDataContext(listView.SelectedIndex);
                }
                else
                {
                    PropertyGrid1.SelectedObject = ITemplate.GetParamValue(listView.SelectedIndex);
                }
                LastSelectedIndex = listView.SelectedIndex;
            }
        }


        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            ITemplate.Save();
            Close();
        }

        private void Button_New_Click(object sender, RoutedEventArgs e)
        {
            int oldnum = ITemplate.Count;
            CreateTemplate createWindow = new CreateTemplate(ITemplate) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            createWindow.ShowDialog();
            if (oldnum!= ITemplate.Count)
            {
                ListView1.SelectedIndex= ITemplate.Count-1;
                if (ListView1.View is GridView gridView)
                    GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);

            }
        }


        private void Button_Del_Click(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), $"是否删除{ITemplate.Code}模板,删除后无法恢复!", Application.Current.MainWindow.Title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
                {
                    int index = ListView1.SelectedIndex;
                    ITemplate.Delete(ListView1.SelectedIndex);
                    if (index > ITemplate.Count)
                        index = ITemplate.Count - 1;
                    ListView1.SelectedIndex = index;
                }
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "请先选择", "ColorVision");
            }
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }


        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is TemplateModelBase templateModelBase)
            {
                templateModelBase.IsEditMode = false;
            }
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {

        }


        private void TextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is TemplateModelBase templateModelBase)
            {
                templateModelBase.IsEditMode = true;
            }
        }



        private void Button_Export_Click(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedIndex < 0)
            {
                MessageBox.Show("请选择您要导出的流程", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                return;
            }
            ITemplate.Export(ListView1.SelectedIndex);
        }

        private void Button_Import_Click(object sender, RoutedEventArgs e)
        {
            if (ITemplate.Import())
            {
                int oldnum = ITemplate.Count;
                CreateTemplate createWindow = new CreateTemplate(ITemplate, true) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                createWindow.ShowDialog();
                if (oldnum != ITemplate.Count)
                {
                    ListView1.SelectedIndex = ITemplate.Count - 1;
                    if (ListView1.View is GridView gridView)
                        GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);

                }
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

                    var filteredResults = FilterHelper.FilterByKeywords<TemplateModelBase>(ITemplate.ItemsSource, textBox.Text);
                    // 更新 ListView 的数据源
                    ListView1.ItemsSource = filteredResults;

                    // 显示或隐藏无结果文本
                    if (filteredResults.Count == 0)
                    {
                        SearchNoneText.Visibility = Visibility.Visible;
                        SearchNoneText.Text = $"未找到{textBox.Text}相关模板";
                    }
                }
            }
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && ListView1.View is GridView gridView)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
        }
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

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
                            if (columnName == Engine.Properties.Resources.SerialNumber1 && typeof(ISortID).IsAssignableFrom(itemType))
                            {
                                SortableExtension.InvokeSortMethod("SortByID", itemType, collection, item.IsSortD);
                            }
                            else if (columnName == Engine.Properties.Resources.Name && typeof(ISortKey).IsAssignableFrom(itemType))
                            {
                                SortableExtension.InvokeSortMethod("SortByKey", itemType, collection, item.IsSortD);
                            }
                            else if (columnName == Engine.Properties.Resources.Choice)
                            {
                                foreach (var modebase in ITemplate.ItemsSource.OfType<TemplateModelBase>())
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




        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Control button && button.Tag is TemplateModelBase templateModelBase)
            {
                templateModelBase.IsEditMode = true;
            }
        }
    }
}
