using ColorVision.Common.Sorts;
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

namespace ColorVision.Solution.Views
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

        MeasureImgResultDao MeasureImgResultDao = new MeasureImgResultDao();
        AlgResultMasterDao algResultMasterDao = new AlgResultMasterDao();

        private void Page_Initialized(object sender, EventArgs e)
        {
            foreach (var item in MeasureImgResultDao.GetAllByBatchid(ViewBatchResult.Id))
            {
                ViewResultCameras.AddUnique(new ViewResultCamera(item));
            }

            listView1.ItemsSource = ViewResultCameras;


            foreach (var item in algResultMasterDao.GetAllByBatchid(ViewBatchResult.Id))
            {
                AlgorithmResults.AddUnique(new AlgorithmResult(item));
            }
            listView2.ItemsSource = AlgorithmResults;

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
