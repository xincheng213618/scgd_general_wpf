using ColorVision.Extension;
using cvColorVision;
using FlowEngineLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Device.Camera
{
    /// <summary>
    /// DevicePGControl.xaml 的交互逻辑
    /// </summary>
    public partial class DeviceCameraControl : UserControl
    {
        public DeviceCamera DeviceCamera { get; set; }

        public DeviceServiceCamera Service { get => DeviceCamera.DeviceService; }

        public DeviceCameraControl(DeviceCamera mQTTDeviceCamera)
        {
            DeviceCamera = mQTTDeviceCamera;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            CameraID.ItemsSource = DeviceCamera.Service.DevicesSN;

            this.DataContext = DeviceCamera;

            ComboxCameraType.ItemsSource = from e1 in Enum.GetValues(typeof(CameraType)).Cast<CameraType>()
                                           select new KeyValuePair<CameraType, string>(e1, e1.ToDescription());

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
                    else
                    {
                        ComboxCameraChannel.ItemsSource = from e1 in Enum.GetValues(typeof(ImageChannel)).Cast<ImageChannel>()
                                                          select new KeyValuePair<ImageChannel, string>(e1, e1.ToDescription());
                    };
                }

            };



            var ImageChannelTypeList = new[]{
                 new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_X, "Channel_R"),
                 new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_Y, "Channel_G"),
                 new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_Z, "Channel_B")
            };
            chType1.ItemsSource = ImageChannelTypeList;
            chType2.ItemsSource = ImageChannelTypeList;
            chType3.ItemsSource = ImageChannelTypeList;


            Dictionary<ImageChannelType, ComboBox> keyValuePairs = new Dictionary<ImageChannelType, ComboBox>() { };
            keyValuePairs.Add(ImageChannelType.Gray_X,chType1);
            keyValuePairs.Add(ImageChannelType.Gray_Y, chType2);
            keyValuePairs.Add(ImageChannelType.Gray_Z, chType3);


            chType1.SelectionChanged += (s, e) =>
            {
                ComboBox comboBox = keyValuePairs[Service.Config.CFW.CFW[0].ChannelType];
                ImageChannelType lasttemp = keyValuePairs.FirstOrDefault(x => x.Value == chType1).Key;
                comboBox.SelectedValue = lasttemp;
                keyValuePairs[lasttemp] = comboBox;
                keyValuePairs[Service.Config.CFW.CFW[0].ChannelType] = chType1;


            };
            chType2.SelectionChanged += (s, e) =>
            {
                ComboBox comboBox = keyValuePairs[Service.Config.CFW.CFW[1].ChannelType];
                ImageChannelType lasttemp = keyValuePairs.FirstOrDefault(x => x.Value == chType2).Key;
                comboBox.SelectedValue = lasttemp;

                keyValuePairs[lasttemp] = comboBox;
                keyValuePairs[Service.Config.CFW.CFW[1].ChannelType] = chType2;
            };
            chType3.SelectionChanged += (s, e) =>
            {
                ComboBox comboBox = keyValuePairs[Service.Config.CFW.CFW[2].ChannelType];
                ImageChannelType lasttemp = keyValuePairs.FirstOrDefault(x => x.Value == chType3).Key;
                comboBox.SelectedValue = lasttemp;

                keyValuePairs[lasttemp] = comboBox;
                keyValuePairs[Service.Config.CFW.CFW[2].ChannelType] = chType3;
            };


            List<int> BaudRates = new List<int> { 115200, 9600, 300, 600, 1200, 2400, 4800, 14400, 19200, 38400, 57600 };
            TextBaudRate.ItemsSource = BaudRates;
            TextBaudRate1.ItemsSource = BaudRates;


            List<string> Serials = new List<string> { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7" ,"COM8" };
            TextSerial.ItemsSource = Serials;
            TextSerial1.ItemsSource = Serials;

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

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MQTTEditContent.Visibility = Visibility.Collapsed;
            MQTTShowContent.Visibility = Visibility.Visible;
            ButtonEdit.Visibility = Visibility.Visible;
        }

        private void ButtonEdit_Click(object sender, RoutedEventArgs e)
        {
            MQTTShowContent.Visibility = Visibility.Collapsed;
            MQTTEditContent.Visibility = Visibility.Visible;
            ButtonEdit.Visibility = Visibility.Collapsed;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (Service.DeviceStatus == DeviceStatus.UnInit)
            {
                Service.Init();
            }
            else
            {
                Service.UnInit();
            }


        }


    }
}
