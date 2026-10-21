using ColorVision.Themes;
using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// ServiceInstallWindow.xaml 的交互逻辑
    /// </summary>
    [SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The window releases log capture in OnClosed.")]
    public partial class ServiceInstallWindow : Window
    {
        public ServiceInstallViewModel ViewModel { get; }

        public ServiceInstallWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            ViewModel = new ServiceInstallViewModel();
            ViewModel.InstallationCompleted += ViewModel_InstallationCompleted;
            DataContext = ViewModel;
            OperationLog.StartCapture();
        }

        private void ViewModel_InstallationCompleted(object? sender, EventArgs e) => Close();

        protected override void OnClosed(EventArgs e)
        {
            ViewModel.InstallationCompleted -= ViewModel_InstallationCompleted;
            OperationLog.Dispose();
            base.OnClosed(e);
        }
    }
}
