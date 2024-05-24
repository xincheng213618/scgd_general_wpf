using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Sorts;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Services.ShowPage.Dao
{
    public class ViewArchiveDetailResult : ViewModelBase
    {
        public ViewArchiveDetailResult(ArchivedDetailModel  model)
        {
            ArchivedDetailModel = model;
        }

        public ArchivedDetailModel ArchivedDetailModel { get; set; }
    }


    /// <summary>
    /// ArchiveDetailPage.xaml 的交互逻辑
    /// </summary>
    public partial class ArchiveDetailPage : Page
    {
        public Frame Frame { get; set; }
        public ViewArchiveResult ViewArchiveResult { get; set; }
        public ArchiveDetailPage(Frame MainFrame , ViewArchiveResult viewArchiveResult)
        {
            Frame = MainFrame;
            ViewArchiveResult = viewArchiveResult;
            InitializeComponent();
        }


        public ObservableCollection<ViewArchiveDetailResult> ViewResults { get; set; } = new ObservableCollection<ViewArchiveDetailResult>();
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
            if (ViewArchiveResult.ArchivedMasterModel.Code == null) return;
            foreach (var item in ArchivedDetailDao.Instance.GetAllByParam(new System.Collections.Generic.Dictionary<string, object>() { { "p_guid", ViewArchiveResult.ArchivedMasterModel.Code} }))
            {
                ViewResults.Add(new ViewArchiveDetailResult(item));
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
                                break;
                            case "测量时间":
                                item.IsSortD = !item.IsSortD;
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
                EditArchived editArchived = new(configArchivedModel) {  Owner =Application.Current.GetActiveWindow()};
                editArchived.ShowDialog();
            }


        }
    }
}
