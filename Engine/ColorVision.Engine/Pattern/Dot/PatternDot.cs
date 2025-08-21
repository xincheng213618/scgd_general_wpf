using ColorVision.Common.MVVM;
using ColorVision.Engine.Pattern.NineDot;
using ColorVision.UI;
using NPOI.SS.UserModel;
using OpenCvSharp;
using SkiaSharp;
using System;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;
using static iText.Kernel.Pdf.Colorspace.PdfPattern;

namespace ColorVision.Engine.Pattern.Dot
{
    public class PatternDotConfig:ViewModelBase,IConfig
    {
        public SolidColorBrush MainBrush { get => _MainBrush; set { _MainBrush = value; NotifyPropertyChanged(); } }
        private SolidColorBrush _MainBrush = Brushes.Black;

        public SolidColorBrush AltBrush { get => _AltBrush; set { _AltBrush = value; NotifyPropertyChanged(); } }
        private SolidColorBrush _AltBrush = Brushes.Green;

        public int Spacing { get => _Spacing; set { _Spacing = value; NotifyPropertyChanged(); } }
        private int _Spacing = 20;
        public int Radius { get => _Radius; set { _Radius = value; NotifyPropertyChanged(); } }
        private int _Radius = 3;

        public int StartX { get => _StartX; set { _StartX = value; NotifyPropertyChanged(); } }
        private int _StartX = -1;

        public int StartY { get => _StartY; set { _StartY = value; NotifyPropertyChanged(); } }
        private int _StartY = -1;

        /// <summary>
        /// 列数，-1表示自适应
        /// </summary>
        public int Col { get => _Col; set { _Col = value; NotifyPropertyChanged(); } }
        private int _Col = -1;

        /// <summary>
        /// 行数，-1表示自适应
        /// </summary>
        public int Row { get => _Row; set { _Row = value; NotifyPropertyChanged(); } }
        private int _Row = -1;

        /// <summary>
        /// 是否使用矩形（为 false 时使用圆形）
        /// </summary>
        public bool UseRectangle { get => _UseRectangle; set { _UseRectangle = value; NotifyPropertyChanged(); } }
        private bool _UseRectangle = false;

        /// <summary>
        /// 矩形宽度（像素）
        /// </summary>
        public int RectWidth { get => _RectWidth; set { _RectWidth = value; NotifyPropertyChanged(); } }
        private int _RectWidth = 6;

        /// <summary>
        /// 矩形高度（像素）
        /// </summary>
        public int RectHeight { get => _RectHeight; set { _RectHeight = value; NotifyPropertyChanged(); } }
        private int _RectHeight = 6;

    }

    [DisplayName("点阵")]
    public class PatternDot : IPatternBase<PatternDotConfig>
    {
        public static PatternDotConfig Config => ConfigService.Instance.GetRequiredService<PatternDotConfig>();
        public override ViewModelBase GetConfig() => Config;
        public override UserControl GetPatternEditor() => new DotEditor(Config);

        public override Mat Gen(int height, int width)
        {
            Mat mat = new Mat(height, width, MatType.CV_8UC3, Config.MainBrush.ToScalar());

            int spacing = Math.Max(1, Config.Spacing);
            int radius = Math.Max(1, Config.Radius);

            // 起点默认到单元中心
            int startX = Config.StartX > 0 ? Config.StartX : spacing / 2;
            int startY = Config.StartY > 0 ? Config.StartY : spacing / 2;

            // 自适应行列（优先使用配置值）
            int nCol = Config.Col > 0 ? Config.Col : (int)Math.Ceiling((width - startX) / (double)spacing);
            int nRow = Config.Row > 0 ? Config.Row : (int)Math.Ceiling((height - startY) / (double)spacing);


            Scalar color = Config.AltBrush.ToScalar();
            bool useRect = Config.UseRectangle;
            int rectW = Config.RectWidth > 0 ? Config.RectWidth : Math.Max(1, 2 * radius);
            int rectH = Config.RectHeight > 0 ? Config.RectHeight : Math.Max(1, 2 * radius);

            for (int i = 0; i < nRow; i++)
            {
                int y = startY + i * spacing;
                if (y >= height) break;
                for (int j = 0; j < nCol; j++)
                {
                    int x = startX + j * spacing;
                    if (x >= width) break;

                    if (!useRect)
                    {
                        // 画实心圆
                        Cv2.Circle(mat, new OpenCvSharp.Point(x, y), radius, color, -1);
                    }
                    else
                    {
                        // 以 (x,y) 为中心的实心矩形
                        int x0 = x - rectW / 2;
                        int y0 = y - rectH / 2;

                        int xClip = Math.Max(0, x0);
                        int yClip = Math.Max(0, y0);
                        int wClip = Math.Min(rectW, width - xClip);
                        int hClip = Math.Min(rectH, height - yClip);

                        if (wClip > 0 && hClip > 0)
                        {
                            Cv2.Rectangle(mat, new Rect(xClip, yClip, wClip, hClip), color, -1);
                        }
                    }
                }
            }
            return mat;
        }
    }
}
