using ColorVision.Database;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Menus;
using ColorVision.Themes.Controls;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.SMU.Templates
{
    public class MenuSMUParam : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);

        public override string GuidId => "SMUParam";
        public override int Order => 12;
        public override string Header => Properties.Resources.MenuSUM;

        public override void Execute()
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.DatabaseConnectionFailed, "ColorVision");
                return;
            }
            new TemplateEditorWindow(new TemplateSMUParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }

    }
}
