using ColorVision.Common.MVVM;
using ColorVision.UI;
using log4net;
using System.Windows;

namespace Conoscope.MVS
{
    public enum PixelType
    {
        PixelType_Gvsp_BayerGB8 = 0x0108000a,
        PixelType_Gvsp_RGB8_Packed = 0x02180014
    }

    public class MVSViewWindowConfig : ViewModelBase, IConfig
    {
        public static MVSViewWindowConfig Instance => ConfigService.Instance.GetRequiredService<MVSViewWindowConfig>();

        public PixelType PixelType { get => _PixelType; set { _PixelType = value; OnPropertyChanged(); } }
        private PixelType _PixelType = PixelType.PixelType_Gvsp_BayerGB8;

        public bool IsCoverBayer { get => _IsCoverBayer; set { _IsCoverBayer = value; OnPropertyChanged(); } }
        private bool _IsCoverBayer = true;


        public double Exposure { get => _Exposure; set { _Exposure = value; OnPropertyChanged(); } }
        private double _Exposure;


        public double MaxExposure { get => _MaxExposure; set { _MaxExposure = value; OnPropertyChanged(); } }
        private double _MaxExposure = 2499;

        public double SelectedGratingDiameterMillimeters { get => _SelectedGratingDiameterMillimeters; set { _SelectedGratingDiameterMillimeters = value; OnPropertyChanged(); } }
        private double _SelectedGratingDiameterMillimeters;


    }

    public class MVSViewManager:ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MVSViewManager));
        private static MVSViewManager _instance;
        private static readonly object _locker = new();
        public static MVSViewManager GetInstance() { lock (_locker) { return _instance ??= new MVSViewManager(); } }

        public MVSViewWindowConfig Config { get; set; }
        public RelayCommand EditMVSViewConfigCommand { get; set; }
        public bool IsOpen { get; set; }

        public int Count { get => _Count; set { _Count = value; OnPropertyChanged(); } }
        private int _Count;

        public MVSViewManager() 
        {
            Config = MVSViewWindowConfig.Instance;
            EditMVSViewConfigCommand = new RelayCommand(a => EditMVSViewConfig());
        }


        public void EditMVSViewConfig()
        {
            new PropertyEditorWindow(Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

    }
}
