using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.SettingUp;
using System;
using System.Collections.Generic;
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
        public MQTTManager MQTTManager { get; set; }
        public WindowService()
        {
            InitializeComponent();
        }
        public ObservableCollection<MQTTServiceKind> MQTTServices { get; set; }
        private void Window_Initialized(object sender, EventArgs e)
        {
            MQTTManager = MQTTManager.GetInstance();

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
            else if (TreeView1.SelectedItem is MQTTDeviceCamera  mQTTDeviceCamera)
            {
                StackPanelShow.Children.Add(new MQTTDeviceCameraControl(mQTTDeviceCamera));
            }
            else if (TreeView1.SelectedItem is MQTTDevice mQTTDevice)
            {
                StackPanelShow.Children.Add(new MQTTDeviceControl(mQTTDevice));
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            CameraConfig cameraConfig1 = new CameraConfig
            {
                SendTopic = "Camera",
                SubscribeTopic = "CameraService",
                Name = "相机1",
                ID = "e29b14429bc375b1",
                CameraType = CameraType.LVQ,
                TakeImageMode = TakeImageMode.Normal,
                ImageBpp = 8
            };
            cameraConfig1.Name = "BV6000";

            MQTTCamera Camera1 = new MQTTCamera(cameraConfig1);

            foreach (var item in MQTTManager.MQTTCameras)
                item.Dispose();


            MQTTManager.MQTTCameras.Clear();
            MQTTManager.ServiceHeartbeats.Clear();

            MQTTManager.MQTTCameras.Add(Camera1);
            MQTTManager.ServiceHeartbeats.Add(Camera1);

            MQTTManager.MQTTCameras.Clear();

            foreach (var item in MQTTManager.MQTTSpectrums)
                item.Dispose();
            MQTTManager.MQTTCameras.Clear();

            MQTTSpectrum mQTTSpectrum = new MQTTSpectrum();
            MQTTManager.MQTTSpectrums.Add(mQTTSpectrum);


            MQTTManager.Reload();
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
