#pragma warning disable CS8625
using ColorVision.Common.Utilities;
using ColorVision.Engine.Draw;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.POI;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.Themes.Controls;
using cvColorVision;
using CVCommCore.CVAlgorithm;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Media
{
    public class PoiImageViewComponent : IImageViewComponent
    {
        public ImageView ImageView { get; set; }

        public void Execute(ImageView imageView)
        {
            ImageView = imageView;
            if (MySqlControl.GetInstance().IsConnect)
            {
                ImageView.ComboxPOITemplate.ItemsSource = TemplatePoi.Params.CreateEmpty();
                ImageView.ComboxPOITemplate.SelectedIndex = 0;
                ImageView.ToolBarAl.Visibility = Visibility.Visible;
                
            }
            else
            {
                Task.Run(() => LoadMysql(imageView));
            }
            ImageView.ComboxPOITemplate.SelectionChanged += ComboxPOITemplate_SelectionChanged;
            ImageView.ButtonCalculPOI.Click += CalculPOI_Click;
        }

        public async Task LoadMysql(ImageView imageView)
        {
            await Task.Delay(100);
            await MySqlControl.GetInstance().Connect();
            if (MySqlControl.GetInstance().IsConnect)
            {
                new TemplatePoi().Load();
                imageView.ComboxPOITemplate.ItemsSource = TemplatePoi.Params.CreateEmpty();
                imageView.ComboxPOITemplate.SelectedIndex = 0;
                ImageView.ToolBarAl.Visibility = Visibility.Visible;
            }
        }
        private void ComboxPOITemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedValue is PoiParam poiParams)
            {
                ImageView.ImageShow.Clear();
                ImageView.DrawingVisualLists.Clear();

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
                            ImageView.ImageShow.AddVisual(Circle);
                            break;
                        case RiPointTypes.Rect:
                            DVRectangleText Rectangle = new();
                            Rectangle.Attribute.Rect = new Rect(item.PixX - item.PixWidth / 2, item.PixY - item.PixHeight / 2, item.PixWidth, item.PixHeight);
                            Rectangle.Attribute.Brush = Brushes.Transparent;
                            Rectangle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Rectangle.Attribute.Id = item.Id;
                            Rectangle.Attribute.Text = item.Name;
                            Rectangle.Render();
                            ImageView.ImageShow.AddVisual(Rectangle);
                            break;
                        case RiPointTypes.Mask:
                            break;
                    }
                }
            }
        }


        private void CalculPOI_Click(object sender, RoutedEventArgs e)
        {
            if (!ImageView.Config.IsCVCIE)
            {
                MessageBox1.Show("仅对CVCIE图像支持");
                return;
            }
            if (ImageView.ComboxPOITemplate.SelectedValue is not PoiParam poiParams)
            {
                MessageBox1.Show("需要配置关注点");
                return;
            }


            ObservableCollection<PoiResultCIExyuvData> PoiResultCIExyuvDatas = new ObservableCollection<PoiResultCIExyuvData>();
            int result = ConvertXYZ.CM_SetFilter(ImageView.Config.ConvertXYZhandle, poiParams.PoiConfig.Filter.Enable, poiParams.PoiConfig.Filter.Threshold);
            result = ConvertXYZ.CM_SetFilterNoArea(ImageView.Config.ConvertXYZhandle, poiParams.PoiConfig.Filter.NoAreaEnable, poiParams.PoiConfig.Filter.Threshold);
            result = ConvertXYZ.CM_SetFilterXYZ(ImageView.Config.ConvertXYZhandle, poiParams.PoiConfig.Filter.XYZEnable, (int)poiParams.PoiConfig.Filter.XYZType, poiParams.PoiConfig.Filter.Threshold);

            poiParams.PoiPoints.Clear();
            foreach (var item in ImageView.DrawingVisualLists)
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
                        Tag = circle.Tag,
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
                        Tag = circleProperties.Tag,
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
                        Tag = rectangle.Tag,
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
                        Tag = rectangleProperties.Tag,
                    };
                    poiParams.PoiPoints.Add(poiParamData);
                }
            }



            foreach (var item in poiParams.PoiPoints)
            {
                POIPoint pOIPoint = new POIPoint() { Id = item.Id, Name = item.Name, PixelX = (int)item.PixX, PixelY = (int)item.PixY, PointType = (POIPointTypes)item.PointType, Height = (int)item.PixHeight, Width = (int)item.PixWidth };
                var sss = GetCVCIE(pOIPoint);
                PoiResultCIExyuvDatas.Add(sss);
            }


            WindowCVCIE windowCIE = new WindowCVCIE(PoiResultCIExyuvDatas) { Owner = Application.Current.GetActiveWindow() };
            windowCIE.Show();
        }


        public PoiResultCIExyuvData GetCVCIE(POIPoint pOIPoint)
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
                    _ = ConvertXYZ.CM_GetXYZxyuvCircle(ImageView.Config.ConvertXYZhandle, x, y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, 1);
                    break;
                case POIPointTypes.Circle:
                    _ = ConvertXYZ.CM_GetXYZxyuvCircle(ImageView.Config.ConvertXYZhandle, x, y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, (int)(rect / 2));
                    break;
                case POIPointTypes.Rect:
                    _ = ConvertXYZ.CM_GetXYZxyuvRect(ImageView.Config.ConvertXYZhandle, x, y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, rect, rect2);
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

            int i = ConvertXYZ.CM_GetxyuvCCTWaveCircle(ImageView.Config.ConvertXYZhandle, x, y, ref dx, ref dy, ref du, ref dv, ref CCT, ref Wave, (int)(rect / 2));
            poiResultCIExyuvData.CCT = CCT;
            poiResultCIExyuvData.Wave = Wave;

            return poiResultCIExyuvData;
        }


    }
}
