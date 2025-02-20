using ColorVision.Engine.MySql;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Templates
{
    public abstract class MenuItemTemplateBase: MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);

        public override void Execute()
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.DatabaseConnectionFailed, "ColorVision");
                return;
            }
            ShowTemplateWindow();
        }
        public abstract ITemplate Template { get; }

        public virtual void ShowTemplateWindow()
        {
            new TemplateEditorWindow(Template) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); 
        }
    }
}
