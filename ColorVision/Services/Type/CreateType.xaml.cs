using ColorVision.Common.MVVM;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Algorithm;
using ColorVision.Services.Devices.Calibration;
using ColorVision.Services.Devices.Camera;
using ColorVision.Services.Devices;
using ColorVision.Services.Devices.Camera.Configs;
using ColorVision.Services.Devices.CfwPort;
using ColorVision.Services.Devices.FileServer;
using ColorVision.Services.Devices.Motor;
using ColorVision.Services.Devices.PG;
using ColorVision.Services.Devices.Sensor;
using ColorVision.Services.Devices.SMU.Configs;
using ColorVision.Services.Devices.SMU;
using ColorVision.Services.Devices.Spectrum.Configs;
using ColorVision.Services.Devices.Spectrum;
using ColorVision.Services.Type;
using ColorVision.Settings;
using Newtonsoft.Json;
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
using ColorVision.Common.Utilities;
using ColorVision.Services.RC;
using ColorVision.Services.Terminal;

namespace ColorVision.Services.Type
{
    /// <summary>
    /// EditTerminal.xaml 的交互逻辑
    /// </summary>
    public partial class CreateType : Window
    {
        public TypeService TypeService { get; set; }
        public CreateType(TypeService typeService)
        {
            TypeService = typeService;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            CreateCode.Text = TypeService.NewCreateFileName($"SVR.{TypeService.ServiceTypes}.Default");
            CreateName.Text = TypeService.NewCreateFileName($"SVR.{TypeService.ServiceTypes}.Default");

            DataContext = TypeService;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!ServicesHelper.IsInvalidPath(CreateCode.Text, "资源标识") || !ServicesHelper.IsInvalidPath(CreateName.Text, "资源名称"))
                return;

            if (TypeService.ServicesCodes.Contains(CreateCode.Text))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "设备标识已存在,不允许重复添加");
                return;
            }


            SysResourceModel sysResource = new SysResourceModel(CreateName.Text, CreateCode.Text, TypeService.SysDictionaryModel.Value, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);

            TerminalServiceConfig terminalServiceConfig = new TerminalServiceConfig() { HeartbeatTime = 5000 };

            terminalServiceConfig.SendTopic = $"{TypeService.ServiceTypes}/{CreateCode.Text}/CMD/{ConfigHandler.GetInstance().SoftwareConfig.RcServiceConfig.RCName}";
            terminalServiceConfig.SubscribeTopic = $"{TypeService.ServiceTypes}/{CreateCode.Text}/STATUS/{ConfigHandler.GetInstance().SoftwareConfig.RcServiceConfig.RCName}";

            sysResource.Value = JsonConvert.SerializeObject(terminalServiceConfig);

            VSysResourceDao resourceDao = new VSysResourceDao();
            resourceDao.Save(sysResource);

            int pkId = sysResource.PKId;
            if (pkId > 0 && resourceDao.GetById(pkId) is SysResourceModel model)
            {
                TerminalService terminalService = TypeService.ServiceTypes switch
                {
                    ServiceTypes.Camera => new TerminalCamera(model),
                    _ => new TerminalService(model),
                };
                TypeService.AddChild(terminalService);
                ServiceManager.GetInstance().TerminalServices.Add(terminalService);

                MQTTRCService.GetInstance().RestartServices(TypeService.ServiceTypes.ToString());
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "创建成功，正在重启服务", "ColorVision");
                Close();
            }
            else
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "创建成功，正在重启服务", "ColorVision");
            }

        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }
    }
}
