using ColorVision.Database;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Templates.Menus
{
    public abstract class MenuItemTemplateBase: MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);

        public override void Execute()
        {
            ShowTemplateWindow();
        }
        public abstract ITemplate Template { get; }

        public virtual void ShowTemplateWindow()
        {
            new TemplateEditorWindow(Template) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show(); 
        }
    }
}
