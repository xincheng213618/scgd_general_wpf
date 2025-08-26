using ColorVision.Common.MVVM;
using System.Windows;
using System.Windows.Controls;
using ColorVision.UI.Authorizations;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices.PG.Templates;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.PG
{
    public class DevicePG : DeviceService<ConfigPG>
    {
        public MQTTPG DService { get; set; }

        [CommandDisplayAttribute("PG模板配置",Order =100)]
        public RelayCommand EditPGTemplateCommand { get; set; }


        public DevicePG(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTPG(Config);

            EditCommand = new RelayCommand(a =>
            {
                EditPG window = new(this);
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            }, a => AccessControl.Check(PermissionMode.Administrator));

            EditPGTemplateCommand = new RelayCommand(a => EditPGTemplate());
        }
        public override UserControl GetDeviceInfo() => new InfoPG(this);

        public override UserControl GetDisplayControl() => new DisplayPG(this);

        public override MQTTServiceBase? GetMQTTService() => DService;

        public static void EditPGTemplate()
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new TemplateEditorWindow(new TemplatePGParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }
}
