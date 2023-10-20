using ColorVision.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Device.PG
{
    /// <summary>
    /// DevicePGControl.xaml 的交互逻辑
    /// </summary>
    public partial class DevicePGControl : UserControl
    {
        public DevicePG DevicePG { get; set; }
        public ServiceManager ServiceControl { get; set; }

        public DevicePGControl(DevicePG devicePG)
        {
            DevicePG = devicePG;
            InitializeComponent();

            DevicePG.PGService.ReLoadCategoryLib();
            pgCategory.ItemsSource = DevicePG.PGService.PGCategoryLib;

            foreach (var item in DevicePG.PGService.PGCategoryLib)
            {
                if (item.Key.Equals(DevicePG.Config.Category, StringComparison.Ordinal))
                {
                    pgCategory.SelectedItem = item;
                    break;
                }
            }
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ServiceControl = ServiceManager.GetInstance();
            this.DataContext = DevicePG;

            IsNet.Checked += IsNet_Checked;
            IsComm.Checked += IsComm_Checked;
        }

        private void IsComm_Checked(object sender, RoutedEventArgs e)
        {
            TextBlockPGIP.Text = "串口";
            TextBlockPGPort.Text = "波特率";
        }

        private void IsNet_Checked(object sender, RoutedEventArgs e)
        {
            TextBlockPGIP.Text = "IP地址";
            TextBlockPGPort.Text = "端口";
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MQTTEditContent.Visibility = Visibility.Collapsed;
            MQTTShowContent.Visibility = Visibility.Visible;

            DevicePG.Config.Category = pgCategory.Text;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MQTTShowContent.Visibility = Visibility.Collapsed;
            MQTTEditContent.Visibility = Visibility.Visible;

        }
    }
}
