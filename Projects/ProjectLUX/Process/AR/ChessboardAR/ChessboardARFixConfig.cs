#pragma warning disable

using ColorVision.Common.MVVM;
using ProjectLUX;
using ProjectLUX.Fix;
using ProjectLUX.Process.ChessboardAR;
using System.ComponentModel;

namespace ProjectLUX.Process.ChessboardAR
{
    public class ChessboardARFixConfig : ViewModelBase, IFixConfig
    {
        [Category("Chessboard")]
        public double ChessboardContrast { get => _ChessboardContrast; set { _ChessboardContrast = value; OnPropertyChanged(); } }
        private double _ChessboardContrast = 1;
    }

}