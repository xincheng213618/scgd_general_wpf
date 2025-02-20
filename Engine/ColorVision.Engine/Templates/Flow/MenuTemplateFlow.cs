using ColorVision.Engine.MySql;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Templates.Flow
{
    public class MenuTemplateFlow : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);
        public override int Order => 0;
        public override string Header => Properties.Resources.MenuFlow;
        public override void Execute()
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new TemplateEditorWindow(new TemplateFlow()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }
}
