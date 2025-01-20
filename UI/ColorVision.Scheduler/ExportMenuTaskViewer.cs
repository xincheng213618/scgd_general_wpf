using ColorVision.Common.MVVM;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Scheduler
{
    public class ExportMenuTaskViewer :  IMenuItem,IHotKey
    {
        public string? OwnerGuid => "Tool";

        public string? GuidId => nameof(ExportMenuTaskViewer);

        public int Order => 10;

        public Visibility Visibility => Visibility.Visible;

        public string? Header => Properties.Resources.TaskViewerWindow;

        public string? InputGestureText => null;

        public object? Icon => null;

        public ICommand Command => new RelayCommand(A => new TaskViewerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show());

        public HotKeys HotKeys => new HotKeys(Properties.Resources.TaskViewerWindow, Hotkey.None, () => { new TaskViewerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show(); });
    }
}
