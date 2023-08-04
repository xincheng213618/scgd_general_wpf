using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.SettingUp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

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

            //ServiceConfig serviceConfig = new ServiceConfig();
            //serviceConfig.SendTopic = "Camera";
            //serviceConfig.SubscribeTopic = "CameraService";
            //CameraConfig cameraConfig1 = new CameraConfig(serviceConfig);
            //cameraConfig1.Name = "相机1";
            //cameraConfig1.CameraID = "e29b14429bc375b1";
            //cameraConfig1.CameraType = CameraType.LVQ;
            //cameraConfig1.TakeImageMode = TakeImageMode.Normal;
            //cameraConfig1.ImageBpp = 8;
            //cameraConfig1.Name = "BV";

            //MQTTCamera Camera1 = new MQTTCamera(cameraConfig1);

            //MQTTManager.GetInstance().MQTTCameras.Clear();
            //MQTTManager.GetInstance().ServiceHeartbeats.Clear();
            //MQTTManager.GetInstance().MQTTCameras.Add(new KeyValuePair<string, MQTTCamera>("camera", Camera1));
            //MQTTManager.GetInstance().ServiceHeartbeats.Add(Camera1);

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
            MQTTManager.GetInstance().Reload();
        }
    }
}
