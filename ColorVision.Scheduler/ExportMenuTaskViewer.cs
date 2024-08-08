using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Scheduler
{
    public class ExportMenuTaskViewer :  IMenuItem,IHotKey
    {
        public string? OwnerGuid => "Tool";
        public string? GuidId => "TaskViewerWindow";

        public int Order => 10;

        public Visibility Visibility => Visibility.Visible;

        public string? Header => "TaskViewerWindow";

        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new RelayCommand(A => new TaskViewerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show());

        public HotKeys HotKeys => new HotKeys("TaskViewerWindow", Hotkey.None, () => { new TaskViewerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show(); });
    }
}
