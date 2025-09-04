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

    public static class ImageEditorHelper
    {
        /// <summary>
        /// 根据指定的边距缩放一个四边形
        /// </summary>
        /// <param name="points">多边形的四个顶点列表</param>
        /// <param name="top">上边距</param>
        /// <param name="bottom">下边距</param>
        /// <param name="left">左边距</param>
        /// <param name="right">右边距</param>
        /// <returns>缩放后的新顶点列表</returns>
        public static List<Point> ScalePolygon(List<Point> points, double top, double bottom, double left, double right)
        {
            if (points.Count != 4)
            {
                // 如果不是四边形，则直接返回原点
                return points;
            }

            // 1. 计算多边形的几何中心
            double centerX = (points[0].X + points[1].X + points[2].X + points[3].X) / 4;
            double centerY = (points[0].Y + points[1].Y + points[2].Y + points[3].Y) / 4;
            var center = new Point(centerX, centerY);

            // 2. 计算原始多边形的宽度和高度
            // 这里我们假设点是按顺序排列的，例如左上、右上、右下、左下
            double originalWidth = Math.Max(points[1].X, points[2].X) - Math.Min(points[0].X, points[3].X);
            double originalHeight = Math.Max(points[2].Y, points[3].Y) - Math.Min(points[0].Y, points[1].Y);

            if (originalWidth <= 0 || originalHeight <= 0)
            {
                // 避免除以零的错误
                return points;
            }

            // 3. 计算水平和垂直方向的缩放比例
            // 新宽度 = 原宽度 - 左边距 - 右边距
            // 新高度 = 原高度 - 上边距 - 下边距
            double scaleX = (originalWidth - left - right) / originalWidth;
            double scaleY = (originalHeight - top - bottom) / originalHeight;

            var newPoints = new List<Point>();
            foreach (var point in points)
            {
                // 4. 对每个顶点进行缩放
                // a. 计算顶点相对于中心的向量
                double vecX = point.X - center.X;
                double vecY = point.Y - center.Y;

                // b. 根据比例缩放向量
                double scaledVecX = vecX * scaleX;
                double scaledVecY = vecY * scaleY;

                // c. 将缩放后的向量加回中心点，得到新顶点的位置
                double newX = center.X + scaledVecX;
                double newY = center.Y + scaledVecY;

                newPoints.Add(new Point(newX, newY));
            }

            return newPoints;
        }


        /// <summary>
        /// Insets or outsets a convex polygon by a given offset.
        /// </summary>
        /// <param name="polygon">The list of points defining the polygon.</param>
        /// <param name="offset">The distance to inset (positive) or outset (negative).</param>
        /// <returns>A new list of points for the offset polygon.</returns>
        public static List<Point> InsetPolygon(List<Point> polygon, double offset)
        {
            if (polygon == null || polygon.Count < 3) return polygon;

            var newPoints = new List<Point>();
            int n = polygon.Count;

            for (int i = 0; i < n; i++)
            {
                Point p_prev = polygon[(i + n - 1) % n];
                Point p_curr = polygon[i];
                Point p_next = polygon[(i + 1) % n];

                Vector v1 = p_curr - p_prev;
                Vector v2 = p_next - p_curr;

                v1.Normalize();
                v2.Normalize();

                // Get normal vectors pointing outwards
                Vector n1 = new Vector(-v1.Y, v1.X);
                Vector n2 = new Vector(-v2.Y, v2.X);

                // Bisector vector
                Vector bisector = n1 + n2;
                if (bisector.LengthSquared < 1e-9) // Edges are collinear
                {
                    bisector = n1;
                }
                bisector.Normalize();

                // Calculate the shift amount
                double angle = Vector.AngleBetween(v1, -v2);
                if (double.IsNaN(angle) || angle == 0) continue;

                double sin_half_angle = Math.Sin(Math.PI * angle / 360.0);
                if (Math.Abs(sin_half_angle) < 1e-9) continue;

                double shift = offset / sin_half_angle;

                newPoints.Add(p_curr + bisector * shift);
            }

            return newPoints;
        }

    }

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

        public GraphicTypes GraphicTypes { set; get; } = GraphicTypes.Quadrilateral;

        [JsonIgnore]
        public bool IsAreaCircle { get => GraphicTypes == GraphicTypes.Circle; set { if (value) GraphicTypes = GraphicTypes.Circle; OnPropertyChanged(); } }
        [JsonIgnore]
        public bool IsAreaRect { get => GraphicTypes == GraphicTypes.Rect; set { if (value) GraphicTypes = GraphicTypes.Rect; OnPropertyChanged(); } }
        [JsonIgnore]
        public bool IsQuadrilateral { get => GraphicTypes == GraphicTypes.Quadrilateral; set { if (value) GraphicTypes = GraphicTypes.Quadrilateral; OnPropertyChanged(); } }

        [JsonIgnore]
        public bool IsAreaPolygon { get => GraphicTypes == GraphicTypes.Polygon; set { if (value) GraphicTypes = GraphicTypes.Polygon; OnPropertyChanged(); } }


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

        public Rect AreaRect { get => _AreaRect; set { _AreaRect = value; OnPropertyChanged(); } }
        private Rect _AreaRect;

        public int AreaRectRow { get => _AreaRectRow; set { _AreaRectRow = value; OnPropertyChanged(); } }
        private int _AreaRectRow = 3;

        public int AreaRectCol { get => _AreaRectCol; set { _AreaRectCol = value; OnPropertyChanged(); } }
        private int _AreaRectCol = 3;

        public Point Center { get; set; } = new Point() { X = 200, Y = 200 };


        [JsonIgnore]
        public int CenterX { get => (int)Center.X; set { Center = new Point(value, Center.Y); OnPropertyChanged(); } }
        [JsonIgnore]
        public int CenterY { get => (int)Center.Y; set { Center = new Point(Center.X, value); OnPropertyChanged(); } }


    }


    /// <summary>
    /// GraphicEditingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class GraphicEditingWindow : Window
    {
        GraphicEditingConfig Config { get; set; }

        ImageView ImageView;


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

            Config = new GraphicEditingConfig();
            this.DataContext = Config;
            this.Closed += (s, e) => { ImageView = null; };
        }

        private void FindLuminousAreaCorner_Click(object sender, RoutedEventArgs e)
        {
            if (ImageView.HImageCache != null)
            {
                string FindLuminousAreajson = Config.FindLuminousAreaCorner.ToJsonN();
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
                                Config.Polygon1X = (int)corners[0][0];
                                Config.Polygon1Y = (int)corners[0][1];
                                Config.Polygon2X = (int)corners[1][0];
                                Config.Polygon2Y = (int)corners[1][1];
                                Config.Polygon3X = (int)corners[2][0];
                                Config.Polygon3Y = (int)corners[2][1];
                                Config.Polygon4X = (int)corners[3][0];
                                Config.Polygon4Y = (int)corners[3][1];
                            }

                            List<Point> pts_src = new();
                            pts_src.Add(Config.Polygon1);
                            pts_src.Add(Config.Polygon2);
                            pts_src.Add(Config.Polygon3);
                            pts_src.Add(Config.Polygon4);

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
            DrawingGraphicPosition pOIPosition = DrawingGraphicPosition.Internal;

            if (ComboBoxBorderType2.SelectedValue is  DrawingGraphicPosition pOIPosition1)
            {
                pOIPosition = pOIPosition1;
            }
            int Num = 0;

            switch (Config.GraphicTypes)
            {
                case GraphicTypes.Circle:
                    if (Config.AreaCircleNum < 1)
                    {
                        MessageBox.Show("绘制的个数不能小于1", "ColorVision");
                        return;
                    }

                    for (int i = 0; i < Config.AreaCircleNum; i++)
                    {
                        Num++;

                        double x1 = Config.CenterX + Config.AreaCircleRadius * Math.Cos(i * 2 * Math.PI / Config.AreaCircleNum + Math.PI / 180 * Config.AreaCircleAngle);
                        double y1 = Config.CenterY + Config.AreaCircleRadius * Math.Sin(i * 2 * Math.PI / Config.AreaCircleNum + Math.PI / 180 * Config.AreaCircleAngle);

                        int did = start + i + 1; ;
                        switch (Config.DefaultPointType)
                        {
                            case GraphicTypes.Circle:

                                switch (pOIPosition)
                                {
                                    case DrawingGraphicPosition.LineOn:
                                        x1 = Config.CenterX + Config.AreaCircleRadius * Math.Cos(i * 2 * Math.PI / Config.AreaCircleNum + Math.PI / 180 * Config.AreaCircleAngle);
                                        y1 = Config.CenterY + Config.AreaCircleRadius * Math.Sin(i * 2 * Math.PI / Config.AreaCircleNum + Math.PI / 180 * Config.AreaCircleAngle);
                                        break;
                                    case DrawingGraphicPosition.Internal:
                                        x1 = Config.CenterX + (Config.AreaCircleRadius - Config.DefaultCircleRadius) * Math.Cos(i * 2 * Math.PI / Config.AreaCircleNum + Math.PI / 180 * Config.AreaCircleAngle);
                                        y1 = Config.CenterY + (Config.AreaCircleRadius - Config.DefaultCircleRadius) * Math.Sin(i * 2 * Math.PI / Config.AreaCircleNum + Math.PI / 180 * Config.AreaCircleAngle);
                                        break;
                                    case DrawingGraphicPosition.External:
                                        x1 = Config.CenterX + (Config.AreaCircleRadius + Config.DefaultCircleRadius) * Math.Cos(i * 2 * Math.PI / Config.AreaCircleNum + Math.PI / 180 * Config.AreaCircleAngle);
                                        y1 = Config.CenterY + (Config.AreaCircleRadius + Config.DefaultCircleRadius) * Math.Sin(i * 2 * Math.PI / Config.AreaCircleNum + Math.PI / 180 * Config.AreaCircleAngle);
                                        break;
                                    default:
                                        break;
                                }

                                CircleTextProperties circleTextProperties = new CircleTextProperties();
                                circleTextProperties.Center = new Point(x1, y1);
                                circleTextProperties.Radius = Config.DefaultCircleRadius;
                                circleTextProperties.Brush = Brushes.Transparent;
                                circleTextProperties.Pen = new Pen(Brushes.Red, (double)Config.DefaultCircleRadius / 30);
                                circleTextProperties.Id = did;
                                circleTextProperties.Name = did.ToString();
                                circleTextProperties.Text = string.Format("{0}{1}", TagName, did.ToString());
                                DVCircleText Circle = new DVCircleText(circleTextProperties);

                                Circle.Render();
                                ImageShow.AddVisual(Circle);
                                break;
                            case GraphicTypes.Rect:

                                switch (pOIPosition)
                                {
                                    case DrawingGraphicPosition.LineOn:
                                        x1 = Config.CenterX + Config.AreaCircleRadius * Math.Cos(i * 2 * Math.PI / Config.AreaCircleNum + Math.PI / 180 * Config.AreaCircleAngle);
                                        y1 = Config.CenterY + Config.AreaCircleRadius * Math.Sin(i * 2 * Math.PI / Config.AreaCircleNum + Math.PI / 180 * Config.AreaCircleAngle);
                                        break;
                                    case DrawingGraphicPosition.Internal:
                                        x1 = Config.CenterX + (Config.AreaCircleRadius - Config.DefaultRectWidth / 2) * Math.Cos(i * 2 * Math.PI / Config.AreaCircleNum + Math.PI / 180 * Config.AreaCircleAngle);
                                        y1 = Config.CenterY + (Config.AreaCircleRadius - Config.DefaultRectHeight / 2) * Math.Sin(i * 2 * Math.PI / Config.AreaCircleNum + Math.PI / 180 * Config.AreaCircleAngle);
                                        break;
                                    case DrawingGraphicPosition.External:
                                        x1 = Config.CenterX + (Config.AreaCircleRadius + Config.DefaultRectWidth / 2) * Math.Cos(i * 2 * Math.PI / Config.AreaCircleNum + Math.PI / 180 * Config.AreaCircleAngle);
                                        y1 = Config.CenterY + (Config.AreaCircleRadius + Config.DefaultRectHeight / 2) * Math.Sin(i * 2 * Math.PI / Config.AreaCircleNum + Math.PI / 180 * Config.AreaCircleAngle);
                                        break;
                                    default:
                                        break;
                                }
                                RectangleTextProperties rectangleTextProperties = new RectangleTextProperties();
                                rectangleTextProperties.Rect = new System.Windows.Rect(x1 - Config.DefaultRectWidth / 2, y1 - Config.DefaultRectHeight / 2, Config.DefaultRectWidth, Config.DefaultRectHeight);
                                rectangleTextProperties.Brush = Brushes.Transparent;
                                rectangleTextProperties.Pen = new Pen(Brushes.Red, (double)Config.DefaultRectWidth / 30);
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

                    int cols = Config.AreaRectCol;
                    int rows = Config.AreaRectRow;

                    if (rows < 1 || cols < 1)
                    {
                        MessageBox.Show("点阵数的行列不能小于1", "ColorVision");
                        return;
                    }
                    double Width = Config.AreaRect.Width;
                    double Height = Config.AreaRect.Height;


                    double startU = Config.AreaRect.Y;
                    double startD = Config.AreaRect.Y + Config.AreaRect.Height;
                    double startL = Config.AreaRect.X;
                    double startR = Config.AreaRect.X + Config.AreaRect.Width;

                    switch (Config.DefaultPointType)
                    {
                        case GraphicTypes.Circle:
                            switch (pOIPosition)
                            {
                                case DrawingGraphicPosition.LineOn:
                                    break;
                                case DrawingGraphicPosition.Internal:
                                    startU += Config.DefaultCircleRadius;
                                    startD += Config.DefaultCircleRadius;
                                    startL += Config.DefaultCircleRadius;
                                    startR += Config.DefaultCircleRadius;
                                    break;
                                case DrawingGraphicPosition.External:
                                    startU -= Config.DefaultCircleRadius;
                                    startD -= Config.DefaultCircleRadius;
                                    startL -= Config.DefaultCircleRadius;
                                    startR -= Config.DefaultCircleRadius;
                                    break;
                                default:
                                    break;
                            }
                            break;
                        case GraphicTypes.Rect:
                            switch (pOIPosition)
                            {
                                case DrawingGraphicPosition.LineOn:
                                    break;
                                case DrawingGraphicPosition.Internal:
                                    startU += Config.DefaultRectHeight / 2;
                                    startD += Config.DefaultRectHeight / 2;
                                    startL += Config.DefaultRectWidth / 2;
                                    startR += Config.DefaultRectWidth / 2;
                                    break;
                                case DrawingGraphicPosition.External:
                                    startU -= Config.DefaultRectHeight / 2;
                                    startD -= Config.DefaultRectHeight / 2;
                                    startL -= Config.DefaultRectWidth / 2;
                                    startR -= Config.DefaultRectWidth / 2;
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
                            switch (Config.DefaultPointType)
                            {
                                case GraphicTypes.Circle:
                                    CircleTextProperties circleTextProperties = new CircleTextProperties();
                                    circleTextProperties.Center = new Point(x1, y1);
                                    circleTextProperties.Radius = Config.DefaultCircleRadius;
                                    circleTextProperties.Brush = Brushes.Transparent;
                                    circleTextProperties.Pen = new Pen(Brushes.Red, (double)Config.DefaultCircleRadius / 30);
                                    circleTextProperties.Id = did;
                                    circleTextProperties.Name = did.ToString();
                                    circleTextProperties.Text = string.Format("{0}{1}", TagName, did.ToString());

                                    DVCircleText Circle = new DVCircleText(circleTextProperties);
                                    Circle.IsShowText = Config.IsShowText;

                                    Circle.Render();
                                    ImageShow.AddVisual(Circle);
                                    break;
                                case GraphicTypes.Rect:
                                    RectangleTextProperties rectangleTextProperties = new RectangleTextProperties();
                                    rectangleTextProperties.Rect = new System.Windows.Rect(x1 - (double)Config.DefaultRectWidth / 2, y1 - Config.DefaultRectHeight / 2, Config.DefaultRectWidth, Config.DefaultRectHeight);
                                    rectangleTextProperties.Brush = Brushes.Transparent;
                                    rectangleTextProperties.Pen = new Pen(Brushes.Red, (double)Config.DefaultRectWidth / 30);
                                    rectangleTextProperties.Id = did;
                                    rectangleTextProperties.Name = did.ToString();
                                    rectangleTextProperties.Text = string.Format("{0}{1}", TagName, did.ToString());
                                    DVRectangleText Rectangle = new DVRectangleText(rectangleTextProperties);
                                    Rectangle.IsShowText = Config.IsShowText;
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
                        Config.Polygon1,
                        Config.Polygon2,
                        Config.Polygon3,
                        Config.Polygon4,
                    ];

                    List<Point> points = Helpers.SortPolyPoints(pts_src);


                    double offset = 0;
                    switch (Config.DefaultPointType)
                    {
                        case GraphicTypes.Circle:
                            offset = Config.DefaultCircleRadius;
                            break;
                        case GraphicTypes.Rect:
                            offset = Math.Min(Config.DefaultRectWidth, Config.DefaultRectHeight) / 2.0;
                            break;
                    }

                    if (offset > 0)
                    {
                        switch (pOIPosition)
                        {
                            case DrawingGraphicPosition.Internal:
                                points = ImageEditorHelper.InsetPolygon(points, offset);
                                break;
                            case DrawingGraphicPosition.External:
                                points = ImageEditorHelper.InsetPolygon(points, -offset);
                                break;
                            case DrawingGraphicPosition.LineOn:
                            default:
                                // Do nothing
                                break;
                        }
                    }

                    cols = Config.AreaRectCol;
                    rows = Config.AreaRectRow;





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
                            switch (Config.DefaultPointType)
                            {
                                case GraphicTypes.Circle:
                                    CircleTextProperties circleTextProperties = new CircleTextProperties();
                                    circleTextProperties.Center = new Point(point.X, point.Y);
                                    circleTextProperties.Radius = Config.DefaultCircleRadius;
                                    circleTextProperties.Brush = Brushes.Transparent;
                                    circleTextProperties.Pen = new Pen(Brushes.Red, (double)Config.DefaultCircleRadius / 30);
                                    circleTextProperties.Id = did;
                                    circleTextProperties.Name = did.ToString();
                                    circleTextProperties.Text = string.Format("{0}{1}", TagName, did.ToString());
                                    DVCircleText Circle = new DVCircleText(circleTextProperties);
                                    Circle.Render();
                                    ImageShow.AddVisual(Circle);
                                    break;
                                case GraphicTypes.Rect:
                                    RectangleTextProperties rectangleTextProperties = new RectangleTextProperties();
                                    rectangleTextProperties.Rect = new System.Windows.Rect(point.X - Config.DefaultRectWidth / 2, point.Y - Config.DefaultRectHeight / 2, Config.DefaultRectWidth, Config.DefaultRectHeight);
                                    rectangleTextProperties.Brush = Brushes.Transparent;
                                    rectangleTextProperties.Pen = new Pen(Brushes.Red, (double)Config.DefaultRectWidth / 30);
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
                string FindLuminousAreajson = Config.FindLuminousArea.ToJsonN();
                Task.Run(() =>
                {
                    int length = OpenCVMediaHelper.M_FindLuminousArea((HImage)ImageView.HImageCache, FindLuminousAreajson, out IntPtr resultPtr);
                    if (length > 0)
                    {
                        string result = Marshal.PtrToStringAnsi(resultPtr);
                        Console.WriteLine("Result: " + result);
                        OpenCVMediaHelper.FreeResult(resultPtr);
                        MRect rect = Newtonsoft.Json.JsonConvert.DeserializeObject<MRect>(result);

                        if (rect != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Config.AreaRect = new Rect() { X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height };

                                DVDatumRectangle Rectangle = new DVDatumRectangle();
                                Rectangle.Attribute.Rect = new System.Windows.Rect(rect.X, rect.Y, rect.Width, rect.Height);
                                Rectangle.Attribute.Brush = Brushes.Transparent;
                                Rectangle.Attribute.Pen = new Pen(Brushes.Blue, 1 / ImageView.Zoombox1.ContentMatrix.M11);
                                Rectangle.Render();
                                ImageShow.AddVisual(Rectangle);
                            });
                        }

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
        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender is UIElement uie)
                    uie.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));

                e.Handled = true;
            }
        }

        private void ShowPoiConfig_Click(object sender, RoutedEventArgs e)
        {

        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void AreaRectFull(object sender, RoutedEventArgs e)
        {
            switch (Config.GraphicTypes)
            {
                case GraphicTypes.Circle:
                    break;
                case GraphicTypes.Rect:
                    Config.DefaultRectHeight = (int) Config.AreaRect.Height / Config.AreaRectRow;
                    Config.DefaultRectWidth = (int) Config.AreaRect.Width / Config.AreaRectCol;
                    break;
                case GraphicTypes.Quadrilateral:

                    // 1. 获取四边形的四个顶点X, Y坐标
                    var xCoords = new[] { Config.Polygon1X, Config.Polygon2X, Config.Polygon3X, Config.Polygon4X };
                    var yCoords = new[] { Config.Polygon1Y, Config.Polygon2Y, Config.Polygon3Y, Config.Polygon4Y };

                    // 2. 找出X和Y坐标的最大值和最小值
                    double minX = xCoords.Min();
                    double maxX = xCoords.Max();
                    double minY = yCoords.Min();
                    double maxY = yCoords.Max();

                    // 3. 计算包围盒的宽度和高度
                    double quadrilateralWidth = maxX - minX;
                    double quadrilateralHeight = maxY - minY;

                    // 4. 与矩形情况一样，计算默认的宽度和高度
                    // 避免除以零的错误
                    if (Config.AreaRectRow > 0 && Config.AreaRectCol > 0)
                    {
                        Config.DefaultRectHeight = (int)(quadrilateralHeight / Config.AreaRectRow);
                        Config.DefaultRectWidth = (int)(quadrilateralWidth / Config.AreaRectCol);
                    }

                    break;
                case GraphicTypes.Point:
                    break;
                case GraphicTypes.Polygon:
                    break;
                default:
                    break;
            }
        }
    }
}
