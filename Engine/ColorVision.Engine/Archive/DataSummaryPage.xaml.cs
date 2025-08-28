using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services.RC;
using ColorVision.Solution.Searches;
using ColorVision.UI.Sorts;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Archive.Dao
{


    public class ViewBatchResult : ViewModelBase
    {
        public MeasureBatchModel MeasureBatchModel { get; set; }

        public ContextMenu ContextMenu { get; set; }

        public ViewBatchResult()
        {

        }
        public ViewBatchResult(MeasureBatchModel batchResultMasterModel)
        {
            MeasureBatchModel = batchResultMasterModel;
            ContextMenu = new ContextMenu();
        }
    }

    /// <summary>
    /// DataSummaryPage.xaml 的交互逻辑
    /// </summary>
    public partial class DataSummaryPage : Page,ISolutionPage
    {
        public string PageTitle => nameof(DataSummaryPage);

        public Frame Frame { get; set; }

        public DataSummaryPage() { }
        public DataSummaryPage(Frame MainFrame)
        {
            Frame = MainFrame;
            InitializeComponent();
        }
        public ObservableCollection<ViewBatchResult> ViewResults { get; set; } = new ObservableCollection<ViewBatchResult>();
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
            var BatchResultMasterModels = MySqlControl.GetInstance().DB.Queryable<MeasureBatchModel>().OrderByDescending(x => x.Id).Take(100).ToList();
            foreach (var item in BatchResultMasterModels)
            {
                ViewResults.Add(new ViewBatchResult(item));
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
            foreach (var item in MySqlControl.GetInstance().DB.Queryable<MeasureBatchModel>().Where(x => x.Code == SearchBox.Text).ToList())
            {
                ViewResults.Add(new ViewBatchResult(item));
            }
        }

        private void Query_Click(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
            foreach (var item in MySqlControl.GetInstance().DB.Queryable<MeasureBatchModel>().Where(x => x.Code == SearchBox.Text).ToList())
            {
                ViewResults.Add(new ViewBatchResult(item));
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
                Type type = typeof(ViewBatchResult);

                var properties = type.GetProperties();
                foreach (var property in properties)
                {
                    var attribute = property.GetCustomAttribute<DisplayNameAttribute>();
                    if (attribute != null)
                    {
                        string displayName = attribute.DisplayName;
                        displayName = Properties.Resources.ResourceManager?.GetString(displayName, Thread.CurrentThread.CurrentUICulture) ?? displayName;
                        if (displayName == gridViewColumnHeader.Content.ToString())
                        {
                            var item = GridViewColumnVisibilities.FirstOrDefault(x => x.ColumnName.ToString() == displayName);
                            if (item != null)
                            {
                                item.IsSortD = !item.IsSortD;
                                ViewResults.SortByProperty(property.Name, item.IsSortD);
                            }
                        }
                    }
                }
            }

        }
        private void listView1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                Frame.Navigate(new BatchDataHistory(Frame, ViewResults[listView.SelectedIndex].MeasureBatchModel));
            }
        }
        private void Arch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ViewBatchResult viewBatchResult && viewBatchResult.MeasureBatchModel.Code !=null)
            {
                MqttRCService.GetInstance().Archived(viewBatchResult.MeasureBatchModel.Code);
                MessageBox.Show("归档指令已经发送");
                Frame.Refresh();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            MqttRCService.GetInstance().ArchivedAll();
            MessageBox.Show("全部归档指令已经发送");
        }

        private void AdvanceQuery_Click(object sender, RoutedEventArgs e)
        {
            GenericQuery<MeasureBatchModel, ViewBatchResult> genericQuery = new GenericQuery<MeasureBatchModel, ViewBatchResult>(MySqlControl.GetInstance().DB, ViewResults, t => new ViewBatchResult(t));
            GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }; ;
            genericQueryWindow.ShowDialog();
        }
    }
}
