
using ColorVision.Database;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Menus;
using ColorVision.Themes.Controls;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.PG.Templates
{
    public class ExportPGParam : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);

        public override string GuidId => "PGParam";
        public override int Order => 11;
        public override string Header => Properties.Resources.PgTemplateConfig;

        public override void Execute()
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.DatabaseConnectionFailed, "ColorVision");
                return;
            }
            new TemplateEditorWindow(new TemplatePGParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }

    }
}
