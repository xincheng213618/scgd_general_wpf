using ColorVision.UI.Desktop.Properties;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.UI.Desktop.Wizards
{
    public class WizardExport : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override int Order => 9000;
        public override string Header => Resources.Wizard;
        public override void Execute()
        {
            new WizardWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }

}

