
using System.Collections.Generic;

namespace ColorVision.Engine.Templates.POI.POIOutput
{
    public class PoiOutputParam : ParamModBase
    {
        public PoiOutputParam()
        {


        }

        public PoiOutputParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {

        }

        public bool XIsEnable { get => GetValue(_XIsEnable); set { SetProperty(ref _XIsEnable, value); OnPropertyChanged(); } }
        private bool _XIsEnable;
        public string? XFileName { get => GetValue(_XFileName); set { SetProperty(ref _XFileName, value); OnPropertyChanged(); } }
        private string? _XFileName;

        public bool YIsEnable { get => GetValue(_YIsEnable); set { SetProperty(ref _YIsEnable, value); OnPropertyChanged(); } }
        private bool _YIsEnable;

        public string? YFileName { get => GetValue(_YFileName); set { SetProperty(ref _YFileName, value); OnPropertyChanged(); } }
        private string? _YFileName;

        public bool ZIsEnable { get => GetValue(_ZIsEnable); set { SetProperty(ref _ZIsEnable, value); OnPropertyChanged(); } }
        private bool _ZIsEnable;
        public string? ZFileName { get => GetValue(_ZFileName); set { SetProperty(ref _ZFileName, value); OnPropertyChanged(); } }
        private string? _ZFileName;


        public bool xIsEnable { get => GetValue(_xIsEnable); set { SetProperty(ref _xIsEnable, value); OnPropertyChanged(); } }
        private bool _xIsEnable;

        public string? xFileName { get => GetValue(_xFileName); set { SetProperty(ref _xFileName, value); OnPropertyChanged(); } }
        private string? _xFileName;

        public bool yIsEnable { get => GetValue(_yIsEnable); set { SetProperty(ref _yIsEnable, value); OnPropertyChanged(); } }
        private bool _yIsEnable;
        public string? yFileName { get => GetValue(_yFileName); set { SetProperty(ref _yFileName, value); OnPropertyChanged(); } }
        private string? _yFileName;

        public bool uIsEnable { get => GetValue(_uIsEnable); set { SetProperty(ref _uIsEnable, value); OnPropertyChanged(); } }
        private bool _uIsEnable;

        public string? uFileName { get => GetValue(_uFileName); set { SetProperty(ref _uFileName, value); OnPropertyChanged(); } }
        private string? _uFileName;

        public bool vIsEnable { get => GetValue(_vIsEnable); set { SetProperty(ref _vIsEnable, value); OnPropertyChanged(); } }
        private bool _vIsEnable;

        public string? vFileName { get => GetValue(_vFileName); set { SetProperty(ref _vFileName, value); OnPropertyChanged(); } }
        private string? _vFileName;


        public bool CCTIsEnable { get => GetValue(_CCTIsEnable); set { SetProperty(ref _CCTIsEnable, value); OnPropertyChanged(); } }
        private bool _CCTIsEnable;

        public string? CCTFileName { get => GetValue(_CCTFileName); set { SetProperty(ref _CCTFileName, value); OnPropertyChanged(); } }
        private string? _CCTFileName;

        public bool XYZIsEnable { get => GetValue(_XYZIsEnable); set { SetProperty(ref _XYZIsEnable, value); OnPropertyChanged(); } }
        private bool _XYZIsEnable;

        public string? XYZFileName { get => GetValue(_XYZFileName); set { SetProperty(ref _XYZFileName, value); OnPropertyChanged(); } }
        private string? _XYZFileName;


        public bool WaveIsEnable { get => GetValue(_WaveIsEnable); set { SetProperty(ref _WaveIsEnable, value); OnPropertyChanged(); } }
        private bool _WaveIsEnable;

        public string? WaveFileName { get => GetValue(_WaveFileName); set { SetProperty(ref _WaveFileName, value); OnPropertyChanged(); } }
        private string? _WaveFileName;

        public string? MaskFileName { get => GetValue(_MaskFileName); set { SetProperty(ref _MaskFileName, value); OnPropertyChanged(); } }
        private string? _MaskFileName;




        public int Width { get => GetValue(_Width); set { SetProperty(ref _Width, value); OnPropertyChanged(); } }
        private int _Width;
        public int Height { get => GetValue(_Height); set { SetProperty(ref _Height, value); OnPropertyChanged(); } }
        private int _Height;
    }
}
