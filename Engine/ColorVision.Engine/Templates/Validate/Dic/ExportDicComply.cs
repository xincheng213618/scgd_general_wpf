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
        public override string Header => "编辑默认合规字典";

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new TemplateEditorWindow(new TemplateDicComply()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }


}
