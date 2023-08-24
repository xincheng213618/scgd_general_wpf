using ColorVision.MQTT;
using ColorVision.MQTT.Camera;
using ColorVision.MQTT.PG;
using ColorVision.MQTT.Service;
using ColorVision.MQTT.SMU;
using ColorVision.MQTT.Spectrum;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.SettingUp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Service
{

    /// <summary>
    /// WindowService.xaml 的交互逻辑
    /// </summary>
    public partial class WindowService1 : Window
    {
        public WindowService1()
        {
            InitializeComponent();
        }

        public ObservableCollection<MQTTDevice> MQTTDevices { get; set; }
        private void Window_Initialized(object sender, EventArgs e)
        {
            MQTTDevices = ServiceControl.GetInstance().MQTTDevices;
            TreeView1.ItemsSource = MQTTDevices;
            Grid1.DataContext = GlobalSetting.GetInstance().SoftwareConfig.UserConfig;

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
            else if (TreeView1.SelectedItem is DevicePG  devicePG)
            {
                StackPanelShow.Children.Add(new DevicePGControl(devicePG));

                
            }

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ServiceControl.GetInstance().GenControl(MQTTDevices);

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
            ServiceSettingWindow Service = new ServiceSettingWindow();
            Service.Closed += async (s, e) =>
            {
                if (Service.MQTTDevices1.Count > 0)
                {
                    MQTTDevices = Service.MQTTDevices1;
                    TreeView1.ItemsSource = MQTTDevices;
                }
                await Task.Delay(10);
                TreeViewItem firstNode = TreeView1.ItemContainerGenerator.ContainerFromIndex(1) as TreeViewItem;

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
