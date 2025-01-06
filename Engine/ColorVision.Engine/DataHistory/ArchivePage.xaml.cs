using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Solution.Searches;
using ColorVision.UI.PropertyEditor;
using ColorVision.UI.Sorts;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.DataHistory.Dao
{
    public class ViewArchiveResult : ViewModelBase, ISortID,ISortCreateTime
    {
        public ViewArchiveResult(ArchivedMasterModel  archivedMasterModel)
        {
            ArchivedMasterModel = archivedMasterModel;
        }
        public ArchivedMasterModel ArchivedMasterModel { get; set; }
        public int Id { get => ArchivedMasterModel.Id; set => throw new NotImplementedException(); }
        public DateTime? CreateTime { get => ArchivedMasterModel.CreateDate; }
    }

    /// <summary>
    /// ArchivePage.xaml 的交互逻辑
    /// </summary>
    public partial class ArchivePage : Page, ISolutionPage
    {
        public string PageTitle => nameof(ArchivePage);

        public Frame Frame { get; set; }

        public ArchivePage() { }
        public ArchivePage(Frame MainFrame)
        {
            Frame = MainFrame;
            InitializeComponent();
        }


        public ObservableCollection<ViewArchiveResult> ViewResults { get; set; } = new ObservableCollection<ViewArchiveResult>();
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
            foreach (var item in ArchivedMasterDao.Instance.GetAll())
            {
                ViewResults.Add(new ViewArchiveResult(item));
            }
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            listView1.ItemsSource = ViewResults;
            if (listView1.View is GridView gridView)
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilities);
        }
        private void KeyEnter(object sender, KeyEventArgs e)
        {

        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewResults.Clear();
            foreach (var item in ArchivedMasterDao.Instance.ConditionalQuery(SearchBox.Text))
            {
                ViewResults.AddUnique(new ViewArchiveResult(item));
            }
        }

        private void Query_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in ArchivedMasterDao.Instance.ConditionalQuery(SearchBox.Text))
            {
                ViewResults.AddUnique(new ViewArchiveResult(item));
            }
        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilities { get; set; } = new ObservableCollection<GridViewColumnVisibility>();


        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && listView1.View is GridView gridView)
                 GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilities);
        }


        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader gridViewColumnHeader && gridViewColumnHeader.Content != null)
            {
                foreach (var item in GridViewColumnVisibilities)
                {
                    if (item.ColumnName.ToString() == gridViewColumnHeader.Content.ToString())
                    {
                        switch (item.ColumnName)
                        {
                            case "序号":
                                item.IsSortD = !item.IsSortD;
                                ViewResults.SortByID(item.IsSortD);
                                break;
                            case "测量时间":
                                item.IsSortD = !item.IsSortD;
                                ViewResults.SortByCreateTime(item.IsSortD);
                                break;
                            default:
                                break;
                        }
                        break;
                    }
                }
            }

        }
        private void listView1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                Frame.Navigate(new ArchiveDetailPage(Frame, ViewResults[listView.SelectedIndex]));
            }
        }

        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            ConfigArchivedModel configArchivedModel = ConfigArchivedDao.Instance.GetById(1);
            if (configArchivedModel == null)
            {
                MessageBox.Show("找不到归档配置信息");
            }
            else
            {
                PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(configArchivedModel, false) { Owner = Application.Current.GetActiveWindow() ,WindowStartupLocation =WindowStartupLocation.CenterOwner };
                propertyEditorWindow.Submited += (s, e) => { ConfigArchivedDao.Instance.Save(configArchivedModel); };
                propertyEditorWindow.ShowDialog();
            }
        }
    }
}
