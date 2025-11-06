using ColorVision.Database;
using ColorVision.Engine.Templates.Menus;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Templates.SysDictionary
{
    public class MenuDefalutDicAlg : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuITemplateAlgorithm);
        public override int Order => 999999;
        public override string Header => ColorVision.Engine.Properties.Resources.EditDefaultAlgorithmDictionary;

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.DatabaseConnectionFailed, "ColorVision");
                return;
            }
            new TemplateEditorWindow(new TemplateModParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }
}
