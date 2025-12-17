using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Views;
using ColorVision.UI.Authorizations;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using ColorVision.Database;
using ColorVision.Themes.Controls;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Extension;

namespace ColorVision.Engine.Services.Devices.SMU
{
    public class DeviceSMU : DeviceService<ConfigSMU>
    {
        public MQTTSMU DService { get; set; }

        public ViewSMU View { get; set; }
        public IDisplayConfigBase DisplayConfig => DisplayConfigManager.Instance.GetDisplayConfig<IDisplayConfigBase>(Config.Code);

        public DeviceSMU(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTSMU(this,Config);
            View = new ViewSMU();
            View.View.Title = ColorVision.Engine.Properties.Resources.SMUView+$" - {Config.Code}";
            this.SetIconResource("SMUDrawingImage", View.View);


            EditCommand = new RelayCommand(a =>
            {
                EditSMU window = new(this);
                window.Icon = Icon;
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            }, a => AccessControl.Check(PermissionMode.Administrator));

            EditSMUTemplateCommand = new RelayCommand(a => EditSMUTemplate());
        }


        [CommandDisplay("MenuSUM",Order =100)]
        public RelayCommand EditSMUTemplateCommand { get; set; }

        public static void EditSMUTemplate()
        {

            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.DatabaseConnectionFailed, "ColorVision");
                return;
            }
            new TemplateEditorWindow(new TemplateSMUParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }


        public override UserControl GetDeviceInfo() => new InfoSMU(this);
        public override UserControl GetDisplayControl() => new DisplaySMU(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
