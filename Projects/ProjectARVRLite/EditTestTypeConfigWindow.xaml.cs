using System.Windows;

namespace ProjectARVRLite
{
    /// <summary>
    /// EditTestTypeConfigWindow.xaml 的交互逻辑
    /// </summary>
    public partial class EditTestTypeConfigWindow : Window
    {
        TestTypeConfigManager TestTypeConfigManager { get; set; }

        public EditTestTypeConfigWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            TestTypeConfigManager = TestTypeConfigManager.GetInstance();
            TestTypeDataGrid.ItemsSource = TestTypeConfigManager.TestTypeConfigs;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            TestTypeConfigManager.Save();
            this.Close();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            // Reset all to enabled
            foreach (var config in TestTypeConfigManager.TestTypeConfigs)
            {
                config.IsEnabled = true;
            }
            TestTypeConfigManager.Save();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            TestTypeConfigManager.Save();
            this.Close();
        }
    }
}
