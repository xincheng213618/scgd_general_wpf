using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Projects.ProjectShiYuan
{
    public class ShiyuanProjectExport : IMenuItem
    {
        public string? OwnerGuid => "Tool";

        public string? GuidId => "ProjectShiyuan";

        public int Order => 100;
        public Visibility Visibility => Visibility.Visible;
        public string? Header => "ProjectShiyuan";
        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new(A => Execute());

        private static void Execute()
        {
            new ShiyuanProjectWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }
}
