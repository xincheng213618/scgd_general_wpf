using ColorVision.Services.Device;
using System;
using System.Collections.ObjectModel;
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
        public ObservableCollection<ServiceKind> MQTTServices { get; set; }
        private void Window_Initialized(object sender, EventArgs e)
        {
            MQTTServices = ServiceManager.GetInstance().Services;
            TreeView1.ItemsSource = MQTTServices;
            ButtonOK.Focus();
        }

        private void TreeView1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            StackPanelShow.Children.Clear();
            if (TreeView1.SelectedItem is BaseChannel baseObject)
                StackPanelShow.Children.Add(baseObject.GetDeviceControl());

            if (TreeView1.SelectedItem is BaseServiceTerminal baseService)
                StackPanelShow.Children.Add(baseService.GenDeviceControl());
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ServiceManager.GetInstance().GenDeviceDisplayControl();
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
            ColorVision.Util.Tool.ExecuteCommandAsAdmin("net stop RegistrationCenterService");
            ColorVision.Util.Tool.ExecuteCommandAsAdmin("net stop CVMainService_x64");

            // ... 可能需要等待服务完全停止 ...

            ColorVision.Util.Tool.ExecuteCommandAsAdmin("net start RegistrationCenterService");
            ColorVision.Util.Tool.ExecuteCommandAsAdmin("net start CVMainService_x64");
        }
    }
}
