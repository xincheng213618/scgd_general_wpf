using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Algorithm.Dao;
using ColorVision.Services.Devices.Algorithm.Views;
using ColorVision.Services.Devices.Camera.Views;
using ColorVision.UI.Sorts;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Services.ShowPage.Dao
{
    /// <summary>
    /// BatchShowPage.xaml 的交互逻辑
    /// </summary>
    public partial class BatchShowPage : Page
    {
        public Frame Frame { get; set; }
        public ViewBatchResult ViewBatchResult { get; set; }

        public BatchShowPage(Frame frame, ViewBatchResult viewBatchResult)
        {
            Frame = frame;
            ViewBatchResult = viewBatchResult;
            InitializeComponent();
        }

        public ObservableCollection<ViewResultCamera> ViewResultCameras { get; set; } = new ObservableCollection<ViewResultCamera>();
        public ObservableCollection<AlgorithmResult> AlgorithmResults { get; set; } = new ObservableCollection<AlgorithmResult>();

        MeasureImgResultDao MeasureImgResultDao = new();

        private void Page_Initialized(object sender, EventArgs e)
        {
            TextBatch.Text = "批次 " + ViewBatchResult.BatchCode +" 结果";

            listView1.ItemsSource = ViewResultCameras;


            listView2.ItemsSource = AlgorithmResults;

        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ViewResultCameras.Clear();
            foreach (var item in MeasureImgResultDao.GetAllByBatchid(ViewBatchResult.Id))
            {
                ViewResultCameras.AddUnique(new ViewResultCamera(item));
            }
            if (ViewResultCameras.Count == 0) StactPanelImage.Visibility = Visibility.Collapsed;

            AlgorithmResults.Clear();
            foreach (var item in AlgResultMasterDao.Instance.GetAllByBatchid(ViewBatchResult.Id))
            {
                AlgorithmResults.AddUnique(new AlgorithmResult(item));
            }
            if (AlgorithmResults.Count == 0) StactPanelAlg.Visibility = Visibility.Collapsed;

        }

        private void listView1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                ViewResultCameras[listView.SelectedIndex].Open();
            }
        }

        private void MenuItem_Delete_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {

        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {

        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {

        }

        AlgorithmView? AlgorithmView;
        private void listView2_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                if (AlgorithmView == null)
                {
                    AlgorithmView = new AlgorithmView();
                    Window window = new Window() { Content = AlgorithmView };
                    window.Closed += (s, args) => AlgorithmView = null; // 订阅窗口关闭事件
                    window.Show();
                }

                AlgorithmView.AlgResults.Add(AlgorithmResults[listView.SelectedIndex]);
                AlgorithmView.RefreshResultListView();
            }
        }
    }
}
