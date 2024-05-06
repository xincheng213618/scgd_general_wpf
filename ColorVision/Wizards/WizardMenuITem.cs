using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Properties;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.Wizards
{
    public class WizardMenuITem :IMenuItem
    {
        public string? OwnerGuid => "Help";
        public string? GuidId => "Wizard";
        public int Order => 10000;
        public string? Header => Resource.Wizard;

        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new RelayCommand(A => Execute());

        private static void Execute()
        {
            new WizardWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }
}
