#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.Common.Utilities;
using ColorVision.Engine.Draw;
using ColorVision.Engine.Media;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.Compliance;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.Distortion;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.FOV;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.Ghost;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.LedCheck;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.MTF;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.POI;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.BuildPoi;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.SFR;
using ColorVision.Net;
using ColorVision.UI.Sorts;
using ColorVision.UI.Views;
using CVCommCore.CVAlgorithm;
using log4net;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{

    /// <summary>
    /// ViewSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class AlgorithmView : UserControl,IView
    {
        private static readonly ILog logg = LogManager.GetLogger(typeof(AlgorithmView));
        public View View { get; set; }

        public AlgorithmView()
        {
            InitializeComponent();
        }

        private NetFileUtil netFileUtil;

        public static ViewAlgorithmConfig Config => ViewAlgorithmConfig.Instance;
        public ObservableCollection<IResultHandle> ResultHandles { get; set; } = new ObservableCollection<IResultHandle>();


        private void UserControl_Initialized(object sender, EventArgs e)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IResultHandle).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IResultHandle  algorithmResultRender)
                    {
                        ResultHandles.Add(algorithmResultRender);
                    }
                }
            }

            this.DataContext = this;
            View = new View();
            ImageView.SetConfig(Config.ImageViewConfig);
            if (listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }
            listView1.ItemsSource = ViewResults;
            var keyValuePairs =
            TextBoxType.ItemsSource = Enum.GetValues(typeof(AlgorithmResultType))
                .Cast<AlgorithmResultType>()
                .Select(e1 => new KeyValuePair<AlgorithmResultType, string>(e1, e1.ToString()))
                .ToList();

            netFileUtil = new NetFileUtil();
            netFileUtil.handler += NetFileUtil_handler;
        }

        private void NetFileUtil_handler(object sender, NetFileEvent arg)
        {
            if (arg.Code == 0 && arg.FileData.data != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    OpenImage(arg.FileData);
                });
            }
        }

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && listView1.View is GridView gridView)
            {
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
            }
        }

        public ObservableCollection<AlgorithmResult> ViewResults { get; set; } = new ObservableCollection<AlgorithmResult>();

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex < 0) return;

  
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
                    case AlgorithmResultType.POI_XYZ:
                        var PoiResultCIExyuvDatas = result.ViewResults.ToSpecificViewResults<PoiResultCIExyuvData>();
                        PoiResultCIExyuvData.SaveCsv(PoiResultCIExyuvDatas, dialog.FileName);
                        ImageUtils.SaveImageSourceToFile(ImageView.ImageShow.Source, Path.Combine(Path.GetDirectoryName(dialog.FileName), Path.GetFileNameWithoutExtension(dialog.FileName) + ".png"));
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
                foreach (var item in ViewResults)
                {
                    value += item.Id + ","
                        + item.Batch + ","
                        + item.POITemplateName + ","
                        + item.FilePath + ","
                        + item.CreateTime + ","
                        + item.ResultType + ","
                        + item.TotalTime + ","
                        + item.Result + ","
                        + item.ResultDesc + ","
                        + Environment.NewLine;
                }
                file.WriteLine(value);
                ImageSource bitmapSource = ImageView.ImageShow.Source;
                ImageUtils.SaveImageSourceToFile(bitmapSource, Path.Combine( Path.GetDirectoryName(dialog.FileName),Path.GetFileNameWithoutExtension(dialog.FileName) + ".png"));
            }
        }

        public void AlgResultMasterModelDataDraw(AlgResultMasterModel result)
        {
            if (result != null)
            {
                AlgorithmResult algorithmResult = new AlgorithmResult(result);
                ViewResults.AddUnique(algorithmResult, Config.InsertAtBeginning);
                if (Config.AutoRefreshView)
                    RefreshResultListView();
            }
        }

        public void RefreshResultListView()
        {
            if (listView1.Items.Count > 0) listView1.SelectedIndex = Config.InsertAtBeginning? 0: listView1.Items.Count - 1;
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

            var ResultHandle = ResultHandles.FirstOrDefault(a => a.ResultType == ViewResults[listView1.SelectedIndex].ResultType);
            if (ResultHandle != null)
            {
                ResultHandle.Handle(this,ViewResults[listView1.SelectedIndex]);
                return;
            }

            if (ViewResults[listView1.SelectedIndex] is AlgorithmResult result)
            {
                ImageView.ImageShow.Clear();
                List<POIPoint> DrawPoiPoint = new();
                List<string> header = new();
                List<string> bdHeader = new();
                if (File.Exists(result.FilePath))
                {
                    ImageView.OpenImage(result.FilePath);
                }
                switch (result.ResultType)
                {
                    case AlgorithmResultType.LightArea:
                        break;
                    case AlgorithmResultType.POI_XYZ_File:
                    case AlgorithmResultType.POI_Y_File:
                        if (result.ViewResults == null)
                        {
                            result.ViewResults = new ObservableCollection<IViewResult>(AlgResultPoiCieFileDao.Instance.GetAllByPid(result.Id));
                        }
                        header = new List<string> { "file_name", "FileUrl" , "FileType" };
                        bdHeader = new List<string> { "FileName", "FileUrl" , "FileType", };
                        break;
                    case AlgorithmResultType.POI:
                        if (result.ViewResults == null)
                        {
                            result.ViewResults = new ObservableCollection<IViewResult>();
                            List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(result.Id);
                            int id = 0;
                            foreach (var item in POIPointResultModels)
                            {
                                PoiResultData poiResult = new(item) { Id = id++ };
                                result.ViewResults.Add(poiResult);
                            };
                        }

                        header = new List<string> { "名称", "位置", "大小", "形状", "Validate" };
                        bdHeader = new List<string> { "Name", "PixelPos", "PixelSize", "Shapes", "POIPointResultModel.ValidateResult" };

                        foreach (var item in result.ViewResults)
                        {
                            if (item is PoiResultData poiResultData)
                            {
                                DrawPoiPoint.Add(poiResultData.Point);
                            }
                        }
                        AddPOIPoint(DrawPoiPoint);
                        break;
                    case AlgorithmResultType.LEDStripDetection:
                    case AlgorithmResultType.POI_XYZ:
                        if (result.ViewResults == null)
                        {
                            result.ViewResults = new ObservableCollection<IViewResult>();
                            List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(result.Id);
                            int id = 0;
                            foreach (var item in POIPointResultModels)
                            {
                                PoiResultCIExyuvData poiResultCIExyuvData = new(item) { Id = id++ };
                                result.ViewResults.Add(poiResultCIExyuvData);
                            };
                        }
                        header = new List<string> { "Id", Properties.Resources.Name, Properties.Resources.Position, Properties.Resources.Shape, Properties.Resources.Size, "CCT", "Wave", "X", "Y", "Z", "u", "v", "x", "y", "Validate" };
                        if (result.ResultType == AlgorithmResultType.LEDStripDetection)
                        {
                            header = new List<string> { "Id", Properties.Resources.Name, Properties.Resources.Position, Properties.Resources.Shape };
                        }

                        bdHeader = new List<string> { "Id", "Name", "PixelPos", "Shapes", "PixelSize", "CCT", "Wave", "X", "Y", "Z", "u", "v", "x", "y", "POIPointResultModel.ValidateResult" };

                        foreach (var item in result.ViewResults)
                        {
                            if (item is PoiResultCIExyuvData poiResultData)
                            {
                                DrawPoiPoint.Add(poiResultData.Point);
                            }
                        }
                        AddPOIPoint(DrawPoiPoint);
                        break;
                    case AlgorithmResultType.POI_Y:
                        if (result.ViewResults == null)
                        {
                            result.ViewResults = new ObservableCollection<IViewResult>();
                            List<PoiPointResultModel> POIPointResultModels = PoiPointResultDao.Instance.GetAllByPid(result.Id);
                            foreach (var item in POIPointResultModels)
                            {
                                PoiResultCIEYData poiResultCIExyuvData = new(item);
                                result.ViewResults.Add(poiResultCIExyuvData);
                            };
                        }

                        header = new() { "名称", "位置", "大小", "形状", "Y", "Validate" };
                        bdHeader = new() { "Name", "PixelPos", "PixelSize", "Shapes", "Y", "POIPointResultModel.ValidateResult" };
                        foreach (var item in result.ViewResults)
                        {
                            if (item is PoiResultData poiResultData)
                            {
                                DrawPoiPoint.Add(poiResultData.Point);
                            }
                        }

                        AddPOIPoint(DrawPoiPoint);
                        break;
                    case AlgorithmResultType.FOV:

                        if (result.ViewResults == null)
                        {
                            result.ViewResults = new ObservableCollection<IViewResult>();
                            List<AlgResultFOVModel> AlgResultFOVModels = AlgResultFOVDao.Instance.GetAllByPid(result.Id);
                            foreach (var item in AlgResultFOVModels)
                            {
                                ViewResultFOV fOVResultData = new(item);
                                result.ViewResults.Add(fOVResultData);
                            };
                        }
                        header = new() { "Pattern", "Type", "Degrees" };
                        bdHeader = new() { "Pattern", "Type", "Degrees" };

                        break;
                    case AlgorithmResultType.SFR:
                        if (result.ViewResults == null)
                        {
                            result.ViewResults = new ObservableCollection<IViewResult>();
                            List<AlgResultSFRModel> AlgResultSFRModels = AlgResultSFRDao.Instance.GetAllByPid(result.Id);
                            foreach (var item in AlgResultSFRModels)
                            {
                                var Pdfrequencys = JsonConvert.DeserializeObject<float[]>(item.Pdfrequency);
                                var PdomainSamplingDatas = JsonConvert.DeserializeObject<float[]>(item.PdomainSamplingData);
                                for (int i = 0; i < Pdfrequencys.Length; i++)
                                {
                                    ViewResultSFR resultData = new(Pdfrequencys[i], PdomainSamplingDatas[i]);
                                    result.ViewResults.Add(resultData);
                                }
                            };
                        }
                        header = new() { "pdfrequency", "pdomainSamplingData" };
                        bdHeader = new() { "pdfrequency", "pdomainSamplingData" };

                        if (result.ViewResults.Count > 0)
                        {
                            AddRect(new Rect(10, 10, 10, 10));
                        }

                        break;
                    case AlgorithmResultType.OLED_JND_CalVas:
                    case AlgorithmResultType.MTF:
                        if (result.ViewResults == null)
                        {
                            result.ViewResults = new ObservableCollection<IViewResult>();
                            List<PoiPointResultModel> AlgResultMTFModels = PoiPointResultDao.Instance.GetAllByPid(result.Id);
                            foreach (var item in AlgResultMTFModels)
                            {
                                ViewResultMTF mTFResultData = new(item);
                                result.ViewResults.Add(mTFResultData);
                            }
                        }
                        header = new() { "位置", "大小", "形状", "MTF", "Value" };
                        bdHeader = new() { "PixelPos", "PixelSize", "Shapes", "Articulation", "AlgResultMTFModel.ValidateResult" };

                        foreach (var item in result.ViewResults)
                        {
                            if (item is PoiResultData poiResultData)
                            {
                                DrawPoiPoint.Add(poiResultData.Point);
                            }
                        }
                        AddPOIPoint(DrawPoiPoint);
                        break;
                    case AlgorithmResultType.Ghost:
                        if (result.ViewResults == null)
                        {
                            result.ViewResults = new ObservableCollection<IViewResult>();
                            List<AlgResultGhostModel> AlgResultGhostModels = AlgResultGhostDao.Instance.GetAllByPid(result.Id);
                            foreach (var item in AlgResultGhostModels)
                            {
                                ViewResultGhost ghostResultData = new(item);
                                result.ViewResults.Add(ghostResultData);
                            }
                        }

                        if (result.ViewResults.Count != 0 && result.ViewResults[0] is ViewResultGhost viewResultGhost)
                        {
                            try
                            {
                                string GhostPixels = viewResultGhost.GhostPixels;
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

                                string LedPixels = viewResultGhost.LedPixels;
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

                        }
                        header = new() { "质心坐标", "光斑灰度", "鬼影灰度" };
                        bdHeader = new() { "LedCenters", "LedBlobGray", "GhostAvrGray" };
                        break;
                    case AlgorithmResultType.Distortion:
                        if (result.ViewResults == null)
                        {
                            result.ViewResults = new ObservableCollection<IViewResult>();
                            var Distortions = AlgResultDistortionDao.Instance.GetAllByPid(result.Id);
                            foreach (var item in Distortions)
                            {
                                ViewResultDistortion distortionResultData = new(item);
                                result.ViewResults.Add(distortionResultData);
                            }
                        }
                        header = new() { "类型", "斜率", "布点", "角点", "畸变率" };
                        bdHeader = new() { "DisTypeDesc", "SlopeTypeDesc", "LayoutTypeDesc", "CornerTypeDesc", "MaxRatio" };

                        if (result.ViewResults.Count > 0 && result.ViewResults[0] is ViewResultDistortion viewResultDistortion)
                        {
                            List<Point> points1 = new();
                            foreach (var item in viewResultDistortion.FinalPoints)
                            {
                                points1.Add(new Point(item.X, item.Y));
                            }
                            AddPoint(points1);
                        }
                        break;
                    case AlgorithmResultType.OLED_FindDotsArrayMem:
                    case AlgorithmResultType.LedCheck:
                        if (result.ViewResults == null)
                        {
                            result.ViewResults = new ObservableCollection<IViewResult>();
                            List<PoiPointResultModel> AlgResultLedcheckModels = PoiPointResultDao.Instance.GetAllByPid(result.Id);
                            foreach (var item in AlgResultLedcheckModels)
                            {
                                ViewResultLedCheck ledResultData = new(new Point((double)item.PoiX, (double)item.PoiY), (double)item.PoiWidth / 2);
                                result.ViewResults.Add(ledResultData);
                            };
                        }
                        header = new List<string> { "坐标", "半径" };
                        bdHeader = new List<string> { "Point", "Radius" };

                        List<Point> points = new();
                        List<double> Radius = new();


                        foreach (var item in result.ViewResults)
                        {
                            if (item is ViewResultLedCheck viewResultLedCheck)
                            {
                                points.Add(viewResultLedCheck.Point);
                                Radius.Add(viewResultLedCheck.Radius);
                            }
                        }
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            for (int i = 0; i < points.Count; i++)
                            {
                                if (i % 10000 == 0)
                                    await Task.Delay(30);

                                DVCircle Circle = new();
                                Circle.Attribute.Center = points[i];
                                Circle.Attribute.Radius = Radius[i];
                                Circle.Attribute.Brush = Brushes.Transparent;
                                Circle.Attribute.Pen = new Pen(Brushes.Red, 2);
                                Circle.Render();
                                ImageView.ImageShow.OnlyAddVisual(Circle);
                            }
                        });

                        break;
                    case AlgorithmResultType.BuildPOI:
                        if (result.ViewResults == null)  
                        {
                            result.ViewResults = new ObservableCollection<IViewResult>();
                            List<PoiPointResultModel> AlgResultMTFModels = PoiPointResultDao.Instance.GetAllByPid(result.Id);
                            foreach (var item in AlgResultMTFModels)
                            {
                                ViewResultBuildPoi mTFResultData = new(item);
                                result.ViewResults.Add(mTFResultData);
                            }
                        }
                        foreach (var item in result.ViewResults)
                        {
                            if (item is PoiResultData poiResultData)
                                DrawPoiPoint.Add(poiResultData.Point);
                        }
                        AddPOIPoint(DrawPoiPoint);
                        Config.IsShowSideListView = false;
                        break;
                    case AlgorithmResultType.Compliance_Contrast:
                    case AlgorithmResultType.Compliance_Math:
                    case AlgorithmResultType.Compliance_Contrast_CIE_Y:
                    case AlgorithmResultType.Compliance_Math_CIE_Y:
                        if (result.ViewResults == null)
                        {
                            result.ViewResults = ComplianceYDao.Instance.GetAllByPid(result.Id).ToViewResults();
                        }

                        bdHeader = new() { "名称", "值", "Validate" };
                        bdHeader = new() { "Name", "DataValue", "ValidateResult" };
                        break;
                    case AlgorithmResultType.Compliance_Contrast_CIE_XYZ:
                    case AlgorithmResultType.Compliance_Math_CIE_XYZ:
                        if (result.ViewResults == null)
                        {
                            result.ViewResults = ComplianceXYZDao.Instance.GetAllByPid(result.Id).ToViewResults();
                        }
                        header = new() { "名称", "x", "y", "z", "xxx", "yyy", "zzz", "cct", "wave", "Validate" };
                        bdHeader = new() { "Name", "DataValuex", "DataValuey", "DataValuez", "DataValuexxx", "DataValueyyy", "DataValuezzz", "DataValueCCT", "DataValueWave", "ValidateResult" };
                        break;
                    default:
                        break;
                }

                if (listViewSide.View is GridView gridView)
                {
                    LeftGridViewColumnVisibilitys.Clear();
                    gridView.Columns.Clear();
                    for (int i = 0; i < header.Count; i++)
                        gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                    listViewSide.ItemsSource = result.ViewResults;
                }
            }
        }

        public void AddPoint(List<Point> points)
        {
            int id = 0;
            foreach (var item in points)
            {
                id++;
                DVCircleText Circle = new();
                Circle.Attribute.Center = item;
                Circle.Attribute.Radius = 20 / ImageView.Zoombox1.ContentMatrix.M11;
                Circle.Attribute.Brush = Brushes.Transparent;
                Circle.Attribute.Pen = new Pen(Brushes.Red, 1 / ImageView.Zoombox1.ContentMatrix.M11);
                Circle.Attribute.Id = id;
                Circle.Render();
                ImageView.AddVisual(Circle);
            }
        }

        public void AddRect(Rect rect)
        {
            DVRectangleText Rectangle = new();
            Rectangle.Attribute.Rect = new Rect(rect.X, rect.Y, rect.Width, rect.Height);
            Rectangle.Attribute.Brush = Brushes.Transparent;
            Rectangle.Attribute.Pen = new Pen(Brushes.Red, rect.Width / 30.0);
            Rectangle.Render();
            ImageView.AddVisual(Rectangle);
        }

        private void listView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && listView1.SelectedIndex > -1)
            {
                int temp = listView1.SelectedIndex;
                ViewResults.RemoveAt(temp);
            }
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
            if (ListRow2.ActualHeight > 32)
            {
                var listView = !IsExchange ? listView1 : listViewSide;
                listView.Height = ListRow2.ActualHeight - 32;
                ListRow2.Height = GridLength.Auto;
                ListRow1.Height = new GridLength(1, GridUnitType.Star);

            }
        }

        internal void OpenImage(CVCIEFile fileInfo)
        {
            ImageView.OpenImage(fileInfo.ToWriteableBitmap());
        }


        private void Button_Delete_Click(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
        }

        private void Search1_Click(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
            List<AlgResultMasterModel> algResults = AlgResultMasterDao.Instance.GetAll();
            foreach (var item in algResults)
            {
                AlgorithmResult algorithmResult = new(item);
                ViewResults.AddUnique(algorithmResult);
            }
        }
        private void Search_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
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
                ViewResults.Clear();
                List<AlgResultMasterModel> algResults = AlgResultMasterDao.Instance.GetAll();
                foreach (var item in algResults)
                {
                    AlgorithmResult algorithmResult = new(item);
                    ViewResults.AddUnique(algorithmResult);
                }
                SerchPopup.IsOpen = false;
                return;
            }
            else
            {
                string altype = string.Empty;
                if (TextBoxType.SelectedValue is AlgorithmResultType algorithmResultType)
                    altype = ((int)algorithmResultType).ToString();

                ViewResults.Clear();
                List<AlgResultMasterModel> algResults = AlgResultMasterDao.Instance.ConditionalQuery(TextBoxId.Text, TextBoxBatch.Text, altype.ToString(), TextBoxFile.Text ,SearchTimeSart.SelectedDateTime,SearchTimeEnd.SelectedDateTime);
                foreach (var item in algResults)
                {
                    AlgorithmResult algorithmResult = new(item);
                    ViewResults.AddUnique(algorithmResult);
                }
            }
            SerchPopup.IsOpen = false;
        }



        public async void AddPOIPoint(List<POIPoint> PoiPoints)
        {
            ImageView.ImageShow.Clear();
            await Task.Delay(1000);
            for (int i = 0; i < PoiPoints.Count; i++)
            {
                if (i % 10000 == 0)
                    await Task.Delay(30);

                var item = PoiPoints[i];
                switch (item.PointType)
                {
                    case POIPointTypes.Circle:
                        DVCircle Circle = new();
                        Circle.Attribute.Center = new Point(item.PixelX, item.PixelY);
                        Circle.Attribute.Radius = item.Radius;
                        Circle.Attribute.Brush = Brushes.Transparent;
                        Circle.Attribute.Pen = new Pen(Brushes.Red, 1);
                        Circle.Attribute.Id = item.Id ?? -1;
                        Circle.Render();
                        ImageView.AddVisual(Circle);
                        break;
                    case POIPointTypes.Rect:
                        DVRectangle Rectangle = new();
                        Rectangle.Attribute.Rect = new Rect(item.PixelX - item.Width / 2, item.PixelY - item.Height / 2, item.Width, item.Height);
                        Rectangle.Attribute.Brush = Brushes.Transparent;
                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1);
                        Rectangle.Attribute.Id = item.Id ?? -1;
                        Rectangle.Render();
                        ImageView.AddVisual(Rectangle);
                        break;
                    case POIPointTypes.SolidPoint:
                        DVCircle Circle1 = new();
                        Circle1.Attribute.Center = new Point(item.PixelX, item.PixelY);
                        Circle1.Attribute.Radius = 10;
                        Circle1.Attribute.Brush = Brushes.Red;
                        Circle1.Attribute.Pen = new Pen(Brushes.Red, 1);
                        Circle1.Attribute.Id = item.Id ?? -1;
                        Circle1.Render();
                        ImageView.AddVisual(Circle1);
                        break;
                    default:
                        break;
                }

            }
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
            if (listView1.SelectedItems.Count > 0)
            {
                using var dialog = new System.Windows.Forms.FolderBrowserDialog();
                dialog.Description = "请选择保存文件的文件夹";
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                string selectedPath = dialog.SelectedPath;

                foreach (var selectedItem in listView1.SelectedItems)
                {
                    if (selectedItem is AlgorithmResult result)
                    {
                        string fileName = System.IO.Path.Combine(selectedPath, $"{result.Batch}.csv");

                        switch (result.ResultType)
                        {
                            case AlgorithmResultType.POI:
                                // Handle POI result saving logic here
                                break;
                            case AlgorithmResultType.POI_XYZ:
                                var PoiResultCIExyuvDatas = result.ViewResults.ToSpecificViewResults<PoiResultCIExyuvData>();
                                PoiResultCIExyuvData.SaveCsv(PoiResultCIExyuvDatas, fileName);
                                break;
                            case AlgorithmResultType.POI_Y:
                                var PoiResultCIEYDatas = result.ViewResults.ToSpecificViewResults<PoiResultCIEYData>();
                                PoiResultCIEYData.SaveCsv(PoiResultCIEYDatas, fileName);
                                // Handle POI_Y result saving logic here
                                break;
                            case AlgorithmResultType.FOV:
                                // Handle SendCommand result saving logic here
                                break;
                            case AlgorithmResultType.SFR:
                                // Handle SFR result saving logic here
                                break;
                            case AlgorithmResultType.MTF:
                                // Handle SendCommand result saving logic here
                                break;
                            case AlgorithmResultType.Ghost:
                                // Handle Ghost result saving logic here
                                break;
                            case AlgorithmResultType.LedCheck:
                                // Handle LedCheck result saving logic here
                                break;
                            case AlgorithmResultType.LightArea:
                                // Handle LightArea result saving logic here
                                break;
                            case AlgorithmResultType.Distortion:
                                // Handle Distortion result saving logic here
                                break;
                            case AlgorithmResultType.BuildPOI:
                                // Handle BuildPOI result saving logic here
                                break;
                            default:
                                // Handle default case here
                                break;
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("您需要先选择数据");
            }
        }

        private void ButtonChart_Click(object sender, RoutedEventArgs e)
        {
            if (listView1.SelectedIndex > -1)
            {
                if (listView1.Items[listView1.SelectedIndex] is AlgorithmResult result)
                {
                    if (result.ResultType == AlgorithmResultType.POI_XYZ)
                    {
                        var PoiResultCIExyuvDatas = result.ViewResults.ToSpecificViewResults<PoiResultCIExyuvData>();
                        if (PoiResultCIExyuvDatas.Count !=0)
                        {
                            WindowChart windowChart = new(PoiResultCIExyuvDatas);
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
