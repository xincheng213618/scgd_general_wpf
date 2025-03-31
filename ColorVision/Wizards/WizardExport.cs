using ColorVision.Properties;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Wizards
{
    public class WizardExport :MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => "Wizard";
        public override int Order => 9000;
        public override string Header => Resources.Wizard;

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            new WizardWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }
}
