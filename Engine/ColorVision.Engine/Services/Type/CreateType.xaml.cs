using ColorVision.Database;
using ColorVision.Engine.Rbac;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Themes;
using Newtonsoft.Json;
using System;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Types
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
            this.ApplyCaption();
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

            SysResourceModel sysResource = new SysResourceModel();
            sysResource.Name = CreateName.Text;
            sysResource.Code = CreateCode.Text;
            sysResource.Type = TypeService.SysDictionaryModel.Value;
            sysResource.TenantId = UserConfig.Instance.TenantId;


            TerminalServiceConfig terminalServiceConfig = new() { HeartbeatTime = 5000 };

            terminalServiceConfig.SendTopic = $"{TypeService.ServiceTypes}/{CreateCode.Text}/CMD/{RCSetting.Instance.Config.RCName}";
            terminalServiceConfig.SubscribeTopic = $"{TypeService.ServiceTypes}/{CreateCode.Text}/STATUS/{RCSetting.Instance.Config.RCName}";

            sysResource.Value = JsonConvert.SerializeObject(terminalServiceConfig);

            int pkId = MySqlControl.GetInstance().DB.Insertable(sysResource).ExecuteReturnIdentity();
            sysResource.Id = pkId;

            if (pkId > 0)
            {
                TerminalService terminalService = new TerminalService(sysResource);
                TypeService.AddChild(terminalService);
                ServiceManager.GetInstance().TerminalServices.Add(terminalService);

                MqttRCService.GetInstance().RestartServices(TypeService.ServiceTypes.ToString());
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "创建成功，正在重启服务", "ColorVision");
                Close();
            }
            else
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "创建失败，数据库插入失败，请联系开发人员", "ColorVision");
            }

        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }
    }
}
