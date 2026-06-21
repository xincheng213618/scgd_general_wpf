using ColorVision.UI.LogImp;
using ColorVision.Themes;
using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    [SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "WPF window releases the log binder in OnClosed.")]
    public partial class ServiceManagerWindow : Window
    {
        private ModuleLogViewerBinder? _logBinder;

        public ServiceManagerWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            var viewModel = ServiceManagerViewModel.Instance;
            DataContext = viewModel;
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
