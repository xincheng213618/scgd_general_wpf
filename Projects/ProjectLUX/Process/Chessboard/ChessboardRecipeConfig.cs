
using ColorVision.Common.MVVM;
using ProjectLUX.Recipe;
using System.ComponentModel;

namespace ProjectLUX.Process.Chessboard
{
    public class ChessboardRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("Chessboard")]
        public RecipeBase ChessboardContras { get => _ChessboardContras; set { _ChessboardContras = value; OnPropertyChanged(); } }
        private RecipeBase _ChessboardContras = new RecipeBase(50, 0);
    }
}