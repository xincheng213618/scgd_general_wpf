using ColorVision.Common.MVVM;
using ColorVision.UI;
using NPOI.SS.UserModel;
using OpenCvSharp;
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

    }

    [DisplayName("点阵")]
    public class PatternDot : IPattern
    {
        public static PatternDotConfig Config => ConfigService.Instance.GetRequiredService<PatternDotConfig>();
        public ViewModelBase GetConfig() => Config;

        public Mat Gen(int height, int width)
        {
            Mat mat = new Mat(height, width, MatType.CV_8UC3, Config.MainBrush.ToScalar());
            int spacing = Config.Spacing;
            int radius = Config.Radius;

            int startX = Config.StartX;
            int startY = Config.StartY;
            int col = Config.Col;
            int row = Config.Row;

            int nCol = col > 0 ? col : (width - startX + spacing - 1) / spacing;
            int nRow = row > 0 ? row : (height - startY + spacing - 1) / spacing;
            startX = startX >0 ? startX : (spacing) / 2;
            startY = startY > 0 ? startX : (spacing) / 2;

            for (int i = 0; i < nRow; i++)
            {
                int y = startY + i * spacing;
                if (y >= height) break;
                for (int j = 0; j < nCol; j++)
                {
                    int x = startX + j * spacing;
                    if (x >= width) break;
                    OpenCvSharp.Cv2.Circle(mat, new OpenCvSharp.Point(x, y), radius, Config.AltBrush.ToScalar(), -1);
                }
            }
            return mat;
        }
        public UserControl GetPatternEditor() => new DotEditor();
    }
}
