﻿using ColorVision.Common.Extension;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.Services.Devices.Camera
{
    /// <summary>
    /// CreateWindow.xaml 的交互逻辑
    /// </summary>
    public partial class CreateWindow : Window
    {
        public TerminalCamera TerminalCamera { get; set; }
        public CreateWindow(TerminalCamera terminalCamera)
        {
            TerminalCamera = terminalCamera;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = TerminalCamera;

            var Config = TerminalCamera.CreateConfig;


            CameraID.ItemsSource = TerminalCamera.MQTTServiceTerminalBase.DevicesSN;

            ComboxCameraType.ItemsSource = from e1 in Enum.GetValues(typeof(CameraType)).Cast<CameraType>()
                                           select new KeyValuePair<CameraType, string>(e1, e1.ToDescription());

            ComboxCameraTakeImageMode.ItemsSource = from e1 in Enum.GetValues(typeof(TakeImageMode)).Cast<TakeImageMode>()
                                                    select new KeyValuePair<TakeImageMode, string>(e1, e1.ToDescription());

            ComboxCameraImageBpp.ItemsSource = from e1 in Enum.GetValues(typeof(ImageBpp)).Cast<ImageBpp>()
                                               select new KeyValuePair<ImageBpp, string>(e1, e1.ToDescription());

            var type = TerminalCamera.CreateConfig.CameraType;

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



            var ImageChannelTypeList = new[]{
                 new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_X, "Channel_R"),
                 new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_Y, "Channel_G"),
                 new KeyValuePair<ImageChannelType, string>(ImageChannelType.Gray_Z, "Channel_B")
            };
            chType1.ItemsSource = ImageChannelTypeList;
            chType2.ItemsSource = ImageChannelTypeList;
            chType3.ItemsSource = ImageChannelTypeList;


            Dictionary<ImageChannelType, ComboBox> keyValuePairs = new Dictionary<ImageChannelType, ComboBox>() { };
            keyValuePairs.Add(ImageChannelType.Gray_X, chType1);
            keyValuePairs.Add(ImageChannelType.Gray_Y, chType2);
            keyValuePairs.Add(ImageChannelType.Gray_Z, chType3);


            chType1.SelectionChanged += (s, e) =>
            {
                ComboBox comboBox = keyValuePairs[Config.CFW.ChannelCfgs[0].Chtype];
                ImageChannelType lasttemp = keyValuePairs.FirstOrDefault(x => x.Value == chType1).Key;
                comboBox.SelectedValue = lasttemp;
                keyValuePairs[lasttemp] = comboBox;
                keyValuePairs[Config.CFW.ChannelCfgs[0].Chtype] = chType1;


            };
            chType2.SelectionChanged += (s, e) =>
            {
                ComboBox comboBox = keyValuePairs[Config.CFW.ChannelCfgs[1].Chtype];
                ImageChannelType lasttemp = keyValuePairs.FirstOrDefault(x => x.Value == chType2).Key;
                comboBox.SelectedValue = lasttemp;

                keyValuePairs[lasttemp] = comboBox;
                keyValuePairs[Config.CFW.ChannelCfgs[1].Chtype] = chType2;
            };
            chType3.SelectionChanged += (s, e) =>
            {
                ComboBox comboBox = keyValuePairs[Config.CFW.ChannelCfgs[2].Chtype];
                ImageChannelType lasttemp = keyValuePairs.FirstOrDefault(x => x.Value == chType3).Key;
                comboBox.SelectedValue = lasttemp;

                keyValuePairs[lasttemp] = comboBox;
                keyValuePairs[Config.CFW.ChannelCfgs[2].Chtype] = chType3;
            };


            List<int> BaudRates = new List<int> { 115200, 9600, 300, 600, 1200, 2400, 4800, 14400, 19200, 38400, 57600 };
            TextBaudRate.ItemsSource = BaudRates;
            TextBaudRate1.ItemsSource = BaudRates;


            List<string> Serials = new List<string> { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8" };
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
                switch (Config.MotorConfig.eFOCUSCOMMUN)
                {
                    case FOCUS_COMMUN.VID_SERIAL:
                        Config.MotorConfig.BaudRate = 115200;
                        break;
                    case FOCUS_COMMUN.CANON_SERIAL:
                        Config.MotorConfig.BaudRate = 38400;
                        break;
                    case FOCUS_COMMUN.NED_SERIAL:
                        Config.MotorConfig.BaudRate = 115200;
                        break;
                    case FOCUS_COMMUN.LONGFOOT_SERIAL:
                        Config.MotorConfig.BaudRate = 115200;
                        break;
                    default:
                        break;
                }
            };


        }
    }
}