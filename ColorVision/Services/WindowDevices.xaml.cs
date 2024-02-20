using ColorVision.Services.Devices;
using ColorVision.Settings;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services
{

    /// <summary>
    /// WindowService.xaml 的交互逻辑
    /// </summary>
    public partial class WindowDevices : Window
    {
        public WindowDevices()
        {
            InitializeComponent();
        }

        public ObservableCollection<DeviceService> MQTTDevices { get; set; }
        private void Window_Initialized(object sender, EventArgs e)
        {
            MQTTDevices = ServiceManager.GetInstance().LastGenControl ?? ServiceManager.GetInstance().DeviceServices;
            TreeView1.ItemsSource = MQTTDevices;
            Grid1.DataContext = ConfigHandler.GetInstance().SoftwareConfig.UserConfig;
            ButtonOK.Focus();
        }

        private void TreeView1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            StackPanelShow.Children.Clear();
            if (TreeView1.SelectedItem is DeviceService baseObject)
                StackPanelShow.Children.Add(baseObject.GetDeviceControl());

            if (TreeView1.SelectedItem is TerminalServiceBase baseService)
                StackPanelShow.Children.Add(baseService.GenDeviceControl());
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ServiceManager.GetInstance().GenControl(MQTTDevices);

            this.Close();
        }

        private void TreeView1_Loaded(object sender, RoutedEventArgs e)
        {
            TreeViewItem firstNode = TreeView1.ItemContainerGenerator.ContainerFromIndex(0) as TreeViewItem;

            // 选中第一个节点
            if (firstNode != null)
            {
                firstNode.IsSelected = true;
                firstNode.Focus();
            }
        }



        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            WindowDevicesSetting Service = new WindowDevicesSetting(MQTTDevices) { Owner = this,WindowStartupLocation =WindowStartupLocation.CenterOwner };
            Service.Closed += async (s, e) =>
            {
                if (Service.MQTTDevices1.Count > 0)
                {
                    MQTTDevices = Service.MQTTDevices1;
                    TreeView1.ItemsSource = MQTTDevices;
                }
                await Task.Delay(10);
                TreeViewItem firstNode = TreeView1.ItemContainerGenerator.ContainerFromIndex(0) as TreeViewItem;
                // 选中第一个节点
                if (firstNode != null)
                {
                    firstNode.IsSelected = true;
                    firstNode.Focus();
                }
            };
            Service.ShowDialog();

        }
    }
}
