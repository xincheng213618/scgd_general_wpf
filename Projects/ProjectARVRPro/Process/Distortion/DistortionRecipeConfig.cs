#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using ProjectARVRPro;
using ProjectARVRPro;
using ProjectARVRPro.Process.Distortion;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace ProjectARVRPro.Process.Distortion
{
    public class DistortionRecipeConfig : ViewModelBase, IRecipeConfig
    {

        [Category("Distortion")]
        public double HorizontalTVDistortionMin { get => _HorizontalTVDistortionMin; set { _HorizontalTVDistortionMin = value; OnPropertyChanged(); } }
        private double _HorizontalTVDistortionMin;
        [Category("Distortion")]
        public double HorizontalTVDistortionMax { get => _HorizontalTVDistortionMax; set { _HorizontalTVDistortionMax = value; OnPropertyChanged(); } }
        private double _HorizontalTVDistortionMax = 2.1;
        [Category("Distortion")]
        public double VerticalTVDistortionMin { get => _VerticalTVDistortionMin; set { _VerticalTVDistortionMin = value; OnPropertyChanged(); } }
        private double _VerticalTVDistortionMin;
        [Category("Distortion")]
        public double VerticalTVDistortionMax { get => _VerticalTVDistortionMax; set { _VerticalTVDistortionMax = value; OnPropertyChanged(); } }
        private double _VerticalTVDistortionMax = 2.1;
    }
}