#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.Draw;
using ColorVision.MySql.Service;
using ColorVision.Net;
using ColorVision.Services.Devices.Algorithm.Dao;
using ColorVision.Sorts;
using ColorVision.Util;
using HandyControl.Tools.Extension;
using log4net;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Services.Devices.Algorithm.Views
{
    /// <summary>
    /// ViewSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class AlgorithmView : UserControl,IView
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(AlgorithmView));
        public View View { get; set; }
        public event CurSelectionChanged OnCurSelectionChanged;
        public DeviceAlgorithm Device { get; set; }
        public AlgorithmView(DeviceAlgorithm deviceAlgorithm)
        {
            Device = deviceAlgorithm;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            TextBox TextBox1 = new TextBox() { Width = 10, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.Transparent };
            Grid.SetColumn(TextBox1, 0);
            Grid.SetRow(TextBox1, 0);
            MainGrid.Children.Insert(0, TextBox1);
            this.MouseDown += (s, e) =>
            {
                TextBox1.Focus();
            };

            View = new View();


            listView1.ItemsSource = AlgResults;


            //色度
            List<string> cieBdHeader = new List<string> { "Name", "PixelPos", "PixelSize", "Shapes", "CCT", "Wave", "X", "Y", "Z", "u", "v", "x", "y" };
            List<string> cieHeader = new List<string> { "名称", "位置", "大小", "形状", "CCT", "Wave", "X", "Y", "Z", "u", "v", "x", "y" };

            GridView gridView2 = new GridView();
            for (int i = 0; i < cieHeader.Count; i++)
            {
                gridView2.Columns.Add(new GridViewColumn() { Header = cieHeader[i], DisplayMemberBinding = new Binding(cieBdHeader[i]) });
            }
            listView2.View = gridView2;
            listView2.ItemsSource = PoiResultDatas;
            //亮度
            List<string> bdheadersY = new List<string> { "Name", "PixelPos", "PixelSize", "Shapes", "Y" };
            List<string> headersY = new List<string> { "名称", "位置", "大小", "形状", "Y" };
            GridView gridViewY = new GridView();
            for (int i = 0; i < headersY.Count; i++)
            {
                gridViewY.Columns.Add(new GridViewColumn() { Header = headersY[i], DisplayMemberBinding = new Binding(bdheadersY[i]) });
            }
            listViewY.View = gridViewY;
            listViewY.ItemsSource = PoiYResultDatas;


            var keyValuePairs = Enum.GetValues(typeof(AlgorithmResultType))
                .Cast<AlgorithmResultType>()
                .Select(e1 => new KeyValuePair<AlgorithmResultType, string>(e1, e1.ToString()))
                .ToList();
            TextBoxType.ItemsSource = keyValuePairs;




        }

        public ObservableCollection<PoiResultData> PoiResultDatas { get; set; } = new ObservableCollection<PoiResultData>();
        public ObservableCollection<PoiResultData> PoiYResultDatas { get; set; } = new ObservableCollection<PoiResultData>();
        public ObservableCollection<AlgorithmResult> AlgResults { get; set; } = new ObservableCollection<AlgorithmResult>();

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0)
            {
                MessageBox.Show(Application.Current.MainWindow, "您需要先选择数据", "ColorVision");
                return;
            }

            using var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "CSV files (*.csv) | *.csv";
            dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                using StreamWriter file = new StreamWriter(dialog.FileName, true, Encoding.UTF8);
                if (listView1.View is GridView gridView1)
                {
                    string headers = "";
                    foreach (var item in gridView1.Columns)
                    {
                        headers += item.Header.ToString() + ",";
                    }
                    file.WriteLine(headers);
                }
                string value = "";
                foreach (var item in AlgResults)
                {
                    value += item.Id + "," 
                        +item.Batch + "," 
                        + item.POITemplateName  + "," 
                        + item.FilePath +","
                        + item.CreateTime + ","
                        + item.ResultTypeDis + ","
                        + item.TotalTime + ","
                        + item.Result + ","
                        + item.ResultDesc + ","
                        + Environment.NewLine;
                }
                file.WriteLine(value);
                ImageSource bitmapSource = ImageView.ImageShow.Source;
                ImageUtil.SaveImageSourceToFile(bitmapSource, Path.Combine( Path.GetDirectoryName(dialog.FileName),Path.GetFileNameWithoutExtension(dialog.FileName) + ".png"));
            }
        }

        public void AlgResultDataDraw(AlgResultMasterModel result)
        {
            AlgResultDataDraw(result.Id.ToString(), result.BatchCode, result.ImgFile, result.TName, result.ImgFileType, result.ResultCode, result.Result, result.TotalTime);
        }
        public void AlgResultDataDraw(string key, string serialNumber, string imgFileName, string templateName, AlgorithmResultType resultType, int? resultCode, string resultDesc, long totalTime)
        {
            AlgorithmResult result = new AlgorithmResult(AlgResults.Count + 1, serialNumber, imgFileName, templateName, DateTime.Now, resultType, resultCode, resultDesc, totalTime);
            AlgResults.Add(result);
            RefreshResultListView();
        }
        public void PoiDataDraw(AlgResultMasterModel result, List<POIResultCIEY> results)
        {
            PoiDataDraw(result.Id.ToString(), result.BatchCode, result.ImgFile, result.TName, results, result.ResultCode, result.Result, result.TotalTime);
        }

        public void PoiDataDraw(string key, string serialNumber, string imgFileName, string templateName, List<POIResultCIEY> results, int? resultCode, string resultDesc, long totalTime)
        {
            AlgorithmResult result = new AlgorithmResult(AlgResults.Count + 1, serialNumber, imgFileName, templateName, DateTime.Now, AlgorithmResultType.POI_Y, resultCode, resultDesc, totalTime);
            AlgResults.Add(result);
            foreach (var item in results)
            {
                PoiResultCIEYData resultData = new PoiResultCIEYData(item.Point, item.Data);
                result.PoiData.Add(resultData);
            }
            RefreshResultListView();
        }
        public void PoiDataDraw(AlgResultMasterModel result, List<POIResultCIExyuv> results)
        {
            PoiDataDraw(result.Id.ToString(), result.BatchCode, result.ImgFile, result.TName, results, result.ResultCode, result.Result, result.TotalTime);
        }
        public void PoiDataDraw(string key, string serialNumber, string imgFileName, string templateName, List<POIResultCIExyuv> results, int? resultCode, string resultDesc, long totalTime)
        {
            AlgorithmResult result = new AlgorithmResult(AlgResults.Count + 1, serialNumber, imgFileName, templateName, DateTime.Now, AlgorithmResultType.POI_XY_UV, resultCode, resultDesc, totalTime);
            AlgResults.Add(result);
            foreach (var item in results)
            {
                PoiResultCIExyuvData resultData = new PoiResultCIExyuvData(item.Point, item.Data);
                result.PoiData.Add(resultData);
            }
            RefreshResultListView();
        }


        AlgResultLedcheckDao algResultLedcheckDao = new AlgResultLedcheckDao();

        public void AlgResultMasterModelDataDraw(AlgResultMasterModel result)
        {
            AlgorithmResult algorithmResult = new AlgorithmResult(result);
            AlgResults.Add(algorithmResult);
            RefreshResultListView();
        }

        private void RefreshResultListView()
        {
            if (listView1.Items.Count > 0) listView1.SelectedIndex = listView1.Items.Count - 1;
            listView1.ScrollIntoView(listView1.SelectedItem);
        }
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

        }

        /// <summary>
        /// 专门位鬼影设计的类
        /// </summary>
        class Point1
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        ResultService resultService = new ResultService();

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listView1.SelectedIndex < 0)
                return;
            AlgorithmResult result = listView1.Items[listView1.SelectedIndex] as AlgorithmResult;
            if(result != null)
            {
                PoiResultDatas.Clear();
                PoiYResultDatas.Clear();
                ImageView.ResetPOIPoint();
                listViewSide.Visibility = Visibility.Collapsed;
                List<POIPoint> DrawPoiPoint = new List<POIPoint>();
                switch (result.ResultType)
                {   
                    case AlgorithmResultType.POI:
                        OnCurSelectionChanged?.Invoke(result);
                        break;
                    case AlgorithmResultType.POI_XY_UV:
                        OnCurSelectionChanged?.Invoke(result);
                        listViewY.Hide();
                        listView2.Show();

                        foreach (var item in result.PoiData)
                        {
                            PoiResultDatas.Add(item);
                            DrawPoiPoint.Add(item.Point);
                        }
                        ImageView.AddPOIPoint(DrawPoiPoint);
                        break;
                    case AlgorithmResultType.POI_Y:
                        OnCurSelectionChanged?.Invoke(result);
                        listView2.Hide();
                        listViewY.Show();
                        foreach (var item in result.PoiData)
                        {
                            PoiYResultDatas.Add(item);
                            DrawPoiPoint.Add(item.Point);
                        }
                        ImageView.AddPOIPoint(DrawPoiPoint);
                        break;
                    case AlgorithmResultType.FOV:
                        ImageView.OpenImage(result.FilePath);
                        listViewSide.Visibility = Visibility.Visible;

                        if (result.SFRData == null)
                        {
                            result.FOVData = new ObservableCollection<FOVResultData>();
                            List<AlgResultFOVModel> AlgResultFOVModels = resultService.GetFOVByPid(result.Id);
                            foreach (var item in AlgResultFOVModels)
                            {
                                FOVResultData fOVResultData = new FOVResultData(item);
                                result.FOVData.Add(fOVResultData);
                            };
                        }

                        List<string> bdheadersFOV = new List<string> { "Pattern", "Type", "Degrees" };
                        List<string> headersFOV = new List<string> { "Pattern", "Type", "Degrees" };
                        GridView gridViewFOV = new GridView();
                        for (int i = 0; i < headersFOV.Count; i++)
                        {
                            gridViewFOV.Columns.Add(new GridViewColumn() { Header = headersFOV[i], DisplayMemberBinding = new Binding(bdheadersFOV[i]) });
                        }
                        listViewSide.View = gridViewFOV;
                        listViewSide.Visibility =Visibility.Visible;
                        listViewSide.ItemsSource = result.FOVData;
                        break;
                    case AlgorithmResultType.SFR:
                        ImageView.OpenImage(result.FilePath);
                        listViewSide.Visibility = Visibility.Visible;

                        if (result.SFRData == null)
                        {
                            result.SFRData = new ObservableCollection<SFRResultData>();
                            List<AlgResultSFRModel> AlgResultSFRModels = resultService.GetSFRByPid(result.Id);
                            foreach (var item in AlgResultSFRModels)
                            {
                                var Pdfrequencys = JsonConvert.DeserializeObject<float[]>(item.Pdfrequency);
                                var PdomainSamplingDatas = JsonConvert.DeserializeObject<float[]>(item.PdomainSamplingData);
                                for (int i = 0; i < Pdfrequencys.Length; i++)
                                {
                                    SFRResultData resultData = new SFRResultData(Pdfrequencys[i], PdomainSamplingDatas[i]);
                                    result.SFRData.Add(resultData);
                                }
                            };
                        }

                        List<string> bdheadersSFR = new List<string> { "pdfrequency", "pdomainSamplingData" };
                        List<string> headersSFR = new List<string> { "pdfrequency", "pdomainSamplingData" };
                        GridView gridViewSFR = new GridView();
                        for (int i = 0; i < headersSFR.Count; i++)
                        {
                            gridViewSFR.Columns.Add(new GridViewColumn() { Header = headersSFR[i], DisplayMemberBinding = new Binding(bdheadersSFR[i]) });
                        }
                        listViewSide.View = gridViewSFR;
                        listViewSide.ItemsSource = result.SFRData;
                        if (result.SFRData.Count > 0)
                        {
                            ImageView.AddRect(new Rect(10,10,10,10));
                        }

                        break;
                    case AlgorithmResultType.MTF:
                        ImageView.OpenImage(result.FilePath);
                        listViewSide.Visibility = Visibility.Visible;
                        if (result.MTFData == null)
                        {
                            result.MTFData = new ObservableCollection<MTFResultData>();
                            List<AlgResultMTFModel> AlgResultMTFModels = resultService.GetMTFByPid(result.Id);
                            foreach (var item in AlgResultMTFModels)
                            {
                                MTFResultData mTFResultData = new MTFResultData(item);
                                result.MTFData.Add(mTFResultData);
                            }
                        }


                        List<string> bdheadersMTF = new List<string> { "PixelPos", "PixelSize", "Shapes", "Articulation" };
                        List<string> headersMTF = new List<string> { "位置", "大小", "形状", "MTF" };
                        GridView gridViewMTF = new GridView();
                        for (int i = 0; i < headersMTF.Count; i++)
                        {
                            gridViewMTF.Columns.Add(new GridViewColumn() { Header = headersMTF[i], DisplayMemberBinding = new Binding(bdheadersMTF[i]) });
                        }
                        listViewSide.View = gridViewMTF;
                        listViewSide.ItemsSource = result.MTFData;
                        foreach (var item in result.MTFData)
                        {
                            DrawPoiPoint.Add(item.Point);
                        }
                        ImageView.AddPOIPoint(DrawPoiPoint);

                        break;
                    case AlgorithmResultType.Ghost:
                        if (result.GhostData == null)
                        {
                            result.GhostData = new ObservableCollection<GhostResultData>();
                            List<AlgResultGhostModel> AlgResultGhostModels = resultService.GetGhostByPid(result.Id);
                            foreach (var item in AlgResultGhostModels)
                            {
                                GhostResultData ghostResultData = new GhostResultData(item);
                                result.GhostData.Add(ghostResultData);
                            }
                        }
                        try
                        {
                            string GhostPixels = result.GhostData[0].GhostPixels;
                            List<List<Point1>> GhostPixel = JsonConvert.DeserializeObject<List<List<Point1>>>(GhostPixels);
                            int[] Ghost_pixel_X;
                            int[] Ghost_pixel_Y;
                            List<Point1> Points = new List<Point1>();
                            foreach (var item in GhostPixel)
                                foreach (var item1 in item)
                                    Points.Add(item1);

                            if (Points != null)
                            {
                                Ghost_pixel_X = new int[Points.Count];
                                Ghost_pixel_Y = new int[Points.Count];
                                for (int i = 0; i < Points.Count; i++)
                                {
                                    Ghost_pixel_X[i] = (int)Points[i].X;
                                    Ghost_pixel_Y[i] = (int)Points[i].Y;
                                }
                            }
                            else
                            {
                                Ghost_pixel_X = new int[1] { 1 };
                                Ghost_pixel_Y = new int[1] { 1 };
                            }

                            string LedPixels = result.GhostData[0].LedPixels;
                            List<List<Point1>> LedPixel = JsonConvert.DeserializeObject<List<List<Point1>>>(LedPixels);
                            int[] LED_pixel_X;
                            int[] LED_pixel_Y;

                            Points.Clear();
                            foreach (var item in LedPixel)
                                foreach (var item1 in item)
                                    Points.Add(item1);

                            if (Points != null)
                            {
                                LED_pixel_X = new int[Points.Count];
                                LED_pixel_Y = new int[Points.Count];
                                for (int i = 0; i < Points.Count; i++)
                                {
                                    LED_pixel_X[i] = (int)Points[i].X;
                                    LED_pixel_Y[i] = (int)Points[i].Y;
                                }
                            }
                            else
                            {
                                LED_pixel_X = new int[1] { 1 };
                                LED_pixel_Y = new int[1] { 1 };
                            }
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ImageView.OpenGhostImage(result.FilePath, LED_pixel_X, LED_pixel_Y, Ghost_pixel_X, Ghost_pixel_Y);
                            });


                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }


                        listViewSide.Visibility = Visibility.Visible;
                        List<string> bdheadersGhost = new List<string> { "LedCenters", "LedBlobGray", "GhostAvrGray" };
                        List<string> headersGhost = new List<string> { "质心坐标", "光斑灰度", "鬼影灰度" };
                        GridView gridViewGhost = new GridView();
                        for (int i = 0; i < headersGhost.Count; i++)
                        {
                            gridViewGhost.Columns.Add(new GridViewColumn() { Header = headersGhost[i], DisplayMemberBinding = new Binding(bdheadersGhost[i]) });
                        }
                        listViewSide.View = gridViewGhost;
                        listViewSide.ItemsSource = result.GhostData;
                        break;
                    case AlgorithmResultType.Distortion:
                        ImageView.OpenImage(result.FilePath);
                        listViewSide.Visibility = Visibility.Visible;

                        if (result.DistortionData == null)
                        {
                            result.DistortionData = new ObservableCollection<DistortionResultData>();
                            var Distortions = resultService.GetDistortionByPid(result.Id);
                            foreach (var item in Distortions)
                            {
                                DistortionResultData distortionResultData = new DistortionResultData(item);
                                result.DistortionData.Add(distortionResultData);
                            }
                        }
                        List<string> bdheadersDis = new List<string> { "DisTypeDesc", "SlopeTypeDesc", "LayoutTypeDesc", "CornerTypeDesc", "MaxRatio" };
                        List<string> headersDis = new List<string> { "类型", "斜率", "布点", "角点", "畸变率" };
                        GridView gridViewDis = new GridView();
                        for (int i = 0; i < headersDis.Count; i++)
                        {
                            gridViewDis.Columns.Add(new GridViewColumn() { Header = headersDis[i], DisplayMemberBinding = new Binding(bdheadersDis[i]) });
                        }
                        listViewSide.View = gridViewDis;
                        listViewSide.ItemsSource = result.DistortionData;
                        if (result.DistortionData.Count > 0)
                        {
                            List<Point> points = new List<Point>();
                            foreach (var item in result.DistortionData[0].FinalPoints)
                            {
                                points.Add(new Point(item.X, item.Y));
                            }
                            ImageView.AddPoint(points);
                        }
                       
                        break;
                    case AlgorithmResultType.Calibration:
                        break;
                    case AlgorithmResultType.LedCheck:
                        ImageView.OpenImage(result.FilePath);
                        listViewSide.Visibility = Visibility.Visible;
                        if (result.LedResultDatas == null)
                        {
                            result.LedResultDatas = new ObservableCollection<LedResultData>();
                            List<AlgResultLedcheckModel> AlgResultLedcheckModels = algResultLedcheckDao.GetAllByPid(result.Id);
                            foreach (var item in AlgResultLedcheckModels)
                            {
                                LedResultData ledResultData = new LedResultData(new Point((double)item.PosX, (double)item.PosY), (double)item.Radius);
                                result.LedResultDatas.Add(ledResultData);
                            };
                        }

                        bdheadersDis = new List<string> { "Point", "Radius" };
                        headersDis = new List<string> { "坐标", "半径"};
                        gridViewDis = new GridView();
                        for (int i = 0; i < headersDis.Count; i++)
                        {
                            gridViewDis.Columns.Add(new GridViewColumn() { Header = headersDis[i], DisplayMemberBinding = new Binding(bdheadersDis[i]) });
                        }
                        listViewSide.View = gridViewDis;
                        listViewSide.ItemsSource = result.LedResultDatas;
                        if (result.LedResultDatas.Count > 0)
                        {
                            List<Point> points = new List<Point>();
                            foreach (var item in result.LedResultDatas)
                            {
                                DrawingVisualCircle Circle = new DrawingVisualCircle();
                                Circle.Attribute.Center = item.Point;
                                Circle.Attribute.Radius = item.Radius;
                                Circle.Attribute.Brush = Brushes.Transparent;
                                Circle.Attribute.Pen = new Pen(Brushes.Red, 2);
                                Circle.Render();
                                ImageView.ImageShow.AddVisual(Circle);
                            }
                        }
                        break;

                    default:
                        break;
                }

            }
        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && listView1.SelectedIndex > -1)
            {
                int temp = listView1.SelectedIndex;
                AlgResults.RemoveAt(temp);
            }
        }

        private void listView2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void GridSplitter_DragCompleted1(object sender, DragCompletedEventArgs e)
        {
            listView2.Width = ListCol2.ActualWidth;
            ListCol1.Width = new GridLength(1, GridUnitType.Star);
            ListCol2.Width = GridLength.Auto;
        }
        private void listViewY_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        internal void OpenImage(CVCIEFileInfo fileInfo)
        {
            ImageView.OpenImage(fileInfo);
        }

        private void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            AlgResults.Clear();
        }

        private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            listView1.Height = ListRow2.ActualHeight - 38;
            ListRow2.Height = GridLength.Auto;
            ListRow1.Height = new GridLength(1, GridUnitType.Star);
        }
        AlgResultMasterDao algResultMasterDao = new AlgResultMasterDao();
        private void Search_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Search1_Click(object sender, RoutedEventArgs e)
        {
            SerchPopup.IsOpen = true;
            TextBoxType.SelectedIndex = -1;
            TextBoxId.Text = string.Empty;
            TextBoxBatch.Text = string.Empty;
            TextBoxFile.Text = string.Empty;
        }

        private void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextBoxId.Text)&& string.IsNullOrEmpty(TextBoxBatch.Text) && string.IsNullOrEmpty(TextBoxType.Text) && string.IsNullOrEmpty(TextBoxFile.Text))
            {
                AlgResults.Clear();
                List<AlgResultMasterModel> algResults = algResultMasterDao.GetAll();
                foreach (var item in algResults)
                {
                    AlgorithmResult algorithmResult = new AlgorithmResult(item);
                    AlgResults.Add(algorithmResult);
                }
                return;
            }
            else
            {
                string altype = string.Empty;
                if (TextBoxType.SelectedValue is AlgorithmResultType algorithmResultType)
                    altype = ((int)algorithmResultType).ToString();

                AlgResults.Clear();
                List<AlgResultMasterModel> algResults = algResultMasterDao.ConditionalQuery(TextBoxId.Text, TextBoxBatch.Text, altype.ToString(), TextBoxFile.Text);
                foreach (var item in algResults)
                {
                    AlgorithmResult algorithmResult = new AlgorithmResult(item);
                    AlgResults.Add(algorithmResult);
                }
            }
        }

        private void Order_Click(object sender, RoutedEventArgs e)
        {
            OrderPopup.IsOpen = true;
        }

        private void Radio_Checked(object sender, RoutedEventArgs e)
        {
            if (RadioID?.IsChecked == true)
            {
                AlgResults.SortByID(RadioUp?.IsChecked == false);
            }

            if (RadioBatch?.IsChecked == true)
            {
                AlgResults.SortByBatch(RadioUp?.IsChecked == false);
            }

            if (RadioFilePath?.IsChecked == true)
            {
                AlgResults.SortByFilePath(RadioUp?.IsChecked == false);
            }

            if (RadioCreateTime?.IsChecked == true)
            {
                AlgResults.SortByCreateTime(RadioUp?.IsChecked == false);
            }

            OrderPopup.IsOpen = false;
        }
    }
}
