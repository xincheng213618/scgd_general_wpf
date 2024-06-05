#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.Draw;
using ColorVision.Net;
using ColorVision.Engine.Services.Devices.Algorithm.Dao;
using ColorVision.Common.Utilities;
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
using ColorVision.UI.Sorts;
using CVCommCore.CVAlgorithm;
using ColorVision.UI.Views;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{
    /// <summary>
    /// ViewSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class AlgorithmView : UserControl,IView
    {
        private static readonly ILog logg = LogManager.GetLogger(typeof(AlgorithmView));
        public View View { get; set; }

        public event CurSelectionChanged OnCurSelectionChanged;
        public AlgorithmView()
        {
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            TextBox TextBox1 = new() { Width = 10, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.Transparent };
            Grid.SetColumn(TextBox1, 0);
            Grid.SetRow(TextBox1, 0);
            MainGrid.Children.Insert(0, TextBox1);
            MouseDown += (s, e) =>
            {
                TextBox1.Focus();
            };

            View = new View();

            listView1.ItemsSource = AlgResults;

            var keyValuePairs =
            TextBoxType.ItemsSource = Enum.GetValues(typeof(AlgorithmResultType))
                .Cast<AlgorithmResultType>()
                .Select(e1 => new KeyValuePair<AlgorithmResultType, string>(e1, e1.ToString()))
                .ToList();

            if (listView1.View is GridView gridView)
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
        }
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
            }
        }

        public ObservableCollection<AlgorithmResult> AlgResults { get; set; } = new ObservableCollection<AlgorithmResult>();

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0)
            {
                return;
            }
            if (listView1.SelectedIndex < 0 ||listView1.Items[listView1.SelectedIndex] is not AlgorithmResult result)
            {
                MessageBox.Show(Application.Current.MainWindow, "您需要先选择数据", "ColorVision");
                return;
            }
            else
            {
                using var dialog = new System.Windows.Forms.SaveFileDialog();
                dialog.Filter = "CSV files (*.csv) | *.csv";
                dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                dialog.RestoreDirectory = true;
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                switch (result.ResultType)
                {   
                    case AlgorithmResultType.POI_XY_UV:
                        PoiResultCIExyuvData.SaveCsv(result.PoiResultCIExyuvDatas, dialog.FileName);
                        ImageUtil.SaveImageSourceToFile(ImageView.ImageShow.Source, Path.Combine(Path.GetDirectoryName(dialog.FileName), Path.GetFileNameWithoutExtension(dialog.FileName) + ".png"));
                        return;
                    default:
                        break;

                }
                using StreamWriter file = new(dialog.FileName, true, Encoding.UTF8); 
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
                        + item.Batch + ","
                        + item.POITemplateName + ","
                        + item.FilePath + ","
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

        public void AlgResultMasterModelDataDraw(AlgResultMasterModel result)
        {
            if (result != null)
            {
                AlgorithmResult algorithmResult = new(result);
                AlgResults.AddUnique(algorithmResult);
                RefreshResultListView();
            }
        }

        public void RefreshResultListView()
        {
            if (listView1.Items.Count > 0) listView1.SelectedIndex = listView1.Items.Count - 1;
            listView1.ScrollIntoView(listView1.SelectedItem);
        }

        /// <summary>
        /// 专门位鬼影设计的类
        /// </summary>
        sealed class Point1
        {
            public int X { get; set; }
            public int Y { get; set; }
        }


        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listView1.SelectedIndex < 0) return;
            if (listView1.Items[listView1.SelectedIndex] is AlgorithmResult result)
            {
                ImageView.ImageShow.Clear();
                ImageView.FilePath = result.FilePath;
                List<POIPoint> DrawPoiPoint = new();
                List<string> cieBdHeader = new();
                List<string> cieHeader = new();
                switch (result.ResultType)
                {
                    case AlgorithmResultType.POI:
                        OnCurSelectionChanged?.Invoke(result);

                        if (result.PoiResultDatas == null)
                        {
                            result.PoiResultDatas = new ObservableCollection<PoiResultData>();
                            List<POIPointResultModel> POIPointResultModels = POIPointResultDao.Instance.GetAllByPid(result.Id);
                            foreach (var item in POIPointResultModels)
                            {
                                PoiResultData poiResult = new(item);
                                result.PoiResultDatas.Add(poiResult);
                            };
                        }

                        cieBdHeader = new List<string> { "Name", "PixelPos", "PixelSize", "Shapes" , "POIPointResultModel.ValidateResult" };
                        cieHeader = new List<string> { "名称", "位置", "大小", "形状" ,"Validate" };

                        if (listViewSide.View is GridView gridViewPOI)
                        {
                            LeftGridViewColumnVisibilitys.Clear();
                            gridViewPOI.Columns.Clear();
                            for (int i = 0; i < cieHeader.Count; i++)
                                gridViewPOI.Columns.Add(new GridViewColumn() { Header = cieHeader[i], DisplayMemberBinding = new Binding(cieBdHeader[i]) });
                        }

                        listViewSide.ItemsSource = result.PoiResultDatas;

                        foreach (var item in result.PoiResultDatas)
                            DrawPoiPoint.Add(item.Point);
                        ImageView.AddPOIPoint(DrawPoiPoint);
                        listViewSide.Visibility = Visibility.Visible;

                        break;
                    case AlgorithmResultType.POI_XY_UV:
                        OnCurSelectionChanged?.Invoke(result);
                        if (result.PoiResultCIExyuvDatas == null)
                        {
                            result.PoiResultCIExyuvDatas = new ObservableCollection<PoiResultCIExyuvData>();
                            List<POIPointResultModel> POIPointResultModels = POIPointResultDao.Instance.GetAllByPid(result.Id);
                            foreach (var item in POIPointResultModels)
                            {
                                PoiResultCIExyuvData poiResultCIExyuvData = new(item);
                                result.PoiResultCIExyuvDatas.Add(poiResultCIExyuvData);
                            };
                        }

                        cieBdHeader = new List<string> { "POIPoint.Id", "Name", "PixelPos", "PixelSize", "Shapes", "CCT", "Wave", "X", "Y", "Z", "u", "v", "x", "y", "POIPointResultModel.ValidateResult" };
                        cieHeader = new List<string> { "Id", ColorVision.Engine.Properties.Resources.Name, ColorVision.Engine.Properties.Resources.Position, ColorVision.Engine.Properties.Resources.Size, ColorVision.Engine.Properties.Resources.Shape, "CCT", "Wave", "X", "Y", "Z", "u", "v", "x", "y", "Validate" };

                        if (listViewSide.View is GridView gridViewPOI_XY_UV)
                        {
                            LeftGridViewColumnVisibilitys.Clear();
                            gridViewPOI_XY_UV.Columns.Clear();
                            for (int i = 0; i < cieHeader.Count; i++)
                                gridViewPOI_XY_UV.Columns.Add(new GridViewColumn() { Header = cieHeader[i], DisplayMemberBinding = new Binding(cieBdHeader[i]) });
                        }

                        listViewSide.ItemsSource = result.PoiResultCIExyuvDatas;

                        foreach (var item in result.PoiResultCIExyuvDatas)
                            DrawPoiPoint.Add(item.Point);
                        ImageView.AddPOIPoint(DrawPoiPoint);
                        listViewSide.Visibility = Visibility.Visible;
                        break;
                    case AlgorithmResultType.POI_Y:
                        OnCurSelectionChanged?.Invoke(result);

                        if (result.PoiResultCIEYDatas == null)
                        {
                            result.PoiResultCIEYDatas = new ObservableCollection<PoiResultCIEYData>();
                            List<POIPointResultModel> POIPointResultModels = POIPointResultDao.Instance.GetAllByPid(result.Id);
                            foreach (var item in POIPointResultModels)
                            {
                                PoiResultCIEYData poiResultCIExyuvData = new(item);
                                result.PoiResultCIEYDatas.Add(poiResultCIExyuvData);
                            };
                        }

                        //亮度
                        List<string> bdheadersY = new() { "Name", "PixelPos", "PixelSize", "Shapes", "Y" , "POIPointResultModel.ValidateResult" };
                        List<string> headersY = new() { "名称", "位置", "大小", "形状", "Y", "Validate" };

                        if (listViewSide.View is GridView gridViewY)
                        {
                            LeftGridViewColumnVisibilitys.Clear();
                            gridViewY.Columns.Clear();
                            for (int i = 0; i < headersY.Count; i++)
                                gridViewY.Columns.Add(new GridViewColumn() { Header = headersY[i], DisplayMemberBinding = new Binding(bdheadersY[i]) });
                        }

                        listViewSide.ItemsSource = result.PoiResultCIEYDatas;

                        DrawPoiPoint = new List<POIPoint>();
                        foreach (var item in result.PoiResultCIEYDatas)
                            DrawPoiPoint.Add(item.Point);
                        ImageView.AddPOIPoint(DrawPoiPoint);
                        listViewSide.Visibility = Visibility.Visible;
                        break;
                    case AlgorithmResultType.FOV:
                        ImageView.OpenImage(result.FilePath);
                        listViewSide.Visibility = Visibility.Visible;

                        if (result.SFRData == null)
                        {
                            result.FOVData = new ObservableCollection<FOVResultData>();
                            List<AlgResultFOVModel> AlgResultFOVModels = AlgResultFOVDao.Instance.GetAllByPid(result.Id);
                            foreach (var item in AlgResultFOVModels)
                            {
                                FOVResultData fOVResultData = new(item);
                                result.FOVData.Add(fOVResultData);
                            };
                        }

                        List<string> bdheadersFOV = new() { "Pattern", "Type", "Degrees" };
                        List<string> headersFOV = new() { "Pattern", "Type", "Degrees" };

                        if (listViewSide.View is GridView gridViewFOV)
                        {
                            LeftGridViewColumnVisibilitys.Clear();
                            gridViewFOV.Columns.Clear();
                            for (int i = 0; i < headersFOV.Count; i++)
                                gridViewFOV.Columns.Add(new GridViewColumn() { Header = headersFOV[i], DisplayMemberBinding = new Binding(bdheadersFOV[i]) });
                        }

                        listViewSide.Visibility = Visibility.Visible;
                        listViewSide.ItemsSource = result.FOVData;
                        break;
                    case AlgorithmResultType.SFR:
                        ImageView.OpenImage(result.FilePath);
                        listViewSide.Visibility = Visibility.Visible;

                        if (result.SFRData == null)
                        {
                            result.SFRData = new ObservableCollection<SFRResultData>();
                            List<AlgResultSFRModel> AlgResultSFRModels = AlgResultSFRDao.Instance.GetAllByPid(result.Id);
                            foreach (var item in AlgResultSFRModels)
                            {
                                var Pdfrequencys = JsonConvert.DeserializeObject<float[]>(item.Pdfrequency);
                                var PdomainSamplingDatas = JsonConvert.DeserializeObject<float[]>(item.PdomainSamplingData);
                                for (int i = 0; i < Pdfrequencys.Length; i++)
                                {
                                    SFRResultData resultData = new(Pdfrequencys[i], PdomainSamplingDatas[i]);
                                    result.SFRData.Add(resultData);
                                }
                            };
                        }

                        List<string> bdheadersSFR = new() { "pdfrequency", "pdomainSamplingData" };
                        List<string> headersSFR = new() { "pdfrequency", "pdomainSamplingData" };

                        if (listViewSide.View is GridView gridViewSFR)
                        {
                            LeftGridViewColumnVisibilitys.Clear();
                            gridViewSFR.Columns.Clear();
                            for (int i = 0; i < headersSFR.Count; i++)
                                gridViewSFR.Columns.Add(new GridViewColumn() { Header = headersSFR[i], DisplayMemberBinding = new Binding(bdheadersSFR[i]) });
                        }

                        listViewSide.ItemsSource = result.SFRData;
                        if (result.SFRData.Count > 0)
                        {
                            ImageView.AddRect(new Rect(10, 10, 10, 10));
                        }

                        break;
                    case AlgorithmResultType.MTF:
                        ImageView.OpenImage(result.FilePath);
                        listViewSide.Visibility = Visibility.Visible;
                        if (result.MTFData == null)
                        {
                            result.MTFData = new ObservableCollection<MTFResultData>();
                            List<AlgResultMTFModel> AlgResultMTFModels = AlgResultMTFDao.Instance.GetAllByPid(result.Id);
                            foreach (var item in AlgResultMTFModels)
                            {
                                MTFResultData mTFResultData = new(item);
                                result.MTFData.Add(mTFResultData);
                            }
                        }

                        List<string> bdheadersMTF = new() { "PixelPos", "PixelSize", "Shapes", "Articulation" , "AlgResultMTFModel.ValidateResult" };
                        List<string> headersMTF = new() { "位置", "大小", "形状", "MTF","Value" };

                        if (listViewSide.View is GridView gridViewMTF)
                        {
                            LeftGridViewColumnVisibilitys.Clear();
                            gridViewMTF.Columns.Clear();
                            for (int i = 0; i < headersMTF.Count; i++)
                                gridViewMTF.Columns.Add(new GridViewColumn() { Header = headersMTF[i], DisplayMemberBinding = new Binding(bdheadersMTF[i]) });
                        }

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
                            List<AlgResultGhostModel> AlgResultGhostModels = AlgResultGhostDao.Instance.GetAllByPid(result.Id);
                            foreach (var item in AlgResultGhostModels)
                            {
                                GhostResultData ghostResultData = new(item);
                                result.GhostData.Add(ghostResultData);
                            }
                        }
                        try
                        {
                            string GhostPixels = result.GhostData[0].GhostPixels;
                            List<List<Point1>> GhostPixel = JsonConvert.DeserializeObject<List<List<Point1>>>(GhostPixels);
                            int[] Ghost_pixel_X;
                            int[] Ghost_pixel_Y;
                            List<Point1> Points = new();
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
                        List<string> bdheadersGhost = new() { "LedCenters", "LedBlobGray", "GhostAvrGray" };
                        List<string> headersGhost = new() { "质心坐标", "光斑灰度", "鬼影灰度" };

                        if (listViewSide.View is GridView gridViewGhost)
                        {
                            LeftGridViewColumnVisibilitys.Clear();
                            gridViewGhost.Columns.Clear();
                            for (int i = 0; i < headersGhost.Count; i++)
                                gridViewGhost.Columns.Add(new GridViewColumn() { Header = headersGhost[i], DisplayMemberBinding = new Binding(bdheadersGhost[i]) });
                        }

                        listViewSide.ItemsSource = result.GhostData;
                        break;
                    case AlgorithmResultType.Distortion:
                        ImageView.OpenImage(result.FilePath);
                        listViewSide.Visibility = Visibility.Visible;

                        if (result.DistortionData == null)
                        {
                            result.DistortionData = new ObservableCollection<DistortionResultData>();
                            var Distortions = AlgResultDistortionDao.Instance.GetAllByPid(result.Id);
                            foreach (var item in Distortions)
                            {
                                DistortionResultData distortionResultData = new(item);
                                result.DistortionData.Add(distortionResultData);
                            }
                        }
                        List<string> bdheadersDis = new() { "DisTypeDesc", "SlopeTypeDesc", "LayoutTypeDesc", "CornerTypeDesc", "MaxRatio" };
                        List<string> headersDis = new() { "类型", "斜率", "布点", "角点", "畸变率" };

                        if (listViewSide.View is GridView gridViewDis)
                        {
                            LeftGridViewColumnVisibilitys.Clear();
                            gridViewDis.Columns.Clear();
                            for (int i = 0; i < headersDis.Count; i++)
                                gridViewDis.Columns.Add(new GridViewColumn() { Header = headersDis[i], DisplayMemberBinding = new Binding(bdheadersDis[i]) });
                        }
                        listViewSide.ItemsSource = result.DistortionData;
                        if (result.DistortionData.Count > 0)
                        {
                            List<Point> points = new();
                            foreach (var item in result.DistortionData[0].FinalPoints)
                            {
                                points.Add(new Point(item.X, item.Y));
                            }
                            ImageView.AddPoint(points);
                        }

                        break;
                    case AlgorithmResultType.LedCheck:
                        ImageView.OpenImage(result.FilePath);
                        listViewSide.Visibility = Visibility.Visible;
                        if (result.LedResultDatas == null)
                        {
                            result.LedResultDatas = new ObservableCollection<LedResultData>();
                            List<AlgResultMTFModel> AlgResultLedcheckModels = AlgResultMTFDao.Instance.GetAllByPid(result.Id);
                            foreach (var item in AlgResultLedcheckModels)
                            {
                                LedResultData ledResultData = new(new Point((double)item.PoiX, (double)item.PoiY), (double)item.PoiWidth / 2);
                                result.LedResultDatas.Add(ledResultData);
                            };
                        }

                        bdheadersDis = new List<string> { "Point", "Radius" };
                        headersDis = new List<string> { "坐标", "半径" };
                        if (listViewSide.View is GridView gridView)
                        {
                            LeftGridViewColumnVisibilitys.Clear();
                            gridView.Columns.Clear();
                            for (int i = 0; i < headersDis.Count; i++)
                                gridView.Columns.Add(new GridViewColumn() { Header = headersDis[i], DisplayMemberBinding = new Binding(bdheadersDis[i]) });
                        }
                        listViewSide.ItemsSource = result.LedResultDatas;
                        if (result.LedResultDatas.Count > 0)
                        {
                            List<Point> points = new();
                            foreach (var item in result.LedResultDatas)
                            {
                                DrawingVisualCircle Circle = new();
                                Circle.Attribute.Center = item.Point;
                                Circle.Attribute.Radius = item.Radius;
                                Circle.Attribute.Brush = Brushes.Transparent;
                                Circle.Attribute.Pen = new Pen(Brushes.Red, 2);
                                Circle.Render();
                                ImageView.ImageShow.AddVisual(Circle);
                            }
                        }
                        break;
                    case AlgorithmResultType.BuildPOI:
                        ImageView.OpenImage(result.FilePath);
                        listViewSide.Visibility = Visibility.Collapsed;
                        if (result.BuildPoiResultData == null)
                        {
                            result.BuildPoiResultData = new ObservableCollection<BuildPoiResultData>();
                            List<AlgResultMTFModel> AlgResultMTFModels = AlgResultMTFDao.Instance.GetAllByPid(result.Id);
                            foreach (var item in AlgResultMTFModels)
                            {
                                BuildPoiResultData mTFResultData = new(item);
                                result.BuildPoiResultData.Add(mTFResultData);
                            }
                        }
                        listViewSide.ItemsSource = result.BuildPoiResultData;
                        foreach (var item in result.BuildPoiResultData)
                        {
                            DrawPoiPoint.Add(item.Point);
                        }
                        ImageView.AddPOIPoint(DrawPoiPoint);
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
            var listView = IsExchange ? listView1 : listViewSide;

            listView.Width = ListCol2.ActualWidth;
            ListCol1.Width = new GridLength(1, GridUnitType.Star);
            ListCol2.Width = GridLength.Auto;
        }
        private void GridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            var listView = !IsExchange ? listView1 : listViewSide;
            listView.Height = ListRow2.ActualHeight - 38;
            ListRow2.Height = GridLength.Auto;
            ListRow1.Height = new GridLength(1, GridUnitType.Star);
        }

        internal void OpenImage(CVCIEFile fileInfo)
        {
            ImageView.OpenImage(fileInfo);
        }

        private void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            AlgResults.Clear();
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
            if (string.IsNullOrEmpty(TextBoxId.Text)&& string.IsNullOrEmpty(TextBoxBatch.Text) && string.IsNullOrEmpty(TextBoxType.Text) && string.IsNullOrEmpty(TextBoxFile.Text) && SearchTimeSart.SelectedDateTime ==DateTime.MinValue)
            {
                AlgResults.Clear();
                List<AlgResultMasterModel> algResults = AlgResultMasterDao.Instance.GetAll();
                foreach (var item in algResults)
                {
                    AlgorithmResult algorithmResult = new(item);
                    AlgResults.AddUnique(algorithmResult);
                }
                SerchPopup.IsOpen = false;
                return;
            }
            else
            {
                string altype = string.Empty;
                if (TextBoxType.SelectedValue is AlgorithmResultType algorithmResultType)
                    altype = ((int)algorithmResultType).ToString();

                AlgResults.Clear();
                List<AlgResultMasterModel> algResults = AlgResultMasterDao.Instance.ConditionalQuery(TextBoxId.Text, TextBoxBatch.Text, altype.ToString(), TextBoxFile.Text ,SearchTimeSart.SelectedDateTime,SearchTimeEnd.SelectedDateTime);
                foreach (var item in algResults)
                {
                    AlgorithmResult algorithmResult = new(item);
                    AlgResults.AddUnique(algorithmResult);
                }
            }
            SerchPopup.IsOpen = false;
        }

        public ObservableCollection<GridViewColumnVisibility> LeftGridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void ContextMenu1_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && listViewSide.View is GridView gridView && LeftGridViewColumnVisibilitys.Count ==0)
                GridViewColumnVisibility.GenContentMenuGridViewColumnZero(contextMenu, gridView.Columns, LeftGridViewColumnVisibilitys);
        }
        bool IsExchange;
        private void Exchange_Click(object sender, RoutedEventArgs e)
        {
            IsExchange = !IsExchange;
            var listD = IsExchange ? listView1 : listViewSide;
            var listL = IsExchange ? listViewSide : listView1;
            if (listD.Parent is Grid parent1 && listL.Parent is Grid parent2 )
            {
                var tempCol = Grid.GetColumn(listD);
                var tempRow = Grid.GetRow(listD);   

                var tempCol1 = Grid.GetColumn(listL);
                var tempRow1 = Grid.GetRow(listL);

                parent1.Children.Remove(listD);
                parent2.Children.Remove(listL);

                parent1.Children.Add(listL);
                parent2.Children.Add(listD);

                Grid.SetColumn(listD, tempCol1);
                Grid.SetRow(listD, tempRow1);

                Grid.SetColumn(listL, tempCol);
                Grid.SetRow(listL, tempRow);


                listD.Width = listL.ActualWidth;
                listL.Height = listD.ActualHeight;
                listD.Height = double.NaN;
                listL.Width = double.NaN;
            }
        }


        private void SideSave_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex>0 && listView1.Items[listView1.SelectedIndex] is AlgorithmResult result)
            {
                if (listView1.SelectedIndex < 0)
                {
                    MessageBox.Show("您需要先选择数据");
                    return;
                }
                using var dialog = new System.Windows.Forms.SaveFileDialog();
                dialog.Filter = "CSV files (*.csv) | *.csv";
                dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
                dialog.RestoreDirectory = true;
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                switch (result.ResultType)
                {   
                    case AlgorithmResultType.POI:
                        break;
                    case AlgorithmResultType.POI_XY_UV:
                        PoiResultCIExyuvData.SaveCsv(result.PoiResultCIExyuvDatas,dialog.FileName);
                        break;
                    case AlgorithmResultType.POI_Y:
                        break;
                    case AlgorithmResultType.FOV:
                        break;
                    case AlgorithmResultType.SFR:
                        break;
                    case AlgorithmResultType.MTF:
                        break;
                    case AlgorithmResultType.Ghost:
                        break;
                    case AlgorithmResultType.LedCheck:
                        break;
                    case AlgorithmResultType.LightArea:
                        break;
                    case AlgorithmResultType.Distortion:
                        break;
                    case AlgorithmResultType.BuildPOI:
                        break;
                    default:
                        break;
                }
            }
        }

        private void ButtonChart_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex > -1)
            {
                if (listView1.Items[listView1.SelectedIndex] is AlgorithmResult result)
                {
                    if (result.ResultType == AlgorithmResultType.POI_XY_UV)
                    {
                        if (result.PoiResultCIExyuvDatas.Count != 0)
                        {
                            WindowChart windowChart = new(result.PoiResultCIExyuvDatas);
                            windowChart.Show();
                        }
                        else
                        {
                            MessageBox.Show("结果为空");
                        }
                    }
                    else
                    {
                        MessageBox.Show("暂不支持其他");
                    }
                }
            }
            else
            {
                MessageBox.Show("没有选择条目");
            }
        }
    }
}
