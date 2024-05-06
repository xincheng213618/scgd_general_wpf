using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using System.Windows;
using ColorVision.UI;
using ColorVision.Properties;

namespace ColorVision.Update
{
    public class ChangeLogMenuITem : IMenuItem
    {
        public string? OwnerGuid => "Help";
        public string? GuidId => "ChangeLog";
        public int Order => 10001;
        public string? Header => Resource.ChangeLog;

        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new RelayCommand(A => Execute());

        private static void Execute()
        {
            new ChangelogWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }
}
