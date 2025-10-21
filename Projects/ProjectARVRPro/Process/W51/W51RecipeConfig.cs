#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using ProjectARVRPro;
using ProjectARVRPro;
using ProjectARVRPro.Process.W51;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace ProjectARVRPro.Process.W51
{
    public class W51RecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("W51")]
        [DisplayName("Horizontal Field Of View Angle(°) Min")]
        public double HorizontalFieldOfViewAngleMin { get => _HorizontalFieldOfViewAngleMin; set { _HorizontalFieldOfViewAngleMin = value; OnPropertyChanged(); } }
        private double _HorizontalFieldOfViewAngleMin = 23.5;
        [Category("W51")]
        [DisplayName("Horizontal Field Of View Angle(°) Max")]
        public double HorizontalFieldOfViewAngleMax { get => _HorizontalFieldOfViewAngleMax; set { _HorizontalFieldOfViewAngleMax = value; OnPropertyChanged(); } }
        private double _HorizontalFieldOfViewAngleMax = 24.5;
        [Category("W51")]
        [DisplayName("Vertical Field of View Angle(°) Min")]
        public double VerticalFieldOfViewAngleMin { get => _VerticalFieldOfViewAngleMin; set { _VerticalFieldOfViewAngleMin = value; OnPropertyChanged(); } }
        private double _VerticalFieldOfViewAngleMin = 21.5;
        [Category("W51")]
        [DisplayName("Vertical Field of View Angle(°) Max")]
        public double VerticalFieldOfViewAngleMax { get => _VerticalFieldOfViewAngleMax; set { _VerticalFieldOfViewAngleMax = value; OnPropertyChanged(); } }
        private double _VerticalFieldOfViewAngleMax = 22.5;
        [Category("W51")]
        [DisplayName("Diagonal  Field of View Angle(°) Min")]
        public double DiagonalFieldOfViewAngleMin { get => _DiagonalFieldOfViewAngleMin; set { _DiagonalFieldOfViewAngleMin = value; OnPropertyChanged(); } }
        private double _DiagonalFieldOfViewAngleMin = 11.5;
        [Category("W51")]
        [DisplayName("Diagonal  Field of View Angle(°) Max")]
        public double DiagonalFieldOfViewAngleMax { get => _DiagonalFieldOfViewAngleMax; set { _DiagonalFieldOfViewAngleMax = value; OnPropertyChanged(); } }
        private double _DiagonalFieldOfViewAngleMax = 12.5;
    }
}