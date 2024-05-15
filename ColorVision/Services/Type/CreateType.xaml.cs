using ColorVision.Common.Utilities;
using ColorVision.Services.Core;
using ColorVision.Services.Dao;
using ColorVision.Services.Devices.Camera;
using ColorVision.Services.RC;
using ColorVision.Services.Terminal;
using ColorVision.UserSpace;
using Newtonsoft.Json;
using System;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Services.Types
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


            SysResourceModel sysResource = new(CreateName.Text, CreateCode.Text, TypeService.SysDictionaryModel.Value, UserConfig.Instance.TenantId);

            TerminalServiceConfig terminalServiceConfig = new() { HeartbeatTime = 5000 };

            terminalServiceConfig.SendTopic = $"{TypeService.ServiceTypes}/{CreateCode.Text}/CMD/{RCSetting.Instance.RCServiceConfig.RCName}";
            terminalServiceConfig.SubscribeTopic = $"{TypeService.ServiceTypes}/{CreateCode.Text}/STATUS/{RCSetting.Instance.RCServiceConfig.RCName}";

            sysResource.Value = JsonConvert.SerializeObject(terminalServiceConfig);

            VSysResourceDao resourceDao = new();
            resourceDao.Save(sysResource);

            int pkId = sysResource.Id;
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
