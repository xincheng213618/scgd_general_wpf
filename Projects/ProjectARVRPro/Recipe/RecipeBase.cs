#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using NPOI.SS.Formula.Functions;
using ProjectARVRPro;
using ProjectARVRPro;
using ProjectARVRPro.Process;
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

namespace ProjectARVRPro.Recipe
{
    public class RecipeBase : ViewModelBase
    {
        public RecipeBase()
        {

        }
        public RecipeBase(double min,double max)
        {
            _Min = min;
            _Max = max;
        }
        public double Min { get => _Min; set { _Min = value; OnPropertyChanged(); } }
        private double _Min;

        public double Max { get => _Max; set { _Max = value; OnPropertyChanged(); } }
        private double _Max;
    }
}