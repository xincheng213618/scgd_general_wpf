using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using System.Windows;
using ColorVision.Properties;
using ColorVision.UI.Menus;

namespace ColorVision.Update
{
    public class ChangeLogMenuITem : IMenuItem
    {
        public string? OwnerGuid => "Help";
        public string? GuidId => "ChangeLog";
        public int Order => 10001;
        public string? Header => Resources.ChangeLog;

        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new(A => Execute());

        private static void Execute()
        {
            new ChangelogWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
        public Visibility Visibility => Visibility.Visible;
    }
}
