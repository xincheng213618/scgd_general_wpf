using ColorVision.Engine.MySql;
using ColorVision.UI.Authorizations;
using System.Windows;

namespace ColorVision.Engine.Templates.SysDictionary
{
    public class MenuDicModParam : MenuITemplateAlgorithm
    {

        public override int Order => 99;
        public override string Header => "编辑默认算法字典";

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new TemplateEditorWindow(new TemplateModParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }
}
