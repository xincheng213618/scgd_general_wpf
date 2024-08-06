#pragma warning disable IDE1006
using ColorVision.Engine.Templates;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POIGenCali
{
    public class PoiGenCaliParam : ParamBase
    {

        public PoiGenCaliParam()
        {
        }

        public PoiGenCaliParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {

        }

        public bool XYZIsEnable { get =>GetValue(_XYZIsEnable); set { SetProperty(ref _XYZIsEnable, value); NotifyPropertyChanged(); } }
        private bool _XYZIsEnable;

        public double XYZThreshold { get => GetValue(_XYZThreshold); set { SetProperty(ref _XYZThreshold, value); NotifyPropertyChanged(); } }
        private double _XYZThreshold = 1;

        public GenType XYZXGenType { get => GetValue(_XYZXGenType); set { SetProperty(ref _XYZXGenType, value); NotifyPropertyChanged(); } }
        private GenType _XYZXGenType;

        public bool xyIsEnable { get => GetValue(_xyIsEnable); set { SetProperty(ref _xyIsEnable, value); NotifyPropertyChanged(); } }
        private bool _xyIsEnable;

        public double xyThreshold { get => GetValue(_xyThreshold); set { SetProperty(ref _xyThreshold, value); NotifyPropertyChanged(); } }
        private double _xyThreshold = 1;

        public GenType xyGenType { get => GetValue(_xyGenType); set { SetProperty(ref _xyGenType, value); NotifyPropertyChanged(); } }
        private GenType _xyGenType;


        public bool uvIsEnable { get => GetValue(_uvIsEnable); set { SetProperty(ref _uvIsEnable, value); NotifyPropertyChanged(); } }
        private bool _uvIsEnable;

        public double uvThreshold { get => GetValue(_uvThreshold); set { SetProperty(ref _uvThreshold, value); NotifyPropertyChanged(); } }
        private double _uvThreshold = 1;

        public GenType uvGenType { get => GetValue(_uvGenType); set { SetProperty(ref _uvGenType, value); NotifyPropertyChanged(); } }
        private GenType _uvGenType;


        public bool LabIsEnable { get => GetValue(_LabIsEnable); set { SetProperty(ref _LabIsEnable, value); NotifyPropertyChanged(); } }
        private bool _LabIsEnable;

        public double LabThreshold { get => GetValue(_LabThreshold); set { SetProperty(ref _LabThreshold, value); NotifyPropertyChanged(); } }
        private double _LabThreshold = 1;

        public GenType LabGenType { get => GetValue(_LabGenType); set { SetProperty(ref _LabGenType, value); NotifyPropertyChanged(); } }
        private GenType _LabGenType;

        public GenCalibrationType GenCalibrationType { get => GetValue(_GenCalibrationType); set { SetProperty(ref _GenCalibrationType, value); NotifyPropertyChanged(); } }
        private GenCalibrationType _GenCalibrationType;


    }
}
