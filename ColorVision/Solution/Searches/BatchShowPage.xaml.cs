using ColorVision.UI.Sorts;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Algorithm.Dao;
using ColorVision.Services.Devices.Algorithm.Views;
using ColorVision.Services.Devices.Camera.Views;
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
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ColorVision.Solution.Searches
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


    }
}
