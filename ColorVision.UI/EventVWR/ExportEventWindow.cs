using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.UI.EventVWR
{
    public class ExportEventWindow : IMenuItem
    {
        public string? OwnerGuid => "Help";
        public string? GuidId => "EventWindow";

        public int Order => 1000;
        public Visibility Visibility => Visibility.Visible;

        public string? Header => "EventWindow";

        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new(A => Execute());

        private static void Execute()
        {
            new EventWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }
}
