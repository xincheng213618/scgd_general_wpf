using ColorVision.Common.MVVM;
using ColorVision.UI;
using log4net;
using System.Windows;

namespace Conoscope.Core
{

    public class ConoscopeManager : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ConoscopeManager));
        private static ConoscopeManager _instance;
        private static readonly object _locker = new();
        public static ConoscopeManager GetInstance() { lock (_locker) { return _instance ??= new ConoscopeManager(); } }

        public ConoscopeConfig Config { get; set; }
        public ConoscopeGlobalReferenceStore GlobalReferences { get; }
        public RelayCommand EditConoscopeConfigCommand { get; set; }

        public void EditConoscopeConfig()
        {
            new ConoscopeConfigWindow(Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            ConoscopeModuleService.RefreshAllConoscopeConfiguration();
        }

        public ConoscopeManager() 
        {
            Config =ConfigService.Instance.GetRequiredService<ConoscopeConfig>();
            GlobalReferences = new ConoscopeGlobalReferenceStore(Config);
            EditConoscopeConfigCommand = new RelayCommand(a=> EditConoscopeConfig());
        }

    }
}
