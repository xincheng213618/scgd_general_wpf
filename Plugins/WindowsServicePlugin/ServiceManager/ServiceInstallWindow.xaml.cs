using ColorVision.UI.LogImp;
using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// ServiceInstallWindow.xaml 的交互逻辑
    /// </summary>
    [SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "WPF window releases the log binder in OnClosed.")]
    public partial class ServiceInstallWindow : Window
    {
        public ServiceInstallViewModel ViewModel { get; }
        private ModuleLogViewerBinder? _logBinder;

        public ServiceInstallWindow()
        {
            InitializeComponent();
            ViewModel = new ServiceInstallViewModel();
            DataContext = ViewModel;
            _logBinder = new ModuleLogViewerBinder(LogViewer, "WindowsServicePlugin.ServiceManager");
        }

        protected override void OnClosed(EventArgs e)
        {
            _logBinder?.Dispose();
            _logBinder = null;
            base.OnClosed(e);
        }
    }
}
