using ColorVision.Common.MVVM;
using ProjectLUX.Fix;
using System.ComponentModel;

namespace ProjectLUX.Process.Sprectrum
{
    public class SprectrumFixConfig : ViewModelBase, IFixConfig
    {
        [Category("Sprectrum")]
        public double LuminousFlux { get => _LuminousFlux; set { _LuminousFlux = value; OnPropertyChanged(); } }
        private double _LuminousFlux = 1;
    }

}