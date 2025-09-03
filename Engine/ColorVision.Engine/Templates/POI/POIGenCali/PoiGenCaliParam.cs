#pragma warning disable IDE1006,CA1708
using System.Collections.Generic;

namespace ColorVision.Engine.Templates.POI.POIGenCali
{
    public class PoiGenCaliParam : ParamModBase
    {

        public PoiGenCaliParam()
        {
        }

        public PoiGenCaliParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {

        }

        public bool XIsEnable { get => GetValue(_XIsEnable); set { SetProperty(ref _XIsEnable, value); OnPropertyChanged(); } }
        private bool _XIsEnable;

        public bool YIsEnable { get => GetValue(_YIsEnable); set { SetProperty(ref _YIsEnable, value); OnPropertyChanged(); } }
        private bool _YIsEnable;

        public bool ZIsEnable { get => GetValue(_ZIsEnable); set { SetProperty(ref _ZIsEnable, value); OnPropertyChanged(); } }
        private bool _ZIsEnable;

        public double XThreshold { get => GetValue(_XThreshold); set { SetProperty(ref _XThreshold, value); OnPropertyChanged(); } }
        private double _XThreshold = 1;

        public double YThreshold { get => GetValue(_YThreshold); set { SetProperty(ref _YThreshold, value); OnPropertyChanged(); } }
        private double _YThreshold = 1;

        public double ZThreshold { get => GetValue(_ZThreshold); set { SetProperty(ref _ZThreshold, value); OnPropertyChanged(); } }
        private double _ZThreshold = 1;

        public GenType XGenType { get => GetValue(_XGenType); set { SetProperty(ref _XGenType, value); OnPropertyChanged(); } }
        private GenType _XGenType;

        public GenType YGenType { get => GetValue(_YGenType); set { SetProperty(ref _YGenType, value); OnPropertyChanged(); } }
        private GenType _YGenType;

        public GenType ZGenType { get => GetValue(_ZGenType); set { SetProperty(ref _ZGenType, value); OnPropertyChanged(); } }
        private GenType _ZGenType;



        public bool xIsEnable { get => GetValue(_xIsEnable); set { SetProperty(ref _xIsEnable, value); OnPropertyChanged(); } }
        private bool _xIsEnable;

        public bool yIsEnable { get => GetValue(_yIsEnable); set { SetProperty(ref _yIsEnable, value); OnPropertyChanged(); } }
        private bool _yIsEnable;

        public double xThreshold { get => GetValue(_xThreshold); set { SetProperty(ref _xThreshold, value); OnPropertyChanged(); } }
        private double _xThreshold = 1;

        public double yThreshold { get => GetValue(_yThreshold); set { SetProperty(ref _yThreshold, value); OnPropertyChanged(); } }
        private double _yThreshold = 1;


        public GenType xGenType { get => GetValue(_xGenType); set { SetProperty(ref _xGenType, value); OnPropertyChanged(); } }
        private GenType _xGenType = GenType.Difference;

        public GenType yGenType { get => GetValue(_yGenType); set { SetProperty(ref _yGenType, value); OnPropertyChanged(); } }
        private GenType _yGenType;



        public bool uIsEnable { get => GetValue(_uIsEnable); set { SetProperty(ref _uIsEnable, value); OnPropertyChanged(); } }
        private bool _uIsEnable;

        public bool vIsEnable { get => GetValue(_vIsEnable); set { SetProperty(ref _vIsEnable, value); OnPropertyChanged(); } }
        private bool _vIsEnable;

        public double uThreshold { get => GetValue(_uThreshold); set { SetProperty(ref _uThreshold, value); OnPropertyChanged(); } }
        private double _uThreshold = 1;

        public double vThreshold { get => GetValue(_vThreshold); set { SetProperty(ref _vThreshold, value); OnPropertyChanged(); } }
        private double _vThreshold = 1;

        public GenType uGenType { get => GetValue(_uGenType); set { SetProperty(ref _uGenType, value); OnPropertyChanged(); } }
        private GenType _uGenType;

        public GenType vGenType { get => GetValue(_vGenType); set { SetProperty(ref _vGenType, value); OnPropertyChanged(); } }
        private GenType _vGenType;



        public bool LabIsEnable { get => GetValue(_LabIsEnable); set { SetProperty(ref _LabIsEnable, value); OnPropertyChanged(); } }
        private bool _LabIsEnable;

        public double LabThreshold { get => GetValue(_LabThreshold); set { SetProperty(ref _LabThreshold, value); OnPropertyChanged(); } }
        private double _LabThreshold = 1;

        public GenType LabGenType { get => GetValue(_LabGenType); set { SetProperty(ref _LabGenType, value); OnPropertyChanged(); } }
        private GenType _LabGenType = GenType.Difference;

        public GenCalibrationType GenCalibrationType { get => GetValue(_GenCalibrationType); set { SetProperty(ref _GenCalibrationType, value); OnPropertyChanged(); } }
        private GenCalibrationType _GenCalibrationType = GenCalibrationType.BrightnessAndChroma;


    }
}
