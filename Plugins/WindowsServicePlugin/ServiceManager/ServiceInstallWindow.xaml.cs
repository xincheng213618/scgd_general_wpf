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
            DataContext = ViewModel;
            OperationLog.StartCapture();
        }

        protected override void OnClosed(EventArgs e)
        {
            OperationLog.Dispose();
            base.OnClosed(e);
        }
    }
}
