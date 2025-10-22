#pragma warning disable

using ColorVision.Common.MVVM;
using ProjectARVRPro;
using ProjectARVRPro.Fix;
using ProjectARVRPro.Process.OpticCenter;
using System.ComponentModel;

namespace ProjectARVRPro.Process.OpticCenter
{
    public class OpticCenterFixConfig : ViewModelBase, IFixConfig
    {
        [Category("OpticCenter")]
        public double ImageCenterXTilt { get => _ImageCenterXTilt; set { _ImageCenterXTilt = value; OnPropertyChanged(); } }
        private double _ImageCenterXTilt = 1;

        [Category("OpticCenter")]
        public double ImageCenterYTilt { get => _ImageCenterYTilt; set { _ImageCenterYTilt = value; OnPropertyChanged(); } }
        private double _ImageCenterYTilt = 1;

        [Category("OpticCenter")]
        public double ImageCenterRotation { get => _ImageCenterRotation; set { _ImageCenterRotation = value; OnPropertyChanged(); } }
        private double _ImageCenterRotation = 1;

        [Category("OpticCenter")]
        public double OptCenterRotation { get => _OptCenterRotation; set { _OptCenterRotation = value; OnPropertyChanged(); } }
        private double _OptCenterRotation = 1;

        [Category("OpticCenter")]
        public double OptCenterXTilt { get => _OptCenterXTilt; set { _OptCenterXTilt = value; OnPropertyChanged(); } }
        private double _OptCenterXTilt = 1;

        [Category("OpticCenter")]
        public double OptCenterYTilt { get => _OptCenterYTilt; set { _OptCenterYTilt = value; OnPropertyChanged(); } }
        private double _OptCenterYTilt = 1;
    }

}