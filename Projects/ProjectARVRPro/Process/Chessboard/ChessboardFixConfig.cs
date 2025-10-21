#pragma warning disable

using ColorVision.Common.MVVM;
using ProjectARVRPro;
using ProjectARVRPro.Fix;
using ProjectARVRPro.Process.Chessboard;
using System.ComponentModel;

namespace ProjectARVRPro.Process.Chessboard
{
    public class ChessboardFixConfig : ViewModelBase, IFixConfig
    {
        [Category("Chessboard")]
        public double ChessboardContrast { get => _ChessboardContrast; set { _ChessboardContrast = value; OnPropertyChanged(); } }
        private double _ChessboardContrast = 1;
    }

}