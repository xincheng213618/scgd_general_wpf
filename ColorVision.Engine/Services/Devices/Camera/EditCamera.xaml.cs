using ColorVision.Common.Extension;
using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Themes;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;


namespace ColorVision.Engine.Services.Devices.Camera
{
    /// <summary>
    /// EditCamera.xaml 的交互逻辑
    /// </summary>
    public partial class EditCamera : Window
    {
        public DeviceCamera DeviceCamera { get; set; }

        public MQTTCamera Service { get => DeviceCamera.DeviceService; }

        public ConfigCamera EditConfig { get; set; }

        public EditCamera(DeviceCamera mQTTDeviceCamera)
        {
            DeviceCamera = mQTTDeviceCamera;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }
        public ObservableCollection<PhyCamera> PhyCameras { get; set; } = new ObservableCollection<PhyCamera>();

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            CameraPhyID.ItemsSource = PhyCameraManager.GetInstance().PhyCameras;
            CameraPhyID.SelectedItem = PhyCameraManager.GetInstance().GetPhyCamera(DeviceCamera.Config.CameraID);
            CameraPhyID.DisplayMemberPath = "Name";



            var type = DeviceCamera.Config.CameraType;

            List<int> BaudRates = new() { 115200, 9600, 300, 600, 1200, 2400, 4800, 14400, 19200, 38400, 57600 };
            List<string> Serials = new() { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM10" };


            ComboxeEvaFunc.ItemsSource = from e1 in Enum.GetValues(typeof(EvaFunc)).Cast<EvaFunc>()
                                         select new KeyValuePair<EvaFunc, string>(e1, e1.ToString());
            EditConfig = DeviceCamera.Config.Clone();
            DataContext = DeviceCamera;
            EditContent.DataContext = EditConfig;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DeviceCamera.PhyCamera?.ReleaseDeviceCamera();
            EditConfig.CopyTo(DeviceCamera.Config);
            if (DeviceCamera.PhyCamera !=null)
                DeviceCamera.PhyCamera.ConfigChanged += DeviceCamera.PhyCameraConfigChanged;


            Close();
        }

        private void CameraPhyID_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedIndex > -1 && EditConfig !=null)
            {
                UpdateConfig();
            }
        }
        public void UpdateConfig()
        {
            if (CameraPhyID.SelectedIndex > -1)
            {
                var phyCamera = PhyCameraManager.GetInstance().PhyCameras[CameraPhyID.SelectedIndex];
                EditConfig.Channel = phyCamera.Config.Channel;
                EditConfig.CFW.CopyFrom(phyCamera.Config.CFW);
                EditConfig.MotorConfig.CopyFrom(phyCamera.Config.MotorConfig);

                EditConfig.CameraID = phyCamera.Config.CameraID;
                EditConfig.CameraType = phyCamera.Config.CameraType;
                EditConfig.CameraMode = phyCamera.Config.CameraMode;
                EditConfig.CameraModel = phyCamera.Config.CameraModel;
                EditConfig.TakeImageMode = phyCamera.Config.TakeImageMode;
                EditConfig.ImageBpp = phyCamera.Config.ImageBpp;
            }
        }

        private void UpdateConfig_Click(object sender, RoutedEventArgs e)
        {
            UpdateConfig();
        }
    }
}
