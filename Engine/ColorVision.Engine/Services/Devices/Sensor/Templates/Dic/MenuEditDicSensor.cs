using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Sensor.Templates.Dic
{
    public class MenuEditDicSensor : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplateSensor);
        public override int Order => 99;
        public override string Header => "编辑默认传感器字典";

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new TemplateEditorWindow(new TemplateSensorDicModParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }

}
