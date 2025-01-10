#pragma warning disable CS8625
using ColorVision.Engine.Media;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.Themes.Controls;
using ColorVision.Util.Draw.Special;
using cvColorVision;
using CVCommCore.CVAlgorithm;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.POI.Image
{
    public class PoiImageViewComponent : IImageViewComponent
    {
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
                            case RiPointTypes.Circle:
                                DVCircleText Circle = new();
                                Circle.Attribute.Center = new Point(item.PixX, item.PixY);
                                Circle.Attribute.Radius = item.PixHeight / 2;
                                Circle.Attribute.Brush = Brushes.Transparent;
                                Circle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                                Circle.Attribute.Id = item.Id;
                                Circle.Attribute.Text = item.Name;
                                Circle.Render();
                                imageView.ImageShow.AddVisual(Circle);
                                break;
                            case RiPointTypes.Rect:
                                DVRectangleText Rectangle = new();
                                Rectangle.Attribute.Rect = new Rect(item.PixX - item.PixWidth / 2, item.PixY - item.PixHeight / 2, item.PixWidth, item.PixHeight);
                                Rectangle.Attribute.Brush = Brushes.Transparent;
                                Rectangle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                                Rectangle.Attribute.Id = item.Id;
                                Rectangle.Attribute.Text = item.Name;
                                Rectangle.Render();
                                imageView.ImageShow.AddVisual(Rectangle);
                                break;
                            case RiPointTypes.Mask:
                                break;
                        }
                    }
                }
            }


            void CalculPOI_Click(object sender, RoutedEventArgs e)
            {
                if (!imageView.Config.GetProperties<bool>("IsCVCIE") == true)
                {
                    MessageBox1.Show("仅对CVCIE图像支持");
                    return;
                }
                if (imageView.ComboxPOITemplate.SelectedValue is not PoiParam poiParams)
                {
                    MessageBox1.Show("需要配置关注点");
                    return;
                }


                int result = ConvertXYZ.CM_SetFilter(imageView.Config.ConvertXYZhandle, poiParams.PoiConfig.Filter.Enable, poiParams.PoiConfig.Filter.Threshold);
                result = ConvertXYZ.CM_SetFilterNoArea(imageView.Config.ConvertXYZhandle, poiParams.PoiConfig.Filter.NoAreaEnable, poiParams.PoiConfig.Filter.Threshold);
                result = ConvertXYZ.CM_SetFilterXYZ(imageView.Config.ConvertXYZhandle, poiParams.PoiConfig.Filter.XYZEnable, (int)poiParams.PoiConfig.Filter.XYZType, poiParams.PoiConfig.Filter.Threshold);

                poiParams.PoiPoints.Clear();
                foreach (var item in imageView.DrawingVisualLists)
                {
                    BaseProperties drawAttributeBase = item.BaseAttribute;
                    if (drawAttributeBase is CircleTextProperties circle)
                    {
                        PoiPoint poiParamData = new PoiPoint()
                        {
                            PointType = RiPointTypes.Circle,
                            PixX = circle.Center.X,
                            PixY = circle.Center.Y,
                            PixWidth = circle.Radius * 2,
                            PixHeight = circle.Radius * 2,
                            Name = circle.Text
                        };
                        poiParams.PoiPoints.Add(poiParamData);
                    }
                    else if (drawAttributeBase is CircleProperties circleProperties)
                    {
                        PoiPoint poiParamData = new PoiPoint()
                        {
                            PointType = RiPointTypes.Circle,
                            PixX = circleProperties.Center.X,
                            PixY = circleProperties.Center.Y,
                            PixWidth = circleProperties.Radius * 2,
                            PixHeight = circleProperties.Radius * 2,
                            Name = circleProperties.Id.ToString()
                        };
                        poiParams.PoiPoints.Add(poiParamData);
                    }
                    else if (drawAttributeBase is RectangleTextProperties rectangle)
                    {
                        PoiPoint poiParamData = new()
                        {
                            Name = rectangle.Text,
                            PointType = RiPointTypes.Rect,
                            PixX = rectangle.Rect.X + rectangle.Rect.Width / 2,
                            PixY = rectangle.Rect.Y + rectangle.Rect.Height / 2,
                            PixWidth = rectangle.Rect.Width,
                            PixHeight = rectangle.Rect.Height,
                        };
                        poiParams.PoiPoints.Add(poiParamData);
                    }
                    else if (drawAttributeBase is RectangleProperties rectangleProperties)
                    {
                        PoiPoint poiParamData = new PoiPoint()
                        {
                            PointType = RiPointTypes.Rect,
                            PixX = rectangleProperties.Rect.X + rectangleProperties.Rect.Width / 2,
                            PixY = rectangleProperties.Rect.Y + rectangleProperties.Rect.Height / 2,
                            PixWidth = rectangleProperties.Rect.Width,
                            PixHeight = rectangleProperties.Rect.Height,
                        };
                        poiParams.PoiPoints.Add(poiParamData);
                    }
                }

                if (imageView.Config.GetProperties<float[]>("Exp") is float[] exp && exp.Length == 1)
                {
                    ObservableCollection<PoiResultCIEYData> PoiResultCIEYData = new ObservableCollection<PoiResultCIEYData>();

                    foreach (var item in poiParams.PoiPoints)
                    {
                        POIPoint pOIPoint = new POIPoint() { Id = item.Id, Name = item.Name, PixelX = (int)item.PixX, PixelY = (int)item.PixY, PointType = (POIPointTypes)item.PointType, Height = (int)item.PixHeight, Width = (int)item.PixWidth };
                        var sss = GetCVCIEY(pOIPoint);
                        PoiResultCIEYData.Add(sss);
                    }
                    new WindowCVCIE(PoiResultCIEYData) { Owner = Application.Current.GetActiveWindow() }.Show();
                }
                else
                {
                    ObservableCollection<PoiResultCIExyuvData> PoiResultCIExyuvDatas = new ObservableCollection<PoiResultCIExyuvData>();


                    foreach (var item in poiParams.PoiPoints)
                    {
                        POIPoint pOIPoint = new POIPoint() { Id = item.Id, Name = item.Name, PixelX = (int)item.PixX, PixelY = (int)item.PixY, PointType = (POIPointTypes)item.PointType, Height = (int)item.PixHeight, Width = (int)item.PixWidth };
                        var sss = GetCVCIE(pOIPoint);
                        PoiResultCIExyuvDatas.Add(sss);
                    }
                    new WindowCVCIE(PoiResultCIExyuvDatas) { Owner = Application.Current.GetActiveWindow() }.Show();

                }

            }

            PoiResultCIEYData GetCVCIEY(POIPoint poiPoint)
            {
                int x = poiPoint.PixelX; int y = poiPoint.PixelY; int rect = poiPoint.Width; int rect2 = poiPoint.Height;
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
                int x = pOIPoint.PixelX; int y = pOIPoint.PixelY; int rect = pOIPoint.Width; int rect2 = pOIPoint.Height;
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
                imageView.ImageEditViewMode.ShowImageInfo = true;

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
                            int result = ConvertXYZ.CM_GetXYZxyuvRect(imageView.Config.ConvertXYZhandle, xx, yy, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, imageView.Config.CVCIENum, imageView.Config.CVCIENum);
                            
                            windowCIE.ChangeSelect(dx, dy);
                        }
                        else
                        {
                            windowCIE.ChangeSelect(e);
                        }
                    }

                    imageView.ImageEditViewMode.MouseMagnifier.MouseMoveColorHandler += mouseMoveColorHandler;
                    windowCIE.Closed += (s, e) =>
                    {
                        imageView.ImageEditViewMode.MouseMagnifier.MouseMoveColorHandler -= mouseMoveColorHandler;
                        imageView.ImageEditViewMode.ShowImageInfo = false;
                        windowCIE = null;
                    };
                }
                windowCIE.Show();
                windowCIE.Activate();
            }

            imageView.Button1931.Click += ButtonCIE1931_Click;


            void CalculSFR()
            {
                if (imageView.ComboxPOITemplate.SelectedValue is not PoiParam poiParams)
                {
                    MessageBox1.Show("需要配置关注点");
                    return;
                }
                foreach (var item in imageView.DrawingVisualLists)
                {
                    BaseProperties drawAttributeBase = item.BaseAttribute;
                    if (drawAttributeBase is CircleTextProperties circle)
                    {
                        PoiPoint poiParamData = new PoiPoint()
                        {
                            PointType = RiPointTypes.Circle,
                            PixX = circle.Center.X,
                            PixY = circle.Center.Y,
                            PixWidth = circle.Radius * 2,
                            PixHeight = circle.Radius * 2,
                            Name = circle.Text
                        };
                        poiParams.PoiPoints.Add(poiParamData);
                    }
                    else if (drawAttributeBase is CircleProperties circleProperties)
                    {
                        PoiPoint poiParamData = new PoiPoint()
                        {
                            PointType = RiPointTypes.Circle,
                            PixX = circleProperties.Center.X,
                            PixY = circleProperties.Center.Y,
                            PixWidth = circleProperties.Radius * 2,
                            PixHeight = circleProperties.Radius * 2,
                            Name = circleProperties.Id.ToString()
                        };
                        poiParams.PoiPoints.Add(poiParamData);
                    }
                    else if (drawAttributeBase is RectangleTextProperties rectangle)
                    {
                        PoiPoint poiParamData = new()
                        {
                            Name = rectangle.Text,
                            PointType = RiPointTypes.Rect,
                            PixX = rectangle.Rect.X + rectangle.Rect.Width / 2,
                            PixY = rectangle.Rect.Y + rectangle.Rect.Height / 2,
                            PixWidth = rectangle.Rect.Width,
                            PixHeight = rectangle.Rect.Height,
                        };
                        poiParams.PoiPoints.Add(poiParamData);
                    }
                    else if (drawAttributeBase is RectangleProperties rectangleProperties)
                    {
                        PoiPoint poiParamData = new PoiPoint()
                        {
                            PointType = RiPointTypes.Rect,
                            PixX = rectangleProperties.Rect.X + rectangleProperties.Rect.Width / 2,
                            PixY = rectangleProperties.Rect.Y + rectangleProperties.Rect.Height / 2,
                            PixWidth = rectangleProperties.Rect.Width,
                            PixHeight = rectangleProperties.Rect.Height,
                        };
                        poiParams.PoiPoints.Add(poiParamData);
                    }
                }


                cvColorVision.HImage ToCHImage(HImage hImage)
                {
                    cvColorVision.HImage image = new cvColorVision.HImage() { nWidth = (uint)hImage.cols, nHeight = (uint)hImage.rows };
                    image.pData = hImage.pData;
                    image.nBpp = (uint)hImage.depth;
                    return image;
                };

                foreach (var item in poiParams.PoiPoints)
                {
                    POIPoint pOIPoint = new POIPoint() { Id = item.Id, Name = item.Name, PixelX = (int)item.PixX, PixelY = (int)item.PixY, PointType = (POIPointTypes)item.PointType, Height = (int)item.PixHeight, Width = (int)item.PixWidth };

                    float[] pdfrequency = new float[10];
                    float[] pdomainSamplingData = new float[10];
                    int  i = cvCameraCSLib.SFRCalculation(ToCHImage((HImage)imageView.HImageCache),new CRECT() { x = pOIPoint.PixelX, y = pOIPoint.PixelY,cx = pOIPoint.Width,cy = pOIPoint.Height }, 1,pdfrequency, pdomainSamplingData,10);
                }


            }
             
        }

    }
}
