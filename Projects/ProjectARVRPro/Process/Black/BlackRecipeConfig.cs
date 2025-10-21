#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using ProjectARVRPro;
using ProjectARVRPro;
using ProjectARVRPro.Process.Black;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace ProjectARVRPro.Process.Black
{
    public class BlackRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("Black")]

        public double FOFOContrastMin { get => _FOFOContrastMin; set { _FOFOContrastMin = value; OnPropertyChanged(); } }
        private double _FOFOContrastMin = 100000;
        [Category("Black")]
        public double FOFOContrastMax { get => _FOFOContrastMax; set { _FOFOContrastMax = value; OnPropertyChanged(); } }
        private double _FOFOContrastMax;
    }
}