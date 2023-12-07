using ColorVision.Services.Algorithm.MySql;
using ColorVision.Services.Algorithm.Result;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Services.Algorithm
{
    /// <summary>
    /// WindowSolution.xaml 的交互逻辑
    /// </summary>
    public partial class WindowSolution : Window
    {
        public WindowSolution()
        {
            InitializeComponent();
        }

        public ObservableCollection<PoiResult> PoiResults { get; set; } = new ObservableCollection<PoiResult>();

        public ObservableCollection<FOVResult> FOVResults { get; set; } = new ObservableCollection<FOVResult>();
        public ObservableCollection<MTFResult> MTFResults { get; set; } = new ObservableCollection<MTFResult>();

        public ObservableCollection<SFRResult> SFRResults { get; set; } = new ObservableCollection<SFRResult>();


        public ObservableCollection<GhostResult> GhostResults { get; set; } = new ObservableCollection<GhostResult>();

        public ObservableCollection<DistortionResult> DistortionResults { get; set; } = new ObservableCollection<DistortionResult>();



        private void Window_Initialized(object sender, EventArgs e)
        {

        }


        public void FOV()
        {
            AlgorithmFOVResult algorithmFovResult = new AlgorithmFOVResult();

            var algorithmFovResults = algorithmFovResult.GetAll();
            FOVResults.Clear();
            foreach (var item in algorithmFovResults)
            {
                FOVResults.Add(new FOVResult(item));
            }
            algorithmFovResults.Clear();


            GridView gridView = new GridView();
            List<string> headers = new List<string> { "序号", "批次号", "img_id", "fovDegrees", "coordinates1", "coordinates2", "coordinates3", "coordinates4", "执行结果" };
            List<string> bdheaders = new List<string> { "Model.Id", "Batch.Code", "Model.ImgId", "Model.FovDegrees", "Model.Coordinates1", "Model.Coordinates2", "Model.Coordinates3", "Model.Coordinates4", "Model.Result" };
            for (int i = 0; i < headers.Count; i++)
            {
                gridView.Columns.Add(new GridViewColumn() { Header = headers[i], Width = 100, DisplayMemberBinding = new Binding(bdheaders[i]) });
            }
            ListView1.View = gridView;
            ListView1.ItemsSource = FOVResults;
        }

        public void MTF()
        {
            AlgorithmMTFResult AlgorithmMTFResult = new AlgorithmMTFResult();
           var results = AlgorithmMTFResult.GetAll();
            MTFResults.Clear();
            foreach (var item in results)
            {
                MTFResults.Add(new MTFResult(item));
            }
            results.Clear();



            GridView gridView = new GridView();
            List<string> headers = new List<string> { "序号", "批次号", "img_id", "MTF", "执行结果" };
            List<string> bdheaders = new List<string> { "Model.Id", "Batch.Code", "Model.ImgId","Model.Value", "Model.Result" };
            for (int i = 0; i < headers.Count; i++)
            {
                gridView.Columns.Add(new GridViewColumn() { Header = headers[i], Width = 100, DisplayMemberBinding = new Binding(bdheaders[i]) });
            }
            ListView1.View = gridView;
            ListView1.ItemsSource = MTFResults;
        }

        public void SFR()
        {
            AlgorithmSFRResult AlgorithmMTFResult = new AlgorithmSFRResult();
            var results = AlgorithmMTFResult.GetAll();
            SFRResults.Clear();
            foreach (var item in results)
            {
                SFRResults.Add(new SFRResult(item));
            }
            results.Clear();



            GridView gridView = new GridView();
            List<string> headers = new List<string> { "序号", "批次号", "img_id", "pdfrequency", "pdomainSamplingData", "执行结果" };
            List<string> bdheaders = new List<string> { "Model.Id", "Batch.Code", "Model.ImgId", "Model.Pdfrequency", "Model.PdomainSamplingData", "Model.Result" };
            for (int i = 0; i < headers.Count; i++)
            {
                gridView.Columns.Add(new GridViewColumn() { Header = headers[i], Width = 100, DisplayMemberBinding = new Binding(bdheaders[i]) });
            }
            ListView1.View = gridView;
            ListView1.ItemsSource = SFRResults;
        }


        public void Ghost()
        {
            AlgorithmGhostResult Result = new AlgorithmGhostResult();
            var results = Result.GetAll();
            GhostResults.Clear();
            foreach (var item in results)
            {
                GhostResults.Add(new GhostResult(item));
            }
            results.Clear();



            GridView gridView = new GridView();
            List<string> headers = new List<string> { "序号", "批次号", "img_id", "LedCenters_X", "LedCenters_Y", "blobGray", "ghostAverageGray", "singleLedPixelNum", "LED_pixel_X", "LED_pixel_Y", "singleGhostPixelNum", "Ghost_pixel_X", "Ghost_pixel_Y", "执行结果" };
            List<string> bdheaders = new List<string> { "Model.Id", "Batch.Code", "Model.ImgId", "Model.LedCentersX", "Model.LedCentersY", "Model.BlobGray", "Model.GhostAverageGray", "Model.SingleLedPixelNum", "Model.LEDPixelX", "Model.LEDPixelY", "Model.SingleGhostPixelNum", "Model.GhostPixelX", "Model.GhostPixelY", "Model.Result" };
            for (int i = 0; i < headers.Count; i++)
            {
                gridView.Columns.Add(new GridViewColumn() { Header = headers[i], Width = 100, DisplayMemberBinding = new Binding(bdheaders[i]) });
            }
            ListView1.View = gridView;
            ListView1.ItemsSource = GhostResults;
        }


        public void Distortion()
        {
            AlgorithmDistortionResult Result = new AlgorithmDistortionResult();
            var results = Result.GetAll();
            DistortionResults.Clear();
            foreach (var item in results)
            {
                DistortionResults.Add(new DistortionResult(item));
            }
            results.Clear();



            GridView gridView = new GridView();
            List<string> headers = new List<string> { "序号", "批次号", "img_id", "finalPointsX", "finalPointsY", "最大畸变点横坐标" , "最大畸变点纵坐标", "图像最大畸变率", "图像XOY方向的旋转角度", "执行结果" };
            List<string> bdheaders = new List<string> { "Model.Id", "Batch.Code", "Model.ImgId", "Model.FinalPointsX", "Model.FinalPointsY", "Model.PointX", "Model.PointY", "Model.MaxErrorRatio", "Model.T", "Model.Result" };
            for (int i = 0; i < headers.Count; i++)
            {
                gridView.Columns.Add(new GridViewColumn() { Header = headers[i], Width = 100, DisplayMemberBinding = new Binding(bdheaders[i]) });
            }
            ListView1.View = gridView;
            ListView1.ItemsSource = DistortionResults;
        }

        public void POI()
        {
            MessageBox.Show("开发中");
            ListView1.ItemsSource = PoiResults;
        }




        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                switch (button.Tag.ToString())
                {
                    case "Fov":
                        FOV();
                        break;
                    case "SFR":
                        SFR();
                        break;
                    case "MTF":
                        MTF();
                        break;
                    case "POI":
                        POI();
                        break;
                    case "Ghost":
                        Ghost();
                        break;
                    case "Distortion":
                        Distortion();
                        break;
                    default:
                        break;
                }
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var sortedItems = FOVResults.OrderByDescending(f => f.Model.Id).ToList();

            FOVResults.Clear();

            // Add the sorted items back into the original ObservableCollection
            foreach (var item in sortedItems)
            {
                FOVResults.Add(item);
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var sortedItems = FOVResults.OrderBy(f => f.Model.Id).ToList();

            FOVResults.Clear();

            // Add the sorted items back into the original ObservableCollection
            foreach (var item in sortedItems)
            {
                FOVResults.Add(item);
            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            var sortedItems = FOVResults.OrderByDescending(f => f.Batch?.Name).ToList();

            FOVResults.Clear();

            // Add the sorted items back into the original ObservableCollection
            foreach (var item in sortedItems)
            {
                FOVResults.Add(item);
            }
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            var sortedItems = FOVResults.OrderBy(f => f.Batch?.Name).ToList();

            FOVResults.Clear();

            // Add the sorted items back into the original ObservableCollection
            foreach (var item in sortedItems)
            {
                FOVResults.Add(item);
            }
        }
    }
}
