using ColorVision.Database;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Templates.Validate
{
    public class ExportDicComply : MenuItemBase
    {
        public override string OwnerGuid => nameof(ExportComply);

        public override string GuidId => "ComplyEdit";
        public override int Order => 999;
        public override string Header =>ColorVision.Engine.Properties.Resources.EditDefaultComplianceDictionary;

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.DatabaseConnectionFailed, "ColorVision");
                return;
            }
            new TemplateEditorWindow(new TemplateDicComply()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }


}
