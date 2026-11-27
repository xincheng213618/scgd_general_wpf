using ColorVision.Database;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Themes;
using ColorVision.UI.Authorizations;
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
        public TerminalService? CreatedTerminal { get; private set; }
        private readonly SysResourceDao resourceDao = SysResourceDao.Instance;
        private readonly MySqlConnectedEndpoint? endpoint = WorkstationConfiguration.ConnectedEndpoint;
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
            if (!AccessControl.Evaluate("service.create", PermissionMode.Administrator, TypeService.CreationResourceId).Allowed)
            {
                MessageBox.Show(this, EngineLocalization.Get("当前权限不足，无法创建服务。"), "ColorVision");
                return;
            }
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

            terminalServiceConfig.SendTopic = $"{TypeService.ServiceTypes}/{CreateCode.Text}/CMD/{MqttRCService.RCNodeName}";
            terminalServiceConfig.SubscribeTopic = $"{TypeService.ServiceTypes}/{CreateCode.Text}/STATUS/{MqttRCService.RCNodeName}";

            sysResource.Value = JsonConvert.SerializeObject(terminalServiceConfig);
            AccessControl.Demand("service.create", PermissionMode.Administrator, TypeService.CreationResourceId);
            bool useLocal = resourceDao.UseLocal;
            int pkId;
            try
            {
                if (!ReferenceEquals(resourceDao, SysResourceDao.Instance)) pkId = 0;
                else if (useLocal) pkId = resourceDao.SaveAndReturnId(sysResource, local: true);
                else if (endpoint is { IsCurrent: true })
                {
                    using var db = endpoint.CreateDbClient();
                    pkId = endpoint.IsCurrent ? db.Insertable(sysResource).ExecuteReturnIdentity() : 0;
                }
                else pkId = 0;
            }
            catch (Exception error)
            {
                log4net.LogManager.GetLogger(typeof(CreateType)).Warn("Could not insert the terminal resource into its selected store.", error);
                MessageBox.Show(this, Properties.Resources.CreationFailedDatabaseInsertFailed, "ColorVision");
                return;
            }
            sysResource.Id = pkId;

            if (pkId > 0 || SysResourceDao.IsLocalId(pkId))
            {
                if (pkId > 0 && endpoint?.IsCurrent != true)
                {
                    MessageBox.Show(this, EngineLocalization.Get("服务已写入原配置库。连接已变更，请重新加载后查看。"), "ColorVision");
                    Close();
                    return;
                }

                TerminalService terminalService = new(sysResource)
                {
                    ResourceDatabaseEndpoint = endpoint,
                    LocalConfigurationResourceDao = useLocal ? resourceDao : null
                };
                TypeService.AddChild(terminalService);
                ServiceManager.GetInstance().TerminalServices.Add(terminalService);
                CreatedTerminal = terminalService;

                if (!SysResourceDao.IsLocalId(pkId)) MqttRCService.GetInstance().RestartServices(TypeService.ServiceTypes.ToString());
                MessageBox.Show(WindowHelpers.GetActiveWindow(), SysResourceDao.IsLocalId(pkId) ? Properties.Resources.LocalConfigurationCreated : Properties.Resources.CreationSuccessRestartingService, "ColorVision");
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
