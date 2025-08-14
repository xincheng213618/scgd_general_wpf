using ColorVision.Common.MVVM;
using ColorVision.UI;
using OpenCvSharp;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Pattern.Checkerboard
{
    public enum CheckerboardSizeMode
    {
        ByGridCount,
        ByCellSize
    }
    public class PatternCheckerboardConfig:ViewModelBase,IConfig
    {
        public SolidColorBrush MainBrush { get => _MainBrush; set { _MainBrush = value; NotifyPropertyChanged(); } }
        private SolidColorBrush _MainBrush = Brushes.White;

        public SolidColorBrush AltBrush { get => _AltBrush; set { _AltBrush = value; NotifyPropertyChanged(); } }
        private SolidColorBrush _AltBrush = Brushes.Black;

        public int GridX { get => _GridX; set { _GridX = value; NotifyPropertyChanged(); } }
        private int _GridX = 8;
        public int GridY { get => _GridY; set { _GridY = value; NotifyPropertyChanged(); } }
        private int _GridY = 8;
        public int CellW { get => _CellW; set { _CellW = value; NotifyPropertyChanged(); } }
        private int _CellW = 32;
        public int CellH { get => _CellH; set { _CellH = value; NotifyPropertyChanged(); } }
        private int _CellH = 32;

        public CheckerboardSizeMode SizeMode { get => _SizeMode; set { _SizeMode = value; NotifyPropertyChanged(); } }
        private CheckerboardSizeMode _SizeMode = CheckerboardSizeMode.ByGridCount;
    }

    [DisplayName("棋盘格")]
    public class PatternCheckerboard : IPatternBase
    {
        public static PatternCheckerboardConfig Config => ConfigService.Instance.GetRequiredService<PatternCheckerboardConfig>();
        public override ViewModelBase GetConfig() => Config;
        public override UserControl GetPatternEditor() => new CheckerboardEditor();


        public override Mat Gen(int height, int width)
        {
            var mat = new Mat(height, width, MatType.CV_8UC3, Config.MainBrush.ToScalar());

            int gridX, gridY, cellW, cellH;

            if (Config.SizeMode == CheckerboardSizeMode.ByGridCount)
            {
                gridX = Config.GridX;
                gridY = Config.GridY;
                cellW = width / gridX;
                cellH = height / gridY;
            }
            else // ByCellSize
            {
                cellW = Config.CellW;
                cellH = Config.CellH;
                gridX = width / cellW;
                gridY = height / cellH;
                // 保证至少有一格
                if (gridX < 1) gridX = 1;
                if (gridY < 1) gridY = 1;
            }
            // 自动补全到边界
            for (int y = 0; y < gridY; y++)
            {
                for (int x = 0; x < gridX; x++)
                {
                    int startX = x * cellW;
                    int startY = y * cellH;
                    int w = (x == gridX - 1) ? width - startX : cellW;
                    int h = (y == gridY - 1) ? height - startY : cellH;
                    if ((x + y) % 2 == 1)
                        Cv2.Rectangle(mat, new Rect(startX, startY, w, h), Config.AltBrush.ToScalar(), -1);
                }
            }
            return mat;
        }
    }
}
