using ColorVision.Common.MVVM;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Linq;
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
        private static readonly double[] DefaultGratingDiameters = new[] { 3d, 2d, 1d, 0.5d };

        public static MVSViewWindowConfig Instance => ConfigService.Instance.GetRequiredService<MVSViewWindowConfig>();

        public MVSViewWindowConfig()
        {
            _GratingDiametersMillimeters = new ObservableCollection<double>(DefaultGratingDiameters);
            _SelectedGratingDiameterMillimeters = DefaultGratingDiameters[0];
        }

        public PixelType PixelType { get => _PixelType; set { _PixelType = value; OnPropertyChanged(); } }
        private PixelType _PixelType = PixelType.PixelType_Gvsp_BayerGB8;

        public bool IsCoverBayer { get => _IsCoverBayer; set { _IsCoverBayer = value; OnPropertyChanged(); } }
        private bool _IsCoverBayer = true;


        public double Exposure { get => _Exposure; set { _Exposure = value; OnPropertyChanged(); } }
        private double _Exposure;


        public double MaxExposure { get => _MaxExposure; set { _MaxExposure = value; OnPropertyChanged(); } }
        private double _MaxExposure = 2499;

        public bool OnlyShowCs200Devices { get => _OnlyShowCs200Devices; set { if (_OnlyShowCs200Devices == value) return; _OnlyShowCs200Devices = value; OnPropertyChanged(); } }
        private bool _OnlyShowCs200Devices = true;

        public double SelectedGratingDiameterMillimeters { get => _SelectedGratingDiameterMillimeters; set { _SelectedGratingDiameterMillimeters = value; OnPropertyChanged(); } }
        private double _SelectedGratingDiameterMillimeters;

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public ObservableCollection<double> GratingDiametersMillimeters
        {
            get => _GratingDiametersMillimeters;
            set
            {
                _GratingDiametersMillimeters = value ?? new ObservableCollection<double>();
                NormalizeGratingDiameters();
                OnPropertyChanged();
            }
        }
        private ObservableCollection<double> _GratingDiametersMillimeters;

        public void EnsureSelectedGratingDiameter()
        {
            if (_GratingDiametersMillimeters.Count == 0)
            {
                SelectedGratingDiameterMillimeters = 0;
                return;
            }

            double matchedDiameter = _GratingDiametersMillimeters.FirstOrDefault(value => AreClose(value, SelectedGratingDiameterMillimeters));
            if (matchedDiameter <= 0)
            {
                SelectedGratingDiameterMillimeters = _GratingDiametersMillimeters[0];
                return;
            }

            if (!AreClose(matchedDiameter, SelectedGratingDiameterMillimeters))
            {
                SelectedGratingDiameterMillimeters = matchedDiameter;
            }
        }

        public bool TryAddGratingDiameter(double diameterMillimeters)
        {
            diameterMillimeters = NormalizeGratingDiameter(diameterMillimeters);
            if (diameterMillimeters <= 0)
            {
                return false;
            }

            double matchedDiameter = _GratingDiametersMillimeters.FirstOrDefault(value => AreClose(value, diameterMillimeters));
            if (matchedDiameter > 0)
            {
                SelectedGratingDiameterMillimeters = matchedDiameter;
                return true;
            }

            _GratingDiametersMillimeters.Add(diameterMillimeters);
            NormalizeGratingDiameters();
            OnPropertyChanged(nameof(GratingDiametersMillimeters));
            SelectedGratingDiameterMillimeters = diameterMillimeters;
            return true;
        }

        public bool RemoveGratingDiameter(double diameterMillimeters)
        {
            double matchedDiameter = _GratingDiametersMillimeters.FirstOrDefault(value => AreClose(value, diameterMillimeters));
            if (matchedDiameter <= 0)
            {
                return false;
            }

            _GratingDiametersMillimeters.Remove(matchedDiameter);
            NormalizeGratingDiameters();
            OnPropertyChanged(nameof(GratingDiametersMillimeters));
            EnsureSelectedGratingDiameter();
            return true;
        }

        private void NormalizeGratingDiameters()
        {
            _GratingDiametersMillimeters = new ObservableCollection<double>(
                _GratingDiametersMillimeters
                    .Where(value => value > 0)
                    .Select(NormalizeGratingDiameter)
                    .Distinct()
                    .OrderByDescending(value => value));

            EnsureSelectedGratingDiameter();
        }

        private static double NormalizeGratingDiameter(double diameterMillimeters)
        {
            return Math.Round(diameterMillimeters, 4);
        }

        private static bool AreClose(double left, double right)
        {
            return Math.Abs(left - right) < 0.0001;
        }

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
        public Core.ConoscopeModelProfile CurrentModelProfile => Core.ConoscopeManager.GetInstance().Config.CurrentModelProfile;
        public Array PixelTypes { get; } = Enum.GetValues<PixelType>();

        public int Count { get => _Count; set { _Count = value; OnPropertyChanged(); } }
        private int _Count;

        public MVSViewManager() 
        {
            Config = MVSViewWindowConfig.Instance;
            Config.EnsureSelectedGratingDiameter();
            EditMVSViewConfigCommand = new RelayCommand(a => EditMVSViewConfig());
            Core.ConoscopeManager.GetInstance().Config.ModelTypeChanged += ConoscopeConfig_ModelTypeChanged;
        }

        private void ConoscopeConfig_ModelTypeChanged(object? sender, Core.ConoscopeModelType e)
        {
            OnPropertyChanged(nameof(CurrentModelProfile));
        }


        public void EditMVSViewConfig()
        {
            new MVSGratingSettingsWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

    }
}
