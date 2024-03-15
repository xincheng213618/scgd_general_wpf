using ColorVision.Common.MVVM;
using ColorVision.MVVM;
using ColorVision.Services.DAO;
using ColorVision.Services.Devices.Spectrum.Views;
using ColorVision.Sorts;
using MQTTMessageLib.Camera;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ColorVision.Solution.View
{
    public class ViewBatchResult : ViewModelBase,ISortID,ISortCreateTime, ISortBatch
    {
        public ViewBatchResult(BatchResultMasterModel batchResultMasterModel)
        {
            Id = batchResultMasterModel.Id;
            Batch = batchResultMasterModel.Name;
            BatchCode = batchResultMasterModel.Code;
            CreateTime = batchResultMasterModel.CreateDate;
            TotalTime = TimeSpan.FromMilliseconds((double)(batchResultMasterModel.TotalTime??0));
        }
        public int Id { get { return _Id; } set { _Id = value; NotifyPropertyChanged(); } }
        private int _Id;

        public string? Batch { get { return _Batch; } set { _Batch = value; NotifyPropertyChanged(); } }
        private string? _Batch;
        public string? BatchCode { get { return _BatchCode; } set { _BatchCode = value; NotifyPropertyChanged(); } }
        private string? _BatchCode;

        public DateTime? CreateTime { get=> _CreateTime;  set { _CreateTime = value; NotifyPropertyChanged(); } }
        private DateTime? _CreateTime;

        public TimeSpan? TotalTime { get => _TotalTime; set { _TotalTime = value; NotifyPropertyChanged(); } }
        private TimeSpan? _TotalTime;

    }

    /// <summary>
    /// DataSummaryPage.xaml 的交互逻辑
    /// </summary>
    public partial class DataSummaryPage : UserControl
    {
        public Frame Frame { get; set; }

        public DataSummaryPage(Frame MainFrame)
        {
            Frame = MainFrame;
            InitializeComponent();
        }
        BatchResultMasterDao batchResultMasterDao = new BatchResultMasterDao();

        public ObservableCollection<ViewBatchResult> ViewBatchResults { get; set; } = new ObservableCollection<ViewBatchResult>();

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            var BatchResultMasterModels = batchResultMasterDao.GetAll();
            foreach (var item in BatchResultMasterModels)
            {
                ViewBatchResults.Add(new ViewBatchResult(item));
            }
            listView1.ItemsSource = ViewBatchResults;
        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                Frame.Navigate(new BatchShowPage(Frame, ViewBatchResults[listView.SelectedIndex]));
            }
        }
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilities { get; set; }
        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && listView1.View is GridView gridView)
                GridViewColumnVisibilities = GridViewColumnVisibility.GenContentMenuGridViewColumnZero(contextMenu, gridView.Columns);
        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {

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
                                ViewBatchResults.SortByID(item.IsSortD);
                                break;
                            case "测量时间":
                                item.IsSortD = !item.IsSortD;
                                ViewBatchResults.SortByCreateTime(item.IsSortD);
                                break;
                            case "批次号":
                                item.IsSortD = !item.IsSortD;
                                ViewBatchResults.SortByBatch(item.IsSortD);
                                break;
                            default:
                                break;
                        }
                        break;
                    }
                }
            }

        }


    }
}
