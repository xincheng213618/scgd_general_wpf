using ColorVision.Database;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Menus;
using ColorVision.Themes.Controls;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.SMU.Templates
{
    public class MenuSMUParam : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);

        public override string GuidId => "SMUParam";
        public override int Order => 12;
        public override string Header => Properties.Resources.MenuSUM;

        public override void Execute()
        {
            new TemplateEditorWindow(new TemplateSMUParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }

    }
}
