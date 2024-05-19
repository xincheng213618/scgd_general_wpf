using ColorVision.Common.Utilities;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Camera.Configs;
using ColorVision.UserSpace;
using cvColorVision;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Windows;

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

            DataContext = this;

            var Config = CreateConfig;

        }
        SysDeviceModel? saveDevConfigInfo(DeviceServiceConfig deviceConfig, SysResourceModel sysResource)
        {
            deviceConfig.Name = CreateCode.Text;
            deviceConfig.Code = CreateName.Text;
            deviceConfig.SendTopic = TerminalService.Config.SendTopic;
            deviceConfig.SubscribeTopic = TerminalService.Config.SubscribeTopic;

            sysResource.Value = JsonConvert.SerializeObject(deviceConfig);
            VSysResourceDao.Instance.Save(sysResource);
            int pkId = sysResource.Id;
            if (pkId > 0 && VSysDeviceDao.Instance.GetById(pkId) is SysDeviceModel model) return model;
            else return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!Services.ServicesHelper.IsInvalidPath(CreateCode.Text, ColorVision.Properties.Resource.Code) || !Services.ServicesHelper.IsInvalidPath(CreateName.Text, ColorVision.Properties.Resource.Name))
                return;

            var deviceS= ServiceManager.GetInstance().DeviceServices.FirstOrDefault(x => x.Code == CreateCode.Text);
            if (deviceS != null)
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "设备标识已存在,不允许重复添加", "ColorVision");
                return;
            }
            SysDeviceModel sysDevModel = null;

            SysResourceModel sysResource = new(CreateName.Text, CreateCode.Text, TerminalService.SysResourceModel.Type, TerminalService.SysResourceModel.Id, UserConfig.Instance.TenantId);
            CreateConfig.Id = CreateCode.Text;
            CreateConfig.Name = CreateName.Text;
            CreateConfig.SendTopic = TerminalService.Config.SendTopic;
            CreateConfig.SubscribeTopic = TerminalService.Config.SubscribeTopic;
            sysResource.Value = JsonConvert.SerializeObject(CreateConfig);

            sysDevModel = saveDevConfigInfo(CreateConfig, sysResource);
            if (sysDevModel != null)
            {
                var deviceService = new DeviceCamera(sysDevModel);
                TerminalService.AddChild(deviceService);
                ServiceManager.GetInstance().DeviceServices.Add(deviceService);
                if (sysDevModel != null && sysDevModel.TypeCode != null && sysDevModel.PCode != null && sysDevModel.Code != null)
                    RC.MQTTRCService.GetInstance().RestartServices(sysDevModel.TypeCode, sysDevModel.PCode, sysDevModel.Code);
                Close();
            }
            else
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "请重新创建新的Code，创建失败", "ColorVision");
            }
        }
    }
}
