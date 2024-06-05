using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Services.PhyCameras
{
    public class ExportPhyCamerManager : IMenuItem
    {
        public string? OwnerGuid => "Tool";

        public string? GuidId => "PhyCamerManager";

        public int Order => 9;

        public string? Header => ColorVision.Engine.Properties.Resources.MenuPhyCameraManager;

        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new(A => Execute());

        private static void Execute()
        {
            new PhyCameraManagerWindow() { Owner = Application.Current.GetActiveWindow() }.ShowDialog();
        }
        public Visibility Visibility => Visibility.Visible;
    }
}
