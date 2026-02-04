
using ColorVision.Common.MVVM;
using ProjectLUX.Recipe;
using System.ComponentModel;

namespace ProjectLUX.Process.ChessboardAR
{
    public class ChessboardARRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("Chessboard")]
        public RecipeBase ChessboardContrast { get => _ChessboardContras; set { _ChessboardContras = value; OnPropertyChanged(); } }
        private RecipeBase _ChessboardContras = new RecipeBase(50, 0);

        [Category("Chessboard")]
        public RecipeBase AverageWhiteLunimance { get => _AverageWhiteLunimance; set { _AverageWhiteLunimance = value; OnPropertyChanged(); } }
        private RecipeBase _AverageWhiteLunimance = new RecipeBase(0, 0);

        [Category("Chessboard")]
        public RecipeBase AverageBlackLunimance { get => _AverageBlackLunimance; set { _AverageBlackLunimance = value; OnPropertyChanged(); } }
        private RecipeBase _AverageBlackLunimance = new RecipeBase(50, 0);

        // Lv Recipe（P1 ~ P16）
        [Category("Chessboard")] public RecipeBase P1Lv { get => _P1Lv; set { _P1Lv = value; OnPropertyChanged(); } }
        private RecipeBase _P1Lv = new RecipeBase(0, 0);
        [Category("Chessboard")] public RecipeBase P2Lv { get => _P2Lv; set { _P2Lv = value; OnPropertyChanged(); } }
        private RecipeBase _P2Lv = new RecipeBase(0, 0);
        [Category("Chessboard")] public RecipeBase P3Lv { get => _P3Lv; set { _P3Lv = value; OnPropertyChanged(); } }
        private RecipeBase _P3Lv = new RecipeBase(0, 0);
        [Category("Chessboard")] public RecipeBase P4Lv { get => _P4Lv; set { _P4Lv = value; OnPropertyChanged(); } }
        private RecipeBase _P4Lv = new RecipeBase(0, 0);
        [Category("Chessboard")] public RecipeBase P5Lv { get => _P5Lv; set { _P5Lv = value; OnPropertyChanged(); } }
        private RecipeBase _P5Lv = new RecipeBase(0, 0);
        [Category("Chessboard")] public RecipeBase P6Lv { get => _P6Lv; set { _P6Lv = value; OnPropertyChanged(); } }
        private RecipeBase _P6Lv = new RecipeBase(0, 0);
        [Category("Chessboard")] public RecipeBase P7Lv { get => _P7Lv; set { _P7Lv = value; OnPropertyChanged(); } }
        private RecipeBase _P7Lv = new RecipeBase(0, 0);
        [Category("Chessboard")] public RecipeBase P8Lv { get => _P8Lv; set { _P8Lv = value; OnPropertyChanged(); } }
        private RecipeBase _P8Lv = new RecipeBase(0, 0);
        [Category("Chessboard")] public RecipeBase P9Lv { get => _P9Lv; set { _P9Lv = value; OnPropertyChanged(); } }
        private RecipeBase _P9Lv = new RecipeBase(0, 0);
        [Category("Chessboard")] public RecipeBase P10Lv { get => _P10Lv; set { _P10Lv = value; OnPropertyChanged(); } }
        private RecipeBase _P10Lv = new RecipeBase(0, 0);
        [Category("Chessboard")] public RecipeBase P11Lv { get => _P11Lv; set { _P11Lv = value; OnPropertyChanged(); } }
        private RecipeBase _P11Lv = new RecipeBase(0, 0);
        [Category("Chessboard")] public RecipeBase P12Lv { get => _P12Lv; set { _P12Lv = value; OnPropertyChanged(); } }
        private RecipeBase _P12Lv = new RecipeBase(0, 0);
        [Category("Chessboard")] public RecipeBase P13Lv { get => _P13Lv; set { _P13Lv = value; OnPropertyChanged(); } }
        private RecipeBase _P13Lv = new RecipeBase(0, 0);
        [Category("Chessboard")] public RecipeBase P14Lv { get => _P14Lv; set { _P14Lv = value; OnPropertyChanged(); } }
        private RecipeBase _P14Lv = new RecipeBase(0, 0);
        [Category("Chessboard")] public RecipeBase P15Lv { get => _P15Lv; set { _P15Lv = value; OnPropertyChanged(); } }
        private RecipeBase _P15Lv = new RecipeBase(0, 0);
        [Category("Chessboard")] public RecipeBase P16Lv { get => _P16Lv; set { _P16Lv = value; OnPropertyChanged(); } }
        private RecipeBase _P16Lv = new RecipeBase(0, 0);

    }
}