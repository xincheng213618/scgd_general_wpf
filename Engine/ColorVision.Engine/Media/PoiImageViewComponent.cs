#pragma warning disable CS8625
using ColorVision.Engine.Media;
using ColorVision.Database;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.FileIO;
using ColorVision.Themes.Controls;
using cvColorVision;
using CVCommCore.CVAlgorithm;
using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ColorVision.ImageEditor.Draw.Special;

namespace ColorVision.Engine.Templates.POI.Image
{
    public class PoiImageViewComponent : IImageComponent
    {
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


        public void Execute(ImageView imageView)
        {
            imageView.ToolBarAl.Visibility  = Visibility.Visible;
            if (MySqlControl.GetInstance().IsConnect)
            {
                imageView.ComboxPOITemplate.ItemsSource = TemplatePoi.Params.CreateEmpty();
                imageView.ComboxPOITemplate.SelectedIndex = 0;
                imageView.ToolBarAl.Visibility = Visibility.Visible;   
            }
            else
            {
                Task.Run(() => LoadMysql(imageView));
            }
            imageView.ComboxPOITemplate.SelectionChanged += ComboxPOITemplate_SelectionChanged;
            imageView.ButtonCalculPOI.Click += CalculPOI_Click;

            async Task LoadMysql(ImageView imageView)
            {
                if (MySqlControl.GetInstance().IsConnect)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        new TemplatePoi().Load();
                        imageView.ComboxPOITemplate.ItemsSource = TemplatePoi.Params.CreateEmpty();
                        imageView.ComboxPOITemplate.SelectedIndex = 0;
                        imageView.ToolBarAl.Visibility = Visibility.Visible;
                    });
                }
                else
                {
                    await Task.Delay(100);
                    await MySqlControl.GetInstance().Connect();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (MySqlControl.GetInstance().IsConnect)
                        {
                            new TemplatePoi().Load();
                            imageView.ComboxPOITemplate.ItemsSource = TemplatePoi.Params.CreateEmpty();
                            imageView.ComboxPOITemplate.SelectedIndex = 0;
                            imageView.ToolBarAl.Visibility = Visibility.Visible;
                        }
                    });
                }


            }

            void ComboxPOITemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
            {
                if (sender is ComboBox comboBox && comboBox.SelectedValue is PoiParam poiParams)
                {
                    imageView.ImageShow.Clear();
                    imageView.DrawingVisualLists.Clear();

                    if (poiParams.Id == -1) return;

                    PoiParam.LoadPoiDetailFromDB(poiParams);
                    foreach (var item in poiParams.PoiPoints)
                    {
                        switch (item.PointType)
                        {
                            case GraphicTypes.Circle:
                                DVCircleText Circle = new();
                                Circle.Attribute.Center = new Point(item.PixX, item.PixY);
                                Circle.Attribute.Radius = item.PixHeight / 2;
                                Circle.Attribute.Brush = Brushes.Transparent;
                                Circle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                                Circle.Attribute.Id = item.Id;
                                Circle.Attribute.Text = item.Name;
                                Circle.Render();
                                imageView.ImageShow.AddVisualCommand(Circle);
                                break;
                            case GraphicTypes.Rect:
                                DVRectangleText Rectangle = new();
                                Rectangle.Attribute.Rect = new Rect(item.PixX - item.PixWidth / 2, item.PixY - item.PixHeight / 2, item.PixWidth, item.PixHeight);
                                Rectangle.Attribute.Brush = Brushes.Transparent;
                                Rectangle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                                Rectangle.Attribute.Id = item.Id;
                                Rectangle.Attribute.Text = item.Name;
                                Rectangle.Render();
                                imageView.ImageShow.AddVisualCommand(Rectangle);
                                break;
                            case GraphicTypes.Quadrilateral:
                                break;
                        }
                    }
                }
            }


            void CalculPOI_Click(object sender, RoutedEventArgs e)
            {
                if (!imageView.Config.GetProperties<bool>("IsCVCIE"))
                {
                    MessageBox1.Show("仅对CVCIE图像支持");
                    return;
                }
                if (!(CVFileUtil.IsCIEFile(imageView.Config.FilePath) && CVFileUtil.ReadCIEFileHeader(imageView.Config.FilePath, out CVCIEFile cVCIEFile) > 0 && cVCIEFile.FileExtType == CVType.CIE)) 
                {
                    MessageBox1.Show("仅对CVCIE图像支持");
                    return;
                }
                if (imageView.ComboxPOITemplate.SelectedValue is not PoiParam poiParams)
                {
                    MessageBox1.Show("需要配置关注点");
                    return;
                }

                if (!imageView.Config.GetProperties<bool>("IsBufferSet"))
                {
                    Action action = imageView.Config.GetProperties<Action>("LoadBuffer");
                    action?.Invoke();
                }


                int result = ConvertXYZ.CM_SetFilter(imageView.Config.ConvertXYZhandle, poiParams.PoiConfig.Filter.Enable, poiParams.PoiConfig.Filter.Threshold);
                result = ConvertXYZ.CM_SetFilterNoArea(imageView.Config.ConvertXYZhandle, poiParams.PoiConfig.Filter.NoAreaEnable, poiParams.PoiConfig.Filter.Threshold);
                result = ConvertXYZ.CM_SetFilterXYZ(imageView.Config.ConvertXYZhandle, poiParams.PoiConfig.Filter.XYZEnable, (int)poiParams.PoiConfig.Filter.XYZType, poiParams.PoiConfig.Filter.Threshold);


                if (imageView.Config.GetProperties<float[]>("Exp") is float[] exp && exp.Length == 1)
                {
                    ObservableCollection<PoiResultCIEYData> PoiResultCIEYData = new ObservableCollection<PoiResultCIEYData>();

                    bool Isshow = imageView.DrawingVisualLists.Count < 1000;
                    foreach (var item in imageView.DrawingVisualLists)
                    {
                        BaseProperties drawAttributeBase = item.BaseAttribute;
                        if (drawAttributeBase is CircleTextProperties circle)
                        {
                            POIPoint pOIPoint = new POIPoint() { Name = circle.Text, PixelX = (int)circle.Center.X, PixelY = (int)circle.Center.Y, PointType = POIPointTypes.Circle, Height = (int)circle.Radius*2, Width = (int)circle.Radius*2 };
                            var sss = GetCVCIEY(pOIPoint);
                            if (Isshow)
                                circle.Msg = "Y:"+ sss.Y.ToString("F1");
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

                    bool Isshow = imageView.DrawingVisualLists.Count < 1000;
                    foreach (var item in imageView.DrawingVisualLists)
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
                                circleProperties.Msg = FormatMessage(CVCIEShowConfig.Instance.Template,sss);
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

            }

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
                        _ = ConvertXYZ.CM_GetYCircle(imageView.Config.ConvertXYZhandle, x, y, ref dYVal, 1);
                        break;
                    case POIPointTypes.Circle:
                        _ = ConvertXYZ.CM_GetYCircle(imageView.Config.ConvertXYZhandle, x, y, ref dYVal, rect / 2);
                        break;
                    case POIPointTypes.Rect:
                        _ = ConvertXYZ.CM_GetYRect(imageView.Config.ConvertXYZhandle, x, y, ref dYVal, rect, rect2);
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
                        _ = ConvertXYZ.CM_GetXYZxyuvCircle(imageView.Config.ConvertXYZhandle, x, y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, 1);
                        break;
                    case POIPointTypes.Circle:
                        _ = ConvertXYZ.CM_GetXYZxyuvCircle(imageView.Config.ConvertXYZhandle, x, y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, rect / 2);
                        break;
                    case POIPointTypes.Rect:
                        _ = ConvertXYZ.CM_GetXYZxyuvRect(imageView.Config.ConvertXYZhandle, x, y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, rect, rect2);
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

                int i = ConvertXYZ.CM_GetxyuvCCTWaveCircle(imageView.Config.ConvertXYZhandle, x, y, ref dx, ref dy, ref du, ref dv, ref CCT, ref Wave, rect / 2);
                poiResultCIExyuvData.CCT = CCT;
                poiResultCIExyuvData.Wave = Wave;

                return poiResultCIExyuvData;
            }


            WindowCIE windowCIE = null;

            void ButtonCIE1931_Click(object sender, RoutedEventArgs e)
            {
                imageView.ImageViewModel.ShowImageInfo = true;

                if (windowCIE == null)
                {
                    windowCIE = new WindowCIE() { Owner = Application.Current.GetActiveWindow() };
                    void mouseMoveColorHandler(object s, ImageInfo e)
                    {
                        if (imageView.Config.Properties.TryGetValue("IsCVCIE", out object obj)&& obj is  bool iscvice &&iscvice)
                        {
                            int xx = e.X;
                            int yy = e.Y;
                            float dXVal = 0;
                            float dYVal = 0;
                            float dZVal = 0;
                            float dx = 0, dy = 0, du = 0, dv = 0;
                            int result = ConvertXYZ.CM_GetXYZxyuvRect(imageView.Config.ConvertXYZhandle, xx, yy, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, (int)imageView.ImageViewModel.MouseMagnifier.RectWidth, (int)imageView.ImageViewModel.MouseMagnifier.RectHeight);
                            
                            windowCIE.ChangeSelect(dx, dy);
                        }
                        else
                        {
                            windowCIE.ChangeSelect(e);
                        }
                    }

                    imageView.ImageViewModel.MouseMagnifier.MouseMoveColorHandler += mouseMoveColorHandler;
                    windowCIE.Closed += (s, e) =>
                    {
                        imageView.ImageViewModel.MouseMagnifier.MouseMoveColorHandler -= mouseMoveColorHandler;
                        imageView.ImageViewModel.ShowImageInfo = false;
                        windowCIE = null;
                    };
                }
                windowCIE.Show();
                windowCIE.Activate();
            }

            imageView.Button1931.Click += ButtonCIE1931_Click;             
        }

    }
}
