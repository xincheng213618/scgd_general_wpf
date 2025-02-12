using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.CameraExposure
{
    public class ExportCameraExp : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);

        public override int Order => 22;
        public override string Header => "相机模板";   

        public override void Execute()
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new TemplateEditorWindow(new TemplateCameraExposure()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }

}
