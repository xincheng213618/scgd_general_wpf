using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// InstallOptionsWindow.xaml 的交互逻辑
    /// </summary>
    public partial class InstallOptionsWindow : Window
    {
        public InstallOptionsWindow()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
