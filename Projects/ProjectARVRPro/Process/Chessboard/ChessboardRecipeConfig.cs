#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using ProjectARVRPro;
using ProjectARVRPro;
using ProjectARVRPro.Process.Chessboard;
using ProjectARVRPro.Recipe;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace ProjectARVRPro.Process.Chessboard
{
    public class ChessboardRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("Chessboard")]
        public RecipeBase ChessboardContras { get => _ChessboardContras; set { _ChessboardContras = value; OnPropertyChanged(); } }
        private RecipeBase _ChessboardContras = new RecipeBase(50, 0);
    }
}