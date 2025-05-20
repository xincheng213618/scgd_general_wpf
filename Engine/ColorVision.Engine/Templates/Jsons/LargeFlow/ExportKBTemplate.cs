using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Menus;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Templates.Jsons.LargeFlow
{

    public class MenuTemplateLargeFlow : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);
        public override int Order => 999;
        public override string Header => "大模板";
        public override void Execute()
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new TemplateEditorWindow(new TemplateLargeFlow()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }


}
