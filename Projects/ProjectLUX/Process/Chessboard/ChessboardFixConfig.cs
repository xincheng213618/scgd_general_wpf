#pragma warning disable

using ColorVision.Common.MVVM;
using ProjectLUX;
using ProjectLUX.Fix;
using ProjectLUX.Process.Chessboard;
using System.ComponentModel;

namespace ProjectLUX.Process.Chessboard
{
    public class ChessboardFixConfig : ViewModelBase, IFixConfig
    {
        [Category("Chessboard")]
        public double ChessboardContrast { get => _ChessboardContrast; set { _ChessboardContrast = value; OnPropertyChanged(); } }
        private double _ChessboardContrast = 1;
    }

}