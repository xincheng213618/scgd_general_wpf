using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.SMU
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
                MessageBox1.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new TemplateEditorWindow(new TemplateSMUParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }

    }
}
