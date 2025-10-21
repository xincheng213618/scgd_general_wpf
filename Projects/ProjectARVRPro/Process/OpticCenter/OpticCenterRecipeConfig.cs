#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using ProjectARVRPro;
using ProjectARVRPro;
using ProjectARVRPro.Process.OpticCenter;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace ProjectARVRPro.Process.OpticCenter
{
    public class OpticCenterRecipeConfig : ViewModelBase, IRecipeConfig
    {
        [Category("OpticCenter")]
        public double OptCenterXTiltMin { get => _OptCenterXTiltMin; set { _OptCenterXTiltMin = value; OnPropertyChanged(); } }
        private double _OptCenterXTiltMin = -0.16;
        [Category("OpticCenter")]
        public double OptCenterXTiltMax { get => _OptCenterXTiltMax; set { _OptCenterXTiltMax = value; OnPropertyChanged(); } }
        private double _OptCenterXTiltMax = 0.16;
        [Category("OpticCenter")]
        public double OptCenterYTiltMin { get => _OptCenterYTiltMin; set { _OptCenterYTiltMin = value; OnPropertyChanged(); } }
        private double _OptCenterYTiltMin = -0.16;
        [Category("OpticCenter")]
        public double OptCenterYTiltMax { get => _OptCenterYTiltMax; set { _OptCenterYTiltMax = value; OnPropertyChanged(); } }
        private double _OptCenterYTiltMax = 0.16;
        [Category("OpticCenter")]
        public double OptCenterRotationMin { get => _OptCenterRotationMin; set { _OptCenterRotationMin = value; OnPropertyChanged(); } }
        private double _OptCenterRotationMin = -0.16;
        [Category("OpticCenter")]
        public double OptCenterRotationMax { get => _OptCenterRotationMax; set { _OptCenterRotationMax = value; OnPropertyChanged(); } }
        private double _OptCenterRotationMax = 0.16;
        [Category("OpticCenter")]
        public double ImageCenterXTiltMin { get => _ImageCenterXTiltMin; set { _ImageCenterXTiltMin = value; OnPropertyChanged(); } }
        private double _ImageCenterXTiltMin = -0.16;
        [Category("OpticCenter")]
        public double ImageCenterXTiltMax { get => _ImageCenterXTiltMax; set { _ImageCenterXTiltMax = value; OnPropertyChanged(); } }
        private double _ImageCenterXTiltMax = 0.16;
        [Category("OpticCenter")]
        public double ImageCenterYTiltMin { get => _ImageCenterYTiltMin; set { _ImageCenterYTiltMin = value; OnPropertyChanged(); } }
        private double _ImageCenterYTiltMin = -0.16;
        [Category("OpticCenter")]
        public double ImageCenterYTiltMax { get => _ImageCenterYTiltMax; set { _ImageCenterYTiltMax = value; OnPropertyChanged(); } }
        private double _ImageCenterYTiltMax = 0.16;
        [Category("OpticCenter")]
        public double ImageCenterRotationMin { get => _ImageCenterRotationMin; set { _ImageCenterRotationMin = value; OnPropertyChanged(); } }
        private double _ImageCenterRotationMin = -0.16;
        [Category("OpticCenter")]
        public double ImageCenterRotationMax { get => _ImageCenterRotationMax; set { _ImageCenterRotationMax = value; OnPropertyChanged(); } }
        private double _ImageCenterRotationMax = 0.16;
    }
}