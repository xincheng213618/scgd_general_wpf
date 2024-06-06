using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Terminal;
using ColorVision.UI.Menus;
using ColorVision.UserSpace;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services
{
    public class WindowDevicesMenuItem : IMenuItem
    {
        public string? OwnerGuid => "Tool";

        public string? GuidId { get; set; } = "WindowDevices";
        public int Order => 3;
        public string? Header => ColorVision.Engine.Properties.Resources.MenuDevice;
        public Visibility Visibility => Visibility.Visible;
        public string? InputGestureText { get; set; }

        public object? Icon { get; set; }

        public RelayCommand Command => new(a =>
        {
            new WindowDevices() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        });

    }



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
            Grid1.DataContext = UserConfig.Instance;
            ButtonOK.Focus();
            ServicesHelper.SelectAndFocusFirstNode(TreeView1);
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
            Close();
        }

        private void TreeView1_Loaded(object sender, RoutedEventArgs e)
        {
        }



        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            WindowDevicesSetting Service = new(MQTTDevices) { Owner = this,WindowStartupLocation =WindowStartupLocation.CenterOwner };
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
