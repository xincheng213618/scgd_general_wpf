using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Views;
using ColorVision.UI.Authorizations;
using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using ColorVision.Engine.MySql;
using ColorVision.Themes.Controls;
using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.SMU
{
    public class DeviceSMU : DeviceService<ConfigSMU>
    {
        public MQTTSMU DService { get; set; }

        public ViewSMU View { get; set; }

        public DeviceSMU(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DService = new MQTTSMU(Config);
            View = new ViewSMU();
            View.View.Title = $"源表视图 - {Config.Code}";
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
                MessageBox1.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new TemplateEditorWindow(new TemplateSMUParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }


        public override UserControl GetDeviceInfo() => new InfoSMU(this);
        public override UserControl GetDisplayControl() => new DisplaySMUControl(this);

        public override MQTTServiceBase? GetMQTTService()
        {
            return DService;
        }
    }
}
