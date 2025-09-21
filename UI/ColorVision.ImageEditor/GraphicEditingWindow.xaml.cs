using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Rasterized;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.Util.Draw.Rectangle;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{

    public static class ImageEditorHelper
    {

        public static List<Point> SortRectanglePoints(List<Point> points)
        {
            if (points.Count != 4) return points;

            // 按Y升序
            var sortedByY = points.OrderBy(p => p.Y).ToList();

            // 上面两个点
            var topPoints = sortedByY.Take(2).OrderBy(p => p.X).ToList();
            // 下面两个点
            var bottomPoints = sortedByY.Skip(2).OrderBy(p => p.X).ToList();

            // 顺序：左上、右上、右下、左下
            return new List<Point> { topPoints[0], topPoints[1], bottomPoints[1], bottomPoints[0] };
        }
        /// <summary>
        /// 根据指定的边距缩放一个四边形
        /// </summary>
        /// <param name="points">多边形的四个顶点列表</param>
        /// <param name="top">上边距</param>
        /// <param name="bottom">下边距</param>
        /// <param name="left">左边距</param>
        /// <param name="right">右边距</param>
        /// <returns>缩放后的新顶点列表</returns>
        public static List<Point>  ScalePolygon(List<Point> points, double top, double bottom, double left, double right)
        {
            if (points.Count != 4)
            {
                // 如果不是四边形，则直接返回原点
                return points;
            }

            points = SortRectanglePoints(points);
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
        /// Insets or outsets a convex polygon by a given offsetx.
        /// </summary>
        /// <param name="polygon">The list of points defining the polygon.</param>
        /// <param name="offset">The distance to inset (positive) or outset (negative).</param>
        /// <returns>A new list of points for the offsetx polygon.</returns>
        public static List<Point> InsetPolygon(List<Point> polygon, double offsetX,double offsetY)
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

                double shift = offsetX / sin_half_angle;
                double shift1 = offsetY / sin_half_angle;

                //newPoints.Add(p_curr + bisector * shift);

                newPoints.Add(new Point(p_curr.X + bisector.X * shift, p_curr.Y + bisector.Y * shift1));
            }

            return newPoints;
        }

    }

    public class FindLuminousArea : Common.MVVM.ViewModelBase
    {
        [DisplayName("Threshold")]
        public int Threshold { get => _Threshold; set { if (value > 255) value = 255; if (value < 0) value = 0; _Threshold = value; OnPropertyChanged(); } }
        private int _Threshold = 100;
    }

    public class FindLuminousAreaCorner : Common.MVVM.ViewModelBase
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

    public enum GraphicDrawTypes
    {
        Circle = 0,
        Rect = 1,
    }



    public enum GraphicBorderType
    {
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


    public class GraphicEditingConfig : Common.MVVM.ViewModelBase
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
        public bool IsQuadrilateral { get => GraphicTypes == GraphicTypes.Quadrilateral; set { if (value) GraphicTypes = GraphicTypes.Quadrilateral; OnPropertyChanged(); } }


        public GraphicDrawTypes DefaultPointType { set; get; }

        [JsonIgnore]
        public bool IsPointCircle { get => DefaultPointType == GraphicDrawTypes.Circle; set { if (value) DefaultPointType = GraphicDrawTypes.Circle; OnPropertyChanged(); } }
        [JsonIgnore]
        public bool IsPointRect { get => DefaultPointType == GraphicDrawTypes.Rect; set { if (value) DefaultPointType = GraphicDrawTypes.Rect; OnPropertyChanged(); } }
        [JsonIgnore]


        public bool LockDeafult { get => _LockDeafult; set { _LockDeafult = value; OnPropertyChanged(); } }
        private bool _LockDeafult;
        public bool UseCenter { get => _UseCenter; set { _UseCenter = value; OnPropertyChanged(); } }
        private bool _UseCenter;

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
            ComboBoxBorderType2.ItemsSource = from e1 in Enum.GetValues(typeof(DrawingGraphicPosition)).Cast<DrawingGraphicPosition>() select new KeyValuePair<DrawingGraphicPosition, string>(e1, e1.ToDescription());
            ComboBoxBorderType2.SelectedIndex = 0;

            Config = new GraphicEditingConfig();
            this.DataContext = Config;
            this.Closed += (s, e) => { ImageView = null; };
        }
        DVDatumPolygon Polygon;
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

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (Config.FindLuminousAreaCorner.UseRotatedRect)
                            {
                                var jObj = Newtonsoft.Json.Linq.JObject.Parse(result);
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
                            }
                            else
                            {
                                MRect rect = Newtonsoft.Json.JsonConvert.DeserializeObject<MRect>(result);
                                {
                                    Config.Polygon1X = rect.X;
                                    Config.Polygon1Y = rect.Y;
                                    Config.Polygon2X = rect.X + rect.Width;
                                    Config.Polygon2Y = rect.Y;
                                    Config.Polygon3X = rect.X + rect.Width;
                                    Config.Polygon3Y = rect.Y + rect.Height;
                                    Config.Polygon4X = rect.X;
                                    Config.Polygon4Y = rect.Y + rect.Height;
                                }
                            }
                            List<Point> pts_src = new();
                            pts_src.Add(Config.Polygon1);
                            pts_src.Add(Config.Polygon2);
                            pts_src.Add(Config.Polygon3);
                            pts_src.Add(Config.Polygon4);

                            List<Point> result1 = Helpers.SortPolyPoints(pts_src);
                            ImageShow.RemoveVisualCommand(Polygon);
                            Polygon = new DVDatumPolygon() { IsComple = true };
                            Polygon.Attribute.Pen = new Pen(Brushes.Blue, 1 / ImageView.Zoombox1.ContentMatrix.M11);
                            Polygon.Attribute.Brush = Brushes.Transparent;
                            Polygon.Attribute.Points.Add(result1[0]);
                            Polygon.Attribute.Points.Add(result1[1]);
                            Polygon.Attribute.Points.Add(result1[2]);
                            Polygon.Attribute.Points.Add(result1[3]);
                            Polygon.Render();
                            ImageShow.AddVisualCommand(Polygon);
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
        private static double ParseDoubleOrDefault(string input, double defaultValue = 0) => double.TryParse(input, out double result) ? result : defaultValue;

        private void ButtonImportMarinSetting(object sender, RoutedEventArgs e)
        {
            double topMargin = ParseDoubleOrDefault(TextBoxUp1.Text);
            double bottomMargin = ParseDoubleOrDefault(TextBoxDown1.Text);
            double leftMargin = ParseDoubleOrDefault(TextBoxLeft1.Text);
            double rightMargin = ParseDoubleOrDefault(TextBoxRight1.Text);

            // 将当前多边形顶点存入一个列表中以便处理
            var polygonPoints = new List<Point>
        {
                Config.Polygon1,
                Config.Polygon2,
                Config.Polygon3,
                Config.Polygon4
        };


            if (ComboBoxBorderType1.SelectedItem is KeyValuePair<GraphicBorderType, string> KeyValue && KeyValue.Key == GraphicBorderType.Relative)
            {
                polygonPoints = ImageEditorHelper.SortRectanglePoints(polygonPoints);

                double originalWidth = Math.Max(polygonPoints[1].X, polygonPoints[2].X) - Math.Min(polygonPoints[0].X, polygonPoints[3].X);
                double originalHeight = Math.Max(polygonPoints[2].Y, polygonPoints[3].Y) - Math.Min(polygonPoints[0].Y, polygonPoints[1].Y);


                // 将百分比边距转换为像素值
                topMargin = originalHeight * topMargin / 100;
                bottomMargin = originalHeight * bottomMargin / 100;
                leftMargin = originalWidth * leftMargin / 100;
                rightMargin = originalWidth * rightMargin / 100;
            }

            // 调用新的缩放方法
            var scaledPolygon = ImageEditorHelper.ScalePolygon(polygonPoints, topMargin, bottomMargin, leftMargin, rightMargin);

            // 更新 PoiConfig 中的顶点坐标
            Config.Polygon1X = (int)scaledPolygon[0].X;
            Config.Polygon1Y = (int)scaledPolygon[0].Y;
            Config.Polygon2X = (int)scaledPolygon[1].X;
            Config.Polygon2Y = (int)scaledPolygon[1].Y;
            Config.Polygon3X = (int)scaledPolygon[2].X;
            Config.Polygon3Y = (int)scaledPolygon[2].Y;
            Config.Polygon4X = (int)scaledPolygon[3].X;
            Config.Polygon4Y = (int)scaledPolygon[3].Y;

            List<Point> result1 = Helpers.SortPolyPoints(scaledPolygon);
            ImageShow.RemoveVisualCommand(Polygon);
            Polygon = new DVDatumPolygon() { IsComple = true };
            Polygon.Attribute.Pen = new Pen(Brushes.Blue, 1 / ImageView.Zoombox1.ContentMatrix.M11);
            Polygon.Attribute.Brush = Brushes.Transparent;
            Polygon.Attribute.Points.Add(result1[0]);
            Polygon.Attribute.Points.Add(result1[1]);
            Polygon.Attribute.Points.Add(result1[2]);
            Polygon.Attribute.Points.Add(result1[3]);
            Polygon.Render();
            ImageShow.AddVisualCommand(Polygon);

        }
        bool IsRun;
        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            if (IsRun) return;
            if (ImageShow.Source is not BitmapSource bitmapImage) return;
            IsRun = true;
            DrawingGraphicPosition pOIPosition = DrawingGraphicPosition.Internal;
            List<Point> pts_src = new List<Point>();

            if (ComboBoxBorderType2.SelectedValue is DrawingGraphicPosition pOIPosition1)
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

                        int did =  i + 1; ;
                        switch (Config.DefaultPointType)
                        {
                            case GraphicDrawTypes.Circle:

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
                                ImageShow.AddVisualCommand(Circle);
                                break;
                            case GraphicDrawTypes.Rect:

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
                                ImageShow.AddVisualCommand(Rectangle);
                                break;
                            default:
                                break;
                        }
                    }
                    break;
                case GraphicTypes.Quadrilateral:
                    List<Point> quadPoints =
                    [
                        Config.Polygon1,
                            Config.Polygon2,
                            Config.Polygon3,
                            Config.Polygon4,
                        ];
                    GenerateGridInQuadrilateral(quadPoints);
                    break;
                default:
                    break;
            }

            IsRun = false;
        }

        private void GenerateGridInQuadrilateral(List<Point> initialPoints)
        {
            List<Point> points = Helpers.SortPolyPoints(initialPoints);

            if (ComboBoxBorderType2.SelectedValue is DrawingGraphicPosition pOIPosition)
            {
                double offsetx = 0;
                double offsety = 0;

                switch (Config.DefaultPointType)
                {
                    case GraphicDrawTypes.Circle:
                        offsetx = Config.DefaultCircleRadius;
                        offsety = Config.DefaultCircleRadius;
                        break;
                    case GraphicDrawTypes.Rect:
                        offsetx = Config.DefaultRectWidth / 2.0;
                        offsety = Config.DefaultRectHeight/ 2.0;
                        break;
                }

                if (offsetx > 0)
                {
                    switch (pOIPosition)
                    {
                        case DrawingGraphicPosition.Internal:
                            points = ImageEditorHelper.InsetPolygon(points, -offsetx,-offsety);
                            break;
                        case DrawingGraphicPosition.External:
                            points = ImageEditorHelper.InsetPolygon(points, offsetx, offsety);
                            break;
                        case DrawingGraphicPosition.LineOn:
                        default:
                            // Do nothing
                            break;
                    }
                }
            }

            int cols = Config.AreaRectCol;
            int rows = Config.AreaRectRow;

            if (rows < 1 || cols < 1)
            {
                MessageBox.Show("点阵数的行列不能小于1", "ColorVision");
                return;
            }

            double rowStep = (rows > 1) ? 1.0 / (rows - 1) : 0;
            double columnStep = (cols > 1) ? 1.0 / (cols - 1) : 0;

            bool IsUseTextMax = rows * cols >= 10000;
            if (IsUseTextMax)
            {
                ImageView.Config.IsLayoutUpdated = false;
                ImageView.Config.IsShowText = false;
            }

            bool IsUseText = ImageView.Config.IsShowText;

            bool useGlobalBitmap = rows * cols >= 50000;


            List<Point> generatedPoints = new List<Point>();
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    // Bilinear interpolation to find the point
                    double u = i * rowStep;
                    double v = j * columnStep;

                    // Using sorted points: 0=TL, 1=TR, 2=BR, 3=BL
                    double x = (1 - u) * (1 - v) * points[0].X + (1 - u) * v * points[1].X + u * v * points[2].X + u * (1 - v) * points[3].X;
                    double y = (1 - u) * (1 - v) * points[0].Y + (1 - u) * v * points[1].Y + u * v * points[2].Y + u * (1 - v) * points[3].Y;

                    Point point = new Point(x, y);
                    generatedPoints.Add(point);
                }
            }
            
            if (useGlobalBitmap)
            {
                // 2. 获取全局画布尺寸（假设 DrawCanvas.ActualWidth/ActualHeight）
                int canvasWidth = (int)Math.Ceiling(ImageShow.ActualWidth);
                int canvasHeight = (int)Math.Ceiling(ImageShow.ActualHeight);
                if (canvasWidth == 0 || canvasHeight == 0) return;

                double minX = generatedPoints.Min(p => p.X);
                minX = minX> 0 ? minX : 0;
                double minY = generatedPoints.Min(p => p.Y);
                minY = minY > 0 ? minY : 0;
                double maxX = generatedPoints.Max(p => p.X);
                maxX = maxX < canvasWidth ? maxX : canvasWidth;
                double maxY = generatedPoints.Max(p => p.Y);
                maxY = maxY < canvasHeight ? maxY : canvasHeight;
                Rect unionRect = new Rect(new Point(minX, minY), new Point(maxX, maxY));
                // 3. 新建全局大图
                var rtb = new RenderTargetBitmap(canvasWidth, canvasHeight, 144, 144, PixelFormats.Pbgra32);

                // 4. 渲染所有选中的Visual到全局
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    for (int i = 0; i < generatedPoints.Count; i++)
                    {
                        var point = generatedPoints[i];
                        switch (Config.DefaultPointType)
                        {
                            case GraphicDrawTypes.Circle:
                                CircleProperties circleTextProperties = new CircleProperties();
                                circleTextProperties.Center = point;
                                circleTextProperties.Radius = Config.DefaultCircleRadius;
                                circleTextProperties.Brush = Brushes.Transparent;
                                circleTextProperties.Pen = new Pen(Brushes.Red, 1);
                                circleTextProperties.Id = i;
                                circleTextProperties.Name = i.ToString();
                                DVCircle Circle = new DVCircle(circleTextProperties);
                                Circle.Render();
                                dc.DrawDrawing(Circle.Drawing);
                                break;
                            case GraphicDrawTypes.Rect:
                                RectangleProperties rectangleTextProperties = new RectangleProperties();
                                rectangleTextProperties.Rect = new Rect(point.X - Config.DefaultRectWidth / 2, point.Y - Config.DefaultRectHeight / 2, Config.DefaultRectWidth, Config.DefaultRectHeight);
                                rectangleTextProperties.Brush = Brushes.Transparent;
                                rectangleTextProperties.Pen = new Pen(Brushes.Red, 1);
                                rectangleTextProperties.Id = i;
                                rectangleTextProperties.Name = i.ToString();
                                DVRectangle Rectangle = new DVRectangle(rectangleTextProperties);
                                Rectangle.Render();
                                dc.DrawDrawing(Rectangle.Drawing);
                                break;
                            default:
                                break;
                        }
                    }
                }

                rtb.Render(dv);
                // 5. 用 CroppedBitmap 截取 unionRect 区域
                var cropRect = new Int32Rect(
                    (int)Math.Floor(unionRect.X),
                    (int)Math.Floor(unionRect.Y),
                    (int)Math.Ceiling(unionRect.Width),
                    (int)Math.Ceiling(unionRect.Height)
                );
                var cropped = new CroppedBitmap(rtb, cropRect);
                var rasterVisual = new RasterizedSelectVisual(cropped, unionRect);
                rasterVisual.Attribute.Tag = generatedPoints;
                ImageShow.AddVisualCommand(rasterVisual);
            }
            else
            {
                for (int i = 0; i < generatedPoints.Count; i++)
                {
                    var point = generatedPoints[i];

                    switch (Config.DefaultPointType)
                    {
                        case GraphicDrawTypes.Circle:
                            CircleTextProperties circleTextProperties = new CircleTextProperties();
                            circleTextProperties.Center = point;
                            circleTextProperties.Radius = Config.DefaultCircleRadius;
                            circleTextProperties.Brush = Brushes.Transparent;
                            circleTextProperties.Pen = new Pen(Brushes.Red, (double)Config.DefaultCircleRadius / 30);
                            circleTextProperties.Id = i;
                            circleTextProperties.Name = i.ToString();
                            circleTextProperties.Text = string.Format("{0}{1}", TagName, i.ToString());
                            circleTextProperties.IsShowText = IsUseText;
                            DVCircleText Circle = new DVCircleText(circleTextProperties);
                            Circle.Render();
                            ImageShow.AddVisualCommand(Circle);
                            break;
                        case GraphicDrawTypes.Rect:
                            RectangleTextProperties rectangleTextProperties = new RectangleTextProperties();
                            rectangleTextProperties.Rect = new System.Windows.Rect(point.X - Config.DefaultRectWidth / 2, point.Y - Config.DefaultRectHeight / 2, Config.DefaultRectWidth, Config.DefaultRectHeight);
                            rectangleTextProperties.Brush = Brushes.Transparent;
                            rectangleTextProperties.Pen = new Pen(Brushes.Red, (double)Config.DefaultRectWidth / 30);
                            rectangleTextProperties.Id = i;
                            rectangleTextProperties.Name = i.ToString();
                            rectangleTextProperties.Text = string.Format("{0}{1}", TagName, i.ToString());
                            rectangleTextProperties.IsShowText = IsUseText;
                            DVRectangleText Rectangle = new DVRectangleText(rectangleTextProperties);
                            Rectangle.Render();
                            ImageShow.AddVisualCommand(Rectangle);
                            break;
                        default:
                            break;
                    }
                }

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
            // 对于圆形区域，此功能可能不适用或需要不同逻辑，暂时跳过
            if (Config.GraphicTypes == GraphicTypes.Circle)
            {
                return;
            }

            // 1. 获取四边形的四个顶点
            List<Point> points = new List<Point>();

            if (Config.GraphicTypes == GraphicTypes.Quadrilateral)
            {
                points.Add(new Point(Config.Polygon1X, Config.Polygon1Y));
                points.Add(new Point(Config.Polygon2X, Config.Polygon2Y));
                points.Add(new Point(Config.Polygon3X, Config.Polygon3Y));
                points.Add(new Point(Config.Polygon4X, Config.Polygon4Y));
            }

            if (!points.Any()) return;

            // 2. 找出包围盒
            double minX = points.Min(p => p.X);
            double maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y);
            double maxY = points.Max(p => p.Y);

            double areaWidth = maxX - minX;
            double areaHeight = maxY - minY;

            int cols = Config.AreaRectCol;
            int rows = Config.AreaRectRow;

            // 避免无效计算
            if (rows <= 0 || cols <= 0) return;

            // 3. 根据内切/外切/在线设置，确定除数
            double divisorCol = cols > 1 ? (double)cols - 1 : 1.0;
            double divisorRow = rows > 1 ? (double)rows - 1 : 1.0;

            if (ComboBoxBorderType2.SelectedValue is DrawingGraphicPosition pOIPosition)
            {
                switch (pOIPosition)
                {
                    case DrawingGraphicPosition.Internal:
                        divisorCol = cols;
                        divisorRow = rows;
                        break;
                    case DrawingGraphicPosition.External:
                        // 当点数为2时，除数为0，会导致问题。至少需要3个点才能定义外切。
                        if (cols > 2) divisorCol = cols - 1.5;
                        if (rows > 2) divisorRow = rows - 1.5;
                        break;
                    case DrawingGraphicPosition.LineOn:
                    default:
                        // 使用默认的 N-1 除数
                        break;
                }
            }

            // 确保除数有效，防止除以零或负数
            if (divisorCol <= 0) divisorCol = 1.0;
            if (divisorRow <= 0) divisorRow = 1.0;

            // 4. 计算并设置默认的宽度和高度
            Config.DefaultRectHeight = (int)(areaHeight / divisorRow);
            Config.DefaultRectWidth = (int)(areaWidth / divisorCol);

            // 对于圆形，让其直径等于矩形较短的边
            Config.DefaultCircleRadius = Math.Min(Config.DefaultRectWidth, Config.DefaultRectHeight) / 2;
        }

        private void Button3_Click(object sender, RoutedEventArgs e)
        {
            ImageView.ImageShow.Clear();
        }
    }
}
