using ColorVision.Database;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.UI.Sorts;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine
{
    /// <summary>
    /// MeasureBatchPage.xaml 的交互逻辑
    /// </summary>
    public partial class MeasureBatchPage : Page
    {
        public Frame Frame { get; set; }
        public MeasureBatchModel MeasureBatchModel { get; set; }

        public MeasureBatchPage(Frame frame, MeasureBatchModel measureBatchModel)
        {
            Frame = frame;
            MeasureBatchModel = measureBatchModel;
            InitializeComponent();
        }

        public ObservableCollection<ViewResultImage> ViewResultImages { get; set; } = new ObservableCollection<ViewResultImage>();
        public ObservableCollection<ViewResultAlg> ViewResultAlgs { get; set; } = new ObservableCollection<ViewResultAlg>();


        private void Page_Initialized(object sender, EventArgs e)
        {
            Title = "批次 " + MeasureBatchModel.Code +" 结果";

            listView1.ItemsSource = ViewResultImages;
            listView2.ItemsSource = ViewResultAlgs;

        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ViewResultImages.Clear();
            foreach (var item in MeasureImgResultDao.Instance.GetAllByBatchId(MeasureBatchModel.Id))
            {
                ViewResultImages.AddUnique(new ViewResultImage(item));
            }

            ViewResultAlgs.Clear();
            foreach (var item in AlgResultMasterDao.Instance.GetAllByBatchId(MeasureBatchModel.Id))
            {
                ViewResultAlgs.AddUnique(new ViewResultAlg(item));
            }

        }

        private void listView1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                ViewResultImages[listView.SelectedIndex].Open();
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
                    Window window = new Window() { Content = AlgorithmView ,Owner =Application.Current.GetActiveWindow() };
                    window.Closed += (s, args) => AlgorithmView = null; // 订阅窗口关闭事件
                    window.Show();
                }

                AlgorithmView.ViewResults.Clear();
                AlgorithmView.ViewResults.Add(ViewResultAlgs[listView.SelectedIndex]);
                AlgorithmView.RefreshResultListView();
            }
        }
    }
}
