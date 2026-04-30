
using ColorVision.Common.MVVM;
using ProjectARVRPro.Recipe;
using System.ComponentModel;

namespace ProjectARVRPro.Process.Chessboard
{
    public class ChessboardRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("Chessboard")]
        public RecipeBase ChessboardContrast { get => _ChessboardContrast; set { _ChessboardContrast = value; OnPropertyChanged(); } }
        private RecipeBase _ChessboardContrast = new RecipeBase(50, 0);
    }
}