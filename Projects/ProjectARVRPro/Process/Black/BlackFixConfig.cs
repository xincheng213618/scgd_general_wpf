#pragma warning disable

using ColorVision.Common.MVVM;
using ProjectARVRPro;
using ProjectARVRPro.Fix;
using ProjectARVRPro.Process.Black;
using System.ComponentModel;

namespace ProjectARVRPro.Process.Black
{
    public class BlackFixConfig : ViewModelBase, IFixConfig
    {
        [Category("Black")]
        public double FOFOContrast { get => _FOFOContrast; set { _FOFOContrast = value; OnPropertyChanged(); } }
        private double _FOFOContrast = 1;
    }

}