#pragma warning disable

using ColorVision.Common.MVVM;
using ProjectLUX;
using ProjectLUX.Fix;
using ProjectLUX.Process.ChessboardAR;
using ProjectLUX.Recipe;
using System.ComponentModel;

namespace ProjectLUX.Process.ChessboardAR
{
    public class ChessboardARFixConfig : ViewModelBase, IFixConfig
    {
        [Category("Chessboard")]
        public double ChessboardContrast { get => _ChessboardContrast; set { _ChessboardContrast = value; OnPropertyChanged(); } }
        private double _ChessboardContrast = 1;

        [Category("Chessboard")]
        public double AverageWhiteLunimance { get => _AverageWhiteLunimance; set { _AverageWhiteLunimance = value; OnPropertyChanged(); } }
        private double _AverageWhiteLunimance = 1;

        [Category("Chessboard")]
        public double AverageBlackLunimance { get => _AverageBlackLunimance; set { _AverageBlackLunimance = value; OnPropertyChanged(); } }
        private double _AverageBlackLunimance = 1;

        [Category("Chessboard")] public double P1Lv { get => _P1Lv; set { _P1Lv = value; OnPropertyChanged(); } }
        private double _P1Lv = 1;
        [Category("Chessboard")] public double P2Lv { get => _P2Lv; set { _P2Lv = value; OnPropertyChanged(); } }
        private double _P2Lv = 1;
        [Category("Chessboard")] public double P3Lv { get => _P3Lv; set { _P3Lv = value; OnPropertyChanged(); } }
        private double _P3Lv = 1;
        [Category("Chessboard")] public double P4Lv { get => _P4Lv; set { _P4Lv = value; OnPropertyChanged(); } }
        private double _P4Lv = 1;
        [Category("Chessboard")] public double P5Lv { get => _P5Lv; set { _P5Lv = value; OnPropertyChanged(); } }
        private double _P5Lv = 1;
        [Category("Chessboard")] public double P6Lv { get => _P6Lv; set { _P6Lv = value; OnPropertyChanged(); } }
        private double _P6Lv = 1;
        [Category("Chessboard")] public double P7Lv { get => _P7Lv; set { _P7Lv = value; OnPropertyChanged(); } }
        private double _P7Lv = 1;
        [Category("Chessboard")] public double P8Lv { get => _P8Lv; set { _P8Lv = value; OnPropertyChanged(); } }
        private double _P8Lv = 1;
        [Category("Chessboard")] public double P9Lv { get => _P9Lv; set { _P9Lv = value; OnPropertyChanged(); } }
        private double _P9Lv = 1;
        [Category("Chessboard")] public double P10Lv { get => _P10Lv; set { _P10Lv = value; OnPropertyChanged(); } }
        private double _P10Lv = 1;
        [Category("Chessboard")] public double P11Lv { get => _P11Lv; set { _P11Lv = value; OnPropertyChanged(); } }
        private double _P11Lv = 1;
        [Category("Chessboard")] public double P12Lv { get => _P12Lv; set { _P12Lv = value; OnPropertyChanged(); } }
        private double _P12Lv = 1;
        [Category("Chessboard")] public double P13Lv { get => _P13Lv; set { _P13Lv = value; OnPropertyChanged(); } }
        private double _P13Lv = 1;
        [Category("Chessboard")] public double P14Lv { get => _P14Lv; set { _P14Lv = value; OnPropertyChanged(); } }
        private double _P14Lv = 1;
        [Category("Chessboard")] public double P15Lv { get => _P15Lv; set { _P15Lv = value; OnPropertyChanged(); } }
        private double _P15Lv = 1;
        [Category("Chessboard")] public double P16Lv { get => _P16Lv; set { _P16Lv = value; OnPropertyChanged(); } }
        private double _P16Lv = 1;

    }

}