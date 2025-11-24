using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.UI;
using log4net;
using System.Windows;

namespace ProjectStarkSemi.Conoscope
{
    public class ConoscopeManager : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ConoscopeManager));
        private static ConoscopeManager _instance;
        private static readonly object _locker = new();
        public static ConoscopeManager GetInstance() { lock (_locker) { return _instance ??= new ConoscopeManager(); } }

        public ConoscopeConfig ConoscopeConfig { get; set; }
        public RelayCommand EditConoscopeConfigCommand { get; set; }

        public void EditConoscopeConfig()
        {
            new PropertyEditorWindow(ConoscopeConfig) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public ConoscopeManager() 
        {
            ConoscopeConfig =ConfigService.Instance.GetRequiredService<ConoscopeConfig>();
            EditConoscopeConfigCommand = new RelayCommand(a=> EditConoscopeConfig());
        }

        public bool IsShowRedChannel { get=> _IsShowRedChannel; set { _IsShowRedChannel = value; OnPropertyChanged(); } }
        private bool _IsShowRedChannel = true;
        public bool IsShowGreenChannel { get => _IsShowGreenChannel; set { _IsShowGreenChannel = value; OnPropertyChanged(); } }
        private bool _IsShowGreenChannel = true;

        public bool IsShowBlueChannel { get => _IsShowBlueChannel; set { _IsShowBlueChannel = value; OnPropertyChanged(); } }
        private bool _IsShowBlueChannel = true;

        public bool IsShowXChannel { get => _IsShowXChannel; set { _IsShowXChannel = value; OnPropertyChanged(); } }
        private bool _IsShowXChannel = true;

        public bool IsShowYChannel { get => _IsShowYChannel; set { _IsShowYChannel = value; OnPropertyChanged(); } }
        private bool _IsShowYChannel = true;

        public bool IsShowZChannel { get => _IsShowZChannel; set { _IsShowZChannel = value; OnPropertyChanged(); } }
        private bool _IsShowZChannel = true;


    }
}
