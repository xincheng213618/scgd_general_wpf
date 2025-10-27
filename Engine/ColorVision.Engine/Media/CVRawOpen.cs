using ColorVision.Common.MVVM;
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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Media
{

    internal static class Extension
    {
        internal static void ClearEventInvocations(this object obj, string eventName)
        {
            var fi = obj.GetType().GetEventField(eventName);
            if (fi == null) return;
            fi.SetValue(obj, null);
        }

        private static FieldInfo? GetEventField(this Type type, string eventName)
        {
            FieldInfo field = null;
            while (type != null)
            {
                /* Find events defined as field */
                field = type.GetField(eventName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null && (field.FieldType == typeof(MulticastDelegate) || field.FieldType.IsSubclassOf(typeof(MulticastDelegate))))
                    break;

                /* Find events defined as property { add; remove; } */
                field = type.GetField("EVENT_" + eventName.ToUpper(System.Globalization.CultureInfo.CurrentCulture), BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                    break;
                type = type.BaseType;
            }
            return field;
        }
    }

    [FileExtension(".cvraw|.cvcie")]
    public record class CVRawOpen(EditorContext EditorContext) : IImageOpen, IIEditorToolContextMenu
    {
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
        public void CVCIESetBuffer(ImageView imageView,string filePath)
        {
            CVCIEFile meta;
            int index;


            Action LoadBuffer = new Action(() =>
            {
                if (imageView.Config.GetProperties<bool>("IsBufferSet")) return;
                var meta = imageView.Config.GetProperties<CVCIEFile>("meta");
                int index = imageView.Config.GetProperties<int>("index");
                CVFileUtil.ReadCIEFileData(filePath, ref meta, index);
                int resultCM_SetBufferXYZ = ConvertXYZ.CM_SetBufferXYZ(imageView.Config.ConvertXYZhandle, (uint)meta.rows, (uint)meta.cols, (uint)meta.bpp, (uint)meta.channels, meta.data);
                log.Debug($"CM_SetBufferXYZ :{resultCM_SetBufferXYZ}");
                imageView.Config.AddProperties("IsBufferSet", true);

            });

            imageView.Config.AddProperties("LoadBuffer", LoadBuffer);


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
            void ShowCVCIE(object sender, ImageInfo imageInfo)
            {
                if (!imageView.Config.GetProperties<bool>("IsBufferSet"))
                {
                    Action action = imageView.Config.GetProperties<Action>("LoadBuffer");
                    action?.Invoke();
                }

                float dXVal = 0;
                float dYVal = 0;
                float dZVal = 0;
                float dx = 0, dy = 0, du = 0, dv = 0;
                var (x2, y2) = FindNearbyPoints(imageInfo.X, imageInfo.Y);
                //要从1,1开始
                x2 += 1;
                y2 += 1;
                switch (imageView.ImageViewModel.MouseMagnifier.MagnigifierType)
                {
                    case MagnigifierType.Circle:
                        if (exp.Length == 1)
                        {
                            int ret = ConvertXYZ.CM_GetYCircle(imageView.Config.ConvertXYZhandle, imageInfo.X, imageInfo.Y, ref dYVal, imageView.ImageViewModel.MouseMagnifier.Radius);
                            string text1 = $"Y:{dYVal:F1}";
                            string text2 = $"";
                            imageView.ImageViewModel.MouseMagnifier.DrawImage(imageInfo, text1, text2);
                        }
                        else
                        {

                            int ret = ConvertXYZ.CM_GetXYZxyuvCircle(imageView.Config.ConvertXYZhandle, imageInfo.X, imageInfo.Y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, imageView.ImageViewModel.MouseMagnifier.Radius);
                            string text1;
                            if (ShowDateFilePath)
                                text1 = $"X:{dXVal:F1},Y:{dYVal:F1},Z:{dZVal:F1},({x2},{y2})";
                            else
                                text1 = $"X:{dXVal:F1},Y:{dYVal:F1},Z:{dZVal:F1}";

                            string text2 = $"x:{dx:F2},y:{dy:F2},u:{du:F2},v:{dv:F2}";
                            imageView.ImageViewModel.MouseMagnifier.DrawImage(imageInfo, text1, text2);
                        }

                        break;
                    case MagnigifierType.Rect:
                        if (exp.Length == 1)
                        {
                            int ret = ConvertXYZ.CM_GetYRect(imageView.Config.ConvertXYZhandle, imageInfo.X, imageInfo.Y, ref dYVal, (int)imageView.ImageViewModel.MouseMagnifier.RectWidth, (int)imageView.ImageViewModel.MouseMagnifier.RectHeight);
                            string text1 = $"Y:{dYVal:F1}";
                            string text2 = $"";
                            imageView.ImageViewModel.MouseMagnifier.DrawImage(imageInfo, text1, text2);
                        }
                        else
                        {
                            int ret = ConvertXYZ.CM_GetXYZxyuvRect(imageView.Config.ConvertXYZhandle, imageInfo.X, imageInfo.Y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, (int)imageView.ImageViewModel.MouseMagnifier.RectWidth, (int)imageView.ImageViewModel.MouseMagnifier.RectHeight);
                            string text1;
                            if (ShowDateFilePath)
                                text1 = $"X:{dXVal:F1},Y:{dYVal:F1},Z:{dZVal:F1},({x2},{y2})";
                            else
                                text1 = $"X:{dXVal:F1},Y:{dYVal:F1},Z:{dZVal:F1}";

                            string text2 = $"x:{dx:F2},y:{dy:F2},u:{du:F2},v:{dv:F2}";
                            imageView.ImageViewModel.MouseMagnifier.DrawImage(imageInfo, text1, text2);
                        }

                        break;
                    default:
                        break;
                }


            }

            imageView.ImageViewModel.MouseMagnifier.ClearEventInvocations("MouseMoveColorHandler");

            imageView.ClearImageEventHandler += (s, e) =>
            {
                int result = ConvertXYZ.CM_ReleaseBuffer(imageView.Config.ConvertXYZhandle);
                imageView.Config.AddProperties("IsBufferSet", false);
            };
            if (!imageView.Config.ConvertXYZhandleOnce)
            {
                int result = ConvertXYZ.CM_InitXYZ(imageView.Config.ConvertXYZhandle);
                log.Info($"ConvertXYZ.CM_InitXYZ :{result}");
                imageView.Config.ConvertXYZhandleOnce = true;
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
                        imageView.OpenImage(CVFileUtil.OpenLocalFileChannel(imageView.Config.FilePath, CVImageChannelType.CIE_XYZ_X).ToWriteableBitmap());
                    }
                    if (layer == "Y")
                    {
                        imageView.OpenImage(CVFileUtil.OpenLocalFileChannel(imageView.Config.FilePath, CVImageChannelType.CIE_XYZ_Y).ToWriteableBitmap());
                    }
                    if (layer == "Z")
                    {
                        imageView.OpenImage(CVFileUtil.OpenLocalFileChannel(imageView.Config.FilePath, CVImageChannelType.CIE_XYZ_Z).ToWriteableBitmap());

                    }
                }
            }

            if (File.Exists(filePath) && CVFileUtil.IsCIEFile(filePath))
            {
                 index = CVFileUtil.ReadCIEFileHeader(imageView.Config.FilePath, out meta);
                if (index <= 0) return;
                if (meta.FileExtType == CVType.CIE)
                {
                    log.Debug(JsonConvert.SerializeObject(meta));
                    imageView.Config.AddProperties("IsCVCIE", true);

                    imageView.Config.AddProperties("meta", meta);
                    imageView.Config.AddProperties("index", index);
                    imageView.Config.AddProperties("Exp", meta.exp);

                    imageView.Config.AddProperties("IsBufferSet",false);
                    exp = meta.exp;

                    imageView.ImageViewModel.MouseMagnifier.MouseMoveColorHandler += ShowCVCIE;

                    if (meta.srcFileName !=null && !File.Exists(meta.srcFileName))
                        meta.srcFileName = Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, meta.srcFileName);

                    if (meta.channels ==3)
                    {
                        if (File.Exists(meta.srcFileName))
                        {
                            ComboBoxLayerItems = new List<string>() { "Src", "R", "G", "B", "X", "Y", "Z" };
                        }
                        else
                        {
                            ComboBoxLayerItems = new List<string>() { "Src", "X", "Y", "Z" };
                        }
                    }
                    else if (meta.channels == 1)
                    {
                        if (File.Exists(meta.srcFileName))
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
                    if (meta.channels == 3)
                    {
                        ComboBoxLayerItems = new List<string>() { "Src", "R", "G", "B" };
                    }
                    else if (meta.channels == 1)
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
                        _ = ConvertXYZ.CM_GetYCircle(EditorContext.Config.ConvertXYZhandle, x, y, ref dYVal, 1);
                        break;
                    case POIPointTypes.Circle:
                        _ = ConvertXYZ.CM_GetYCircle(EditorContext.Config.ConvertXYZhandle, x, y, ref dYVal, rect / 2);
                        break;
                    case POIPointTypes.Rect:
                        _ = ConvertXYZ.CM_GetYRect(EditorContext.Config.ConvertXYZhandle, x, y, ref dYVal, rect, rect2);
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
                        _ = ConvertXYZ.CM_GetXYZxyuvCircle(EditorContext.Config.ConvertXYZhandle, x, y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, 1);
                        break;
                    case POIPointTypes.Circle:
                        _ = ConvertXYZ.CM_GetXYZxyuvCircle(EditorContext.Config.ConvertXYZhandle, x, y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, rect / 2);
                        break;
                    case POIPointTypes.Rect:
                        _ = ConvertXYZ.CM_GetXYZxyuvRect(EditorContext.Config.ConvertXYZhandle, x, y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, rect, rect2);
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

                int i = ConvertXYZ.CM_GetxyuvCCTWaveCircle(EditorContext.Config.ConvertXYZhandle, x, y, ref dx, ref dy, ref du, ref dv, ref CCT, ref Wave, rect / 2);
                poiResultCIExyuvData.CCT = CCT;
                poiResultCIExyuvData.Wave = Wave;

                return poiResultCIExyuvData;
            }


            List<MenuItemMetadata> menuItems = new List<MenuItemMetadata>();
            menuItems.Add(new MenuItemMetadata()
            {
                Header = "导出",
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

                        if (EditorContext.ImageView.ComboxPOITemplate.SelectedValue is not PoiParam poiParams)
                        {
                            MessageBox1.Show("需要配置关注点");
                            return;
                        }

                        if (!EditorContext.Config.GetProperties<bool>("IsBufferSet"))
                        {
                            Action action = EditorContext.Config.GetProperties<Action>("LoadBuffer");
                            action?.Invoke();
                        }


                        int result = ConvertXYZ.CM_SetFilter(EditorContext.Config.ConvertXYZhandle, poiParams.PoiConfig.Filter.Enable, poiParams.PoiConfig.Filter.Threshold);
                        result = ConvertXYZ.CM_SetFilterNoArea(EditorContext.Config.ConvertXYZhandle, poiParams.PoiConfig.Filter.NoAreaEnable, poiParams.PoiConfig.Filter.Threshold);
                        result = ConvertXYZ.CM_SetFilterXYZ(EditorContext.Config.ConvertXYZhandle, poiParams.PoiConfig.Filter.XYZEnable, (int)poiParams.PoiConfig.Filter.XYZType, poiParams.PoiConfig.Filter.Threshold);


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
            if (filePath == null) return;
            CVCIESetBuffer(context.ImageView, filePath);

            try
            {
                await Task.Run(() =>
                {
                    CVCIEFile cVCIEFile = CVFileUtil.OpenLocalCVFile(filePath);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        context.ImageView.OpenImage(cVCIEFile.ToWriteableBitmap());
                        context.ImageView.UpdateZoomAndScale();
                    });
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }
    }
}
