using ColorVision.Database;
using ColorVision.Engine.Templates;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam
{

    public class MenuICameraExp : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuItemCamera);

        public override int Order => 22;
        public override string Header => ColorVision.Engine.Properties.Resources.CameraParameterTemplate;   

        public override void Execute()
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.DatabaseConnectionFailed, "ColorVision");
                return;
            }
            new TemplateEditorWindow(new TemplateCameraRunParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }

}
