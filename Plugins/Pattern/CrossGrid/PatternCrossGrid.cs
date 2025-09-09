using ColorVision.Common.MVVM;
using ColorVision.UI;
using OpenCvSharp;
using System;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Pattern.CrossGrid
{

    public class PatternCrossGridConfig:ViewModelBase,IConfig
    {

        // 网格参数
        public int NumLinesHorizontal { get => _NumLinesHorizontal; set { _NumLinesHorizontal = value; OnPropertyChanged(); } }
        private int _NumLinesHorizontal = 3;

        public int NumLinesVertical { get => _NumLinesVertical; set { _NumLinesVertical = value; OnPropertyChanged(); } }
        private int _NumLinesVertical = 3;

        public int SpacingHorizontal { get => _SpacingHorizontal; set { _SpacingHorizontal = value; OnPropertyChanged(); } }
        private int _SpacingHorizontal = 400;

        public int SpacingVertical { get => _SpacingVertical; set { _SpacingVertical = value; OnPropertyChanged(); } }
        private int _SpacingVertical = 400;

        public int HorizontalLineWidth { get => _HorizontalLineWidth; set { _HorizontalLineWidth = value; OnPropertyChanged(); } }
        private int _HorizontalLineWidth = 9;

        public int VerticalLineWidth { get => _VerticalLineWidth; set { _VerticalLineWidth = value; OnPropertyChanged(); } }
        private int _VerticalLineWidth = 9;


        public SolidColorBrush LineBrush { get => _LineBrush; set { _LineBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _LineBrush = Brushes.White;

        public SolidColorBrush BackgroundBrush { get => _BackgroundBrush; set { _BackgroundBrush = value; OnPropertyChanged(); } }
        private SolidColorBrush _BackgroundBrush = Brushes.Black;


        // 额外的边缘/内边距加粗线（模拟 MATLAB 中 200px 处的加粗线）
        public bool AddEdgeLines { get => _AddEdgeLines; set { _AddEdgeLines = value; OnPropertyChanged(); } }
        private bool _AddEdgeLines = true;

        public int EdgeOffsetPx { get => _EdgeOffsetPx; set { _EdgeOffsetPx = value; OnPropertyChanged(); } }
        private int _EdgeOffsetPx = 200;

        public int EdgeThickness { get => _EdgeThickness; set { _EdgeThickness = value; OnPropertyChanged(); } }
        private int _EdgeThickness = 9;

        // 是否水平拼接两张（等价于 [img, img]）
        public bool DuplicateHorizontally { get => _DuplicateHorizontally; set { _DuplicateHorizontally = value; OnPropertyChanged(); } }
        private bool _DuplicateHorizontally = true;
    }

    [DisplayName("十字网格")]
    public class PatternCrossGrid : IPatternBase<PatternCrossGridConfig>
    {
        public override UserControl GetPatternEditor() => new CrossGridEditor(Config); // 可自定义编辑器

        public override Mat Gen(int height, int width)
        {
            Scalar bg = Config.BackgroundBrush.ToScalar();
            Scalar line = Config.LineBrush.ToScalar();

            Mat img = new Mat(height, width, MatType.CV_8UC3, bg);

            // 计算水平线位置（用 height）
            if (Config.NumLinesHorizontal > 0 && Config.SpacingHorizontal > 0)
            {
                int startY = (height - (Config.NumLinesHorizontal * Config.SpacingHorizontal)) + 1; // 对齐 MATLAB 逻辑
                for (int i = 0; i < Config.NumLinesHorizontal; i++)
                {
                    int y = startY + i * Config.SpacingHorizontal;
                    DrawHorizontalLine(img, y, Config.HorizontalLineWidth, line);
                }
            }

            // 计算竖直线位置（用 width）
            if (Config.NumLinesVertical > 0 && Config.SpacingVertical > 0)
            {
                int startX = (width - (Config.NumLinesVertical * Config.SpacingVertical)) + 1; // 对齐 MATLAB 逻辑
                for (int i = 0; i < Config.NumLinesVertical; i++)
                {
                    int x = startX + i * Config.SpacingVertical;
                    DrawVerticalLine(img, x, Config.VerticalLineWidth, line);
                }
            }

            // 边缘/内边距加粗线，默认在距离边界 EdgeOffsetPx 处加粗（水平+竖直）
            if (Config.AddEdgeLines && Config.EdgeOffsetPx > 0 && Config.EdgeThickness > 0)
            {
                int off = Config.EdgeOffsetPx;
                int t = Config.EdgeThickness;

                // 水平两条
                if (off >= 0 && off < height)
                    DrawHorizontalLine(img, off, t, line);
                if (height - off >= 0 && height - off < height)
                    DrawHorizontalLine(img, height - off, t, line);

                // 竖直两条
                if (off >= 0 && off < width)
                    DrawVerticalLine(img, off, t, line);
                if (width - off >= 0 && width - off < width)
                    DrawVerticalLine(img, width - off, t, line);
            }

            if (Config.DuplicateHorizontally)
            {
                Mat combined = new Mat();
                Cv2.HConcat(new[] { img, img }, combined);
                return combined;
            }

            return img;
        }

        private static void DrawHorizontalLine(Mat img, int centerY, int thickness, Scalar color)
        {
            if (thickness <= 0) return;
            int h = img.Rows;
            int w = img.Cols;
            if (h <= 0 || w <= 0) return;

            // 以 centerY 为中心画 thickness 像素高的矩形
            int half = thickness / 2;
            int y0 = centerY - half;
            if (y0 < 0 || y0 >= h) return;

            // 裁剪高度，避免越界
            int y = Math.Max(0, y0);
            int rectH = Math.Min(thickness, h - y);
            if (rectH <= 0) return;

            Cv2.Rectangle(img, new Rect(0, y, w, rectH), color, -1);
        }

        private static void DrawVerticalLine(Mat img, int centerX, int thickness, Scalar color)
        {
            if (thickness <= 0) return;
            int h = img.Rows;
            int w = img.Cols;
            if (h <= 0 || w <= 0) return;

            int half = thickness / 2;
            int x0 = centerX - half;
            if (x0 < 0 || x0 >= w) return;

            int x = Math.Max(0, x0);
            int rectW = Math.Min(thickness, w - x);
            if (rectW <= 0) return;

            Cv2.Rectangle(img, new Rect(x, 0, rectW, h), color, -1);
        }
    }
}
