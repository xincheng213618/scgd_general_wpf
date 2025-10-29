
using ColorVision.Common.MVVM;
using ProjectLUX.Fix;
using System.ComponentModel;

namespace ProjectLUX.Process.AR.W51AR
{
    public class W51ARFixConfig : ViewModelBase, IFixConfig
    {
        [Category("W51FOV")]
        public double W51HorizontalFieldOfViewAngle { get => _W51HorizontalFieldOfViewAngle; set { _W51HorizontalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private double _W51HorizontalFieldOfViewAngle = 1;
        [Category("W51FOV")]
        public double W51VerticalFieldOfViewAngle { get => _W51VerticalFieldOfViewAngle; set { _W51VerticalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private double _W51VerticalFieldOfViewAngle = 1;

        [Category("W51FOV")]
        public double W51DiagonalFieldOfViewAngle { get => _W51DiagonalFieldOfViewAngle; set { _W51DiagonalFieldOfViewAngle = value; OnPropertyChanged(); } }
        private double _W51DiagonalFieldOfViewAngle = 1;
    }

}