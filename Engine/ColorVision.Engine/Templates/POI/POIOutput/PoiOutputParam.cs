#pragma warning disable IDE1006,CA1708
using ColorVision.Common.MVVM;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;

namespace ColorVision.Engine.Templates.POI.POIOutput
{
    public class PoiOutputParam : ParamModBase
    {

        public static void SetFile(object target, string propertyName)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Tif Files (*.tif)|*.txt|All Files (*.*)|*.*";
                saveFileDialog.Title = "Save File";
                saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                saveFileDialog.RestoreDirectory = true;
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    PropertyInfo prop = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null && prop.CanWrite)
                    {
                        prop.SetValue(target, saveFileDialog.FileName, null);
                    }
                }
            }
        }

        public RelayCommand XSetFileCommand { get; set; }
        public RelayCommand YSetFileCommand { get; set; }
        public RelayCommand ZSetFileCommand { get; set; }
        public RelayCommand xSetFileCommand { get; set; }
        public RelayCommand ySetFileCommand { get; set; }
        public RelayCommand uSetFileCommand { get; set; }
        public RelayCommand vSetFileCommand { get; set; }
        public RelayCommand CCTSetFileCommand { get; set; }
        public RelayCommand WaveSetFileCommand { get; set; }


        public PoiOutputParam()
        {
            XSetFileCommand = new RelayCommand((a) => SetFile(this, nameof(XFileName)));
            YSetFileCommand = new RelayCommand((a) => SetFile(this, nameof(YFileName)));
            ZSetFileCommand = new RelayCommand((a) => SetFile(this, nameof(ZFileName)));
            xSetFileCommand = new RelayCommand((a) => SetFile(this, nameof(xFileName)));
            ySetFileCommand = new RelayCommand((a) => SetFile(this, nameof(yFileName)));
            uSetFileCommand = new RelayCommand((a) => SetFile(this, nameof(uFileName)));
            vSetFileCommand = new RelayCommand((a) => SetFile(this, nameof(vFileName)));
            CCTSetFileCommand = new RelayCommand((a) => SetFile(this, nameof(CCTFileName)));
            WaveSetFileCommand = new RelayCommand((a) => SetFile(this, nameof(WaveFileName)));

        }

        public PoiOutputParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
            XSetFileCommand = new RelayCommand((a) => SetFile(this, nameof(XFileName)));
            YSetFileCommand = new RelayCommand((a) => SetFile(this, nameof(YFileName)));
            ZSetFileCommand = new RelayCommand((a) => SetFile(this, nameof(ZFileName)));
            xSetFileCommand = new RelayCommand((a) => SetFile(this, nameof(xFileName)));
            ySetFileCommand = new RelayCommand((a) => SetFile(this, nameof(yFileName)));
            uSetFileCommand = new RelayCommand((a) => SetFile(this, nameof(uFileName)));
            vSetFileCommand = new RelayCommand((a) => SetFile(this, nameof(vFileName)));
            CCTSetFileCommand = new RelayCommand((a) => SetFile(this, nameof(CCTFileName)));
            WaveSetFileCommand = new RelayCommand((a) => SetFile(this, nameof(WaveFileName)));
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
