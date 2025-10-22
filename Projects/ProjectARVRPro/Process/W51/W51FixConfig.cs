#pragma warning disable

using ColorVision.Common.MVVM;
using ProjectARVRPro;
using ProjectARVRPro.Fix;
using ProjectARVRPro.Process.W51;
using System.ComponentModel;

namespace ProjectARVRPro.Process.W51
{
    public class W51FixConfig : ViewModelBase, IFixConfig
    {
        [Category("W51")]
        public double W51HorizontalFieldOfViewAngle { get => _W51HorizontalFieldOfViewAngle; set { _W51HorizontalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private double _W51HorizontalFieldOfViewAngle = 1;
        [Category("W51")]
        public double W51VerticalFieldOfViewAngle { get => _W51VerticalFieldOfViewAngle; set { _W51VerticalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private double _W51VerticalFieldOfViewAngle = 1;
        [Category("W51")]
        public double W51DiagonalFieldOfViewAngle { get => _W51DiagonalFieldOfViewAngle; set { _W51DiagonalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private double _W51DiagonalFieldOfViewAngle = 1;
    }

}