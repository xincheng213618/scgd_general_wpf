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
            ComboxCameraType.ItemsSource = from e1 in Enum.GetValues(typeof(CameraType)).Cast<CameraType>()
                                           select new KeyValuePair<CameraType, string>(e1, e1.ToDescription());

            ComboxCameraModel.ItemsSource = from e1 in Enum.GetValues(typeof(CameraModel)).Cast<CameraModel>()
                                            select new KeyValuePair<CameraModel, string>(e1, e1.ToDescription());

            ComboxCameraMode.ItemsSource = from e1 in Enum.GetValues(typeof(CameraMode)).Cast<CameraMode>()
                                           select new KeyValuePair<CameraMode, string>(e1, e1.ToDescription());

            ComboxCameraTakeImageMode.ItemsSource = from e1 in Enum.GetValues(typeof(TakeImageMode)).Cast<TakeImageMode>()
                                                    select new KeyValuePair<TakeImageMode, string>(e1, e1.ToDescription());

            ComboxCameraImageBpp.ItemsSource = from e1 in Enum.GetValues(typeof(ImageBpp)).Cast<ImageBpp>()
                                               select new KeyValuePair<ImageBpp, string>(e1, e1.ToDescription());


            var type = DeviceCamera.Config.CameraType;

            if (type == CameraType.LV_Q || type == CameraType.LV_H || type == CameraType.LV_MIL_CL || type == CameraType.MIL_CL)
            {
                ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                  where e1 != ImageChannel.Three
                                                  select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
            }
            else if (type == CameraType.CV_Q || type == CameraType.BV_Q || type == CameraType.BV_H)
            {
                ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                  where e1 != ImageChannel.One
                                                  select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
            }
            else
            {
                ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                  select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());


            };


            ComboxCameraType.SelectionChanged += (s, e) =>
            {
                if (ComboxCameraType.SelectedValue is CameraType type)
                {
                    if (type == CameraType.LV_Q || type == CameraType.LV_H || type == CameraType.LV_MIL_CL || type == CameraType.MIL_CL)
                    {
                        ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                          where e1 != ImageChannel.Three
                                                          select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
                        ComboxCameraChannel.SelectedValue = ImageChannel.One;
                    }
                    else if (type == CameraType.CV_Q || type == CameraType.BV_Q || type == CameraType.BV_H)
                    {
                        ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                          where e1 != ImageChannel.One
                                                          select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
                        ComboxCameraChannel.SelectedValue = ImageChannel.Three;
                    }

                    else
                    {
                        ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                          select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
                    };
                }

            };


            List<int> BaudRates = new() { 115200, 9600, 300, 600, 1200, 2400, 4800, 14400, 19200, 38400, 57600 };
            List<string> Serials = new() { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM10" };

            TextBaudRate.ItemsSource = BaudRates;


            TextSerial.ItemsSource = Serials;

            ComboxeEvaFunc.ItemsSource = from e1 in Enum.GetValues(typeof(EvaFunc)).Cast<EvaFunc>()
                                         select new KeyValuePair<EvaFunc, string>(e1, e1.ToString());

            ComboxMotorType.ItemsSource = from e1 in Enum.GetValues(typeof(FOCUS_COMMUN)).Cast<FOCUS_COMMUN>()
                                          select new KeyValuePair<FOCUS_COMMUN, string>(e1, e1.ToString());
            int index = 0;
            ComboxMotorType.SelectionChanged += (s, e) =>
            {
                if (index++ < 1)
                    return;
                switch (DeviceCamera.Config.MotorConfig.eFOCUSCOMMUN)
                {
                    case FOCUS_COMMUN.VID_SERIAL:
                        DeviceCamera.Config.MotorConfig.BaudRate = 115200;
                        break;
                    case FOCUS_COMMUN.CANON_SERIAL:
                        DeviceCamera.Config.MotorConfig.BaudRate = 38400;
                        break;
                    case FOCUS_COMMUN.NED_SERIAL:
                        DeviceCamera.Config.MotorConfig.BaudRate = 115200;
                        break;
                    case FOCUS_COMMUN.LONGFOOT_SERIAL:
                        DeviceCamera.Config.MotorConfig.BaudRate = 115200;
                        break;
                    default:
                        break;
                }
            };

            EditConfig = DeviceCamera.Config.Clone();
            DataContext = DeviceCamera;
            EditContent.DataContext = EditConfig;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DeviceCamera.PhyCamera?.ReleaseDeviceCamera();
            EditConfig.CopyTo(DeviceCamera.Config);
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
