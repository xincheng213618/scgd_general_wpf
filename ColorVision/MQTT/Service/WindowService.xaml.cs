using ColorVision.Device.Camera;
using ColorVision.Device.PG;
using ColorVision.Device.SMU;
using ColorVision.Device.Spectrum;
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

            if (TreeView1.SelectedItem is MQTTServiceKind mQTTServiceKind)
            {
                StackPanelShow.Children.Add( new MQTTServiceKindControl(mQTTServiceKind));
            }
            else if (TreeView1.SelectedItem is MQTTService mQTTService)
            {
                StackPanelShow.Children.Add(new MQTTServiceControl(mQTTService));
            }
            else if (TreeView1.SelectedItem is DeviceCamera  mQTTDeviceCamera)
            {
                StackPanelShow.Children.Add(new DeviceCameraControl(mQTTDeviceCamera));
            }
            else if (TreeView1.SelectedItem is DeviceSpectrum mQTTDeviceSpectrum)
            {
                StackPanelShow.Children.Add(new DeviceSpectrumControl(mQTTDeviceSpectrum));
            }
            else if (TreeView1.SelectedItem is DeviceSMU mQTTDeviceSMU)
            {
                StackPanelShow.Children.Add(new DeviceSMUControl(mQTTDeviceSMU));
            }
            else if (TreeView1.SelectedItem is DevicePG mQTTDevicePG)
            {
                StackPanelShow.Children.Add(new DevicePGControl(mQTTDevicePG));
            }
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
