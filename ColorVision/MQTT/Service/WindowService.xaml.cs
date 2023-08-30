using ColorVision.Device;
using ColorVision.Device.Camera;
using ColorVision.Device.PG;
using ColorVision.Device.SMU;
using ColorVision.Device.Spectrum;
using ColorVision.MQTT;
using ColorVision.MQTT.Service;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Service
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
        public ObservableCollection<MQTTServiceKind> MQTTServices { get; set; }
        private void Window_Initialized(object sender, EventArgs e)
        {
            MQTTServices = ServiceControl.GetInstance().MQTTServices;
            TreeView1.ItemsSource = MQTTServices;


        }

        private void TreeView1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            StackPanelShow.Children.Clear();
            if (TreeView1.SelectedItem is BaseObject baseObject)
                StackPanelShow.Children.Add(baseObject.GenDeviceControl());
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ServiceControl.GetInstance().GenContorl();
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
    }
}
