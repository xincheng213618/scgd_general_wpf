#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using ProjectARVRPro;
using ProjectARVRPro;
using ProjectARVRPro.Process.Chessboard;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace ProjectARVRPro.Process.Chessboard
{
    public class ChessboardRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("Chessboard")]
        public double ChessboardContrastMin { get => _ChessboardContrastMin; set { _ChessboardContrastMin = value; OnPropertyChanged(); } }
        private double _ChessboardContrastMin = 50;
        [Category("Chessboard")]
        public double ChessboardContrastMax { get => _ChessboardContrastMax; set { _ChessboardContrastMax = value; OnPropertyChanged(); } }
        private double _ChessboardContrastMax;
    }
}