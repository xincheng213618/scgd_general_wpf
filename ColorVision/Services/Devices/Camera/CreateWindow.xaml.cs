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
using System.Windows.Media.Media3D;
using ColorVision.Services.Devices.Camera.Dao;

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
            int pkId = sysResource.PKId;
            if (pkId > 0 && ServiceManager.GetInstance().VSysDeviceDao.GetById(pkId) is SysDeviceModel model) return model;
            else return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!Services.ServicesHelper.IsInvalidPath(CreateCode.Text, "资源标识") || !Services.ServicesHelper.IsInvalidPath(CreateName.Text, "资源名称"))
                return;

            var deviceS= ServiceManager.GetInstance().DeviceServices.FirstOrDefault(x => x.Code == CreateCode.Text);
            if (deviceS != null)
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "设备标识已存在,不允许重复添加", "ColorVision");
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
                Close();
            }
            else
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "请重新创建新的Code，创建失败", "ColorVision");
            }
        }
    }
}
