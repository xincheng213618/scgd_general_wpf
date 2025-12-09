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

        public ConoscopeConfig Config { get; set; }
        public RelayCommand EditConoscopeConfigCommand { get; set; }

        public void EditConoscopeConfig()
        {
            new PropertyEditorWindow(Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public ConoscopeManager() 
        {
            Config =ConfigService.Instance.GetRequiredService<ConoscopeConfig>();
            EditConoscopeConfigCommand = new RelayCommand(a=> EditConoscopeConfig());
        }




    }
}
