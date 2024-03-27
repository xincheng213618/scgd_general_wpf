using ColorVision.Common.Extension;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.IO;
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
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Terminal;
using ColorVision.Settings;
using Newtonsoft.Json;
using ColorVision.Services.Devices.Camera.Configs;
using ColorVision.Common.Utilities;

namespace ColorVision.Services.Devices.Camera
{
    /// <summary>
    /// CreateWindow.xaml 的交互逻辑
    /// </summary>
    public partial class CreateWindow : Window
    {
        public TerminalCamera TerminalService { get; set; }

        public ConfigCamera CreateConfig { get; set; }

        public CreateWindow(TerminalCamera terminalCamera)
        {
            TerminalService = terminalCamera;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            int fromPort = (Math.Abs(new Random().Next()) % 99 + 6800);

            CreateConfig = new ConfigCamera
            {
                CameraType = CameraType.LV_Q,
                TakeImageMode = TakeImageMode.Measure_Normal,
                ImageBpp = ImageBpp.bpp8,
                Channel = ImageChannel.One,
                FileServerCfg = new FileServerCfg()
                {
                    Endpoint = "127.0.0.1",
                    PortRange = string.Format("{0}-{1}", fromPort, fromPort + 5),
                    DataBasePath = "D:\\CVTest",
                },
                VideoConfig = new Video.CameraVideoConfig()
                {
                    Host = "127.0.0.1",
                    Port = (Math.Abs(new Random().Next()) % 99 + 9000),
                }
            };
            CreateCode.Text = TerminalService.NewCreateFileName($"DEV.{TerminalService.ServiceType}.Default");
            CreateName.Text = TerminalService.NewCreateFileName($"DEV.{TerminalService.ServiceType}.Default");

            this.DataContext = this;

            var Config = CreateConfig;


            CameraID.ItemsSource = TerminalService.MQTTServiceTerminalBase.DevicesSN;

            ComboxCameraType.ItemsSource = from e1 in Enum.GetValues(typeof(CameraType)).Cast<CameraType>()
                                           select new KeyValuePair<CameraType, string>(e1, e1.ToDescription());

            ComboxCameraTakeImageMode.ItemsSource = from e1 in Enum.GetValues(typeof(TakeImageMode)).Cast<TakeImageMode>()
                                                    select new KeyValuePair<TakeImageMode, string>(e1, e1.ToDescription());

            ComboxCameraImageBpp.ItemsSource = from e1 in Enum.GetValues(typeof(ImageBpp)).Cast<ImageBpp>()
                                               select new KeyValuePair<ImageBpp, string>(e1, e1.ToDescription());

            var type = Config.CameraType;

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
        SysDeviceModel? saveDevConfigInfo(DeviceServiceConfig deviceConfig, SysResourceModel sysResource)
        {
            deviceConfig.Name = CreateCode.Text;
            deviceConfig.Code = CreateName.Text;
            deviceConfig.SendTopic = TerminalService.Config.SendTopic;
            deviceConfig.SubscribeTopic = TerminalService.Config.SubscribeTopic;

            sysResource.Value = JsonConvert.SerializeObject(deviceConfig);
            ServiceManager.GetInstance().VSysResourceDao.Save(sysResource);
            int pkId = sysResource.PKId;
            if (pkId > 0 && ServiceManager.GetInstance().VSysDeviceDao.GetById(pkId) is SysDeviceModel model) return model;
            else return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!ServicesHelper.IsInvalidPath(CreateCode.Text, "资源标识") || !ServicesHelper.IsInvalidPath(CreateName.Text, "资源名称"))
                return;

            if (TerminalService.ServicesCodes.Contains(CreateCode.Text))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "设备标识已存在,不允许重复添加");
                return;
            }
            SysDeviceModel sysDevModel = null;

            SysResourceModel sysResource = new SysResourceModel(CreateName.Text, CreateCode.Text, TerminalService.SysResourceModel.Type, TerminalService.SysResourceModel.Id, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            CreateConfig.Id = CreateCode.Text;
            CreateConfig.Name = CreateName.Text;
            CreateConfig.SendTopic = TerminalService.Config.SendTopic;
            CreateConfig.SubscribeTopic = TerminalService.Config.SubscribeTopic;
            sysResource.Value = JsonConvert.SerializeObject(CreateConfig);

            sysDevModel = saveDevConfigInfo(CreateConfig, sysResource);
            if (sysDevModel != null)
            {
                if (TerminalService.MQTTServiceTerminalBase is MQTTTerminalCamera cameraService)
                {
                    var deviceService = new DeviceCamera(sysDevModel, cameraService);
                    TerminalService.AddChild(deviceService);
                    ServiceManager.GetInstance().DeviceServices.Add(deviceService);
                }
                if (sysDevModel != null && sysDevModel.TypeCode != null && sysDevModel.PCode != null && sysDevModel.Code != null)
                    RC.MQTTRCService.GetInstance().RestartServices(sysDevModel.TypeCode, sysDevModel.PCode, sysDevModel.Code);
                //MessageBox.Show(WindowHelpers.GetActiveWindow(), "创建成功正在重启服务", "ColorVision");
                this.Close();
            }
            else
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "创建失败", "ColorVision");
            }
        }
    }
}
