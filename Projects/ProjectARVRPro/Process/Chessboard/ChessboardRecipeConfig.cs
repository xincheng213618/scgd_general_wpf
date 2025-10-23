
using ColorVision.Common.MVVM;
using ProjectARVRPro.Recipe;
using System.ComponentModel;

namespace ProjectARVRPro.Process.Chessboard
{
    public class ChessboardRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("Chessboard")]
        public RecipeBase ChessboardContras { get => _ChessboardContras; set { _ChessboardContras = value; OnPropertyChanged(); } }
        private RecipeBase _ChessboardContras = new RecipeBase(50, 0);
    }
}