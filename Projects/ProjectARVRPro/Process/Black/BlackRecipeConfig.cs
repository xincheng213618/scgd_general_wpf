#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using NPOI.SS.Formula.Functions;
using ProjectARVRPro;
using ProjectARVRPro;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.Black;
using ProjectARVRPro.Recipe;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace ProjectARVRPro.Process.Black
{

    public class BlackRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("Black")]
        public RecipeBase FOFOContrast { get => _FOFOContrast; set { _FOFOContrast = value; OnPropertyChanged(); } }
        private RecipeBase _FOFOContrast = new RecipeBase(100000, 0);
    }
}