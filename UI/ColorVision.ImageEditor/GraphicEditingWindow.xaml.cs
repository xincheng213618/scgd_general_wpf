using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.Util.Draw.Rectangle;
using Gu.Wpf.Geometry;
using Newtonsoft.Json;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.ImageEditor
{

    public class FindLuminousArea : ViewModelBase
    {
        [DisplayName("Threshold")]
        public int Threshold { get => _Threshold; set { if (value > 255) value = 255; if (value < 0) value = 0; _Threshold = value; OnPropertyChanged(); } }
        private int _Threshold = 100;
    }

    public class FindLuminousAreaCorner : ViewModelBase
    {
        [DisplayName("Threshold")]
        public int Threshold { get => _Threshold; set { if (value > 255) value = 255; if (value < 0) value = 0; _Threshold = value; OnPropertyChanged(); } }
        private int _Threshold = 100;

        [DisplayName("UseRotatedRect")]
        public bool UseRotatedRect { get => _UseRotatedRect; set { _UseRotatedRect = value; OnPropertyChanged(); } }
        private bool _UseRotatedRect = true;
    }

    public enum GraphicTypes
    {
        Circle = 0,
        Rect = 1,
        Quadrilateral = 2,
        Point = 3,
        Polygon = 4
    }

    public enum GraphicBorderType
    {
        [Description("无")]
        None = -1,
        [Description("绝对值")]
        Absolute,
        [Description("相对值")]
        Relative
    }

    public enum DrawingGraphicPosition
    {
        [Description("线上")]
        LineOn,
        [Description("内切")]
        Internal,
        [Description("外切")]
        External
    }


    public class GraphicEditingConfig:ViewModelBase
    {
        [JsonIgnore]
        public RelayCommand FindLuminousAreaEditCommand { get; set; }
        [JsonIgnore]
        public RelayCommand FindLuminousAreaCornerEditCommand { get; set; }


        public GraphicEditingConfig()
        {

            FindLuminousAreaEditCommand = new RelayCommand(a => new PropertyEditorWindow(FindLuminousArea) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            FindLuminousAreaCornerEditCommand = new RelayCommand(a => new PropertyEditorWindow(FindLuminousAreaCorner) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
        }

        public FindLuminousArea FindLuminousArea { get; set; } = new FindLuminousArea();
        public FindLuminousAreaCorner FindLuminousAreaCorner { get; set; } = new FindLuminousAreaCorner();


        public bool IsShowText { get => _IsShowText; set { _IsShowText = value; OnPropertyChanged(); } }
        private bool _IsShowText = true;

        public GraphicTypes PointType { set; get; } = GraphicTypes.Quadrilateral;

        [JsonIgnore]
        public bool IsAreaCircle { get => PointType == GraphicTypes.Circle; set { if (value) PointType = GraphicTypes.Circle; OnPropertyChanged(); } }
        [JsonIgnore]
        public bool IsAreaRect { get => PointType == GraphicTypes.Rect; set { if (value) PointType = GraphicTypes.Rect; OnPropertyChanged(); } }
        [JsonIgnore]
        public bool IsQuadrilateral { get => PointType == GraphicTypes.Quadrilateral; set { if (value) PointType = GraphicTypes.Quadrilateral; OnPropertyChanged(); } }

        [JsonIgnore]
        public bool IsAreaPolygon { get => PointType == GraphicTypes.Polygon; set { if (value) PointType = GraphicTypes.Polygon; OnPropertyChanged(); } }


        [JsonIgnore]
        public bool IsPointCircle { get => DefaultPointType == GraphicTypes.Circle; set { if (value) DefaultPointType = GraphicTypes.Circle; OnPropertyChanged(); } }
        [JsonIgnore]
        public bool IsPointRect { get => DefaultPointType == GraphicTypes.Rect; set { if (value) DefaultPointType = GraphicTypes.Rect; OnPropertyChanged(); } }
        [JsonIgnore]

        public GraphicTypes DefaultPointType { set; get; }


        public bool LockDeafult { get => _LockDeafult; set { _LockDeafult = value; OnPropertyChanged(); } }
        private bool _LockDeafult;
        public bool UseCenter { get => _UseCenter; set { _UseCenter = value; OnPropertyChanged(); } }
        private bool _UseCenter = false;

        public double DefalutWidth { get => _DefalutWidth; set { if (LockDeafult) return; _DefalutWidth = value; OnPropertyChanged(); } }
        private double _DefalutWidth = 30;

        public double DefalutHeight { get => _DefalutHeight; set { if (LockDeafult) return; _DefalutHeight = value; OnPropertyChanged(); } }
        private double _DefalutHeight = 30;
        public double DefalutRadius { get => _DefalutRadius; set { if (LockDeafult) return; _DefalutRadius = value; OnPropertyChanged(); } }
        private double _DefalutRadius = 30;


        public int DefaultCircleRadius { get => _DefaultCircleRadius; set { _DefaultCircleRadius = value; OnPropertyChanged(); } }
        private int _DefaultCircleRadius = 10;

        public int DefaultRectWidth { get => _DefaultRectWidth; set { _DefaultRectWidth = value; OnPropertyChanged(); } }
        private int _DefaultRectWidth = 20;

        public int DefaultRectHeight { get => _DefaultRectHeight; set { _DefaultRectHeight = value; OnPropertyChanged(); } }
        private int _DefaultRectHeight = 20;



        public Point Polygon1 { get; set; } = new Point() { X = 100, Y = 100 };
        public Point Polygon2 { get; set; } = new Point() { X = 300, Y = 100 };
        public Point Polygon3 { get; set; } = new Point() { X = 300, Y = 300 };
        public Point Polygon4 { get; set; } = new Point() { X = 100, Y = 300 };


        [JsonIgnore()]
        public int Polygon1X { get => (int)Polygon1.X; set { Polygon1 = new Point(value, Polygon1.Y); OnPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon1Y { get => (int)Polygon1.Y; set { Polygon1 = new Point(Polygon1.X, value); OnPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon2X { get => (int)Polygon2.X; set { Polygon2 = new Point(value, Polygon2.Y); OnPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon2Y { get => (int)Polygon2.Y; set { Polygon2 = new Point(Polygon2.X, value); OnPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon3X { get => (int)Polygon3.X; set { Polygon3 = new Point(value, Polygon3.Y); OnPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon3Y { get => (int)Polygon3.Y; set { Polygon3 = new Point(Polygon3.X, value); OnPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon4X { get => (int)Polygon4.X; set { Polygon4 = new Point(value, Polygon4.Y); OnPropertyChanged(); } }
        [JsonIgnore]
        public int Polygon4Y { get => (int)Polygon4.Y; set { Polygon4 = new Point(Polygon4.X, value); OnPropertyChanged(); } }

        public bool IsUserDraw { get => _IsUserDraw; set { _IsUserDraw = value; OnPropertyChanged(); } }
        private bool _IsUserDraw;



        public int AreaCircleRadius { get => _AreaCircleRadius; set { _AreaCircleRadius = value; OnPropertyChanged(); } }
        private int _AreaCircleRadius = 100;

        public int AreaCircleNum { get => _AreaCircleNum; set { _AreaCircleNum = value; OnPropertyChanged(); } }
        private int _AreaCircleNum = 6;

        public int AreaCircleAngle { get => _AreaCircleAngle; set { _AreaCircleAngle = value; OnPropertyChanged(); } }
        private int _AreaCircleAngle;

        public int AreaRectWidth { get => _AreaRectWidth; set { _AreaRectWidth = value; OnPropertyChanged(); } }
        private int _AreaRectWidth = 200;

        public int AreaRectHeight { get => _AreaRectHeight; set { _AreaRectHeight = value; OnPropertyChanged(); } }
        private int _AreaRectHeight = 200;

        public int AreaRectRow { get => _AreaRectRow; set { _AreaRectRow = value; OnPropertyChanged(); } }
        private int _AreaRectRow = 3;

        public int AreaRectCol { get => _AreaRectCol; set { _AreaRectCol = value; OnPropertyChanged(); } }
        private int _AreaRectCol = 3;


        public Point Center { get; set; } = new Point() { X = 200, Y = 200 };


        [JsonIgnore]
        public int CenterX { get => (int)Center.X; set { Center = new Point(value, Center.Y); OnPropertyChanged(); } }
        [JsonIgnore]
        public int CenterY { get => (int)Center.Y; set { Center = new Point(Center.X, value); OnPropertyChanged(); } }

        public int AreaPolygonRow { get => _AreaPolygonRow; set { _AreaPolygonRow = value; OnPropertyChanged(); } }
        private int _AreaPolygonRow = 3;

        public int AreaPolygonCol { get => _AreaPolygonCol; set { _AreaPolygonCol = value; OnPropertyChanged(); } }
        private int _AreaPolygonCol = 3;

    }


    /// <summary>
    /// GraphicEditingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class GraphicEditingWindow : Window
    {
        GraphicEditingConfig PoiConfig { get; set; }

        ImageView? ImageView;


        DrawCanvas ImageShow => ImageView.ImageShow;

        string TagName = "Poi";
        public GraphicEditingWindow(ImageView imageView)
        {
            ImageView = imageView;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            ComboBoxBorderType1.ItemsSource = from e1 in Enum.GetValues(typeof(GraphicBorderType)).Cast<GraphicBorderType>() select new KeyValuePair<GraphicBorderType, string>(e1, e1.ToDescription());
            ComboBoxBorderType1.SelectedIndex = 0;

            ComboBoxBorderType11.ItemsSource = from e1 in Enum.GetValues(typeof(GraphicBorderType)).Cast<GraphicBorderType>() select new KeyValuePair<GraphicBorderType, string>(e1, e1.ToDescription());
            ComboBoxBorderType11.SelectedIndex = 0;

            ComboBoxBorderType2.ItemsSource = from e1 in Enum.GetValues(typeof(DrawingGraphicPosition)).Cast<DrawingGraphicPosition>() select new KeyValuePair<DrawingGraphicPosition, string>(e1, e1.ToDescription());
            ComboBoxBorderType2.SelectedIndex = 0;

            PoiConfig = new GraphicEditingConfig();
            this.DataContext = PoiConfig;
            this.Closed += (s, e) => { ImageView = null; };
        }

        private void ShowPoiConfig_Click(object sender, RoutedEventArgs e)
        {

        }

  

        private void FindLuminousAreaCorner_Click(object sender, RoutedEventArgs e)
        {
            if (ImageView.HImageCache != null)
            {
                string FindLuminousAreajson = PoiConfig.FindLuminousAreaCorner.ToJsonN();
                Task.Run(() =>
                {
                    int length = OpenCVMediaHelper.M_FindLuminousArea((HImage)ImageView.HImageCache, FindLuminousAreajson, out IntPtr resultPtr);
                    if (length > 0)
                    {
                        string result = Marshal.PtrToStringAnsi(resultPtr);
                        Console.WriteLine("Result: " + result);
                        OpenCVMediaHelper.FreeResult(resultPtr);
                        var jObj = Newtonsoft.Json.Linq.JObject.Parse(result);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var corners = jObj["Corners"].ToObject<List<List<float>>>();
                            if (corners.Count == 4)
                            {
                                PoiConfig.Polygon1X = (int)corners[0][0];
                                PoiConfig.Polygon1Y = (int)corners[0][1];
                                PoiConfig.Polygon2X = (int)corners[1][0];
                                PoiConfig.Polygon2Y = (int)corners[1][1];
                                PoiConfig.Polygon3X = (int)corners[2][0];
                                PoiConfig.Polygon3Y = (int)corners[2][1];
                                PoiConfig.Polygon4X = (int)corners[3][0];
                                PoiConfig.Polygon4Y = (int)corners[3][1];
                            }

                            List<Point> pts_src = new();
                            pts_src.Add(PoiConfig.Polygon1);
                            pts_src.Add(PoiConfig.Polygon2);
                            pts_src.Add(PoiConfig.Polygon3);
                            pts_src.Add(PoiConfig.Polygon4);

                            List<Point> result = Helpers.SortPolyPoints(pts_src);
                            DVDatumPolygon Polygon = new() { IsComple = true };
                            Polygon.Attribute.Pen = new Pen(Brushes.Blue, 1 / ImageView.Zoombox1.ContentMatrix.M11);
                            Polygon.Attribute.Brush = Brushes.Transparent;
                            Polygon.Attribute.Points.Add(result[0]);
                            Polygon.Attribute.Points.Add(result[1]);
                            Polygon.Attribute.Points.Add(result[2]);
                            Polygon.Attribute.Points.Add(result[3]);
                            Polygon.Render();
                            ImageShow.AddVisual(Polygon);
                        });
                    }
                    else
                    {
                        Console.WriteLine("Error occurred, code: " + length);
                    }
                });
            }
        }

        private void ButtonImportMarin_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonImportMarinSetting(object sender, RoutedEventArgs e)
        {

        }

        int start = 0;
        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            if (ImageShow.Source is not BitmapSource bitmapImage) return;

            int Num = 0;

            switch (PoiConfig.PointType)
            {
                case GraphicTypes.Circle:
                    if (PoiConfig.AreaCircleNum < 1)
                    {
                        MessageBox.Show("绘制的个数不能小于1", "ColorVision");
                        return;
                    }

                    for (int i = 0; i < PoiConfig.AreaCircleNum; i++)
                    {
                        Num++;

                        double x1 = PoiConfig.CenterX + PoiConfig.AreaCircleRadius * Math.Cos(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                        double y1 = PoiConfig.CenterY + PoiConfig.AreaCircleRadius * Math.Sin(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);

                        int did = start + i + 1; ;
                        switch (PoiConfig.DefaultPointType)
                        {
                            case GraphicTypes.Circle:

                                if (ComboBoxBorderType2.SelectedValue is DrawingGraphicPosition pOIPosition)
                                {
                                    switch (pOIPosition)
                                    {
                                        case DrawingGraphicPosition.LineOn:
                                            x1 = PoiConfig.CenterX + PoiConfig.AreaCircleRadius * Math.Cos(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            y1 = PoiConfig.CenterY + PoiConfig.AreaCircleRadius * Math.Sin(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            break;
                                        case DrawingGraphicPosition.Internal:
                                            x1 = PoiConfig.CenterX + (PoiConfig.AreaCircleRadius - PoiConfig.DefaultCircleRadius) * Math.Cos(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            y1 = PoiConfig.CenterY + (PoiConfig.AreaCircleRadius - PoiConfig.DefaultCircleRadius) * Math.Sin(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            break;
                                        case DrawingGraphicPosition.External:
                                            x1 = PoiConfig.CenterX + (PoiConfig.AreaCircleRadius + PoiConfig.DefaultCircleRadius) * Math.Cos(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            y1 = PoiConfig.CenterY + (PoiConfig.AreaCircleRadius + PoiConfig.DefaultCircleRadius) * Math.Sin(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            break;
                                        default:
                                            break;
                                    }
                                }

                                CircleTextProperties circleTextProperties = new CircleTextProperties();
                                circleTextProperties.Center = new Point(x1, y1);
                                circleTextProperties.Radius = PoiConfig.DefaultCircleRadius;
                                circleTextProperties.Brush = Brushes.Transparent;
                                circleTextProperties.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultCircleRadius / 30);
                                circleTextProperties.Id = did;
                                circleTextProperties.Name = did.ToString();
                                circleTextProperties.Text = string.Format("{0}{1}", TagName, did.ToString());
                                DVCircleText Circle = new DVCircleText(circleTextProperties);

                                Circle.Render();
                                ImageShow.AddVisual(Circle);
                                break;
                            case GraphicTypes.Rect:

                                if (ComboBoxBorderType2.SelectedValue is DrawingGraphicPosition pOIPosition2)
                                {
                                    switch (pOIPosition2)
                                    {
                                        case DrawingGraphicPosition.LineOn:
                                            x1 = PoiConfig.CenterX + PoiConfig.AreaCircleRadius * Math.Cos(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            y1 = PoiConfig.CenterY + PoiConfig.AreaCircleRadius * Math.Sin(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            break;
                                        case DrawingGraphicPosition.Internal:
                                            x1 = PoiConfig.CenterX + (PoiConfig.AreaCircleRadius - PoiConfig.DefaultRectWidth / 2) * Math.Cos(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            y1 = PoiConfig.CenterY + (PoiConfig.AreaCircleRadius - PoiConfig.DefaultRectHeight / 2) * Math.Sin(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            break;
                                        case DrawingGraphicPosition.External:
                                            x1 = PoiConfig.CenterX + (PoiConfig.AreaCircleRadius + PoiConfig.DefaultRectWidth / 2) * Math.Cos(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            y1 = PoiConfig.CenterY + (PoiConfig.AreaCircleRadius + PoiConfig.DefaultRectHeight / 2) * Math.Sin(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            break;
                                        default:
                                            break;
                                    }
                                }
                                RectangleTextProperties rectangleTextProperties = new RectangleTextProperties();
                                rectangleTextProperties.Rect = new System.Windows.Rect(x1 - PoiConfig.DefaultRectWidth / 2, y1 - PoiConfig.DefaultRectHeight / 2, PoiConfig.DefaultRectWidth, PoiConfig.DefaultRectHeight);
                                rectangleTextProperties.Brush = Brushes.Transparent;
                                rectangleTextProperties.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultRectWidth / 30);
                                rectangleTextProperties.Id = did;
                                rectangleTextProperties.Name = did.ToString();
                                rectangleTextProperties.Text = string.Format("{0}{1}", TagName, did.ToString());
                                DVRectangleText Rectangle = new DVRectangleText(rectangleTextProperties);
       
                                Rectangle.Render();
                                ImageShow.AddVisual(Rectangle);
                                break;
                            case GraphicTypes.Quadrilateral:
                                break;
                            default:
                                break;
                        }
                    }
                    break;
                case GraphicTypes.Rect:

                    int cols = PoiConfig.AreaRectCol;
                    int rows = PoiConfig.AreaRectRow;

                    if (rows < 1 || cols < 1)
                    {
                        MessageBox.Show("点阵数的行列不能小于1", "ColorVision");
                        return;
                    }
                    double Width = PoiConfig.AreaRectWidth;
                    double Height = PoiConfig.AreaRectHeight;


                    double startU = PoiConfig.CenterY - Height / 2;
                    double startD = bitmapImage.PixelHeight - PoiConfig.CenterY - Height / 2;
                    double startL = PoiConfig.CenterX - Width / 2;
                    double startR = bitmapImage.PixelWidth - PoiConfig.CenterX - Width / 2;

                    if (ComboBoxBorderType2.SelectedValue is DrawingGraphicPosition pOIPosition1)
                    {
                        switch (PoiConfig.DefaultPointType)
                        {
                            case GraphicTypes.Circle:
                                switch (pOIPosition1)
                                {
                                    case DrawingGraphicPosition.LineOn:
                                        break;
                                    case DrawingGraphicPosition.Internal:
                                        startU += PoiConfig.DefaultCircleRadius;
                                        startD += PoiConfig.DefaultCircleRadius;
                                        startL += PoiConfig.DefaultCircleRadius;
                                        startR += PoiConfig.DefaultCircleRadius;
                                        break;
                                    case DrawingGraphicPosition.External:
                                        startU -= PoiConfig.DefaultCircleRadius;
                                        startD -= PoiConfig.DefaultCircleRadius;
                                        startL -= PoiConfig.DefaultCircleRadius;
                                        startR -= PoiConfig.DefaultCircleRadius;
                                        break;
                                    default:
                                        break;
                                }
                                break;
                            case GraphicTypes.Rect:
                                switch (pOIPosition1)
                                {
                                    case DrawingGraphicPosition.LineOn:
                                        break;
                                    case DrawingGraphicPosition.Internal:
                                        startU += PoiConfig.DefaultRectHeight / 2;
                                        startD += PoiConfig.DefaultRectHeight / 2;
                                        startL += PoiConfig.DefaultRectWidth / 2;
                                        startR += PoiConfig.DefaultRectWidth / 2;
                                        break;
                                    case DrawingGraphicPosition.External:
                                        startU -= PoiConfig.DefaultRectHeight / 2;
                                        startD -= PoiConfig.DefaultRectHeight / 2;
                                        startL -= PoiConfig.DefaultRectWidth / 2;
                                        startR -= PoiConfig.DefaultRectWidth / 2;
                                        break;
                                    default:
                                        break;
                                }
                                break;
                            case GraphicTypes.Quadrilateral:
                                break;
                            default:
                                break;
                        }
                    }


                    double StepRow = (rows > 1) ? (bitmapImage.PixelHeight - startD - startU) / (rows - 1) : 0;
                    double StepCol = (cols > 1) ? (bitmapImage.PixelWidth - startL - startR) / (cols - 1) : 0;


                    int all = rows * cols;

                    for (int i = 0; i < rows; i++)
                    {
                        for (int j = 0; j < cols; j++)
                        {
                            Num++;

                            double x1 = startL + StepCol * j;
                            double y1 = startU + StepRow * i;
                            int did = start + i * cols + j + 1;
                            switch (PoiConfig.DefaultPointType)
                            {
                                case GraphicTypes.Circle:
                                    CircleTextProperties circleTextProperties = new CircleTextProperties();
                                    circleTextProperties.Center = new Point(x1, y1);
                                    circleTextProperties.Radius = PoiConfig.DefaultCircleRadius;
                                    circleTextProperties.Brush = Brushes.Transparent;
                                    circleTextProperties.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultCircleRadius / 30);
                                    circleTextProperties.Id = did;
                                    circleTextProperties.Name = did.ToString();
                                    circleTextProperties.Text = string.Format("{0}{1}", TagName, did.ToString());

                                    DVCircleText Circle = new DVCircleText(circleTextProperties);
                                    Circle.IsShowText = PoiConfig.IsShowText;

                                    Circle.Render();
                                    ImageShow.AddVisual(Circle);
                                    break;
                                case GraphicTypes.Rect:

                                    RectangleTextProperties rectangleTextProperties = new RectangleTextProperties();
                                    rectangleTextProperties.Rect = new System.Windows.Rect(x1 - (double)PoiConfig.DefaultRectWidth / 2, y1 - PoiConfig.DefaultRectHeight / 2, PoiConfig.DefaultRectWidth, PoiConfig.DefaultRectHeight);
                                    rectangleTextProperties.Brush = Brushes.Transparent;
                                    rectangleTextProperties.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultRectWidth / 30);
                                    rectangleTextProperties.Id = did;
                                    rectangleTextProperties.Name = did.ToString();
                                    rectangleTextProperties.Text = string.Format("{0}{1}", TagName, did.ToString());
                                    DVRectangleText Rectangle = new DVRectangleText(rectangleTextProperties);
                                    Rectangle.IsShowText = PoiConfig.IsShowText;
                                    Rectangle.Render();
                                    ImageShow.AddVisual(Rectangle);
                                    break;
                                case GraphicTypes.Quadrilateral:
                                    break;
                                default:
                                    break;
                            }
                        }
                    }

                    break;
                case GraphicTypes.Quadrilateral:
                    List<Point> pts_src =
                    [
                        PoiConfig.Polygon1,
                        PoiConfig.Polygon2,
                        PoiConfig.Polygon3,
                        PoiConfig.Polygon4,
                    ];

                    List<Point> points = Helpers.SortPolyPoints(pts_src);

                    cols = PoiConfig.AreaPolygonCol;
                    rows = PoiConfig.AreaPolygonRow;


                    double rowStep = (rows > 1) ? 1.0 / (rows - 1) : 0;
                    double columnStep = (rows > 1) ? 1.0 / (cols - 1) : 0;
                    for (int i = 0; i < rows; i++)
                    {
                        for (int j = 0; j < cols; j++)
                        {
                            // Calculate the position of the point within the quadrilateral
                            double x = (1 - i * rowStep) * (1 - j * columnStep) * points[0].X +
                                       (1 - i * rowStep) * (j * columnStep) * points[1].X +
                                       (i * rowStep) * (1 - j * columnStep) * points[3].X +
                                       (i * rowStep) * (j * columnStep) * points[2].X;

                            double y = (1 - i * rowStep) * (1 - j * columnStep) * points[0].Y +
                                       (1 - i * rowStep) * (j * columnStep) * points[1].Y +
                                       (i * rowStep) * (1 - j * columnStep) * points[3].Y +
                                       (i * rowStep) * (j * columnStep) * points[2].Y;

                            Point point = new(x, y);

                            int did = start + i * cols + j + 1;
                            switch (PoiConfig.DefaultPointType)
                            {
                                case GraphicTypes.Circle:
                                    CircleTextProperties circleTextProperties = new CircleTextProperties();
                                    circleTextProperties.Center = new Point(point.X, point.Y);
                                    circleTextProperties.Radius = PoiConfig.DefaultCircleRadius;
                                    circleTextProperties.Brush = Brushes.Transparent;
                                    circleTextProperties.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultCircleRadius / 30);
                                    circleTextProperties.Id = did;
                                    circleTextProperties.Name = did.ToString();
                                    circleTextProperties.Text = string.Format("{0}{1}", TagName, did.ToString());
                                    DVCircleText Circle = new DVCircleText(circleTextProperties);
                                    Circle.Render();
                                    ImageShow.AddVisual(Circle);
                                    break;
                                case GraphicTypes.Rect:
                                    RectangleTextProperties rectangleTextProperties = new RectangleTextProperties();
                                    rectangleTextProperties.Rect = new System.Windows.Rect(point.X - PoiConfig.DefaultRectWidth / 2, point.Y - PoiConfig.DefaultRectHeight / 2, PoiConfig.DefaultRectWidth, PoiConfig.DefaultRectHeight);
                                    rectangleTextProperties.Brush = Brushes.Transparent;
                                    rectangleTextProperties.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultRectWidth / 30);
                                    rectangleTextProperties.Id = did;
                                    rectangleTextProperties.Name = did.ToString();
                                    rectangleTextProperties.Text = string.Format("{0}{1}", TagName, did.ToString());
                                    DVRectangleText Rectangle = new DVRectangleText(rectangleTextProperties);

                                    Rectangle.Render();
                                    ImageShow.AddVisual(Rectangle);
                                    break;
                                case GraphicTypes.Quadrilateral:
                                    break;
                                default:
                                    break;
                            }
                        }
                    }

                    break;
                default:
                    break;
            }
        }

        private void FindLuminousArea_Click(object sender, RoutedEventArgs e)
        {
            if (ImageView.HImageCache != null)
            {
                string FindLuminousAreajson = PoiConfig.FindLuminousArea.ToJsonN();
                Task.Run(() =>
                {
                    int length = OpenCVMediaHelper.M_FindLuminousArea((HImage)ImageView.HImageCache, FindLuminousAreajson, out IntPtr resultPtr);
                    if (length > 0)
                    {
                        string result = Marshal.PtrToStringAnsi(resultPtr);
                        Console.WriteLine("Result: " + result);
                        OpenCVMediaHelper.FreeResult(resultPtr);
                        MRect rect = Newtonsoft.Json.JsonConvert.DeserializeObject<MRect>(result);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            DVDatumRectangle Rectangle = new DVDatumRectangle();
                            Rectangle.Attribute.Rect = new System.Windows.Rect(rect.X, rect.Y, rect.Width, rect.Height);
                            Rectangle.Attribute.Brush = Brushes.Transparent;
                            Rectangle.Attribute.Pen = new Pen(Brushes.Blue, 1 / ImageView.Zoombox1.ContentMatrix.M11);
                            Rectangle.Render();
                            ImageShow.AddVisual(Rectangle);
                        });
                    }
                    else
                    {
                        Console.WriteLine("Error occurred, code: " + length);
                    }
                });
            }

        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonImportMarinSetting2(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonImportMarin1_Click(object sender, RoutedEventArgs e)
        {

        }

        private void RadioButtonArea_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void SetDefault_Click(object sender, RoutedEventArgs e)
        {

        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender is UIElement uie)
                    uie.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));

                e.Handled = true;
            }
        }
    }
}
