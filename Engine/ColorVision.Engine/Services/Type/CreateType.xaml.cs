using ColorVision.Database;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Themes;
using Newtonsoft.Json;
using SqlSugar;
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
            if (!ServicesHelper.IsInvalidPath(CreateCode.Text, Properties.Resources.ResourceIdentifier) || !ServicesHelper.IsInvalidPath(CreateName.Text, Properties.Resources.ResourceName))
                return;

            if (TypeService.ServicesCodes.Contains(CreateCode.Text))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), Properties.Resources.DeviceIdentifierAlreadyExists);
                return;
            }

            SysResourceModel sysResource = new SysResourceModel();
            sysResource.Name = CreateName.Text;
            sysResource.Code = CreateCode.Text;
            sysResource.Type = TypeService.SysDictionaryModel.Value;


            TerminalServiceConfig terminalServiceConfig = new() { };

            terminalServiceConfig.SendTopic = $"{TypeService.ServiceTypes}/{CreateCode.Text}/CMD/{RCSetting.Instance.Config.RCName}";
            terminalServiceConfig.SubscribeTopic = $"{TypeService.ServiceTypes}/{CreateCode.Text}/STATUS/{RCSetting.Instance.Config.RCName}";

            sysResource.Value = JsonConvert.SerializeObject(terminalServiceConfig);
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            int pkId = Db.Insertable(sysResource).ExecuteReturnIdentity();
            sysResource.Id = pkId;

            if (pkId > 0)
            {
                TerminalService terminalService = new TerminalService(sysResource);
                TypeService.AddChild(terminalService);
                ServiceManager.GetInstance().TerminalServices.Add(terminalService);

                MqttRCService.GetInstance().RestartServices(TypeService.ServiceTypes.ToString());
                MessageBox.Show(WindowHelpers.GetActiveWindow(), Properties.Resources.CreationSuccessRestartingService, "ColorVision");
                Close();
            }
            else
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), Properties.Resources.CreationFailedDatabaseInsertFailed, "ColorVision");
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
