using ColorVision.Services.RC;
using ColorVision.Services.Devices;
using ColorVision.Services.Terminal;
using ColorVision.Settings;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services
{

    /// <summary>
    /// WindowService.xaml 的交互逻辑
    /// </summary>
    public partial class WindowService : Window
    {
        public WindowService()
        {
            InitializeComponent();
        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            int i = ConfigHandler.GetInstance().SoftwareConfig.ServicesSetting.ShowType;
            switch (i % 3)
            {
                case 0:
                    TreeView1.ItemsSource = ServiceManager.GetInstance().TypeServices;
                    break;
                case 1:
                    TreeView1.ItemsSource = ServiceManager.GetInstance().TerminalServices;
                    break;
                case 2:
                    TreeView1.ItemsSource = ServiceManager.GetInstance().DeviceServices;
                    break;
                default:
                    break;
            }
            SelectAndFocusFirstNode(TreeView1);
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
            ServiceManager.GetInstance().GenDeviceDisplayControl();
            Close();
        }

        private void TreeView1_Loaded(object sender, RoutedEventArgs e)
        {

        }

        public async void SelectAndFocusFirstNode(TreeView treeView)
        {
            await Task.Delay(1);
            if (treeView.Items.Count > 0)
            {
                if (treeView.SelectedItem == null && treeView.ItemContainerGenerator.ContainerFromIndex(0) is TreeViewItem firstNode)
                {
                    firstNode.IsSelected = true;
                    Dispatcher.Invoke(() => firstNode.Focus());
                }
            }
        }
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MQTTRCService.GetInstance().RestartServices();
            MessageBox.Show(Application.Current.MainWindow,"命令已经发送");
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            ConfigHandler.GetInstance().SoftwareConfig.ServicesSetting.ShowType = (ConfigHandler.GetInstance().SoftwareConfig.ServicesSetting.ShowType +1) % 3;
            switch (ConfigHandler.GetInstance().SoftwareConfig.ServicesSetting.ShowType)
            {
                case 0:
                    TreeView1.ItemsSource = ServiceManager.GetInstance().TypeServices;
                    SelectAndFocusFirstNode(TreeView1);
                    break;
                case 1:
                    TreeView1.ItemsSource = ServiceManager.GetInstance().TerminalServices;
                    SelectAndFocusFirstNode(TreeView1);
                    break;
                case 2:
                    TreeView1.ItemsSource = ServiceManager.GetInstance().DeviceServices;
                    SelectAndFocusFirstNode(TreeView1);
                    break;
                default:
                    break;
            }
        }
    }
}
