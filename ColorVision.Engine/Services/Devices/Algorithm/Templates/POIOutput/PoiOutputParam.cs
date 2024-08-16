#pragma warning disable IDE1006,CA1708
using ColorVision.Engine.Templates;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.PoiOutput
{
    public class PoiOutputParam : ParamBase
    {

        public PoiOutputParam()
        {
        }

        public PoiOutputParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {

        }
        public bool XIsEnable { get => GetValue(_XIsEnable); set { SetProperty(ref _XIsEnable, value); NotifyPropertyChanged(); } }
        private bool _XIsEnable;

        public bool YIsEnable { get => GetValue(_YIsEnable); set { SetProperty(ref _YIsEnable, value); NotifyPropertyChanged(); } }
        private bool _YIsEnable;

        public bool ZIsEnable { get => GetValue(_ZIsEnable); set { SetProperty(ref _ZIsEnable, value); NotifyPropertyChanged(); } }
        private bool _ZIsEnable;


        public bool xIsEnable { get => GetValue(_xIsEnable); set { SetProperty(ref _xIsEnable, value); NotifyPropertyChanged(); } }
        private bool _xIsEnable;

        public bool yIsEnable { get => GetValue(_yIsEnable); set { SetProperty(ref _yIsEnable, value); NotifyPropertyChanged(); } }
        private bool _yIsEnable;


        public bool uIsEnable { get => GetValue(_uIsEnable); set { SetProperty(ref _uIsEnable, value); NotifyPropertyChanged(); } }
        private bool _uIsEnable;

        public bool vIsEnable { get => GetValue(_vIsEnable); set { SetProperty(ref _vIsEnable, value); NotifyPropertyChanged(); } }
        private bool _vIsEnable;


        public bool LabIsEnable { get => GetValue(_LabIsEnable); set { SetProperty(ref _LabIsEnable, value); NotifyPropertyChanged(); } }
        private bool _LabIsEnable;

    }
}
