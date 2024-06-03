#pragma warning disable CS8604
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Scheduler
{
    public class ExportMenuTaskViewer :  IMenuItem
    {
        public string? OwnerGuid => "Tool";
        public string? GuidId => "TaskViewerWindow";

        public int Order => 1000;

        public Visibility Visibility => Visibility.Visible;

        public string? Header => "TaskViewerWindow";

        public string? InputGestureText => "Ctrl + F1";

        public object? Icon => null;

        public RelayCommand Command => new(A => new TaskViewerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show());

    }
}
