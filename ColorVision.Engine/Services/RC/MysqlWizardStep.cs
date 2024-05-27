using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.Services.RC
{
    public class RCWizardStep : IWizardStep
    {
        public int Order => 3;

        public string Title => "RC配置";

        public string Description => "RC配置";

        public RelayCommand? RelayCommand => new RelayCommand(a =>
        {
            new RCServiceConnect() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        });
    }
}
