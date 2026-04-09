using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// ServiceInstallWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ServiceInstallWindow : Window
    {
        public ServiceInstallViewModel ViewModel { get; }

        public ServiceInstallWindow()
        {
            InitializeComponent();
            ViewModel = new ServiceInstallViewModel();
            ViewModel.CloseAction = () => Close();
            DataContext = ViewModel;
        }
    }
}
