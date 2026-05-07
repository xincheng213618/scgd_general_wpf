using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.Themes.Controls;
using ColorVision.UI.Menus;
using cvColorVision;
using CVCommCore.CVAlgorithm;
using log4net;
using Newtonsoft.Json;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media
{
    public class CVFilemageEditorConfig : IImageEditorConfig
    {
        [JsonIgnore]
        public bool ConvertXYZhandleOnce { get; set; }

        [JsonIgnore]
        public IntPtr ConvertXYZhandle { get; set; } = Tool.GenerateRandomIntPtr();
    }


    [FileExtension(".cvraw|.cvcie")]
    public record class CVRawOpen(EditorContext EditorContext) : IImageOpen, IIEditorToolContextMenu
    {
        public CVFilemageEditorConfig Config => EditorContext.Config.GetRequiredService<CVFilemageEditorConfig>();

        private static readonly ILog log = LogManager.GetLogger(typeof(CVRawOpen));

        public List<string> ComboBoxLayerItems { get; set; } = new List<string>() { "Src", "R", "G", "B" };
        public List<List<Point>> Points { get; set; } = new List<List<Point>>();

        public (int pointIndex, int listIndex) FindNearbyPoints(int mousex, int mousey)
        {
            for (int listIndex = 0; listIndex < Points.Count; listIndex++)
            {
                List<Point> pointList = Points[listIndex];
                for (int pointIndex = 0; pointIndex < pointList.Count; pointIndex++)
                {
                    Point point = pointList[pointIndex];
                    double deltaX = point.X - (double)mousex;
                    double deltaY = point.Y - (double)mousey;
                    if (!(Math.Abs(deltaX) > 5.0) && !(Math.Abs(deltaY) > 5.0))
                    {
                        double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                        if (distance < 5.0)
                        {
                            return (pointIndex: pointIndex, listIndex: listIndex);
                        }
                    }
                }
            }
            return (pointIndex: -1, listIndex: -1);
        }



        bool ShowDateFilePath;

        private static bool TryGetMouseMagnifier(ImageView imageView, out MouseMagnifierManager magnifier)
        {
            magnifier = imageView.EditorContext.IEditorToolFactory.GetIEditorTool<MouseMagnifierManager>();
            return magnifier != null;
        }

        public void CVCIESetBuffer(ImageView imageView,string filePath)
        {
            CVCIEFile meta;
            int index;
            CvcieMouseProbeController? probeController = null;
            MouseMagnifierManager? mouseMagnifier = null;


            Action LoadBuffer = new Action(() =>
            {
                if (imageView.Config.GetProperties<bool>("IsBufferSet")) return;
                var meta = imageView.Config.GetProperties<CVCIEFile>("meta");
                int index = imageView.Config.GetProperties<int>("index");
                CVFileUtil.ReadCIEFileData(filePath, ref meta, index);
                int resultCM_SetBufferXYZ = ConvertXYZ.CM_SetBufferXYZ(Config.ConvertXYZhandle, (uint)meta.Cols, (uint)meta.Rows, (uint)meta.Bpp, (uint)meta.Channels, meta.Data);
                log.Debug($"CM_SetBufferXYZ :{resultCM_SetBufferXYZ}");
                // ConvertXYZ will hold its own buffer copy; release managed raw data to reduce peak memory.
                meta.Data = null;
                imageView.Config.SetOpenerRuntime("IsBufferSet", true, nameof(CVRawOpen), "CVCIE 原始缓冲是否已经灌入 ConvertXYZ");

            });

            imageView.Config.SetOpenerRuntime("LoadBuffer", LoadBuffer, nameof(CVRawOpen), "延迟加载 CVCIE 原始缓冲的回调");

            ShowDateFilePath = false;
            if (File.Exists(ViewAlgorithmConfig.Instance.ShowDateFilePath))
            {
                Points.Clear();
                log.Info("ShowDateFilePath:" + ViewAlgorithmConfig.Instance.ShowDateFilePath);
                string[] lines = File.ReadAllLines(ViewAlgorithmConfig.Instance.ShowDateFilePath);
                string[] dates = lines[0].Split(',');
                int rows = int.Parse(dates[0]);
                int cols = int.Parse(dates[1]);
                for (int lineIndex = 2; lineIndex < lines.Length; lineIndex++)
                {
                    string[] xy = lines[lineIndex].Split(',');
                    List<Point> points = new List<Point>();
                    for (int i = 0; i < xy.Length; i += 4)
                    {
                        if (double.TryParse(xy[i], out var x) && double.TryParse(xy[i + 1], out var y))
                        {
                            points.Add(new Point(x, y));
                        }
                    }
                    Points.Add(points);
                }
                ShowDateFilePath = true;
            }

            void Config_Cleared(object? sender, EventArgs e)
            {
                imageView.Config.Cleared -= Config_Cleared;
                if (probeController != null)
                {
                    mouseMagnifier?.MouseMoveProbeHandler -= probeController.TryHandleProbe;
                    probeController.Dispose();
                    probeController = null;
                }
                int result = ConvertXYZ.CM_ReleaseBuffer(Config.ConvertXYZhandle);
                result = ConvertXYZ.CM_UnInitXYZ(Config.ConvertXYZhandle);
                result = ConvertXYZ.CM_InitXYZ(Config.ConvertXYZhandle);
            }
            imageView.Config.Cleared += Config_Cleared;

           


            if (!Config.ConvertXYZhandleOnce)
            {
                int result = ConvertXYZ.CM_InitXYZ(Config.ConvertXYZhandle);
                log.Info($"ConvertXYZ.CM_InitXYZ :{result}");
                Config.ConvertXYZhandleOnce = true;
            }
            imageView.Config.FilePath = filePath;
            void ComboBoxLayers1_SelectionChanged(object sender, SelectionChangedEventArgs e)
            {
                if (sender is ComboBox comboBox)
                {
                    string layer = ComboBoxLayerItems[comboBox.SelectedIndex];
                    if (layer == "Src")
                    {
                        imageView.OpenImage(CVFileUtil.OpenLocalFileChannel(imageView.Config.FilePath, CVImageChannelType.SRC).ToWriteableBitmap());
                    }
                    if (layer == "R")
                    {
                        imageView.ExtractChannel(0);
                    }
                    if (layer == "G")
                    {
                        imageView.ExtractChannel(1);
                    }
                    if (layer == "B")
                    {
                        imageView.ExtractChannel(2);
                    }
                    if (layer == "X")
                    {
                        imageView.OpenImage(CVFileUtil.OpenLocalFileChannel(imageView.Config.FilePath, CVImageChannelType.CieXyzX).ToWriteableBitmap());
                    }
                    if (layer == "Y")
                    {
                        imageView.OpenImage(CVFileUtil.OpenLocalFileChannel(imageView.Config.FilePath, CVImageChannelType.CieXyzY).ToWriteableBitmap());
                    }
                    if (layer == "Z")
                    {
                        imageView.OpenImage(CVFileUtil.OpenLocalFileChannel(imageView.Config.FilePath, CVImageChannelType.CieXyzZ).ToWriteableBitmap());

                    }
                }
            }

            if (File.Exists(filePath) && CVFileUtil.IsCIEFile(filePath))
            {
                 index = CVFileUtil.ReadCIEFileHeader(imageView.Config.FilePath, out meta);
                if (index <= 0) return;
                if (meta.FileExtType == CVType.CIE)
                {
                    CvcieProbeSettings probeSettings = CvcieProbeSettings.GetOrCreate(imageView);
                    log.Debug(JsonConvert.SerializeObject(meta));
                    imageView.Config.SetOpenerRuntime("IsCVCIE", true, nameof(CVRawOpen), "当前视图是否由 CVCIE 打开器接管");

                    if (!TryGetMouseMagnifier(imageView, out mouseMagnifier))
                    {
                        log.Warn("CVCIE open: MouseMagnifierManager not found, skip probe integration.");
                    }

                    imageView.Config.SetOpenerRuntime("meta", meta, nameof(CVRawOpen), "CVCIE 文件头和原始缓冲元信息");
                    imageView.Config.SetOpenerRuntime("index", index, nameof(CVRawOpen), "CVCIE 数据块索引");
                    imageView.Config.SetOpenerRuntime("Exp", meta.Exp, nameof(CVRawOpen), "当前 CVCIE 曝光数组");

                    imageView.Config.SetOpenerRuntime("IsBufferSet", false, nameof(CVRawOpen), "CVCIE 原始缓冲是否已经灌入 ConvertXYZ");
                    exp = meta.Exp;
                    probeController = new CvcieMouseProbeController(
                        imageView,
                        Config.ConvertXYZhandle,
                        LoadBuffer,
                        () => exp,
                        () => ShowDateFilePath,
                        FindNearbyPoints,
                        () => probeSettings);
                    mouseMagnifier?.MouseMoveProbeHandler += probeController.TryHandleProbe;

                    if (meta.SrcFileName !=null && !File.Exists(meta.SrcFileName))
                        meta.SrcFileName = Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, meta.SrcFileName);

                    if (meta.Channels ==3)
                    {
                        if (File.Exists(meta.SrcFileName))
                        {
                            ComboBoxLayerItems = new List<string>() { "Src", "R", "G", "B", "X", "Y", "Z" };
                        }
                        else
                        {
                            ComboBoxLayerItems = new List<string>() { "Src", "X", "Y", "Z" };
                        }
                    }
                    else if (meta.Channels == 1)
                    {
                        if (File.Exists(meta.SrcFileName))
                        {
                            ComboBoxLayerItems = new List<string>() { "Src", "Y" };
                        }
                        else
                        {
                            ComboBoxLayerItems = new List<string>() { "Src" };
                        }
                    }
                    else
                    {
                        ComboBoxLayerItems = new List<string>() { "Src" };
                    }
                    imageView.ComboBoxLayers.ItemsSource = ComboBoxLayerItems;
                    imageView.ComboBoxLayers.SelectedIndex = 0;
                    imageView.AddSelectionChangedHandler(ComboBoxLayers1_SelectionChanged);;
                }
                else
                {
                    if (meta.Channels == 3)
                    {
                        ComboBoxLayerItems = new List<string>() { "Src", "R", "G", "B" };
                    }
                    else if (meta.Channels == 1)
                    {
                        ComboBoxLayerItems = new List<string>() { "Src"};
                    }
                    else
                    {
                        ComboBoxLayerItems = new List<string>() { "Src" };
                    }
                    imageView.ComboBoxLayers.ItemsSource = ComboBoxLayerItems;
                    imageView.ComboBoxLayers.SelectedIndex = 0;
                    imageView.AddSelectionChangedHandler(ComboBoxLayers1_SelectionChanged); ;
                }
            }

        }



        public float[] exp { get; set; }

        public List<MenuItemMetadata> GetContextMenuItems()
        {


            PoiResultCIEYData GetCVCIEY(POIPoint poiPoint)
            {
                int x = (int)poiPoint.PixelX; int y = (int)poiPoint.PixelY; int rect = (int)poiPoint.Width; int rect2 = (int)poiPoint.Height;
                PoiResultCIEYData PoiResultCIEYData = new PoiResultCIEYData();
                PoiResultCIEYData.Point = poiPoint;
                float dYVal = 0;

                switch (poiPoint.PointType)
                {
                    case POIPointTypes.None:
                        break;
                    case POIPointTypes.SolidPoint:
                        _ = ConvertXYZ.CM_GetYCircle(Config.ConvertXYZhandle, x, y, ref dYVal, 1);
                        break;
                    case POIPointTypes.Circle:
                        _ = ConvertXYZ.CM_GetYCircle(Config.ConvertXYZhandle, x, y, ref dYVal, rect / 2);
                        break;
                    case POIPointTypes.Rect:
                        _ = ConvertXYZ.CM_GetYRect(Config.ConvertXYZhandle, x, y, ref dYVal, rect, rect2);
                        break;
                    default:
                        break;
                }
                PoiResultCIEYData.Y = dYVal;
                return PoiResultCIEYData;
            }


            PoiResultCIExyuvData GetCVCIE(POIPoint pOIPoint)
            {
                int x = (int)pOIPoint.PixelX; int y = (int)pOIPoint.PixelY; int rect = (int)pOIPoint.Width; int rect2 = (int)pOIPoint.Height;
                PoiResultCIExyuvData poiResultCIExyuvData = new PoiResultCIExyuvData();
                poiResultCIExyuvData.Point = pOIPoint;
                float dXVal = 0;
                float dYVal = 0;
                float dZVal = 0;
                float dx = 0;
                float dy = 0;
                float du = 0;
                float dv = 0;
                float CCT = 0;
                float Wave = 0;

                switch (pOIPoint.PointType)
                {
                    case POIPointTypes.None:
                        break;
                    case POIPointTypes.SolidPoint:
                        _ = ConvertXYZ.CM_GetXYZxyuvCircle(Config.ConvertXYZhandle, x, y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, 1);
                        break;
                    case POIPointTypes.Circle:
                        _ = ConvertXYZ.CM_GetXYZxyuvCircle(Config.ConvertXYZhandle, x, y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, rect / 2);
                        break;
                    case POIPointTypes.Rect:
                        _ = ConvertXYZ.CM_GetXYZxyuvRect(Config.ConvertXYZhandle, x, y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, rect, rect2);
                        break;
                    default:
                        break;
                }

                poiResultCIExyuvData.u = du;
                poiResultCIExyuvData.v = dv;
                poiResultCIExyuvData.x = dx;
                poiResultCIExyuvData.y = dy;
                poiResultCIExyuvData.X = dXVal;
                poiResultCIExyuvData.Y = dYVal;
                poiResultCIExyuvData.Z = dZVal;

                int i = ConvertXYZ.CM_GetxyuvCCTWaveCircle(Config.ConvertXYZhandle, x, y, ref dx, ref dy, ref du, ref dv, ref CCT, ref Wave, rect / 2);
                poiResultCIExyuvData.CCT = CCT;
                poiResultCIExyuvData.Wave = Wave;

                return poiResultCIExyuvData;
            }


            List<MenuItemMetadata> menuItems = new List<MenuItemMetadata>();
            menuItems.Add(new MenuItemMetadata()
            {
                Header = ColorVision.Engine.Properties.Resources.Export,
                GuidId = "CVCIEExport",
                Order = 301,
                Command = new RelayCommand(a =>
                {
                    if (EditorContext.Config.GetProperties<string>("FilePath") is string FilePath && File.Exists(FilePath))
                    {
                        new ExportCVCIE(FilePath).ShowDialog();
                    }
                })
            });
            
            if (EditorContext.Config.GetProperties<bool>("IsCVCIE"))
            {
                MenuItemMetadata menuItemMetadata = new MenuItemMetadata()
                {
                    Header = "POI",
                    GuidId = "POI",
                    Order = 302,
                    Command = new RelayCommand(a =>
                    {

                        if (!PoiImageViewComponent.TryGetSelectedTemplate(EditorContext.ImageView, out PoiParam poiParams))
                        {
                            poiParams = new PoiParam();
                        }

                        if (!EditorContext.Config.GetProperties<bool>("IsBufferSet"))
                        {
                            Action action = EditorContext.Config.GetProperties<Action>("LoadBuffer");
                            action?.Invoke();
                        }


                        int result = ConvertXYZ.CM_SetFilter(Config.ConvertXYZhandle, poiParams.PoiConfig.Filter.Enable, poiParams.PoiConfig.Filter.Threshold);
                        result = ConvertXYZ.CM_SetFilterNoArea(Config.ConvertXYZhandle, poiParams.PoiConfig.Filter.NoAreaEnable, poiParams.PoiConfig.Filter.Threshold);
                        result = ConvertXYZ.CM_SetFilterXYZ(Config.ConvertXYZhandle, poiParams.PoiConfig.Filter.XYZEnable, (int)poiParams.PoiConfig.Filter.XYZType, poiParams.PoiConfig.Filter.Threshold);


                        if (EditorContext.Config.GetProperties<float[]>("Exp") is float[] exp && exp.Length == 1)
                        {
                            ObservableCollection<PoiResultCIEYData> PoiResultCIEYData = new ObservableCollection<PoiResultCIEYData>();

                            bool Isshow = EditorContext.ImageView.DrawingVisualLists.Count < 1000;
                            foreach (var item in EditorContext.ImageView.DrawingVisualLists)
                            {
                                BaseProperties drawAttributeBase = item.BaseAttribute;
                                if (drawAttributeBase is CircleTextProperties circle)
                                {
                                    POIPoint pOIPoint = new POIPoint() { Name = circle.Text, PixelX = (int)circle.Center.X, PixelY = (int)circle.Center.Y, PointType = POIPointTypes.Circle, Height = (int)circle.Radius * 2, Width = (int)circle.Radius * 2 };
                                    var sss = GetCVCIEY(pOIPoint);
                                    if (Isshow)
                                        circle.Msg = "Y:" + sss.Y.ToString("F1");
                                    PoiResultCIEYData.Add(sss);
                                }
                                else if (drawAttributeBase is CircleProperties circleProperties)
                                {

                                    POIPoint pOIPoint = new POIPoint() { Name = circleProperties.Id.ToString(), PixelX = (int)circleProperties.Center.X, PixelY = (int)circleProperties.Center.Y, PointType = POIPointTypes.Circle, Height = (int)circleProperties.Radius * 2, Width = (int)circleProperties.Radius * 2 };
                                    var sss = GetCVCIEY(pOIPoint);
                                    if (Isshow)
                                        circleProperties.Msg = "Y:" + sss.Y.ToString("F1");
                                    PoiResultCIEYData.Add(sss);
                                }
                                else if (drawAttributeBase is RectangleTextProperties rectangle)
                                {
                                    POIPoint pOIPoint = new POIPoint() { Name = rectangle.Id.ToString(), PixelX = (int)(rectangle.Rect.X + rectangle.Rect.Width / 2), PixelY = (int)(rectangle.Rect.Y + rectangle.Rect.Height / 2), PointType = POIPointTypes.Rect, Height = (int)rectangle.Rect.Height, Width = (int)rectangle.Rect.Width };
                                    var sss = GetCVCIEY(pOIPoint);
                                    if (Isshow)
                                        rectangle.Msg = "Y:" + sss.Y.ToString("F1");
                                    PoiResultCIEYData.Add(sss);
                                }
                                else if (drawAttributeBase is RectangleProperties rectangleProperties)
                                {
                                    POIPoint pOIPoint = new POIPoint() { Name = rectangleProperties.Id.ToString(), PixelX = (int)(rectangleProperties.Rect.X + rectangleProperties.Rect.Width / 2), PixelY = (int)(rectangleProperties.Rect.Y + rectangleProperties.Rect.Height / 2), PointType = POIPointTypes.Rect, Height = (int)rectangleProperties.Rect.Height, Width = (int)rectangleProperties.Rect.Width };
                                    var sss = GetCVCIEY(pOIPoint);
                                    if (Isshow)
                                        rectangleProperties.Msg = "Y:" + sss.Y.ToString("F1");
                                    PoiResultCIEYData.Add(sss);
                                }
                            }

                            new WindowCVCIE(PoiResultCIEYData) { Owner = Application.Current.GetActiveWindow() }.Show();
                        }
                        else
                        {
                            ObservableCollection<PoiResultCIExyuvData> PoiResultCIExyuvDatas = new ObservableCollection<PoiResultCIExyuvData>();

                            bool Isshow = EditorContext.ImageView.DrawingVisualLists.Count < 1000;
                            foreach (var item in EditorContext.ImageView.DrawingVisualLists)
                            {
                                BaseProperties drawAttributeBase = item.BaseAttribute;
                                if (drawAttributeBase is CircleTextProperties circle)
                                {
                                    POIPoint pOIPoint = new POIPoint() { Name = circle.Text, PixelX = (int)circle.Center.X, PixelY = (int)circle.Center.Y, PointType = POIPointTypes.Circle, Height = (int)circle.Radius * 2, Width = (int)circle.Radius * 2 };
                                    var sss = GetCVCIE(pOIPoint);
                                    if (Isshow)
                                        if (CVCIEShowConfig.Instance.IsShowString)
                                            circle.Msg = FormatMessage(CVCIEShowConfig.Instance.Template, sss);

                                    PoiResultCIExyuvDatas.Add(sss);
                                }
                                else if (drawAttributeBase is CircleProperties circleProperties)
                                {
                                    POIPoint pOIPoint = new POIPoint() { Name = circleProperties.Id.ToString(), PixelX = (int)circleProperties.Center.X, PixelY = (int)circleProperties.Center.Y, PointType = POIPointTypes.Circle, Height = (int)circleProperties.Radius * 2, Width = (int)circleProperties.Radius * 2 };
                                    var sss = GetCVCIE(pOIPoint);
                                    if (Isshow)
                                        if (CVCIEShowConfig.Instance.IsShowString)
                                            circleProperties.Msg = FormatMessage(CVCIEShowConfig.Instance.Template, sss);
                                    PoiResultCIExyuvDatas.Add(sss);
                                }
                                else if (drawAttributeBase is RectangleTextProperties rectangle)
                                {
                                    POIPoint pOIPoint = new POIPoint() { Name = rectangle.Id.ToString(), PixelX = (int)(rectangle.Rect.X + rectangle.Rect.Width / 2), PixelY = (int)(rectangle.Rect.Y + rectangle.Rect.Height / 2), PointType = POIPointTypes.Rect, Height = (int)rectangle.Rect.Height, Width = (int)rectangle.Rect.Width };
                                    var sss = GetCVCIE(pOIPoint);
                                    if (Isshow)
                                        if (CVCIEShowConfig.Instance.IsShowString)
                                            rectangle.Msg = FormatMessage(CVCIEShowConfig.Instance.Template, sss);

                                    PoiResultCIExyuvDatas.Add(sss);
                                }
                                else if (drawAttributeBase is RectangleProperties rectangleProperties)
                                {
                                    POIPoint pOIPoint = new POIPoint() { Name = rectangleProperties.Id.ToString(), PixelX = (int)(rectangleProperties.Rect.X + rectangleProperties.Rect.Width / 2), PixelY = (int)(rectangleProperties.Rect.Y + rectangleProperties.Rect.Height / 2), PointType = POIPointTypes.Rect, Height = (int)rectangleProperties.Rect.Height, Width = (int)rectangleProperties.Rect.Width };
                                    var sss = GetCVCIE(pOIPoint);
                                    if (Isshow)
                                        if (CVCIEShowConfig.Instance.IsShowString)
                                            rectangleProperties.Msg = FormatMessage(CVCIEShowConfig.Instance.Template, sss);
                                    PoiResultCIExyuvDatas.Add(sss);
                                }
                            }

                            new WindowCVCIE(PoiResultCIExyuvDatas) { Owner = Application.Current.GetActiveWindow() }.Show();

                        }
                    })
                };
                menuItems.Add(menuItemMetadata);
            }
            return menuItems;
        }

        public static string FormatMessage(string template, PoiResultCIExyuvData properties)
        {
            template = template.Replace("\\n", Environment.NewLine);
            return Regex.Replace(template, @"@(\w+):([F\d]+)", match =>
            {
                var propertyName = match.Groups[1].Value;
                var format = match.Groups[2].Value;

                var propertyInfo = typeof(PoiResultCIExyuvData).GetProperty(propertyName);
                if (propertyInfo != null)
                {
                    var value = propertyInfo.GetValue(properties);
                    return string.Format($"{{0:{format}}}", value);
                }
                return match.Value;
            });
        }


        public async void OpenImage(EditorContext context, string? filePath)  
        {
            await Task.Run((Action)(() =>
            {
                CVCIEFile cVCIEFile = CVFileUtil.OpenLocalCVFile(filePath);
                context.Config.SetImageMetadata(ImageViewPropertyKeys.Rows, cVCIEFile.Rows, nameof(CVRawOpen), "当前 CVCIE 图像行数");
                context.Config.SetImageMetadata(ImageViewPropertyKeys.Cols, cVCIEFile.Cols, nameof(CVRawOpen), "当前 CVCIE 图像列数");
                context.Config.SetImageMetadata(ImageViewPropertyKeys.Channel, cVCIEFile.Channels, nameof(CVRawOpen), "当前 CVCIE 图像通道数");
                context.Config.SetImageMetadata("Gain", cVCIEFile.Gain, nameof(CVRawOpen), "CVCIE 采集增益");
                context.Config.SetImageMetadata("exp", cVCIEFile.Exp, nameof(CVRawOpen), "CVCIE 曝光数组");
                context.Config.SetImageMetadata("FileExtType", cVCIEFile.FileExtType, nameof(CVRawOpen), "CVCIE 文件扩展类型");
                context.Config.SetImageMetadata("srcFileName", cVCIEFile.SrcFileName, nameof(CVRawOpen), "CVCIE 关联源文件名");
                OpenCvSharp.Mat mat = cVCIEFile.ToMat();
                cVCIEFile.Dispose();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (context.ImageView.ViewBitmapSource is WriteableBitmap writeableBitmap)
                    {
                        if (!mat.MatUpdateWriteableBitmap(writeableBitmap))
                        {
                            WriteableBitmap writeableBitmap1 = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(mat);
                            mat.Dispose();
                            context.ImageView.OpenImage(writeableBitmap1);
                            context.ImageView.UpdateZoomAndScale();
                        }
                        else
                        {
                            context.Config.SetImageMetadata(ImageViewPropertyKeys.Depth, cVCIEFile.Bpp, nameof(CVRawOpen), "当前 CVCIE 图像位深");
                            context.Config.SetImageMetadata(ImageViewPropertyKeys.DpiX, writeableBitmap.DpiX, nameof(CVRawOpen), "当前 CVCIE 图像水平 DPI");
                            context.Config.SetImageMetadata(ImageViewPropertyKeys.DpiY, writeableBitmap.DpiY, nameof(CVRawOpen), "当前 CVCIE 图像垂直 DPI");
                            //这里需要强制切换过来
                            context.ImageView.ImageShow.Source = writeableBitmap;
                            mat.Dispose();
                        }
                    }
                    else
                    {
                        WriteableBitmap writeableBitmap1 = mat.ToWriteableBitmap();
                        context.ImageView.OpenImage(writeableBitmap1);
                        context.ImageView.UpdateZoomAndScale();
                    }
                    CVCIESetBuffer(context.ImageView, filePath);
                });
            }));


        }
    }
}
