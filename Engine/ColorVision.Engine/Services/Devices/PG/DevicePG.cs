using ColorVision.Common.MVVM;
using System.Windows;
using System.Windows.Controls;
using ColorVision.UI.Authorizations;
using ColorVision.Engine.Services.Devices.PG.Templates;
using ColorVision.Engine.Templates;
using System.ComponentModel;
using ColorVision.UI;

namespace ColorVision.Engine.Services.Devices.PG
{
    public partial class DevicePG : DeviceService<ConfigPG>
    {
        public MQTTPG DService { get; set; }

        public DisplayPGConfig DisplayConfig { get; }

        [CommandDisplayAttribute("PgTemplateConfig",Order =100, CategoryOrder = 2)]
        [Category("AcquisitionDisplay")]
        [Description("CommandPgTemplateHint")]
        public RelayCommand EditPGTemplateCommand { get; set; }


        public DevicePG(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DisplayConfig = DisplayConfigManager.Instance.GetDisplayConfig<DisplayPGConfig>(Config.Code);
            InitializeLocalPG();
            DService = new MQTTPG(Config) { Device = this };
            DService.RefreshBackendStatus();

            EditCommand = new RelayCommand(a =>
            {
                if (!CanConfigure) return;
                EditPG window = new(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                if (window.ShowDialog() == true)
                {
                    TryCommitConfiguration(window, () => ConfigService.Instance.Save<DisplayConfigManager>());
                }
            }, a => CanConfigure);

            EditPGTemplateCommand = new RelayCommand(a => EditPGTemplate());
        }
        public override UserControl GetDeviceInfo() => new InfoPG(this);

        public override UserControl GetDisplayControl() => new DisplayPG(this);

        public override MQTTServiceBase? GetMQTTService() => DService;

        public static void EditPGTemplate()
        {
            new TemplateEditorWindow(new TemplatePGParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }
}
