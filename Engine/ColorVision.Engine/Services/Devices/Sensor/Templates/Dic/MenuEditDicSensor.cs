using ColorVision.Database;
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
        public override string Header => ColorVision.Engine.Properties.Resources.EditDefaultSensorDictionary;

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.DatabaseConnectionFailed, "ColorVision");
                return;
            }
            new TemplateEditorWindow(new TemplateSensorDicModParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }

}
