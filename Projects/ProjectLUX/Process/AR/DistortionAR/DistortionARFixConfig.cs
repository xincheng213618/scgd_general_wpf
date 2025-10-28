#pragma warning disable

using ColorVision.Common.MVVM;
using ProjectLUX;
using ProjectLUX.Fix;
using ProjectLUX.Process.DistortionAR;
using System.ComponentModel;

namespace ProjectLUX.Process.DistortionAR
{
    public class DistortionARFixConfig : ViewModelBase, IFixConfig
    {
        [Category("Distortion")]
        public double HorizontalTVDistortion { get => _HorizontalTVDistortion; set { _HorizontalTVDistortion = value; OnPropertyChanged(); } }
        private double _HorizontalTVDistortion = 1;
        [Category("Distortion")]
        public double VerticalTVDistortion { get => _VerticalTVDistortion; set { _VerticalTVDistortion = value; OnPropertyChanged(); } }
        private double _VerticalTVDistortion = 1;
    }

}